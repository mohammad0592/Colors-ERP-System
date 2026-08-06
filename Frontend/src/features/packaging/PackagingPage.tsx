import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useState, type ReactElement } from 'react';
import { PageHeader } from '../../components/ui/PageHeader';
import {
  StartOnLineButton,
  type StartableLine,
} from '../../components/ui/StartOnLineButton';
import { useAuth } from '../../hooks/useAuth';
import { ApiError } from '../../lib/apiClient';
import { RoleNames } from '../../lib/roles';
import { shiftReportsApi } from '../shifts/api';
import { formatDate } from '../shifts/shiftFormat';
import { packagingApi } from './api';

/**
 * What packaging a shift used (specification section 10).
 *
 * Three materials arrive already answered — large bags, small bags and wooden pallets
 * come from what the shift produced, so the operator cannot get them wrong. The 2 July
 * form says 6.1 large bags where 61 were used, and 4.14 small where 122 were, which is
 * what typing them by hand does.
 *
 * The weights stay typed, because the factory already weighs them. That is what makes
 * the check free: counted against weighed, with no extra work for anybody.
 */
export function PackagingPage(): ReactElement {
  const queryClient = useQueryClient();
  const { hasRole } = useAuth();
  const canRecord = hasRole(RoleNames.Administrator, RoleNames.PackagingOperator);

  const [recording, setRecording] = useState<StartableLine | null>(null);
  const [typed, setTyped] = useState<Record<number, { quantity: string; weight: string }>>({});
  const [notes, setNotes] = useState('');
  const [actionError, setActionError] = useState<string | null>(null);

  const records = useQuery({
    queryKey: ['packaging'],
    queryFn: () => packagingApi.list(),
  });

  // Packaging is used where the bags come off (specification section 4).
  const packingLines = useQuery({
    queryKey: ['shift-reports', 'packing-lines'],
    queryFn: async () => {
      const open = await shiftReportsApi.list(undefined, true);
      const full = await Promise.all(open.map((s) => shiftReportsApi.get(s.id)));
      return full.flatMap((shift) =>
        shift.lines
          .filter((line) => line.formsBags)
          .map((line) => ({
            shiftLineId: line.id,
            lineName: line.productionLineName,
            shiftLabel: `shift ${shift.shiftName}, ${formatDate(shift.productionDate)}`,
          })),
      );
    },
  });

  const draft = useQuery({
    queryKey: ['packaging-draft', recording?.shiftLineId ?? null],
    queryFn: () => packagingApi.draft(recording?.shiftLineId ?? 0),
    enabled: recording !== null,
  });

  const save = useMutation({
    mutationFn: () =>
      packagingApi.save({
        shiftLineId: recording?.shiftLineId ?? 0,
        // Only the typed ones are sent. The counted three are worked out by the server
        // whatever this screen says, so sending them would be theatre.
        lines: (draft.data?.lines ?? [])
          .filter((line) => !line.isCounted)
          .map((line) => ({
            materialId: line.materialId,
            quantity: Number(typed[line.materialId]?.quantity ?? '') || 0,
            weight:
              (typed[line.materialId]?.weight ?? '') === ''
                ? null
                : Number(typed[line.materialId]?.weight),
          })),
        notes: notes.trim() === '' ? null : notes.trim(),
      }),
    onSuccess: () => {
      setActionError(null);
      setRecording(null);
      setTyped({});
      setNotes('');
      void queryClient.invalidateQueries({ queryKey: ['packaging'] });
      void queryClient.invalidateQueries({ queryKey: ['inventory'] });
    },
    onError: (caught: unknown) => {
      setActionError(caught instanceof ApiError ? caught.message : 'Something went wrong.');
    },
  });

  function set(materialId: number, field: 'quantity' | 'weight', value: string): void {
    setTyped((current) => ({
      ...current,
      [materialId]: {
        quantity: current[materialId]?.quantity ?? '',
        weight: current[materialId]?.weight ?? '',
        [field]: value,
      },
    }));
  }

  const lines = packingLines.data ?? [];

  return (
    <>
      <PageHeader
        title="Packaging"
        subtitle="Recorded once at the end of the shift. Bags and pallets are counted by the system from what was produced; tape, shrink and the hood are typed and weighed."
        actions={
          canRecord ? (
            <StartOnLineButton
              lines={lines}
              action="Record packaging"
              onStart={(shiftLineId) => {
                const line = lines.find((l) => l.shiftLineId === shiftLineId);
                if (line !== undefined) {
                  setRecording(line);
                  setTyped({});
                  setNotes('');
                }
              }}
            />
          ) : undefined
        }
      />

      {canRecord && lines.length === 0 && (
        <p className="mb-4 rounded-control border border-line bg-canvas px-4 py-3 text-sm text-ink-soft">
          No packing line is open. Packaging belongs to the line that makes the bags.
        </p>
      )}

      {actionError !== null && (
        <p
          role="alert"
          className="mb-4 rounded-control border border-l-4 border-bad/30 border-l-bad bg-bad-soft px-4 py-3 text-sm font-medium text-bad"
        >
          {actionError}
        </p>
      )}

      {recording !== null && draft.data !== undefined && (
        <section className="card mb-8 p-5">
          <div className="mb-4 flex flex-wrap items-start justify-between gap-3 border-b border-line pb-3">
            <div>
              <h2 className="text-lg font-bold text-ink">
                {draft.data.productionLineName} · shift {draft.data.shiftName}
              </h2>
              <p className="text-sm text-ink-muted">
                {formatDate(draft.data.productionDate)} · {draft.data.bagsProduced} bags
                made · {draft.data.palletsCompleted} pallet
                {draft.data.palletsCompleted === 1 ? '' : 's'} finished
              </p>
            </div>
            <button
              type="button"
              className="text-sm font-medium text-ink-muted hover:text-ink"
              onClick={() => {
                setRecording(null);
              }}
            >
              Cancel
            </button>
          </div>

          {draft.data.alreadyRecorded ? (
            <p className="rounded-control border border-line bg-canvas px-4 py-3 text-sm text-ink-soft">
              Packaging is already recorded for this line. It is written once, at the end
              of the shift.
            </p>
          ) : (
            <>
              <div className="mb-4 overflow-x-auto">
                <table className="w-full text-left text-sm">
                  <thead>
                    <tr className="border-b border-line text-xs tracking-wider text-ink-muted uppercase">
                      <th className="px-2 py-2 font-semibold">Material</th>
                      <th className="px-2 py-2 text-right font-semibold">Used</th>
                      <th className="px-2 py-2 text-right font-semibold">Weight (kg)</th>
                      <th className="px-2 py-2 text-right font-semibold">In stock</th>
                    </tr>
                  </thead>
                  <tbody>
                    {draft.data.lines.map((line) => (
                      <tr key={line.materialId} className="border-b border-line last:border-0">
                        <td className="px-2 py-2">
                          <span className="font-medium text-ink">{line.materialName}</span>
                          {line.isCounted && (
                            <span className="ml-2 rounded-full bg-ok-soft px-2 py-0.5 text-xs font-semibold text-ok">
                              counted
                            </span>
                          )}
                        </td>
                        <td className="px-2 py-2 text-right">
                          {line.isCounted ? (
                            <span className="font-semibold text-ink tabular-nums">
                              {line.quantity} <span className="text-ink-muted">{line.unitSymbol}</span>
                            </span>
                          ) : (
                            <input
                              type="number"
                              step="0.01"
                              min="0"
                              aria-label={`${line.materialName} used`}
                              className="field-input h-10 w-28 text-right text-base"
                              value={typed[line.materialId]?.quantity ?? ''}
                              onChange={(event) => {
                                set(line.materialId, 'quantity', event.target.value);
                              }}
                            />
                          )}
                        </td>
                        <td className="px-2 py-2 text-right">
                          <input
                            type="number"
                            step="0.001"
                            min="0"
                            aria-label={`${line.materialName} weight`}
                            className="field-input h-10 w-28 text-right text-base"
                            value={typed[line.materialId]?.weight ?? ''}
                            onChange={(event) => {
                              set(line.materialId, 'weight', event.target.value);
                            }}
                          />
                          {/* The free check: what the count says it should weigh. */}
                          {line.expectedWeight !== null && line.quantity > 0 && (
                            <p className="mt-0.5 text-xs text-ink-muted">
                              expect {line.expectedWeight}
                            </p>
                          )}
                        </td>
                        <td className="px-2 py-2 text-right tabular-nums text-ink-muted">
                          {line.inStock}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>

              <div className="mb-4">
                <label className="field-label" htmlFor="packaging-notes">
                  Note <span className="font-normal text-ink-muted">(optional)</span>
                </label>
                <input
                  id="packaging-notes"
                  className="field-input"
                  maxLength={300}
                  value={notes}
                  onChange={(event) => {
                    setNotes(event.target.value);
                  }}
                />
              </div>

              <button
                type="button"
                className="btn-primary"
                disabled={save.isPending}
                onClick={() => {
                  save.mutate();
                }}
              >
                {save.isPending ? 'Saving…' : 'Record it and take it out of the store'}
              </button>
              <p className="mt-2 text-xs text-ink-muted">
                Written once, at the end of the shift. Everything here leaves the store
                together, or nothing does.
              </p>
            </>
          )}
        </section>
      )}

      <h2 className="mb-3 text-lg font-bold text-ink">Recorded</h2>

      {records.isPending && <p className="p-6 text-ink-muted">Loading…</p>}
      {records.isError && <p className="p-6 text-bad">Could not load packaging.</p>}

      {records.data?.length === 0 && (
        <p className="card p-8 text-center text-ink-muted">
          No packaging has been recorded yet.
        </p>
      )}

      {records.data?.map((record) => (
        <section key={record.id} className="card mb-4 p-5">
          <div className="mb-3 flex flex-wrap items-baseline justify-between gap-3 border-b border-line pb-2">
            <h3 className="font-bold text-ink">
              {record.productionLineName} · shift {record.shiftName} ·{' '}
              {formatDate(record.productionDate)}
            </h3>
            <p className="text-sm text-ink-muted">by {record.recordedByName}</p>
          </div>

          <div className="overflow-x-auto">
            <table className="w-full text-left text-sm">
              <thead>
                <tr className="border-b border-line text-xs tracking-wider text-ink-muted uppercase">
                  <th className="py-2 pr-4 font-semibold">Material</th>
                  <th className="py-2 pr-4 text-right font-semibold">Used</th>
                  <th className="py-2 pr-4 text-right font-semibold">Weighed</th>
                  <th className="py-2 pr-4 text-right font-semibold">Expected</th>
                  <th className="py-2 text-right font-semibold">Difference</th>
                </tr>
              </thead>
              <tbody>
                {record.lines.map((line) => (
                  <tr key={line.materialId} className="border-b border-line last:border-0">
                    <td className="py-2 pr-4 text-ink">
                      {line.materialName}
                      {line.isCounted && (
                        <span className="ml-2 text-xs text-ink-muted">counted</span>
                      )}
                    </td>
                    <td className="py-2 pr-4 text-right font-semibold tabular-nums text-ink">
                      {line.quantity}
                    </td>
                    <td className="py-2 pr-4 text-right tabular-nums text-ink-soft">
                      {line.weight ?? '—'}
                    </td>
                    <td className="py-2 pr-4 text-right tabular-nums text-ink-muted">
                      {line.expectedWeight ?? '—'}
                    </td>
                    <td className="py-2 text-right">
                      <Difference value={line.weightDifference} />
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          {record.notes !== null && (
            <p className="mt-3 text-sm text-ink-muted">{record.notes}</p>
          )}
        </section>
      ))}
    </>
  );
}

/**
 * Weighed minus expected. Zero means the count and the scale agree, which is the whole
 * point of recording both — a gap is packaging torn, wasted or used somewhere else.
 */
function Difference({ value }: { value: number | null }): ReactElement {
  if (value === null) {
    return <span className="text-ink-muted">—</span>;
  }

  const agreed = Math.abs(value) < 0.0005;

  return (
    <span
      className={[
        'rounded-full px-2 py-0.5 text-xs font-semibold tabular-nums',
        agreed ? 'bg-ok-soft text-ok' : 'bg-warn-soft text-warn',
      ].join(' ')}
    >
      {agreed ? 'agrees' : `${value > 0 ? '+' : ''}${String(value)}`}
    </span>
  );
}

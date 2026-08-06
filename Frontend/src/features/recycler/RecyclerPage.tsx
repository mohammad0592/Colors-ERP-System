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
import { recyclerApi } from './api';

/**
 * Line 3 — the recycler (specification section 11).
 *
 * The smallest screen in the system: one weight. How much recycled material the shift
 * produced, which goes straight back into the store.
 *
 * <b>The scrap going in is not asked for, because it cannot be weighed.</b> The factory
 * keeps scrap in two silos and draws it out to be ground, so there is no moment when a
 * shift's scrap sits on a scale. Nothing here works out a loss from it, and nothing
 * compares it against what the thermo calculated — that figure stands on its own, on the
 * thermo's screens.
 */
export function RecyclerPage(): ReactElement {
  const queryClient = useQueryClient();
  const { hasRole } = useAuth();
  const canRecord = hasRole(RoleNames.Administrator, RoleNames.RecyclerOperator);

  const [recording, setRecording] = useState<StartableLine | null>(null);
  const [recycled, setRecycled] = useState('');
  const [notes, setNotes] = useState('');
  const [actionError, setActionError] = useState<string | null>(null);

  const records = useQuery({
    queryKey: ['recycler'],
    queryFn: () => recyclerApi.list(),
  });

  // The output is recorded where it is made (specification section 4).
  const recyclingLines = useQuery({
    queryKey: ['shift-reports', 'recycling-lines'],
    queryFn: async () => {
      const open = await shiftReportsApi.list(undefined, true);
      const full = await Promise.all(open.map((s) => shiftReportsApi.get(s.id)));
      return full.flatMap((shift) =>
        shift.lines
          .filter((line) => line.recycles)
          .map((line) => ({
            shiftLineId: line.id,
            lineName: line.productionLineName,
            shiftLabel: `shift ${shift.shiftName}, ${formatDate(shift.productionDate)}`,
          })),
      );
    },
  });

  const draft = useQuery({
    queryKey: ['recycler-draft', recording?.shiftLineId ?? null],
    queryFn: () => recyclerApi.draft(recording?.shiftLineId ?? 0),
    enabled: recording !== null,
  });

  const save = useMutation({
    mutationFn: () =>
      recyclerApi.save({
        shiftLineId: recording?.shiftLineId ?? 0,
        recycledMaterialWeight: Number(recycled) || 0,
        notes: notes.trim() === '' ? null : notes.trim(),
      }),
    onSuccess: () => {
      setActionError(null);
      setRecording(null);
      setRecycled('');
      setNotes('');
      void queryClient.invalidateQueries({ queryKey: ['recycler'] });
      // The output went into the store, so both inventory screens are now stale.
      void queryClient.invalidateQueries({ queryKey: ['inventory'] });
      void queryClient.invalidateQueries({ queryKey: ['inventory-movements'] });
    },
    onError: (caught: unknown) => {
      setActionError(caught instanceof ApiError ? caught.message : 'Something went wrong.');
    },
  });

  const lines = recyclingLines.data ?? [];
  const nothingWeighed = (Number(recycled) || 0) <= 0;

  return (
    <>
      <PageHeader
        title="Recycler"
        subtitle="How much recycled material the shift produced. The weight goes straight back into the store, ready for the black recipes."
        actions={
          canRecord ? (
            <StartOnLineButton
              lines={lines}
              action="Record what it produced"
              onStart={(shiftLineId) => {
                const line = lines.find((l) => l.shiftLineId === shiftLineId);
                if (line !== undefined) {
                  setRecording(line);
                  setRecycled('');
                  setNotes('');
                }
              }}
            />
          ) : undefined
        }
      />

      {canRecord && lines.length === 0 && (
        <p className="mb-4 rounded-control border border-line bg-canvas px-4 py-3 text-sm text-ink-soft">
          No recycling line is open. The output is recorded on the line that grinds it.
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
                {formatDate(draft.data.productionDate)}
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
              The recycler is already recorded for this line. It is written once, at the
              end of the shift.
            </p>
          ) : (
            <>
              <div className="mb-4 max-w-sm">
                <label className="field-label" htmlFor="recycled-weight">
                  Recycled material produced (kg)
                </label>
                <input
                  id="recycled-weight"
                  type="number"
                  step="0.001"
                  min="0"
                  className="field-input text-base"
                  value={recycled}
                  onChange={(event) => {
                    setRecycled(event.target.value);
                  }}
                />
                {draft.data.recycledMaterialName !== null && (
                  <p className="mt-1 text-xs text-ink-muted">
                    Added to {draft.data.recycledMaterialName} in the store.
                  </p>
                )}
              </div>

              <div className="mb-4 max-w-sm">
                <label className="field-label" htmlFor="recycler-notes">
                  Note <span className="font-normal text-ink-muted">(optional)</span>
                </label>
                <input
                  id="recycler-notes"
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
                disabled={save.isPending || nothingWeighed}
                onClick={() => {
                  save.mutate();
                }}
              >
                {save.isPending ? 'Saving…' : 'Record it and add it to the store'}
              </button>
              <p className="mt-2 text-xs text-ink-muted">
                Written once, at the end of the shift. The record and the material it puts
                back are one act — both, or neither.
              </p>
            </>
          )}
        </section>
      )}

      <h2 className="mb-3 text-lg font-bold text-ink">Recorded</h2>

      {records.isPending && <p className="p-6 text-ink-muted">Loading…</p>}
      {records.isError && <p className="p-6 text-bad">Could not load the recycler.</p>}

      {records.data?.length === 0 && (
        <p className="card p-8 text-center text-ink-muted">
          Nothing has been recycled yet.
        </p>
      )}

      {records.data !== undefined && records.data.length > 0 && (
        <div className="card overflow-x-auto">
          <table className="w-full text-left text-sm">
            <thead>
              <tr className="border-b border-line text-xs tracking-wider text-ink-muted uppercase">
                <th className="px-4 py-3 font-semibold">Shift</th>
                <th className="px-4 py-3 font-semibold">Line</th>
                <th className="px-4 py-3 text-right font-semibold">Produced</th>
                <th className="px-4 py-3 font-semibold">Recorded by</th>
              </tr>
            </thead>
            <tbody>
              {records.data.map((record) => (
                <tr key={record.id} className="border-b border-line last:border-0">
                  <td className="px-4 py-3">
                    {record.shiftName} · {formatDate(record.productionDate)}
                  </td>
                  <td className="px-4 py-3 text-ink-soft">{record.productionLineName}</td>
                  <td className="px-4 py-3 text-right font-semibold tabular-nums text-ink">
                    {record.recycledMaterialWeight} kg
                  </td>
                  <td className="px-4 py-3 text-ink-soft">
                    {record.recordedByName}
                    {record.notes !== null && (
                      <span className="block text-xs text-ink-muted">{record.notes}</span>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
            {/* What the shifts on this list produced altogether, so the figure does not
                have to be added up by hand. */}
            <tfoot>
              <tr className="border-t-2 border-line font-semibold">
                <td className="px-4 py-3 text-ink" colSpan={2}>
                  {records.data.length} shift{records.data.length === 1 ? '' : 's'}
                </td>
                <td className="px-4 py-3 text-right tabular-nums text-ink">
                  {records.data
                    .reduce((sum, r) => sum + r.recycledMaterialWeight, 0)
                    .toFixed(1)}{' '}
                  kg
                </td>
                <td className="px-4 py-3" />
              </tr>
            </tfoot>
          </table>
        </div>
      )}
    </>
  );
}

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
 * The smallest screen in the system: two weights. Scrap collected off the floor and
 * weighed in, recycled material weighed out, and the output goes back into the store.
 *
 * The loss between them is never typed and never stored — it is worked out from the two
 * figures on the record. What the thermo calculated is shown beside the box so the
 * operator sees the free check from section 11 while typing, but it is never enforced:
 * the two are measured different ways, and a shift that grinds an old pile breaks the
 * comparison honestly.
 */
export function RecyclerPage(): ReactElement {
  const queryClient = useQueryClient();
  const { hasRole } = useAuth();
  const canRecord = hasRole(RoleNames.Administrator, RoleNames.RecyclerOperator);

  const [recording, setRecording] = useState<StartableLine | null>(null);
  const [scrap, setScrap] = useState('');
  const [recycled, setRecycled] = useState('');
  const [notes, setNotes] = useState('');
  const [actionError, setActionError] = useState<string | null>(null);

  const records = useQuery({
    queryKey: ['recycler'],
    queryFn: () => recyclerApi.list(),
  });

  // Scrap is recorded where it is ground (specification section 4).
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
        scrapWeight: Number(scrap) || 0,
        recycledMaterialWeight: Number(recycled) || 0,
        notes: notes.trim() === '' ? null : notes.trim(),
      }),
    onSuccess: () => {
      setActionError(null);
      setRecording(null);
      setScrap('');
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
  const scrapNumber = Number(scrap) || 0;
  const recycledNumber = Number(recycled) || 0;
  const nothingWeighed = scrapNumber <= 0 && recycledNumber <= 0;

  return (
    <>
      <PageHeader
        title="Recycler"
        subtitle="Scrap weighed in, recycled material weighed out. The output goes back into the store, and the loss between the two is worked out for you."
        actions={
          canRecord ? (
            <StartOnLineButton
              lines={lines}
              action="Record the scrap"
              onStart={(shiftLineId) => {
                const line = lines.find((l) => l.shiftLineId === shiftLineId);
                if (line !== undefined) {
                  setRecording(line);
                  setScrap('');
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
          No recycling line is open. Scrap is recorded on the line that grinds it.
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
              <div className="mb-4 grid gap-4 sm:grid-cols-2">
                <div>
                  <label className="field-label" htmlFor="scrap-weight">
                    Scrap weighed in (kg)
                  </label>
                  <input
                    id="scrap-weight"
                    type="number"
                    step="0.001"
                    min="0"
                    className="field-input text-base"
                    value={scrap}
                    onChange={(event) => {
                      setScrap(event.target.value);
                    }}
                  />
                  {/* The free check from section 11: the thermo's own arithmetic,
                      shown beside the scale and never enforced. */}
                  {draft.data.thermoCalculatedScrap !== null && (
                    <p className="mt-1 text-xs text-ink-muted">
                      The thermo calculated {draft.data.thermoCalculatedScrap} kg lost
                      this shift.
                    </p>
                  )}
                </div>

                <div>
                  <label className="field-label" htmlFor="recycled-weight">
                    Recycled material weighed out (kg)
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
              </div>

              <div className="mb-4 rounded-control bg-canvas px-4 py-3">
                <Loss scrap={scrapNumber} recycled={recycledNumber} />
              </div>

              <div className="mb-4">
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
                <th className="px-4 py-3 text-right font-semibold">Scrap in</th>
                <th className="px-4 py-3 text-right font-semibold">Recycled out</th>
                <th className="px-4 py-3 text-right font-semibold">Loss</th>
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
                  <td className="px-4 py-3 text-right tabular-nums">
                    {record.scrapWeight} kg
                  </td>
                  <td className="px-4 py-3 text-right font-semibold tabular-nums text-ink">
                    {record.recycledMaterialWeight} kg
                  </td>
                  <td className="px-4 py-3 text-right">
                    <LossBadge value={record.lossPercentage} />
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
          </table>
        </div>
      )}
    </>
  );
}

/**
 * The loss as the operator types, so he sees it before saving rather than after.
 *
 * Says nothing at all where no scrap was weighed: a share of nothing is not a number,
 * and showing 0% would read as a perfect shift.
 */
function Loss({ scrap, recycled }: { scrap: number; recycled: number }): ReactElement {
  if (scrap <= 0) {
    return (
      <p className="text-sm text-ink-muted">
        {recycled > 0
          ? 'No scrap was weighed in, so there is no loss to work out — this is an old pile being ground.'
          : 'Weigh the scrap, what came out of it, or both.'}
      </p>
    );
  }

  const loss = ((scrap - recycled) / scrap) * 100;

  return (
    <div className="flex flex-wrap items-baseline justify-between gap-2">
      <span className="text-sm text-ink-soft">Loss</span>
      <span className="font-bold text-ink tabular-nums">
        {loss.toFixed(2)}%
        <span className="ml-2 text-sm font-normal text-ink-muted">
          {(scrap - recycled).toFixed(3)} kg
        </span>
      </span>
      {loss < 0 && (
        <p className="w-full text-xs text-ink-muted">
          More came out than went in, so this shift ground scrap it did not collect. That
          is allowed.
        </p>
      )}
    </div>
  );
}

/** Never a red badge: a high loss is the factory's news, not the system's complaint. */
function LossBadge({ value }: { value: number | null }): ReactElement {
  if (value === null) {
    return <span className="text-ink-muted">—</span>;
  }

  return (
    <span
      className={[
        'rounded-full px-2 py-0.5 text-xs font-semibold tabular-nums',
        value < 0 ? 'bg-brand-50 text-brand-700' : 'bg-canvas text-ink-soft',
      ].join(' ')}
    >
      {value.toFixed(2)}%
    </span>
  );
}

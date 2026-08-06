import { useQuery, useQueryClient } from '@tanstack/react-query';
import { useState, type ReactElement } from 'react';
import { PageHeader } from '../../components/ui/PageHeader';
import { useAuth } from '../../hooks/useAuth';
import { RoleNames } from '../../lib/roles';
import { LabelPrintScreen } from '../labels/LabelPrintScreen';
import { formatDate } from '../shifts/shiftFormat';
import { thermoApi, type ThermoRunDto, type ThermoRunSummaryDto } from './api';
import { ThermoTestDialog } from './ThermoTestDialog';

/**
 * The thermo's counting form (specification section 9).
 *
 * Runs waiting to be counted lead, because until they are there are no bags — and with
 * no bags nobody can build a pallet.
 */
export function ThermoTestsPage(): ReactElement {
  const queryClient = useQueryClient();
  const { hasRole } = useAuth();
  const canTest = hasRole(RoleNames.Administrator, RoleNames.ThermoTestPerson);

  const [waitingOnly, setWaitingOnly] = useState(true);
  const [counting, setCounting] = useState<ThermoRunSummaryDto | null>(null);
  const [justSaved, setJustSaved] = useState<ThermoRunDto | null>(null);

  // Saving the form is what creates the bags, so the labels come up straight after it.
  // A run makes a dozen or more at once and they print as one job — going to look each
  // one up in a list of five hundred is not a thing anybody would do.
  const [printing, setPrinting] = useState<{ barcodes: string[]; headline?: string } | null>(
    null,
  );

  const runs = useQuery({
    queryKey: ['thermo-runs', 'tests', waitingOnly],
    queryFn: () => thermoApi.runs(waitingOnly),
  });

  function invalidate(): void {
    void queryClient.invalidateQueries({ queryKey: ['thermo-runs'] });
    void queryClient.invalidateQueries({ queryKey: ['thermo-available-rolls'] });
  }

  if (runs.isPending) {
    return <p className="p-6 text-ink-muted">Loading…</p>;
  }

  if (runs.isError) {
    return <p className="p-6 text-bad">Could not load the runs.</p>;
  }

  // A run still in the machine stays on this list, marked as such. Only a finished one
  // can be counted — the bags are counted at the end, so until then the number does not
  // exist — but hiding the rest would tell the test person everything was done when a
  // roll was still running.
  const rows = runs.data;
  const ready = rows.filter((run) => run.isFinished).length;

  // Now that a running roll stays on the list, the table is only empty when there is
  // genuinely nothing — so two sentences cover it.
  const emptyMessage = waitingOnly
    ? 'Nothing is waiting. Every run has been counted.'
    : 'No roll has been formed yet.';

  return (
    <>
      <PageHeader
        title="Thermo Tests"
        subtitle="Bags, piece weight and bag weight, counted after the run. Saving the form is what creates the bags."
      />

      <section className="mb-6 flex flex-wrap gap-2">
        <Chip
          label={`Waiting to be counted${waitingOnly ? ` (${String(ready)})` : ''}`}
          active={waitingOnly}
          onClick={() => {
            setWaitingOnly(true);
          }}
        />
        <Chip
          label="Every run"
          active={!waitingOnly}
          onClick={() => {
            setWaitingOnly(false);
          }}
        />
      </section>

      {justSaved !== null && (
        <p className="mb-4 rounded-control border border-l-4 border-ok/30 border-l-ok bg-ok-soft px-4 py-3 text-sm font-medium text-ok">
          Roll <strong className="font-mono">{justSaved.rollCode}</strong> made{' '}
          {justSaved.bags.length} bag{justSaved.bags.length === 1 ? '' : 's'} of{' '}
          {justSaved.testReport?.productName} —{' '}
          {justSaved.testReport?.pieceCount.toLocaleString('en-GB')} pieces. The labels
          run from <strong className="font-mono">{justSaved.bags[0]?.barcode}</strong>.
        </p>
      )}

      <div className="card overflow-x-auto">
        <table className="w-full text-left text-sm">
          <thead>
            <tr className="border-b border-line text-xs tracking-wider text-ink-muted uppercase">
              <th className="px-4 py-3 font-semibold">Roll</th>
              <th className="px-4 py-3 font-semibold">Colour · recipe</th>
              <th className="px-4 py-3 font-semibold">Shift</th>
              <th className="px-4 py-3 text-right font-semibold">Minutes</th>
              <th className="px-4 py-3 font-semibold">Product</th>
              <th className="px-4 py-3 text-right font-semibold">Bags</th>
              <th className="px-4 py-3 text-right font-semibold">Pieces</th>
              <th className="px-4 py-3" />
            </tr>
          </thead>
          <tbody>
            {rows.length === 0 && (
              <tr>
                <td colSpan={8} className="px-4 py-8 text-center text-ink-muted">
                  {emptyMessage}
                </td>
              </tr>
            )}
            {rows.map((run) => (
              <tr key={run.id} className="border-b border-line last:border-0">
                <td className="px-4 py-3 font-mono font-semibold text-ink">
                  {run.rollCode}
                </td>
                <td className="px-4 py-3 text-ink-soft">
                  {run.colorName}
                  <span className="ml-2 text-xs text-ink-muted">
                    {run.recipeNumber} {run.recipeFamilyName}
                  </span>
                  {run.isAbsorbent && (
                    <span className="ml-2 rounded-full bg-brand-50 px-2 py-0.5 text-xs font-semibold text-brand-700">
                      ABS
                    </span>
                  )}
                </td>
                <td className="px-4 py-3 whitespace-nowrap text-ink-soft">
                  {run.shiftName} · {formatDate(run.productionDate)}
                </td>
                <td className="px-4 py-3 text-right tabular-nums text-ink-soft">
                  {run.totalTimeMinutes ?? '—'}
                </td>
                <td className="px-4 py-3 text-ink-soft">{run.productName ?? '—'}</td>
                <td className="px-4 py-3 text-right tabular-nums text-ink-soft">
                  {run.bagCount ?? '—'}
                </td>
                <td className="px-4 py-3 text-right tabular-nums text-ink-soft">
                  {run.pieceCount?.toLocaleString('en-GB') ?? '—'}
                </td>
                <td className="px-4 py-3">
                  <div className="flex justify-end">
                    {run.needsTest && run.isFinished && canTest && (
                      <button
                        type="button"
                        className="min-h-9 rounded-control border border-brand-600 bg-brand-600 px-3 text-sm font-medium whitespace-nowrap text-white transition-colors hover:bg-brand-700"
                        onClick={() => {
                          setCounting(run);
                        }}
                      >
                        Count
                      </button>
                    )}
                    {run.needsTest && !run.isFinished && (
                      <span className="text-xs text-ink-muted">Still in the machine</span>
                    )}
                    {/* Already counted, so the bags exist. A torn label must not mean
                        counting the run again. */}
                    {!run.needsTest && (
                      <button
                        type="button"
                        className="min-h-9 rounded-control border border-line px-3 text-sm font-medium whitespace-nowrap text-ink-soft transition-colors hover:border-brand-200 hover:bg-brand-50 hover:text-brand-700"
                        onClick={() => {
                          void thermoApi.run(run.id).then((full) => {
                            setPrinting({ barcodes: full.bags.map((b) => b.barcode) });
                          });
                        }}
                      >
                        Labels
                      </button>
                    )}
                  </div>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {printing !== null && (
        <LabelPrintScreen
          barcodes={printing.barcodes}
          {...(printing.headline === undefined ? {} : { headline: printing.headline })}
          onClose={() => {
            setPrinting(null);
          }}
        />
      )}

      {counting !== null && (
        <ThermoTestDialog
          run={counting}
          onClose={() => {
            setCounting(null);
          }}
          onSaved={(run) => {
            setJustSaved(run);
            setPrinting({
              barcodes: run.bags.map((bag) => bag.barcode),
              headline:
                `Roll ${run.rollCode} made ${String(run.bags.length)} bag` +
                `${run.bags.length === 1 ? '' : 's'}. Print the labels and stick one on each.`,
            });
            invalidate();
          }}
        />
      )}
    </>
  );
}

function Chip({
  label,
  active,
  onClick,
}: {
  label: string;
  active: boolean;
  onClick: () => void;
}): ReactElement {
  return (
    <button
      type="button"
      onClick={onClick}
      className={[
        'min-h-9 rounded-full border px-4 text-sm font-medium transition-colors',
        active
          ? 'border-brand-600 bg-brand-50 text-brand-700'
          : 'border-line text-ink-soft hover:border-brand-200 hover:bg-brand-50',
      ].join(' ')}
    >
      {label}
    </button>
  );
}

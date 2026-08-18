import { useQuery, useQueryClient } from '@tanstack/react-query';
import { useState, type ReactElement } from 'react';
import { useTranslation } from '../../hooks/useTranslation';
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
  const { t } = useTranslation();
  const queryClient = useQueryClient();
  const { hasRole } = useAuth();
  const canTest = hasRole(RoleNames.Administrator, RoleNames.ThermoTestPerson);

  const [waitingOnly, setWaitingOnly] = useState(true);
  const [counting, setCounting] = useState<ThermoRunSummaryDto | null>(null);
  const [justSaved, setJustSaved] = useState<ThermoRunDto | null>(null);

  // Saving the form is what creates the bags, so the labels come up straight after it.
  // A run makes a dozen or more at once and they print as one job — going to look each
  // one up in a list of five hundred is not a thing anybody would do.
  const [printing, setPrinting] = useState<{
    barcodes: string[];
    headline?: string;
  } | null>(null);

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

  // Only the runs that can say what they lost — a roll that was never weighed has no
  // waste figure, and counting it as zero would flatter the total.
  const weighed = rows.filter(
    (run) => run.scrapWeight !== null && run.rollWeight !== null && run.rollWeight > 0,
  );

  const totals =
    weighed.length === 0
      ? null
      : {
          runs: weighed.length,
          rollKg: weighed.reduce((sum, run) => sum + (run.rollWeight ?? 0), 0),
          scrapKg: weighed.reduce((sum, run) => sum + (run.scrapWeight ?? 0), 0),
        };

  // Now that a running roll stays on the list, the table is only empty when there is
  // genuinely nothing — so two sentences cover it.
  const emptyMessage = waitingOnly
    ? 'Nothing is waiting. Every run has been counted.'
    : 'No roll has been formed yet.';

  return (
    <>
      <PageHeader
        title={t('page.thermoTests.title')}
        subtitle={t('page.thermoTests.subtitle')}
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
          label={t('state.everyRun')}
          active={!waitingOnly}
          onClick={() => {
            setWaitingOnly(false);
          }}
        />
      </section>

      {justSaved !== null && (
        <p className="mb-4 rounded-control border border-s-4 border-ok/30 border-s-ok bg-ok-soft px-4 py-3 text-sm font-medium text-ok">
          {t('term.roll')} <strong className="font-mono">{justSaved.rollCode}</strong> made{' '}
          {justSaved.bags.length} bag{justSaved.bags.length === 1 ? '' : 's'} of{' '}
          {justSaved.testReport?.productName} —{' '}
          {justSaved.testReport?.pieceCount.toLocaleString('en-GB')} pieces. The labels
          run from <strong className="font-mono">{justSaved.bags[0]?.barcode}</strong>.
        </p>
      )}

      <div className="card overflow-x-auto">
        <table className="w-full text-start text-sm">
          <thead>
            <tr className="border-b border-line text-xs tracking-wider text-ink-muted uppercase">
              <th className="px-4 py-3 font-semibold">{t('term.roll')}</th>
              <th className="px-4 py-3 font-semibold">{t('term.colourRecipe')}</th>
              <th className="px-4 py-3 font-semibold">{t('term.shift')}</th>
              <th className="px-4 py-3 text-end font-semibold">{t('field.minutes')}</th>
              <th className="px-4 py-3 font-semibold">{t('term.product')}</th>
              <th className="px-4 py-3 text-end font-semibold">{t('term.bags')}</th>
              <th className="px-4 py-3 text-end font-semibold">{t('field.pieces')}</th>
              <th className="px-4 py-3 text-end font-semibold">Waste</th>
              <th className="px-4 py-3" />
            </tr>
          </thead>
          <tbody>
            {rows.length === 0 && (
              <tr>
                <td colSpan={9} className="px-4 py-8 text-center text-ink-muted">
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
                  <span className="ms-2 text-xs text-ink-muted">
                    {run.recipeNumber} {run.recipeFamilyName}
                  </span>
                  {run.isAbsorbent && (
                    <span className="ms-2 rounded-full bg-brand-50 px-2 py-0.5 text-xs font-semibold text-brand-700">
                      ABS
                    </span>
                  )}
                </td>
                <td className="px-4 py-3 whitespace-nowrap text-ink-soft">
                  {run.shiftName} · {formatDate(run.productionDate)}
                </td>
                <td className="px-4 py-3 text-end tabular-nums text-ink-soft">
                  {run.totalTimeMinutes ?? '—'}
                </td>
                <td className="px-4 py-3 text-ink-soft">{run.productName ?? '—'}</td>
                <td className="px-4 py-3 text-end tabular-nums text-ink-soft">
                  {run.bagCount ?? '—'}
                </td>
                <td className="px-4 py-3 text-end tabular-nums text-ink-soft">
                  {run.pieceCount?.toLocaleString('en-GB') ?? '—'}
                </td>
                <td className="px-4 py-3 text-end">
                  <Waste kg={run.scrapWeight} rollKg={run.rollWeight} />
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
          {/* The shift's own total, so the scrap figure does not have to be added up by
              hand off the rows. Only the runs that can say what they lost are in it —
              a roll that was never weighed is left out rather than counted as nothing
              (specification section 9). */}
          {totals !== null && (
            <tfoot>
              <tr className="border-t-2 border-line font-semibold">
                <td className="px-4 py-3 text-ink" colSpan={7}>
                  {totals.runs} run{totals.runs === 1 ? '' : 's'} counted ·{' '}
                  {totals.rollKg.toFixed(1)} kg of rolls formed
                </td>
                <td className="px-4 py-3 text-end tabular-nums text-ink">
                  {totals.scrapKg.toFixed(1)} kg
                  <span className="ms-2 text-xs font-normal text-ink-muted">
                    {((totals.scrapKg / totals.rollKg) * 100).toFixed(1)}%
                  </span>
                </td>
                <td className="px-4 py-3" />
              </tr>
            </tfoot>
          )}
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

/**
 * What the run threw away: the roll's weight less the weight of the plates it made
 * (specification section 9).
 *
 * The percentage is a share of the <b>roll</b> — how much of the material never became
 * product. Not to be confused with the recycler's loss, which is a share of the scrap
 * that went into the grinder. Both are named wherever they are shown.
 *
 * A dash, never a zero, until the roll has been weighed and the run counted.
 */
function Waste({
  kg,
  rollKg,
}: {
  kg: number | null;
  rollKg: number | null;
}): ReactElement {
  if (kg === null || rollKg === null || rollKg <= 0) {
    return <span className="text-ink-muted">—</span>;
  }

  return (
    <span className="tabular-nums text-ink-soft">
      {kg.toFixed(1)} kg
      <span className="ms-1 text-xs text-ink-muted">
        {((kg / rollKg) * 100).toFixed(0)}%
      </span>
    </span>
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

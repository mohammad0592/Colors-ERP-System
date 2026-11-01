import { useQuery } from '@tanstack/react-query';
import { useState, type ReactElement, type ReactNode } from 'react';
import { useSearchParams } from 'react-router-dom';
import { useTranslation } from '../../hooks/useTranslation';
import { PageHeader } from '../../components/ui/PageHeader';
import { ScanField } from '../../components/ui/ScanField';
import { ApiError } from '../../lib/apiClient';
import { formatDate } from '../shifts/shiftFormat';
import { traceApi, type TraceBagDto, type TraceDto } from './api';

/**
 * Where one thing came from, and what it became (specification section 13).
 *
 * Every other report is a summary. This one answers the opposite question: a man holds
 * one label and wants everything behind it. Nothing is stored for it — every link is
 * already a foreign key, so the page cannot disagree with the data because it is the
 * data.
 *
 * Read in order, the chain runs backwards down the page: the thing scanned, then what
 * made it, then what made that, ending at the materials. What it *became* comes last,
 * because the man scanning a roll already knows he has a roll.
 *
 * Everything recorded about each step is shown — every reading, note and correction,
 * and who did each thing when. A trace is usually a complaint being chased, and the one
 * fact left off is the one that was needed. A fact never recorded is left out rather
 * than shown as a dash, so the page stays as long as the story and no longer.
 */
export function TracePage(): ReactElement {
  const { t } = useTranslation();
  const [params, setParams] = useSearchParams();
  const asked = params.get('code') ?? '';
  const [code, setCode] = useState(asked);

  const trace = useQuery({
    queryKey: ['trace', asked],
    queryFn: () => traceApi.get(asked),
    enabled: asked !== '',
    retry: false,
  });

  return (
    <>
      <PageHeader title={t('page.trace.title')} subtitle={t('page.trace.subtitle')} />

      <div className="card mb-6 p-4">
        {/* No list here, and there should not be one. Every other screen offers what it
            already knows about; this one is asked about a label in somebody's hand, and
            a dropdown of every roll, bag and pallet ever made would answer nothing. */}
        <ScanField
          label={t('trace.codeLabel')}
          placeholder="B000081, R000012 or 09GN050826A"
          value={code}
          onChange={setCode}
          onSubmit={(entered) => {
            setParams(entered === '' ? {} : { code: entered });
          }}
          submitLabel="Trace it"
          busy={trace.isFetching && asked !== ''}
        />
      </div>

      {asked === '' && (
        <p className="card p-8 text-center text-ink-muted">{t('trace.nothingScanned')}</p>
      )}

      {trace.isPending && asked !== '' && <p className="p-6 text-ink-muted">Looking…</p>}

      {trace.isError && (
        <p
          role="alert"
          className="rounded-control border border-s-4 border-bad/30 border-s-bad bg-bad-soft px-4 py-3 text-sm font-medium text-bad"
        >
          {trace.error instanceof ApiError
            ? trace.error.message
            : t('trace.lookupFailed')}
        </p>
      )}

      {trace.data !== undefined && <Chain trace={trace.data} />}
    </>
  );
}

function Chain({ trace }: { trace: TraceDto }): ReactElement {
  const { t } = useTranslation();
  // A bag carries its own run; a roll lists every run it went through.
  const runs = trace.thermo !== null ? [trace.thermo] : trace.runs;
  const code =
    trace.bag !== null
      ? trace.bag.productCode
      : trace.kind === 'Pallet'
        ? (trace.pallet?.generalCode ?? null)
        : null;

  return (
    <>
      <div className="card mb-6 p-5">
        <p className="text-xs font-semibold tracking-wider text-ink-muted uppercase">
          {trace.kind}
        </p>
        <p className="font-mono text-2xl font-bold text-ink">{trace.barcode}</p>
        <p className="text-ink-soft">
          {trace.headline}
          {code !== null && <span className="ms-2 font-mono font-semibold">{code}</span>}
        </p>
      </div>

      {trace.pallet !== null && (
        <Step
          title={t('trace.onThePallet')}
          subtitle={`Pallet ${String(trace.pallet.palletNumber)}`}
        >
          <Facts
            rows={[
              ['Barcode', trace.pallet.barcode],
              ['General code', trace.pallet.generalCode],
              ['Holding', trace.pallet.productName ?? 'nothing yet'],
              ['Colour', trace.pallet.colorName ?? '—'],
              [
                'Bags',
                trace.pallet.capacity === null
                  ? String(trace.pallet.bagCount)
                  : `${String(trace.pallet.bagCount)} of ${String(trace.pallet.capacity)}`,
              ],
              ['Pieces', trace.pallet.pieceCount.toLocaleString('en-GB')],
              ['Weight', `${String(trace.pallet.weight)} kg`],
              ['Status', trace.pallet.status],
              [
                'Built on',
                `${trace.pallet.shiftName} · ${formatDate(trace.pallet.productionDate)}`,
              ],
              ['Started', doneBy(trace.pallet.createdAt, trace.pallet.createdByName)],
              ['Full', when(trace.pallet.completedAt)],
              ['Closed', doneBy(trace.pallet.closedAt, trace.pallet.closedByName)],
              ['Shipped', doneBy(trace.pallet.shippedAt, trace.pallet.shippedByName)],
              [
                'Shipping taken back',
                doneBy(
                  trace.pallet.shippingReversedAt,
                  trace.pallet.shippingReversedByName,
                ),
              ],
              ['Why taken back', trace.pallet.shippingReversalReason],
              [
                'Cancelled',
                doneBy(trace.pallet.cancelledAt, trace.pallet.cancelledByName),
              ],
              ['Why cancelled', trace.pallet.cancellationReason],
              ['Note', trace.pallet.notes],
            ]}
          />
        </Step>
      )}

      {trace.bag !== null && (
        <Step title={t('trace.theBag')} subtitle={trace.bag.productName}>
          <Facts
            rows={[
              ['Product code', trace.bag.productCode],
              ['Colour', trace.bag.colorName],
              ['Pieces', String(trace.bag.pieceCount)],
              ['Weight', `${String(trace.bag.weight)} kg`],
              ['Status', trace.bag.status],
              ['From roll', trace.bag.rollCode],
              ['Made', when(trace.bag.createdAt)],
              [
                'On pallet',
                trace.bag.palletNumber === null
                  ? null
                  : (trace.bag.palletBarcode ??
                    `Pallet ${String(trace.bag.palletNumber)}`),
              ],
              ['Note', trace.bag.notes],
              ['Removed', doneBy(trace.bag.removedAt, trace.bag.removedByName)],
              ['Why removed', trace.bag.removalReason],
            ]}
          />
        </Step>
      )}

      {trace.palletHistory.length > 0 && (
        <Step
          title={t('trace.palletHistory')}
          subtitle={`${String(trace.palletHistory.length)} time${trace.palletHistory.length === 1 ? '' : 's'}`}
        >
          <div className="overflow-x-auto">
            <table className="w-full text-start text-sm">
              <thead>
                <tr className="border-b border-line text-xs tracking-wider text-ink-muted uppercase">
                  <th className="py-2 pe-4 font-semibold">{t('term.pallet')}</th>
                  <th className="py-2 pe-4 font-semibold">Put on</th>
                  <th className="py-2 pe-4 font-semibold">Taken off</th>
                  <th className="py-2 font-semibold">Why</th>
                </tr>
              </thead>
              <tbody>
                {trace.palletHistory.map((move) => (
                  <tr
                    key={`${String(move.palletNumber)}-${move.assignedAt}`}
                    className="border-b border-line last:border-0"
                  >
                    <td className="py-2 pe-4 font-mono font-semibold text-ink">
                      {move.palletBarcode ?? `Pallet ${String(move.palletNumber)}`}
                    </td>
                    <td className="py-2 pe-4 text-ink-soft">
                      {doneBy(move.assignedAt, move.assignedByName)}
                    </td>
                    <td className="py-2 pe-4 text-ink-soft">
                      {doneBy(move.reversedAt, move.reversedByName) ?? 'still on it'}
                    </td>
                    <td className="py-2 text-ink-soft">{move.reversalReason ?? '—'}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </Step>
      )}

      {runs.map((run, index) => (
        <Step
          key={run.id}
          title={
            runs.length > 1
              ? `${t('trace.formedAtThermo')} — ${String(index + 1)} / ${String(runs.length)}`
              : t('trace.formedAtThermo')
          }
          subtitle={`${run.productionLineName} · ${run.shiftName} · ${formatDate(run.productionDate)}`}
        >
          <Facts
            rows={[
              ['Operator', run.operatorName],
              ['Product', run.productName ?? 'not named yet'],
              ['Time in', when(run.startedAt)],
              ['Time out', when(run.finishedAt) ?? 'still in the machine'],
              [
                'Time in the machine',
                run.totalTimeMinutes === null
                  ? null
                  : `${String(run.totalTimeMinutes)} min`,
              ],
              ['Taken out unfinished', run.unfinishedReason],
              ['Bags made', run.bagCount === null ? null : String(run.bagCount)],
              [
                'Pieces',
                run.pieceCount === null ? null : run.pieceCount.toLocaleString('en-GB'),
              ],
              [
                'Piece weight',
                run.pieceWeight === null ? null : `${String(run.pieceWeight)} g`,
              ],
              [
                'Bag weight',
                run.bagWeight === null ? null : `${String(run.bagWeight)} kg`,
              ],
              [
                'Absorbency',
                run.absorbentPercentage === null || run.absorbentPercentage === 0
                  ? null
                  : `${String(run.absorbentPercentage)} %`,
              ],
              [
                'Scrap',
                run.scrapWeight === null ? null : `${String(run.scrapWeight)} kg`,
              ],
              ['Counted', doneBy(run.countedAt, run.countedByName) ?? 'not counted yet'],
              ['Count note', run.countNotes],
              ['Corrected', doneBy(run.correctedAt, run.correctedByName)],
              ['Why corrected', run.correctionReason],
              ['Note', run.notes],
            ]}
          />
        </Step>
      ))}

      {trace.roll !== null && (
        <Step title={t('trace.theRoll')} subtitle={trace.roll.rollCode}>
          <Facts
            rows={[
              ['Barcode', trace.roll.barcode],
              [
                'Recipe',
                `${String(trace.roll.recipeNumber)} · ${trace.roll.recipeFamilyName}`,
              ],
              ['Colour', trace.roll.colorName],
              ['Formed into', trace.roll.productName],
              ['Status', trace.roll.status],
              [
                'Extruded on',
                `${trace.roll.shiftName} · ${formatDate(trace.roll.productionDate)}`,
              ],
              [
                'Out of the extruder',
                doneBy(trace.roll.producedAt, trace.roll.producedByName),
              ],
              [
                'Weight',
                trace.roll.weight === null
                  ? 'not measured'
                  : `${String(trace.roll.weight)} kg`,
              ],
              ['Length', trace.roll.length === null ? null : String(trace.roll.length)],
              ['Measured', doneBy(trace.roll.measuredAt, trace.roll.measuredByName)],
              ['Note', trace.roll.notes],
              ['Removed', doneBy(trace.roll.removedAt, trace.roll.removedByName)],
              ['Why removed', trace.roll.removalReason],
            ]}
          />
        </Step>
      )}

      {trace.roll?.test != null && (
        <Step title={t('trace.rollTest')} subtitle={when(trace.roll.test.testedAt) ?? ''}>
          <Facts
            rows={[
              ['Tested by', trace.roll.test.testedByName],
              ['Plate weight', grams(trace.roll.test.plateWeight)],
              ['Thickness RS', mm(trace.roll.test.thicknessRs)],
              ['Thickness RM', mm(trace.roll.test.thicknessRm)],
              ['Thickness LM', mm(trace.roll.test.thicknessLm)],
              ['Thickness LS', mm(trace.roll.test.thicknessLs)],
              ['Average thickness', mm(trace.roll.test.averageThickness)],
              [
                'Thickness in range',
                trace.roll.test.thicknessInSpec === null
                  ? null
                  : trace.roll.test.thicknessInSpec
                    ? 'Yes'
                    : 'No',
              ],
              ['Dry weight', grams(trace.roll.test.absorbencyDryWeight)],
              ['Wet weight', grams(trace.roll.test.absorbencyWetWeight)],
              [
                'Absorbency',
                trace.roll.test.absorbencyPercentage === null
                  ? null
                  : `${String(trace.roll.test.absorbencyPercentage)} %`,
              ],
              ['Note', trace.roll.test.notes],
            ]}
          />
        </Step>
      )}

      {trace.mix !== null && (
        <Step
          title={t('trace.theMix')}
          subtitle={`${trace.mix.productionLineName} · ${trace.mix.shiftName} · ${formatDate(trace.mix.productionDate)} · batch ${String(trace.mix.batchNumber)}`}
        >
          {trace.mix.materials.length === 0 ? (
            <p className="text-sm text-ink-muted">{t('trace.noMaterialIssued')}</p>
          ) : (
            <div className="overflow-x-auto">
              <table className="w-full text-start text-sm">
                <thead>
                  <tr className="border-b border-line text-xs tracking-wider text-ink-muted uppercase">
                    <th className="py-2 pe-4 font-semibold">{t('term.ticket')}</th>
                    <th className="py-2 pe-4 font-semibold">{t('term.material')}</th>
                    <th className="py-2 pe-4 text-end font-semibold">
                      {t('field.issued')}
                    </th>
                    <th className="py-2 pe-4 text-end font-semibold">
                      {t('field.returned')}
                    </th>
                    <th className="py-2 text-end font-semibold">{t('field.used')}</th>
                  </tr>
                </thead>
                <tbody>
                  {trace.mix.materials.map((line, index) => (
                    <tr
                      key={`${String(line.ticketNumber)}-${line.material}-${String(index)}`}
                      className="border-b border-line last:border-0"
                    >
                      <td className="py-2 pe-4 text-ink-muted">{line.ticketNumber}</td>
                      <td className="py-2 pe-4 text-ink">{line.material}</td>
                      <td className="py-2 pe-4 text-end tabular-nums text-ink-soft">
                        {line.issued} {line.unitSymbol}
                      </td>
                      <td className="py-2 pe-4 text-end tabular-nums text-ink-soft">
                        {line.returned}
                      </td>
                      <td className="py-2 text-end font-semibold tabular-nums text-ink">
                        {line.used}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}

          {/* Said out loud rather than hidden: the ticket names the shift line, not the
              mix, so this is what the shift took out — not what is provably in this
              roll. With one mix per shift they are the same set. */}
          {trace.mix.issuedToShiftNotMix && (
            <p className="mt-3 rounded-control border border-s-4 border-warn/30 border-s-warn bg-warn-soft px-4 py-2 text-xs font-medium text-warn">
              These are the materials issued to the shift that made this roll. The ticket
              names the shift, not the mix — with one mix per shift that is the same
              material, but the system cannot prove it roll by roll.
            </p>
          )}
        </Step>
      )}

      {trace.bags.length > 0 && (
        <Step
          title={trace.kind === 'Pallet' ? t('trace.bagsOnIt') : t('trace.whatItBecame')}
          subtitle={`${String(trace.bags.length)} bag${trace.bags.length === 1 ? '' : 's'}`}
        >
          <BagTable bags={trace.bags} />
        </Step>
      )}
    </>
  );
}

/** "01/11/2026 12:44", or null when it has not happened. */
function when(at: string | null): string | null {
  if (at === null) {
    return null;
  }
  const moment = new Date(at);
  return `${moment.toLocaleDateString('en-GB')} ${moment.toLocaleTimeString('en-GB', {
    hour: '2-digit',
    minute: '2-digit',
  })}`;
}

/** "01/11/2026 12:44 · Mohammad", or null when it was never done. */
function doneBy(at: string | null, name: string | null): string | null {
  const moment = when(at);
  if (moment === null) {
    return null;
  }
  return name === null ? moment : `${moment} · ${name}`;
}

function grams(value: number | null): string | null {
  return value === null ? null : `${String(value)} g`;
}

function mm(value: number | null): string | null {
  return value === null ? null : `${String(value)} mm`;
}

function BagTable({ bags }: { bags: TraceBagDto[] }): ReactElement {
  const { t } = useTranslation();

  // A pallet built from more than one roll is exactly what the bag barcodes exist for,
  // so the roll is a column and not a footnote.
  const rolls = [...new Set(bags.map((b) => b.rollCode))];

  return (
    <>
      {rolls.length > 1 && (
        <p className="mb-3 text-sm text-ink-soft">
          {t('field.from')} <strong>{rolls.length}</strong> different rolls:{' '}
          {rolls.join(', ')}
        </p>
      )}

      <div className="overflow-x-auto">
        <table className="w-full text-start text-sm">
          <thead>
            <tr className="border-b border-line text-xs tracking-wider text-ink-muted uppercase">
              <th className="py-2 pe-4 font-semibold">{t('term.bag')}</th>
              <th className="py-2 pe-4 font-semibold">{t('term.fromRoll')}</th>
              <th className="py-2 pe-4 font-semibold">{t('term.product')}</th>
              <th className="py-2 pe-4 text-end font-semibold">{t('field.pieces')}</th>
              <th className="py-2 pe-4 text-end font-semibold">{t('field.weight')}</th>
              <th className="py-2 pe-4 font-semibold">{t('field.status')}</th>
              <th className="py-2 font-semibold">{t('term.pallet')}</th>
            </tr>
          </thead>
          <tbody>
            {bags.map((bag) => (
              <tr key={bag.id} className="border-b border-line last:border-0">
                <td className="py-2 pe-4 font-mono font-semibold text-ink">
                  {bag.barcode}
                </td>
                <td className="py-2 pe-4 font-mono text-xs text-ink-soft">
                  {bag.rollCode}
                </td>
                <td className="py-2 pe-4 text-ink-soft">
                  {bag.productName} · {bag.colorName}
                  <span className="ms-2 font-mono text-xs">{bag.productCode}</span>
                </td>
                <td className="py-2 pe-4 text-end tabular-nums text-ink-soft">
                  {bag.pieceCount}
                </td>
                <td className="py-2 pe-4 text-end tabular-nums text-ink-soft">
                  {bag.weight}
                </td>
                <td className="py-2 pe-4 text-ink-soft">
                  {bag.status}
                  {/* Why a bag is cancelled or defective, where the eye already is. */}
                  {bag.removalReason !== null && (
                    <span className="block text-xs text-warn">{bag.removalReason}</span>
                  )}
                </td>
                <td className="py-2 font-mono text-xs text-ink-soft">
                  {bag.palletBarcode ??
                    (bag.palletNumber === null ? '—' : String(bag.palletNumber))}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </>
  );
}

function Step({
  title,
  subtitle,
  children,
}: {
  title: string;
  subtitle: string;
  children: ReactNode;
}): ReactElement {
  return (
    <section className="card mb-4 p-5">
      <div className="mb-3 border-b border-line pb-2">
        <h2 className="font-bold text-ink">{title}</h2>
        <p className="text-sm text-ink-muted">{subtitle}</p>
      </div>
      {children}
    </section>
  );
}

/** Name and value pairs. A null value was never recorded, and its row is left out. */
function Facts({ rows }: { rows: [string, string | null][] }): ReactElement {
  const shown = rows.filter((row): row is [string, string] => row[1] !== null);
  return (
    <dl className="grid gap-x-6 gap-y-1 text-sm sm:grid-cols-2">
      {shown.map(([label, value]) => (
        <div key={label} className="flex justify-between gap-3 border-b border-line py-1">
          <dt className="text-ink-soft">{label}</dt>
          <dd className="text-end font-semibold text-ink">{value}</dd>
        </div>
      ))}
    </dl>
  );
}

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
          label="Barcode or roll code"
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
        <p className="card p-8 text-center text-ink-muted">
          Nothing scanned yet. A label or a roll code both work.
        </p>
      )}

      {trace.isPending && asked !== '' && <p className="p-6 text-ink-muted">Looking…</p>}

      {trace.isError && (
        <p
          role="alert"
          className="rounded-control border border-s-4 border-bad/30 border-s-bad bg-bad-soft px-4 py-3 text-sm font-medium text-bad"
        >
          {trace.error instanceof ApiError
            ? trace.error.message
            : 'Could not look that up.'}
        </p>
      )}

      {trace.data !== undefined && <Chain trace={trace.data} />}
    </>
  );
}

function Chain({ trace }: { trace: TraceDto }): ReactElement {
  const { t } = useTranslation();
  return (
    <>
      <div className="card mb-6 p-5">
        <p className="text-xs font-semibold tracking-wider text-ink-muted uppercase">
          {trace.kind}
        </p>
        <p className="font-mono text-2xl font-bold text-ink">{trace.barcode}</p>
        <p className="text-ink-soft">{trace.headline}</p>
      </div>

      {trace.pallet !== null && (
        <Step
          title="On the pallet"
          subtitle={`Pallet ${String(trace.pallet.palletNumber)}`}
        >
          <Facts
            rows={[
              ['Barcode', trace.pallet.barcode],
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
            ]}
          />
        </Step>
      )}

      {trace.bag !== null && (
        <Step title="The bag" subtitle={trace.bag.productName}>
          <Facts
            rows={[
              ['Colour', trace.bag.colorName],
              ['Pieces', String(trace.bag.pieceCount)],
              ['Weight', `${String(trace.bag.weight)} kg`],
              ['Status', trace.bag.status],
              ['From roll', trace.bag.rollCode],
            ]}
          />
        </Step>
      )}

      {trace.thermo !== null && (
        <Step
          title="Formed at the thermo"
          subtitle={`${trace.thermo.shiftName} · ${formatDate(trace.thermo.productionDate)}`}
        >
          <Facts
            rows={[
              ['Operator', trace.thermo.operatorName],
              ['Mould', trace.thermo.mouldName ?? '—'],
              ['Product', trace.thermo.productName ?? 'not counted yet'],
              [
                'Time in the machine',
                trace.thermo.totalTimeMinutes === null
                  ? 'still running'
                  : `${String(trace.thermo.totalTimeMinutes)} min`,
              ],
              [
                'Bags made',
                trace.thermo.bagCount === null ? '—' : String(trace.thermo.bagCount),
              ],
              [
                'Pieces',
                trace.thermo.pieceCount === null
                  ? '—'
                  : trace.thermo.pieceCount.toLocaleString('en-GB'),
              ],
              [
                'Piece weight',
                trace.thermo.pieceWeight === null
                  ? '—'
                  : `${String(trace.thermo.pieceWeight)} g`,
              ],
              [
                'Absorbency',
                trace.thermo.absorbentPercentage === null
                  ? '—'
                  : `${String(trace.thermo.absorbentPercentage)} %`,
              ],
            ]}
          />
        </Step>
      )}

      {trace.roll !== null && (
        <Step title="The roll" subtitle={trace.roll.rollCode}>
          <Facts
            rows={[
              ['Barcode', trace.roll.barcode],
              [
                'Recipe',
                `${String(trace.roll.recipeNumber)} · ${trace.roll.recipeFamilyName}`,
              ],
              ['Colour', trace.roll.colorName],
              ['Status', trace.roll.status],
              [
                'Extruded on',
                `${trace.roll.shiftName} · ${formatDate(trace.roll.productionDate)}`,
              ],
              ['By', trace.roll.producedByName],
              [
                'Weight',
                trace.roll.weight === null
                  ? 'not measured'
                  : `${String(trace.roll.weight)} kg`,
              ],
              ['Length', trace.roll.length === null ? '—' : String(trace.roll.length)],
              [
                'Plate weight',
                trace.roll.plateWeight === null
                  ? '—'
                  : `${String(trace.roll.plateWeight)} g`,
              ],
              [
                'Thickness',
                trace.roll.averageThickness === null
                  ? '—'
                  : String(trace.roll.averageThickness),
              ],
            ]}
          />
        </Step>
      )}

      {trace.mix !== null && (
        <Step
          title="The mix, and what went into the shift"
          subtitle={`${trace.mix.productionLineName} · ${trace.mix.shiftName} · ${formatDate(trace.mix.productionDate)}`}
        >
          {trace.mix.materials.length === 0 ? (
            <p className="text-sm text-ink-muted">
              No material was issued to this shift on a ticket.
            </p>
          ) : (
            <div className="overflow-x-auto">
              <table className="w-full text-start text-sm">
                <thead>
                  <tr className="border-b border-line text-xs tracking-wider text-ink-muted uppercase">
                    <th className="py-2 pe-4 font-semibold">{t('term.ticket')}</th>
                    <th className="py-2 pe-4 font-semibold">{t('term.material')}</th>
                    <th className="py-2 pe-4 text-end font-semibold">{t('field.issued')}</th>
                    <th className="py-2 pe-4 text-end font-semibold">{t('field.returned')}</th>
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
          title={trace.kind === 'Pallet' ? 'The bags on it' : 'What it became'}
          subtitle={`${String(trace.bags.length)} bag${trace.bags.length === 1 ? '' : 's'}`}
        >
          <BagTable bags={trace.bags} />
        </Step>
      )}
    </>
  );
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
          {t('field.from')} <strong>{rolls.length}</strong> different rolls: {rolls.join(', ')}
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
                </td>
                <td className="py-2 pe-4 text-end tabular-nums text-ink-soft">
                  {bag.pieceCount}
                </td>
                <td className="py-2 pe-4 text-end tabular-nums text-ink-soft">
                  {bag.weight}
                </td>
                <td className="py-2 pe-4 text-ink-soft">{bag.status}</td>
                <td className="py-2 text-ink-soft">{bag.palletNumber ?? '—'}</td>
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

function Facts({ rows }: { rows: [string, string][] }): ReactElement {
  return (
    <dl className="grid gap-x-6 gap-y-1 text-sm sm:grid-cols-2">
      {rows.map(([label, value]) => (
        <div key={label} className="flex justify-between gap-3 border-b border-line py-1">
          <dt className="text-ink-soft">{label}</dt>
          <dd className="text-end font-semibold text-ink">{value}</dd>
        </div>
      ))}
    </dl>
  );
}

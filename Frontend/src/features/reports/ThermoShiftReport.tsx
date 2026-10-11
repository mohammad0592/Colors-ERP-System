import { useState, type ReactElement, type ReactNode } from 'react';
import { useTranslation } from '../../hooks/useTranslation';
import { ApiError } from '../../lib/apiClient';
import { formatDate } from '../shifts/shiftFormat';
import { reportsApi, type ThermoShiftReportDto } from './api';

/**
 * The thermoforming line's report for one shift (specification section 13).
 *
 * Laid out for reading on a screen — figures first, then the rolls, the yield, the
 * pallets and the packaging. The Excel export is the factory's own paper form instead,
 * cell for cell, because that is what gets printed and filed.
 */
export function ThermoShiftReport({
  report,
}: {
  report: ThermoShiftReportDto;
}): ReactElement {
  const { t } = useTranslation();
  const [exporting, setExporting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function exportExcel(): Promise<void> {
    setError(null);
    setExporting(true);
    try {
      await reportsApi.thermoShiftExcel(report);
    } catch (caught) {
      setError(caught instanceof ApiError ? caught.message : t('msg.reportFailed'));
    } finally {
      setExporting(false);
    }
  }

  const counted = report.rolls.filter((roll) => roll.bagCount !== null);

  return (
    <>
      <section className="card mb-4 flex flex-wrap items-center justify-between gap-4 p-5">
        <div>
          <h2 className="text-lg font-bold text-ink">
            {report.lineName} · {t('term.shift')} {report.shiftName} ·{' '}
            {formatDate(report.productionDate)}
          </h2>
          <p className="text-sm text-ink-muted">{report.status}</p>
        </div>
        <button
          type="button"
          className="btn-primary w-auto px-5"
          disabled={exporting}
          onClick={() => {
            void exportExcel();
          }}
        >
          {exporting ? t('thermoRpt.exporting') : t('thermoRpt.export')}
        </button>
      </section>

      {error !== null && (
        <p
          role="alert"
          className="mb-4 rounded-control border border-s-4 border-bad/30 border-s-bad bg-bad-soft px-4 py-3 text-sm font-medium text-bad"
        >
          {error}
        </p>
      )}

      <section className="mb-6 grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
        <Figure label={t('thermoRpt.operator')} value={report.operatorName ?? '—'}>
          {report.operatorEmployeeNumber ?? t('thermoRpt.notSet')}
        </Figure>
        <Figure
          label={t('thermoRpt.hours')}
          value={`${report.productionStartTime ?? '—'} – ${report.productionEndTime ?? '—'}`}
        >
          {num(report.downtimeHours)} h {t('thermoRpt.down')} ·{' '}
          {num(report.actualProductionHours)} h {t('thermoRpt.producing')}
        </Figure>
        <Figure
          label={t('thermoRpt.electricity')}
          value={`${num(report.electricityUsed)} kWh`}
        >
          {num(report.electricityStartMeter)} → {num(report.electricityEndMeter)}
        </Figure>
        <Figure
          label={t('thermoRpt.machine')}
          value={`${num(report.machineSpeed)} · ${num(report.feedDistanceMm)} mm`}
        >
          {t('thermoRpt.speed')} · {t('thermoRpt.feed')} · {t('thermoRpt.cycle')}{' '}
          {num(report.cycleTimeSeconds)} s
        </Figure>
      </section>

      <section className="card mb-6 grid gap-4 p-5 sm:grid-cols-2">
        <People label={t('term.crew')} names={report.workers} />
        <People label={t('thermoRpt.trainees')} names={report.trainees} />
      </section>

      <Block title={t('thermoRpt.rolls')}>
        {report.rolls.length === 0 ? (
          <Empty>{t('thermoRpt.noRolls')}</Empty>
        ) : (
          <Table
            head={[
              t('term.roll'),
              t('thermoRpt.weight'),
              t('thermoRpt.length'),
              t('thermoRpt.thickness'),
              t('thermoRpt.time'),
              t('term.product'),
              t('term.bags'),
              t('thermoRpt.plates'),
              t('thermoRpt.plateWeight'),
              t('thermoRpt.absorbency'),
              t('thermoRpt.bagWeight'),
              t('thermoRpt.recycled'),
              t('thermoRpt.madeOn'),
              t('thermoRpt.scrap'),
            ]}
            numericFrom={1}
            textColumns={[5, 12]}
          >
            {report.rolls.map((roll) => (
              <tr key={roll.rollCode} className="border-b border-line last:border-0">
                <td className="px-3 py-2 font-mono text-xs whitespace-nowrap text-ink">
                  {roll.rollCode}
                  <span className="ms-2 font-sans text-ink-muted">{roll.colorName}</span>
                </td>
                <Num value={roll.rollWeight} />
                <Num value={roll.rollLength} />
                <Num value={roll.averageThickness} />
                <Num value={roll.totalTimeMinutes} />
                <td className="px-3 py-2 text-ink-soft">{roll.productName ?? '—'}</td>
                {roll.bagCount === null ? (
                  <td
                    colSpan={6}
                    className="px-3 py-2 text-center text-xs text-ink-muted"
                  >
                    {t('thermoRpt.notCounted')}
                  </td>
                ) : (
                  <>
                    <Num value={roll.bagCount} />
                    <Num value={roll.pieceCount} />
                    <Num value={roll.pieceWeight} />
                    <Num value={roll.isAbsorbent ? roll.absorbentPercentage : null} />
                    <Num value={roll.bagWeight} />
                    <Num value={roll.recycledPercentage} />
                  </>
                )}
                <td className="px-3 py-2 text-center text-ink-soft">
                  {roll.rollShiftName}
                </td>
                <Num value={roll.scrapWeight} />
              </tr>
            ))}
            <tr className="bg-canvas font-semibold text-ink">
              <td className="px-3 py-2">{t('thermoRpt.total')}</td>
              <Num value={report.totalRollWeight} />
              <td colSpan={4} />
              <Num value={report.totalBagCount} />
              <Num value={report.totalPieceCount} />
              <td colSpan={5} />
              <Num
                value={
                  counted.length === 0
                    ? null
                    : round(
                        counted.reduce((sum, roll) => sum + (roll.scrapWeight ?? 0), 0),
                      )
                }
              />
            </tr>
          </Table>
        )}
      </Block>

      {report.products.length > 0 && (
        <Block title={t('thermoRpt.yield')}>
          <Table
            head={[
              t('term.product'),
              t('thermoRpt.code'),
              t('thermoRpt.rollsUsed'),
              t('thermoRpt.plates'),
              t('thermoRpt.productWeight'),
              t('thermoRpt.scrap'),
              t('thermoRpt.lossPct'),
            ]}
            numericFrom={2}
          >
            {report.products.map((product) => (
              <tr
                key={product.productName}
                className="border-b border-line last:border-0"
              >
                <td className="px-3 py-2 font-medium text-ink">{product.productName}</td>
                <td className="px-3 py-2 font-mono text-xs text-ink-soft">
                  {product.productCode}
                </td>
                <Num value={product.rollWeight} />
                <Num value={product.pieceCount} />
                <Num value={product.productWeight} />
                <Num value={product.scrapWeight} />
                <td className="px-3 py-2 text-end font-semibold text-ink tabular-nums">
                  {product.scrapPercentage === null
                    ? '—'
                    : `${String(product.scrapPercentage)}%`}
                </td>
              </tr>
            ))}
          </Table>
        </Block>
      )}

      <Block title={t('thermoRpt.pallets')}>
        {report.pallets.length === 0 ? (
          <Empty>{t('thermoRpt.noPallets')}</Empty>
        ) : (
          <Table
            head={[
              t('thermoRpt.pallet'),
              t('term.product'),
              t('thermoRpt.colour'),
              t('term.bags'),
              t('thermoRpt.plates'),
              t('field.status'),
              t('thermoRpt.fromRolls'),
            ]}
            numericFrom={3}
            textColumns={[5, 6]}
          >
            {report.pallets.map((pallet) => (
              <tr
                key={pallet.palletNumber}
                className="border-b border-line last:border-0"
              >
                <td className="px-3 py-2 font-bold text-ink">{pallet.palletNumber}</td>
                <td className="px-3 py-2 text-ink-soft">{pallet.productName ?? '—'}</td>
                <td className="px-3 py-2 text-ink-soft">{pallet.colorName ?? '—'}</td>
                <Num value={pallet.bagCount} />
                <Num value={pallet.pieceCount} />
                <td className="px-3 py-2 text-ink-soft">{pallet.status}</td>
                <td className="px-3 py-2 font-mono text-xs text-ink-muted">
                  {pallet.rollCodes.join(', ') || '—'}
                </td>
              </tr>
            ))}
            <tr className="bg-canvas font-semibold text-ink">
              <td className="px-3 py-2">{report.pallets.length}</td>
              <td colSpan={2} />
              <Num value={report.pallets.reduce((sum, p) => sum + p.bagCount, 0)} />
              <Num
                value={report.pallets.reduce((sum, p) => sum + (p.pieceCount ?? 0), 0)}
              />
              <td colSpan={2} />
            </tr>
          </Table>
        )}
      </Block>

      <Block title={t('thermoRpt.packaging')}>
        {!report.packagingRecorded ? (
          <Empty>{t('thermoRpt.packagingNotRecorded')}</Empty>
        ) : (
          <Table
            head={[t('term.material'), t('thermoRpt.quantity'), t('thermoRpt.weight')]}
            numericFrom={1}
          >
            {report.packaging.map((line) => (
              <tr key={line.materialName} className="border-b border-line last:border-0">
                <td className="px-3 py-2 text-ink">{line.materialName}</td>
                <td className="px-3 py-2 text-end text-ink-soft tabular-nums">
                  {line.quantity}{' '}
                  <span className="text-ink-muted">{line.unitSymbol}</span>
                </td>
                <Num value={line.weight} />
              </tr>
            ))}
          </Table>
        )}
      </Block>

      {report.notes !== null && (
        <Block title={t('field.notes')}>
          <p className="p-4 text-sm whitespace-pre-line text-ink-soft">{report.notes}</p>
        </Block>
      )}
    </>
  );
}

function num(value: number | null): string {
  return value === null ? '—' : value.toLocaleString('en-GB');
}

function round(value: number): number {
  return Math.round(value * 1000) / 1000;
}

function Figure({
  label,
  value,
  children,
}: {
  label: string;
  value: string;
  children: ReactNode;
}): ReactElement {
  return (
    <div className="card p-4">
      <p className="text-xs font-semibold tracking-wider text-ink-muted uppercase">
        {label}
      </p>
      <p className="mt-1 text-xl font-bold text-ink">{value}</p>
      <p className="mt-1 text-xs text-ink-muted">{children}</p>
    </div>
  );
}

function People({ label, names }: { label: string; names: string[] }): ReactElement {
  return (
    <div>
      <p className="mb-2 text-xs font-semibold tracking-wider text-ink-muted uppercase">
        {label}
      </p>
      {names.length === 0 ? (
        <p className="text-sm text-ink-muted">—</p>
      ) : (
        <div className="flex flex-wrap gap-1.5">
          {names.map((name) => (
            <span
              key={name}
              className="rounded-full bg-canvas px-2.5 py-1 text-sm font-medium text-ink-soft"
            >
              {name}
            </span>
          ))}
        </div>
      )}
    </div>
  );
}

function Block({
  title,
  children,
}: {
  title: string;
  children: ReactNode;
}): ReactElement {
  return (
    <section className="mb-6">
      <h3 className="mb-2 text-sm font-bold tracking-wider text-ink-muted uppercase">
        {title}
      </h3>
      <div className="card overflow-x-auto">{children}</div>
    </section>
  );
}

function Empty({ children }: { children: ReactNode }): ReactElement {
  return <p className="p-6 text-center text-sm text-ink-muted">{children}</p>;
}

/**
 * A table whose numeric columns line up on the right. `numericFrom` is the first column
 * that holds numbers; `textColumns` are the exceptions after it.
 */
function Table({
  head,
  numericFrom,
  textColumns = [],
  children,
}: {
  head: string[];
  numericFrom: number;
  textColumns?: number[];
  children: ReactNode;
}): ReactElement {
  return (
    <table className="w-full text-start text-sm">
      <thead>
        <tr className="border-b border-line text-xs tracking-wider text-ink-muted uppercase">
          {head.map((label, index) => (
            <th
              key={label}
              className={[
                'px-3 py-2.5 font-semibold whitespace-nowrap',
                index >= numericFrom && !textColumns.includes(index)
                  ? 'text-end'
                  : 'text-start',
              ].join(' ')}
            >
              {label}
            </th>
          ))}
        </tr>
      </thead>
      <tbody>{children}</tbody>
    </table>
  );
}

function Num({ value }: { value: number | null }): ReactElement {
  return (
    <td className="px-3 py-2 text-end whitespace-nowrap text-ink-soft tabular-nums">
      {num(value)}
    </td>
  );
}

import { useState, type ReactElement, type ReactNode } from 'react';
import { useTranslation } from '../../hooks/useTranslation';
import { ApiError } from '../../lib/apiClient';
import { RollStatusBadge } from '../production/RollStatusBadge';
import { ThicknessVerdict } from '../production/ThicknessVerdict';
import { formatDate } from '../shifts/shiftFormat';
import { reportsApi, type ExtruderShiftReportDto } from './api';

/**
 * The extruder's report for one shift (specification section 13.1): every roll made on it,
 * with everything recorded against each — what it was made to and for, its weight and
 * length, its test, who did what, and where it went.
 *
 * Figures first, then the rolls, then the shift's rolls by product. The Excel export has
 * the same rows, laid out to print.
 */
export function ExtruderShiftReport({
  report,
}: {
  report: ExtruderShiftReportDto;
}): ReactElement {
  const { t } = useTranslation();
  const [exporting, setExporting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function exportExcel(): Promise<void> {
    setError(null);
    setExporting(true);
    try {
      await reportsApi.extruderShiftExcel(report);
    } catch (caught) {
      setError(caught instanceof ApiError ? caught.message : t('msg.reportFailed'));
    } finally {
      setExporting(false);
    }
  }

  return (
    <>
      <section className="card mb-4 flex flex-wrap items-center justify-between gap-4 p-5">
        <div>
          <h2 className="text-lg font-bold text-ink">
            {report.lineName} · {t('term.shift')} {report.shiftName} ·{' '}
            {formatDate(report.productionDate)}
          </h2>
          <p className="text-sm text-ink-muted">
            {report.status}
            {report.batchNumbers.length > 0 &&
              ` · ${t('extruderRpt.batch')} ${report.batchNumbers.join(', ')}`}
          </p>
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
        <Figure label={t('extruderRpt.rollCount')} value={String(report.rollCount)}>
          {report.measuredCount} {t('extruderRpt.measured')} · {report.testedCount}{' '}
          {t('extruderRpt.tested')}
        </Figure>
        <Figure
          label={t('extruderRpt.weightLength')}
          value={`${num(report.totalWeight)} kg`}
        >
          {num(report.totalLength)} {t('thermoRpt.length').toLowerCase()}
        </Figure>
        <Figure label={t('extruderRpt.ready')} value={String(report.readyCount)}>
          {report.outOfSpecCount} {t('extruderRpt.outOfSpec')}
        </Figure>
        <Figure
          label={t('thermoRpt.hours')}
          value={`${report.productionStartTime ?? '—'} – ${report.productionEndTime ?? '—'}`}
        >
          {num(report.downtimeHours)} h {t('thermoRpt.down')} ·{' '}
          {num(report.actualProductionHours)} h {t('thermoRpt.producing')}
        </Figure>
      </section>

      <section className="card mb-6 grid gap-4 p-5 sm:grid-cols-3">
        <People
          label={t('thermoRpt.operator')}
          names={report.operatorName === null ? [] : [report.operatorName]}
        />
        <People label={t('term.crew')} names={report.workers} />
        <People label={t('thermoRpt.trainees')} names={report.trainees} />
      </section>

      <Block title={t('extruderRpt.rolls')}>
        {report.rolls.length === 0 ? (
          <Empty>{t('extruderRpt.noRolls')}</Empty>
        ) : (
          <table className="w-full text-start text-sm">
            <thead>
              <tr className="border-b border-line text-xs tracking-wider text-ink-muted uppercase">
                <Th>{t('term.rollCode')}</Th>
                <Th>{t('rolls.outAt')}</Th>
                <Th>{t('term.recipe')}</Th>
                <Th>{t('rolls.madeFor')}</Th>
                <Th>{t('term.colour')}</Th>
                <Th>{t('field.status')}</Th>
                <Th end>{t('thermoRpt.weight')}</Th>
                <Th end>{t('thermoRpt.length')}</Th>
                <Th end>{t('thermoRpt.plateWeight')}</Th>
                <Th end>RS</Th>
                <Th end>RM</Th>
                <Th end>LM</Th>
                <Th end>LS</Th>
                <Th end>{t('rolls.avgThickness')}</Th>
                <Th end>{t('extruderRpt.range')}</Th>
                <Th end>{t('extruderRpt.dry')}</Th>
                <Th end>{t('extruderRpt.wet')}</Th>
                <Th end>{t('rolls.absorbencyShort')}</Th>
                <Th>{t('rolls.madeBy')}</Th>
                <Th>{t('extruderRpt.measuredBy')}</Th>
                <Th>{t('extruderRpt.testedBy')}</Th>
                <Th>{t('extruderRpt.formedOn')}</Th>
                <Th>{t('field.notes')}</Th>
              </tr>
            </thead>
            <tbody>
              {report.rolls.map((roll) => (
                <tr key={roll.rollCode} className="border-b border-line last:border-0">
                  <td className="px-3 py-2 font-mono text-xs font-semibold whitespace-nowrap text-ink">
                    {roll.rollCode}
                  </td>
                  <td className="px-3 py-2 whitespace-nowrap text-ink-muted">
                    {/* The time only; the date is the shift's (section 19.12). */}
                    {new Date(roll.producedAt).toLocaleTimeString('en-GB', {
                      hour: '2-digit',
                      minute: '2-digit',
                    })}
                  </td>
                  <td className="px-3 py-2 whitespace-nowrap text-ink-soft">
                    {roll.recipeNumber}
                    <span className="ms-1 text-xs text-ink-muted">
                      {roll.recipeFamilyName}
                    </span>
                  </td>
                  <td className="px-3 py-2 text-ink-soft">{roll.productName ?? '—'}</td>
                  <td className="px-3 py-2 text-ink-soft">{roll.colorName}</td>
                  <td className="px-3 py-2">
                    <RollStatusBadge
                      status={roll.status}
                      needsTest={!roll.testComplete}
                      needsMeasure={roll.weight === null}
                    />
                  </td>
                  <Num value={roll.weight} />
                  <Num value={roll.length} />
                  <Num value={roll.plateWeight} />
                  <Num value={roll.thicknessRs} />
                  <Num value={roll.thicknessRm} />
                  <Num value={roll.thicknessLm} />
                  <Num value={roll.thicknessLs} />
                  <td className="px-3 py-2 text-end whitespace-nowrap text-ink tabular-nums">
                    {num(roll.averageThickness)}
                    <ThicknessVerdict inSpec={roll.thicknessInSpec} />
                  </td>
                  <td className="px-3 py-2 text-end whitespace-nowrap text-ink-muted tabular-nums">
                    {range(roll.minThickness, roll.maxThickness)}
                  </td>
                  {roll.isAbsorbent ? (
                    <>
                      <Num value={roll.absorbencyDryWeight} />
                      <Num value={roll.absorbencyWetWeight} />
                      <td className="px-3 py-2 text-end whitespace-nowrap font-semibold text-ink tabular-nums">
                        {roll.absorbencyPercentage === null
                          ? '—'
                          : `${String(roll.absorbencyPercentage)}%`}
                      </td>
                    </>
                  ) : (
                    <td
                      colSpan={3}
                      className="px-3 py-2 text-center text-xs text-ink-muted"
                    >
                      —
                    </td>
                  )}
                  <td className="px-3 py-2 whitespace-nowrap text-ink-soft">
                    {roll.producedByName}
                  </td>
                  <td className="px-3 py-2 whitespace-nowrap text-ink-soft">
                    {roll.measuredByName ?? '—'}
                  </td>
                  <td className="px-3 py-2 whitespace-nowrap text-ink-soft">
                    {roll.testedByName ?? '—'}
                    {roll.testedByName !== null && !roll.testComplete && (
                      <span className="ms-1 text-xs text-warn">
                        ({t('extruderRpt.testOpen')})
                      </span>
                    )}
                  </td>
                  <td className="px-3 py-2 whitespace-nowrap text-ink-soft">
                    {roll.formedOnDate === null
                      ? '—'
                      : `${formatDate(roll.formedOnDate)} · ${roll.formedOnShiftName ?? ''}`}
                  </td>
                  <td className="min-w-48 px-3 py-2 text-xs text-ink-soft">
                    {[roll.notes, roll.testNotes].filter((n) => n !== null).join(' · ') ||
                      '—'}
                  </td>
                </tr>
              ))}
              <tr className="bg-canvas font-semibold text-ink">
                <td className="px-3 py-2">{t('thermoRpt.total')}</td>
                <td colSpan={5} className="px-3 py-2 text-ink-soft">
                  {report.rollCount} {t('extruderRpt.rollsColumn').toLowerCase()}
                </td>
                <Num value={report.totalWeight} />
                <Num value={report.totalLength} />
                <td colSpan={15} />
              </tr>
            </tbody>
          </table>
        )}
      </Block>

      {report.products.length > 0 && (
        <Block title={t('extruderRpt.byProduct')}>
          <table className="w-full text-start text-sm">
            <thead>
              <tr className="border-b border-line text-xs tracking-wider text-ink-muted uppercase">
                <Th>{t('term.product')}</Th>
                <Th end>{t('extruderRpt.rollsColumn')}</Th>
                <Th end>{t('thermoRpt.weight')}</Th>
                <Th end>{t('thermoRpt.length')}</Th>
                <Th end>{t('rolls.avgThickness')}</Th>
                <Th end>{t('rolls.outOfSpec')}</Th>
              </tr>
            </thead>
            <tbody>
              {report.products.map((product) => (
                <tr
                  key={product.productName}
                  className="border-b border-line last:border-0"
                >
                  <td className="px-3 py-2 font-medium text-ink">
                    {product.productName}
                  </td>
                  <Num value={product.rollCount} />
                  <Num value={product.totalWeight} />
                  <Num value={product.totalLength} />
                  <Num value={product.averageThickness} />
                  <Num value={product.outOfSpecCount} />
                </tr>
              ))}
            </tbody>
          </table>
        </Block>
      )}

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

/** "2.8–3.0", "≥ 3.0", or a dash when no range is set. */
function range(min: number | null, max: number | null): string {
  if (min !== null && max !== null) {
    return `${String(min)}–${String(max)}`;
  }
  if (min !== null) {
    return `≥ ${String(min)}`;
  }
  if (max !== null) {
    return `≤ ${String(max)}`;
  }
  return '—';
}

function Th({
  children,
  end = false,
}: {
  children: ReactNode;
  end?: boolean;
}): ReactElement {
  return (
    <th
      className={[
        'px-3 py-2.5 font-semibold whitespace-nowrap',
        end ? 'text-end' : 'text-start',
      ].join(' ')}
    >
      {children}
    </th>
  );
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

function Num({ value }: { value: number | null }): ReactElement {
  return (
    <td className="px-3 py-2 text-end whitespace-nowrap text-ink-soft tabular-nums">
      {num(value)}
    </td>
  );
}

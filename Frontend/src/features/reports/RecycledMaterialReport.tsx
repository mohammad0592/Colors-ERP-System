import { useQuery } from '@tanstack/react-query';
import { useTranslation } from '../../hooks/useTranslation';
import type { ReactElement } from 'react';
import { formatDate } from '../shifts/shiftFormat';
import { reportsApi } from './api';
import type { DateRange } from './dateRange';

/**
 * Recycled material produced, against what the mixer took back out
 * (specification section 13).
 *
 * Both halves matter. The black recipes replace 35% of their resin with recycled
 * material, so they are the only thing that consumes it — and a pile that shrinks faster
 * than it grows is a shift that will run out of the cheap half.
 */
export function RecycledMaterialReport({ range }: { range: DateRange }): ReactElement {
  const { t } = useTranslation();
  const report = useQuery({
    queryKey: ['report-recycled', range.from, range.to],
    queryFn: () => reportsApi.recycledMaterial(range.from, range.to),
  });

  if (report.isPending) {
    return <p className="p-6 text-ink-muted">Loading…</p>;
  }

  if (report.isError) {
    return <p className="p-6 text-bad">{t('msg.reportFailed')}</p>;
  }

  const data = report.data;

  if (data.materialName === null) {
    return (
      <p className="card p-8 text-center text-ink-muted">
        No material is marked as what the recycler makes, so there is nothing to report.
        An administrator sets that once in Master Data.
      </p>
    );
  }

  return (
    <>
      <section className="mb-6 grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
        <Figure label={t('field.made')} value={`${String(data.totalProduced)} kg`}>
          by the recycler in these days
        </Figure>
        <Figure label="Taken back out" value={`${String(data.totalConsumed)} kg`}>
          into the black recipes
        </Figure>
        <Figure
          label={data.difference < 0 ? 'Pile shrank by' : 'Pile grew by'}
          value={`${String(Math.abs(data.difference))} kg`}
        >
          {data.difference < 0 ? 'more was used than made' : 'more was made than used'}
        </Figure>
        <Figure label="In the store now" value={`${String(data.inStock)} kg`}>
          {data.materialName}
        </Figure>
      </section>

      {data.shifts.length === 0 ? (
        <p className="card p-8 text-center text-ink-muted">
          The recycler did not run in these days.
        </p>
      ) : (
        <div className="card overflow-x-auto">
          <table className="w-full text-start text-sm">
            <thead>
              <tr className="border-b border-line text-xs tracking-wider text-ink-muted uppercase">
                <th className="px-4 py-3 font-semibold">{t('term.shift')}</th>
                <th className="px-4 py-3 font-semibold">{t('term.line')}</th>
                <th className="px-4 py-3 text-end font-semibold">{t('field.made')}</th>
                <th className="px-4 py-3 font-semibold">{t('field.recordedBy')}</th>
              </tr>
            </thead>
            <tbody>
              {data.shifts.map((shift) => (
                <tr
                  key={`${String(shift.shiftReportId)}-${shift.productionDate}`}
                  className="border-b border-line last:border-0"
                >
                  <td className="px-4 py-3">
                    {shift.shiftName} · {formatDate(shift.productionDate)}
                  </td>
                  <td className="px-4 py-3 text-ink-soft">{shift.productionLineName}</td>
                  <td className="px-4 py-3 text-end font-semibold tabular-nums text-ink">
                    {shift.produced} kg
                  </td>
                  <td className="px-4 py-3 text-ink-soft">
                    {shift.recordedByName}
                    {shift.notes !== null && (
                      <span className="block text-xs text-ink-muted">{shift.notes}</span>
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

function Figure({
  label,
  value,
  children,
}: {
  label: string;
  value: string;
  children: React.ReactNode;
}): ReactElement {
  return (
    <div className="card p-4">
      <p className="text-xs tracking-wider text-ink-muted uppercase">{label}</p>
      <p className="mt-1 text-2xl font-bold text-ink tabular-nums">{value}</p>
      <p className="mt-1 text-xs text-ink-muted">{children}</p>
    </div>
  );
}

import { useQuery } from '@tanstack/react-query';
import { useTranslation } from '../../hooks/useTranslation';
import { useState, type ReactElement } from 'react';
import { reportsApi } from './api';
import type { DateRange } from './dateRange';

/**
 * Consumption by shift and by recipe (specification section 13).
 *
 * The same records read two ways. A shift is the only thing material is ever issued to,
 * so a recipe's usage is its shifts added together — which is why a shift that switched
 * recipe cannot be attributed to either, and is counted separately instead of guessed at.
 */
export function ConsumptionReport({ range }: { range: DateRange }): ReactElement {
  const { t } = useTranslation();
  const { from, to } = range;
  const [groupBy, setGroupBy] = useState<'Shift' | 'Recipe'>('Shift');
  const [open, setOpen] = useState<string | null>(null);

  const report = useQuery({
    queryKey: ['report-consumption', from, to, groupBy],
    queryFn: () => reportsApi.consumption(from, to, groupBy),
  });

  return (
    <>
      <section className="card mb-4 p-4">
        <label className="field-label" htmlFor="consumption-group">
          Grouped by
        </label>
        <select
          id="consumption-group"
          className="field-input max-w-xs"
          value={groupBy}
          onChange={(event) => {
            setGroupBy(event.target.value === 'Recipe' ? 'Recipe' : 'Shift');
          }}
        >
          <option value="Shift">{t('term.shift')}</option>
          <option value="Recipe">{t('term.recipe')}</option>
        </select>
      </section>

      {report.isPending && <p className="p-6 text-ink-muted">Loading…</p>}
      {report.isError && <p className="p-6 text-bad">{t('msg.reportFailed')}</p>}

      {report.data !== undefined && (
        <>
          {/* Said, never dropped in silence: a short total that hides a switched
              recipe would be read as the whole truth. */}
          {report.data.mixedRecipeShifts > 0 && (
            <p className="mb-4 rounded-control border border-line bg-canvas px-4 py-3 text-sm text-ink-soft">
              {report.data.mixedRecipeShifts} shift
              {report.data.mixedRecipeShifts === 1 ? '' : 's'} ran more than one recipe
              and {report.data.mixedRecipeShifts === 1 ? 'is' : 'are'} not counted here —
              its material cannot be said to belong to either recipe. Group by shift to
              see what it used.
            </p>
          )}

          {report.data.groups.length === 0 ? (
            <p className="card p-8 text-center text-ink-muted">
              Nothing was consumed in these days.
            </p>
          ) : (
            <div className="space-y-3">
              {report.data.groups.map((group) => (
                <section key={group.label} className="card overflow-hidden">
                  <button
                    type="button"
                    className="flex w-full flex-wrap items-center justify-between gap-3 px-5 py-4 text-start transition-colors hover:bg-canvas"
                    onClick={() => {
                      setOpen((current) =>
                        current === group.label ? null : group.label,
                      );
                    }}
                  >
                    <div>
                      <p className="font-bold text-ink">{group.label}</p>
                      <p className="text-sm text-ink-muted">
                        {group.shifts > 1 && `${String(group.shifts)} shifts · `}
                        {group.rollsProduced} roll
                        {group.rollsProduced === 1 ? '' : 's'} ·{' '}
                        {group.rollWeightProduced} kg made
                        {group.recipeNumber !== null && group.shiftReportId !== null && (
                          <> · recipe {group.recipeNumber}</>
                        )}
                      </p>
                    </div>
                    <p className="text-end">
                      <span className="text-lg font-bold text-ink tabular-nums">
                        {group.totalUsed} kg
                      </span>
                      <span className="block text-xs text-ink-muted">
                        {group.materials.length === 0
                          ? 'nothing was issued'
                          : `${open === group.label ? 'hide' : 'show'} the ${String(
                              group.materials.length,
                            )} material${group.materials.length === 1 ? '' : 's'}`}
                      </span>
                    </p>
                  </button>

                  {open === group.label && group.materials.length > 0 && (
                    <div className="overflow-x-auto border-t border-line">
                      <table className="w-full text-start text-sm">
                        <thead>
                          <tr className="border-b border-line text-xs tracking-wider text-ink-muted uppercase">
                            <th className="px-5 py-2 font-semibold">{t('term.material')}</th>
                            <th className="px-5 py-2 text-end font-semibold">{t('field.issued')}</th>
                            <th className="px-5 py-2 text-end font-semibold">
                              {t('field.returned')}
                            </th>
                            <th className="px-5 py-2 text-end font-semibold">{t('field.used')}</th>
                            <th className="px-5 py-2 text-end font-semibold">
                              Per kg of roll
                            </th>
                          </tr>
                        </thead>
                        <tbody>
                          {group.materials.map((material) => (
                            <tr
                              key={material.materialId}
                              className="border-b border-line last:border-0"
                            >
                              <td className="px-5 py-2 text-ink">
                                {material.materialName}
                              </td>
                              <td className="px-5 py-2 text-end tabular-nums text-ink-soft">
                                {material.issued}
                              </td>
                              <td className="px-5 py-2 text-end tabular-nums text-ink-soft">
                                {material.returned === 0 ? '—' : material.returned}
                              </td>
                              <td className="px-5 py-2 text-end font-semibold tabular-nums text-ink">
                                {material.netUsed}{' '}
                                <span className="font-normal text-ink-muted">
                                  {material.unitSymbol}
                                </span>
                              </td>
                              <td className="px-5 py-2 text-end tabular-nums text-ink-muted">
                                {material.perKilogramOfRoll ?? '—'}
                              </td>
                            </tr>
                          ))}
                        </tbody>
                      </table>
                    </div>
                  )}
                </section>
              ))}
            </div>
          )}
        </>
      )}
    </>
  );
}

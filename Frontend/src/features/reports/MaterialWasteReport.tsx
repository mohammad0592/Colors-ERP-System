import type { ReactElement } from 'react';
import { formatDate } from '../shifts/shiftFormat';
import type { MaterialWasteReportDto } from './api';

/**
 * Material waste control (specification section 13).
 *
 * What left the store for this shift, less what came back, against what the recipe says
 * should have been used. The percentages are parts per hundred resin, so the requirement
 * is worked out from the base resin actually consumed — the recipe's own 100%.
 */
export function MaterialWasteReport({
  report,
}: {
  report: MaterialWasteReportDto;
}): ReactElement {
  return (
    <>
      <section className="card mb-4 p-5">
        <h2 className="mb-1 text-lg font-bold text-ink">
          Shift {report.shiftName} · {formatDate(report.productionDate)}
        </h2>

        {report.recipeCount === 1 ? (
          <p className="text-sm text-ink-muted">
            Recipe {report.recipeNumber} — {report.recipeFamilyName}, version{' '}
            {report.recipeVersionNumber}. {report.resinUsed} kg of base resin used, and{' '}
            {report.rollsProduced} roll{report.rollsProduced === 1 ? '' : 's'} came off
            weighing {report.rollWeightProduced} kg.
          </p>
        ) : report.recipeCount === 0 ? (
          <p className="text-sm text-ink-muted">
            No roll was made on this shift, so there is no recipe to hold the material
            against.
          </p>
        ) : (
          <p className="text-sm text-ink-muted">
            This shift ran {report.recipeCount} different recipes, so there is no single
            requirement to compare against. What was used is still shown.
          </p>
        )}

        {/* The honest limit, printed rather than hidden (specification section 13). */}
        <p className="mt-3 rounded-control border border-line bg-canvas px-3 py-2 text-xs text-ink-soft">
          Material is issued to a <strong>shift</strong>, not to a mix. The true sentence
          is “this is what was issued to the shift that made these rolls”, which is the
          same set of materials while the mixer is filled once a shift.
        </p>
      </section>

      {report.lines.length === 0 ? (
        <p className="card p-8 text-center text-ink-muted">
          Nothing was issued to this shift.
        </p>
      ) : (
        <div className="card overflow-x-auto">
          <table className="w-full text-left text-sm">
            <thead>
              <tr className="border-b border-line text-xs tracking-wider text-ink-muted uppercase">
                <th className="px-4 py-3 font-semibold">Material</th>
                <th className="px-4 py-3 text-right font-semibold">Issued</th>
                <th className="px-4 py-3 text-right font-semibold">Returned</th>
                <th className="px-4 py-3 text-right font-semibold">Used</th>
                <th className="px-4 py-3 text-right font-semibold">Recipe</th>
                <th className="px-4 py-3 text-right font-semibold">Should be</th>
                <th className="px-4 py-3 text-right font-semibold">Difference</th>
              </tr>
            </thead>
            <tbody>
              {report.lines.map((line) => (
                <tr key={line.materialId} className="border-b border-line last:border-0">
                  <td className="px-4 py-3">
                    <span className="font-medium text-ink">{line.materialName}</span>
                    {line.isBaseResin && (
                      <span className="ml-2 rounded-full bg-canvas px-2 py-0.5 text-xs font-semibold text-ink-soft">
                        base
                      </span>
                    )}
                    {line.outsideRange && (
                      <span className="ml-2 rounded-full bg-warn-soft px-2 py-0.5 text-xs font-semibold text-warn">
                        outside the range
                      </span>
                    )}
                  </td>
                  <td className="px-4 py-3 text-right tabular-nums text-ink-soft">
                    {line.issued}
                  </td>
                  <td className="px-4 py-3 text-right tabular-nums text-ink-soft">
                    {line.returned === 0 ? '—' : line.returned}
                  </td>
                  <td className="px-4 py-3 text-right font-semibold tabular-nums text-ink">
                    {line.netUsed} <span className="text-ink-muted">{line.unitSymbol}</span>
                  </td>
                  <td className="px-4 py-3 text-right tabular-nums text-ink-muted">
                    {line.targetPercentage === null ? '—' : `${String(line.targetPercentage)}%`}
                  </td>
                  <td className="px-4 py-3 text-right tabular-nums text-ink-soft">
                    {line.required ?? '—'}
                  </td>
                  <td className="px-4 py-3 text-right">
                    <Difference
                      kg={line.difference}
                      percentage={line.differencePercentage}
                    />
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
 * Used less required. Never red on its own: more material than the recipe asks for is
 * news for the supervisor to read, not a fault the system is asserting.
 */
function Difference({
  kg,
  percentage,
}: {
  kg: number | null;
  percentage: number | null;
}): ReactElement {
  if (kg === null) {
    return <span className="text-ink-muted">—</span>;
  }

  const agreed = Math.abs(kg) < 0.0005;

  return (
    <span
      className={[
        'rounded-full px-2 py-0.5 text-xs font-semibold tabular-nums',
        agreed ? 'bg-ok-soft text-ok' : 'bg-canvas text-ink-soft',
      ].join(' ')}
    >
      {agreed ? 'agrees' : `${kg > 0 ? '+' : ''}${String(kg)}`}
      {!agreed && percentage !== null && (
        <span className="ml-1 font-normal text-ink-muted">
          {percentage > 0 ? '+' : ''}
          {percentage}%
        </span>
      )}
    </span>
  );
}

import type { ReactElement } from 'react';
import { formatDate } from '../shifts/shiftFormat';
import type { ShiftSummaryReportDto } from './api';

/**
 * The shift production summary (specification section 13).
 *
 * The paper form's summary block, worked out rather than typed — loss %, loss weight,
 * roll weight used and plate count, per product.
 *
 * The number to watch is the loss: it uses <b>each roll's own measured plate weight</b>,
 * never one shared figure. On the July form every roll was 9 g so it made no difference;
 * the Roll Log shows 9.1, 8.7 and 9.2, so it will.
 */
export function ShiftSummaryReport({
  report,
}: {
  report: ShiftSummaryReportDto;
}): ReactElement {
  return (
    <>
      <section className="card mb-4 p-5">
        <h2 className="mb-1 text-lg font-bold text-ink">
          Shift {report.shiftName} · {formatDate(report.productionDate)}
        </h2>
        <p className="text-sm text-ink-muted">
          {report.status}
          {report.supervisorName !== null && ` · ${report.supervisorName}`}
          {report.electricityUsed !== null && ` · ${String(report.electricityUsed)} kWh`}
        </p>
      </section>

      <section className="mb-6 grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
        <Figure label="Rolls made" value={String(report.rollsProduced)}>
          {report.rollWeightProduced} kg off the extruder
        </Figure>
        <Figure label="Rolls formed" value={String(report.rollsFormed)}>
          {report.rollWeightUsed} kg into the thermo
        </Figure>
        <Figure label="Bags" value={String(report.bagCount)}>
          {report.pieceCount.toLocaleString('en-GB')} pieces
        </Figure>
        <Figure
          label="Lost in forming"
          value={
            report.lossPercentage === null ? '—' : `${String(report.lossPercentage)}%`
          }
        >
          {report.lossWeight} kg of {report.rollWeightUsed} kg
        </Figure>
      </section>

      <section className="mb-6 grid gap-4 sm:grid-cols-3">
        <Figure label="Product made" value={`${String(report.productWeight)} kg`}>
          pieces × each roll’s own plate weight
        </Figure>
        <Figure label="Pallets" value={String(report.palletsBuilt)}>
          {report.palletsCompleted} finished
        </Figure>
        <Figure
          label="Recycled material"
          value={`${String(report.recycledMaterialProduced)} kg`}
        >
          produced and put back in the store
        </Figure>
      </section>

      <h3 className="mb-3 text-lg font-bold text-ink">By product</h3>

      {report.products.length === 0 ? (
        <p className="card p-8 text-center text-ink-muted">
          Nothing was formed on this shift.
        </p>
      ) : (
        <div className="card overflow-x-auto">
          <table className="w-full text-start text-sm">
            <thead>
              <tr className="border-b border-line text-xs tracking-wider text-ink-muted uppercase">
                <th className="px-4 py-3 font-semibold">Product</th>
                <th className="px-4 py-3 text-end font-semibold">Rolls</th>
                <th className="px-4 py-3 text-end font-semibold">Roll weight</th>
                <th className="px-4 py-3 text-end font-semibold">Bags</th>
                <th className="px-4 py-3 text-end font-semibold">Pieces</th>
                <th className="px-4 py-3 text-end font-semibold">Product</th>
                <th className="px-4 py-3 text-end font-semibold">Loss</th>
              </tr>
            </thead>
            <tbody>
              {report.products.map((product) => (
                <tr
                  key={product.productId}
                  className="border-b border-line last:border-0"
                >
                  <td className="px-4 py-3 font-medium text-ink">
                    {product.productName}
                  </td>
                  <td className="px-4 py-3 text-end tabular-nums text-ink-soft">
                    {product.rollsUsed}
                  </td>
                  <td className="px-4 py-3 text-end tabular-nums text-ink-soft">
                    {product.rollWeightUsed} kg
                  </td>
                  <td className="px-4 py-3 text-end tabular-nums text-ink-soft">
                    {product.bagCount}
                  </td>
                  <td className="px-4 py-3 text-end tabular-nums text-ink-soft">
                    {product.pieceCount.toLocaleString('en-GB')}
                  </td>
                  <td className="px-4 py-3 text-end font-semibold tabular-nums text-ink">
                    {product.productWeight} kg
                  </td>
                  <td className="px-4 py-3 text-end tabular-nums text-ink-soft">
                    {product.lossWeight} kg
                    {product.lossPercentage !== null && (
                      <span className="ms-1 text-xs text-ink-muted">
                        {product.lossPercentage}%
                      </span>
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

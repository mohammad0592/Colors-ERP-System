import { useQuery } from '@tanstack/react-query';
import type { ReactElement } from 'react';
import { reportsApi } from './api';
import type { DateRange } from './dateRange';

/**
 * Pallet production (specification section 13).
 *
 * Finished pallets, by the product on them. A pallet takes its product from its first
 * bag, so one still being filled belongs to no product yet — it is counted on its own
 * rather than guessed into a row it might not end up in.
 */
export function PalletProductionReport({ range }: { range: DateRange }): ReactElement {
  const report = useQuery({
    queryKey: ['report-pallets', range.from, range.to],
    queryFn: () => reportsApi.palletProduction(range.from, range.to),
  });

  if (report.isPending) {
    return <p className="p-6 text-ink-muted">Loading…</p>;
  }

  if (report.isError) {
    return <p className="p-6 text-bad">Could not load the report.</p>;
  }

  const data = report.data;

  return (
    <>
      <section className="mb-6 grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
        <Figure label="Finished" value={String(data.palletsCompleted)}>
          full and ready to ship
        </Figure>
        <Figure label="Still being filled" value={String(data.palletsStillOpen)}>
          no product until the first bag
        </Figure>
        <Figure label="Cancelled" value={String(data.palletsCancelled)}>
          their wood went back to the store
        </Figure>
        <Figure label="Started altogether" value={String(data.palletsStarted)}>
          one wooden pallet each
        </Figure>
      </section>

      {data.products.length === 0 ? (
        <p className="card p-8 text-center text-ink-muted">
          No pallet was finished in these days.
        </p>
      ) : (
        <div className="card overflow-x-auto">
          <table className="w-full text-start text-sm">
            <thead>
              <tr className="border-b border-line text-xs tracking-wider text-ink-muted uppercase">
                <th className="px-4 py-3 font-semibold">Product</th>
                <th className="px-4 py-3 text-end font-semibold">Pallets</th>
                <th className="px-4 py-3 text-end font-semibold">Bags</th>
                <th className="px-4 py-3 text-end font-semibold">Pieces</th>
                <th className="px-4 py-3 text-end font-semibold">Weight</th>
              </tr>
            </thead>
            <tbody>
              {data.products.map((product) => (
                <tr
                  key={product.productId}
                  className="border-b border-line last:border-0"
                >
                  <td className="px-4 py-3">
                    <span className="font-medium text-ink">{product.productName}</span>
                    <span className="ms-2 text-xs text-ink-muted">
                      {product.bagsPerPallet} bags to a pallet
                    </span>
                  </td>
                  <td className="px-4 py-3 text-end font-semibold tabular-nums text-ink">
                    {product.palletsCompleted}
                  </td>
                  <td className="px-4 py-3 text-end tabular-nums text-ink-soft">
                    {product.bags}
                  </td>
                  <td className="px-4 py-3 text-end tabular-nums text-ink-soft">
                    {product.pieces.toLocaleString('en-GB')}
                  </td>
                  <td className="px-4 py-3 text-end tabular-nums text-ink-soft">
                    {product.weight} kg
                  </td>
                </tr>
              ))}
            </tbody>
            <tfoot>
              <tr className="border-t-2 border-line font-semibold">
                <td className="px-4 py-3 text-ink">Altogether</td>
                <td className="px-4 py-3 text-end tabular-nums text-ink">
                  {data.products.reduce((sum, p) => sum + p.palletsCompleted, 0)}
                </td>
                <td className="px-4 py-3 text-end tabular-nums text-ink">
                  {data.products.reduce((sum, p) => sum + p.bags, 0)}
                </td>
                <td className="px-4 py-3 text-end tabular-nums text-ink">
                  {data.products
                    .reduce((sum, p) => sum + p.pieces, 0)
                    .toLocaleString('en-GB')}
                </td>
                <td className="px-4 py-3 text-end tabular-nums text-ink">
                  {data.products.reduce((sum, p) => sum + p.weight, 0).toFixed(1)} kg
                </td>
              </tr>
            </tfoot>
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

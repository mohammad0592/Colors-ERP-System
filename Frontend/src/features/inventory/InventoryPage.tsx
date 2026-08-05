import { useQuery, useQueryClient } from '@tanstack/react-query';
import { useState, type ReactElement } from 'react';
import { Link } from 'react-router-dom';
import { PageHeader } from '../../components/ui/PageHeader';
import { useAuth } from '../../hooks/useAuth';
import { RoleNames } from '../../lib/roles';
import { AdjustStockDialog } from './AdjustStockDialog';
import { inventoryApi, type MaterialStockDto } from './api';

/**
 * What is in the store (specification section 6).
 *
 * Every active material is listed, including those never received — a material at
 * zero is exactly what the storekeeper needs to see, and a row missing from the list
 * says nothing at all.
 */
export function InventoryPage(): ReactElement {
  const queryClient = useQueryClient();
  const { hasRole } = useAuth();
  const canAdjust = hasRole(RoleNames.Administrator, RoleNames.Supervisor);
  const canReceive = hasRole(RoleNames.Administrator, RoleNames.InventoryManager);

  const [lowOnly, setLowOnly] = useState(false);
  const [adjusting, setAdjusting] = useState<MaterialStockDto | null>(null);
  const [historyFor, setHistoryFor] = useState<MaterialStockDto | null>(null);

  const stock = useQuery({
    queryKey: ['inventory', lowOnly],
    queryFn: () => inventoryApi.stock(lowOnly),
  });

  const movements = useQuery({
    queryKey: ['inventory-movements', historyFor?.materialId ?? null],
    queryFn: () => inventoryApi.movements(historyFor?.materialId, 50),
  });

  function invalidate(): void {
    void queryClient.invalidateQueries({ queryKey: ['inventory'] });
    void queryClient.invalidateQueries({ queryKey: ['inventory-movements'] });
  }

  if (stock.isPending) {
    return <p className="p-6 text-ink-muted">Loading…</p>;
  }

  if (stock.isError) {
    return <p className="p-6 text-bad">Could not load the store.</p>;
  }

  const lowCount = stock.data.filter((row) => row.isBelowMinimum).length;

  return (
    <>
      <PageHeader
        title="Inventory"
        subtitle="What the store holds, in each material's own unit. Nobody edits a number here — every change is a movement with a reason."
        actions={
          canReceive ? (
            <Link to="/inventory/receive" className="btn-primary h-touch w-auto px-5 text-base">
              Receive materials
            </Link>
          ) : undefined
        }
      />

      <section className="mb-6 flex flex-wrap gap-2">
        <Chip
          label="All materials"
          active={!lowOnly}
          onClick={() => {
            setLowOnly(false);
          }}
        />
        <Chip
          label={`Below minimum (${String(lowCount)})`}
          active={lowOnly}
          tone={lowCount > 0 ? 'warn' : 'normal'}
          onClick={() => {
            setLowOnly(true);
          }}
        />
      </section>

      <div className="card overflow-x-auto">
        <table className="w-full text-left text-sm">
          <thead>
            <tr className="border-b border-line text-xs tracking-wider text-ink-muted uppercase">
              <th className="px-4 py-3 font-semibold">Code</th>
              <th className="px-4 py-3 font-semibold">Material</th>
              <th className="px-4 py-3 font-semibold">Category</th>
              <th className="px-4 py-3 text-right font-semibold">In stock</th>
              <th className="px-4 py-3 text-right font-semibold">Minimum</th>
              <th className="px-4 py-3" />
            </tr>
          </thead>
          <tbody>
            {stock.data.length === 0 && (
              <tr>
                <td colSpan={6} className="px-4 py-8 text-center text-ink-muted">
                  {lowOnly ? 'Nothing is below its minimum.' : 'No materials yet.'}
                </td>
              </tr>
            )}
            {stock.data.map((row) => (
              <tr key={row.materialId} className="border-b border-line last:border-0">
                <td className="px-4 py-3 font-mono text-xs text-ink-muted">{row.code}</td>
                <td className="px-4 py-3 font-medium text-ink">
                  {row.name}
                  {row.isBelowMinimum && (
                    <span className="ml-2 rounded-full bg-warn-soft px-2 py-0.5 text-xs font-semibold text-warn">
                      Low
                    </span>
                  )}
                </td>
                <td className="px-4 py-3 text-ink-soft">{row.categoryName}</td>
                <td className="px-4 py-3 text-right font-semibold text-ink tabular-nums">
                  {row.currentQuantity} <span className="text-ink-muted">{row.baseUnitSymbol}</span>
                </td>
                <td className="px-4 py-3 text-right text-ink-muted tabular-nums">
                  {row.minQuantity}
                </td>
                <td className="px-4 py-3">
                  <div className="flex justify-end gap-2">
                    <Action
                      label="History"
                      onClick={() => {
                        setHistoryFor((current) =>
                          current?.materialId === row.materialId ? null : row,
                        );
                      }}
                    />
                    {canAdjust && (
                      <Action
                        label="Stock count"
                        onClick={() => {
                          setAdjusting(row);
                        }}
                      />
                    )}
                  </div>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      <section className="mt-8">
        <div className="mb-3 flex flex-wrap items-center justify-between gap-3">
          <h2 className="text-lg font-bold text-ink">
            {historyFor === null ? 'Recent movements' : `Movements — ${historyFor.name}`}
          </h2>
          {historyFor !== null && (
            <button
              type="button"
              className="text-sm font-medium text-brand-700 hover:underline"
              onClick={() => {
                setHistoryFor(null);
              }}
            >
              Show every material
            </button>
          )}
        </div>

        <div className="card overflow-x-auto">
          <table className="w-full text-left text-sm">
            <thead>
              <tr className="border-b border-line text-xs tracking-wider text-ink-muted uppercase">
                <th className="px-4 py-3 font-semibold">When</th>
                <th className="px-4 py-3 font-semibold">Material</th>
                <th className="px-4 py-3 font-semibold">Movement</th>
                <th className="px-4 py-3 text-right font-semibold">Quantity</th>
                <th className="px-4 py-3 font-semibold">By</th>
                <th className="px-4 py-3 font-semibold">Note</th>
              </tr>
            </thead>
            <tbody>
              {movements.isPending && (
                <tr>
                  <td colSpan={6} className="px-4 py-6 text-center text-ink-muted">
                    Loading…
                  </td>
                </tr>
              )}
              {movements.data?.length === 0 && (
                <tr>
                  <td colSpan={6} className="px-4 py-8 text-center text-ink-muted">
                    Nothing has moved yet.
                  </td>
                </tr>
              )}
              {movements.data?.map((move) => (
                <tr key={move.id} className="border-b border-line last:border-0">
                  <td className="px-4 py-3 whitespace-nowrap text-ink-muted">
                    {new Date(move.movementDate).toLocaleString('en-GB', {
                      day: '2-digit',
                      month: '2-digit',
                      year: 'numeric',
                      hour: '2-digit',
                      minute: '2-digit',
                    })}
                  </td>
                  <td className="px-4 py-3 text-ink-soft">{move.materialName}</td>
                  <td className="px-4 py-3 text-ink-soft">{move.movementTypeName}</td>
                  {/* The sign is shown, never stored — it comes from the movement type. */}
                  <td
                    className={[
                      'px-4 py-3 text-right font-semibold tabular-nums',
                      move.direction > 0 ? 'text-ok' : 'text-bad',
                    ].join(' ')}
                  >
                    {move.direction > 0 ? '+' : '−'}
                    {move.quantity} <span className="text-ink-muted">{move.baseUnitSymbol}</span>
                  </td>
                  <td className="px-4 py-3 text-ink-soft">{move.userName}</td>
                  <td className="max-w-md px-4 py-3 text-ink-muted">{move.notes ?? '—'}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </section>

      {adjusting !== null && (
        <AdjustStockDialog
          material={adjusting}
          onClose={() => {
            setAdjusting(null);
          }}
          onAdjusted={invalidate}
        />
      )}
    </>
  );
}

function Chip({
  label,
  active,
  onClick,
  tone = 'normal',
}: {
  label: string;
  active: boolean;
  onClick: () => void;
  tone?: 'normal' | 'warn';
}): ReactElement {
  return (
    <button
      type="button"
      onClick={onClick}
      className={[
        'min-h-9 rounded-full border px-4 text-sm font-medium transition-colors',
        active
          ? 'border-brand-600 bg-brand-50 text-brand-700'
          : tone === 'warn'
            ? 'border-warn/40 bg-warn-soft text-warn'
            : 'border-line text-ink-soft hover:border-brand-200 hover:bg-brand-50',
      ].join(' ')}
    >
      {label}
    </button>
  );
}

function Action({
  label,
  onClick,
}: {
  label: string;
  onClick: () => void;
}): ReactElement {
  return (
    <button
      type="button"
      onClick={onClick}
      className="min-h-9 rounded-control border border-line px-3 text-sm font-medium whitespace-nowrap text-ink-soft transition-colors hover:border-brand-200 hover:bg-brand-50 hover:text-brand-700"
    >
      {label}
    </button>
  );
}

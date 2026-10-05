import { useQuery, useQueryClient } from '@tanstack/react-query';
import { useState, type ReactElement } from 'react';
import { Link } from 'react-router-dom';
import { useTranslation } from '../../hooks/useTranslation';
import { PageHeader } from '../../components/ui/PageHeader';
import { useAuth } from '../../hooks/useAuth';
import { RoleNames } from '../../lib/roles';
import { ProducedStockTab } from '../labels/ProducedStockTab';
import { AdjustStockDialog } from './AdjustStockDialog';
import { inventoryApi, type MaterialStockDto } from './api';

type Section = 'raw' | 'packing' | 'Roll' | 'Bag' | 'Pallet';

/**
 * What is in the store (specification section 6).
 *
 * Every active material is listed, including those never received — a material at
 * zero is exactly what the storekeeper needs to see, and a row missing from the list
 * says nothing at all.
 *
 * Materials are split by their category's "issued on tickets" flag, not by the
 * category's name. A material added in master data lands in the right tab by itself,
 * and renaming a category cannot quietly move everything in it to the wrong one.
 */
export function InventoryPage(): ReactElement {
  const { t } = useTranslation();
  const queryClient = useQueryClient();
  const { hasRole } = useAuth();
  const canAdjust = hasRole(RoleNames.Administrator, RoleNames.Supervisor);
  const canReceive = hasRole(RoleNames.Administrator, RoleNames.InventoryManager);

  const [section, setSection] = useState<Section>('raw');
  const [lowOnly, setLowOnly] = useState(false);
  const [adjusting, setAdjusting] = useState<MaterialStockDto | null>(null);
  const [historyFor, setHistoryFor] = useState<MaterialStockDto | null>(null);

  // Everything, once. Each tab is a slice of it, so the "below minimum" count on a
  // tab is right before it is pressed, not only after.
  const stock = useQuery({
    queryKey: ['inventory'],
    queryFn: () => inventoryApi.stock(),
  });

  const movements = useQuery({
    queryKey: ['inventory-movements', historyFor?.materialId ?? null],
    queryFn: () => inventoryApi.movements(historyFor?.materialId, 100),
  });

  function invalidate(): void {
    void queryClient.invalidateQueries({ queryKey: ['inventory'] });
    void queryClient.invalidateQueries({ queryKey: ['inventory-movements'] });
  }

  if (stock.isPending) {
    return <p className="p-6 text-ink-muted">{t('common.loading')}</p>;
  }

  if (stock.isError) {
    return <p className="p-6 text-bad">{t('inventory.loadFailed')}</p>;
  }

  const isMaterials = section === 'raw' || section === 'packing';
  const inSection = stock.data.filter(
    (row) => row.issuedOnTickets === (section === 'raw'),
  );
  const lowCount = inSection.filter((row) => row.isBelowMinimum).length;
  const rows = lowOnly ? inSection.filter((row) => row.isBelowMinimum) : inSection;
  const sectionIds = new Set(inSection.map((row) => row.materialId));
  const sectionMoves = movements.data?.filter((move) => sectionIds.has(move.materialId));

  function show(next: Section): void {
    setSection(next);
    setLowOnly(false);
    setHistoryFor(null);
  }

  const tabs: { key: Section; label: string }[] = [
    { key: 'raw', label: t('inventory.rawMaterials') },
    { key: 'packing', label: t('inventory.packingMaterials') },
    { key: 'Roll', label: t('term.rolls') },
    { key: 'Bag', label: t('term.bags') },
    { key: 'Pallet', label: t('term.pallets') },
  ];

  return (
    <>
      <PageHeader
        title={t('page.inventory.title')}
        subtitle={t('page.inventory.subtitle')}
        actions={
          canReceive ? (
            <Link
              to="/inventory/receive"
              className="btn-primary h-touch w-auto px-5 text-base"
            >
              {t('inventory.receiveMaterials')}
            </Link>
          ) : undefined
        }
      />

      {/* Two different kinds of stock. Materials are weighed and counted in their own
          unit; rolls, bags and pallets are individual things, each with its own label. */}
      <nav className="mb-6 flex gap-1 overflow-x-auto border-b border-line">
        {tabs.map((tab) => (
          <Tab
            key={tab.key}
            label={tab.label}
            active={section === tab.key}
            onClick={() => {
              show(tab.key);
            }}
          />
        ))}
      </nav>

      {/* Keyed by kind so a status picked for rolls does not carry over to bags. */}
      {!isMaterials && <ProducedStockTab key={section} kind={section} />}

      {isMaterials && (
        <>
          <section className="mb-6 flex flex-wrap gap-2">
            <Chip
              label={t('inventory.allMaterials')}
              active={!lowOnly}
              onClick={() => {
                setLowOnly(false);
              }}
            />
            <Chip
              label={`${t('inventory.belowMinimum')} (${String(lowCount)})`}
              active={lowOnly}
              tone={lowCount > 0 ? 'warn' : 'normal'}
              onClick={() => {
                setLowOnly(true);
              }}
            />
          </section>

          <div className="card overflow-x-auto">
            <table className="w-full text-start text-sm">
              <thead>
                <tr className="border-b border-line text-xs tracking-wider text-ink-muted uppercase">
                  <th className="px-4 py-3 font-semibold">{t('field.code')}</th>
                  <th className="px-4 py-3 font-semibold">{t('term.material')}</th>
                  <th className="px-4 py-3 font-semibold">{t('field.category')}</th>
                  <th className="px-4 py-3 text-end font-semibold">
                    {t('field.inStock')}
                  </th>
                  <th className="px-4 py-3 text-end font-semibold">
                    {t('field.minimum')}
                  </th>
                  <th className="px-4 py-3" />
                </tr>
              </thead>
              <tbody>
                {rows.length === 0 && (
                  <tr>
                    <td colSpan={6} className="px-4 py-8 text-center text-ink-muted">
                      {lowOnly
                        ? t('inventory.noneBelowMinimum')
                        : t('inventory.noMaterials')}
                    </td>
                  </tr>
                )}
                {rows.map((row) => (
                  <tr key={row.materialId} className="border-b border-line last:border-0">
                    <td className="px-4 py-3 font-mono text-xs text-ink-muted">
                      {row.code}
                    </td>
                    <td className="px-4 py-3 font-medium text-ink">
                      {row.name}
                      {row.isBelowMinimum && (
                        <span className="ms-2 rounded-full bg-warn-soft px-2 py-0.5 text-xs font-semibold text-warn">
                          {t('inventory.low')}
                        </span>
                      )}
                    </td>
                    <td className="px-4 py-3 text-ink-soft">{row.categoryName}</td>
                    <td className="px-4 py-3 text-end font-semibold text-ink tabular-nums">
                      {row.currentQuantity}{' '}
                      <span className="text-ink-muted">{row.baseUnitSymbol}</span>
                    </td>
                    <td className="px-4 py-3 text-end text-ink-muted tabular-nums">
                      {row.minQuantity}
                    </td>
                    <td className="px-4 py-3">
                      <div className="flex justify-end gap-2">
                        <Action
                          label={t('inventory.history')}
                          onClick={() => {
                            setHistoryFor((current) =>
                              current?.materialId === row.materialId ? null : row,
                            );
                          }}
                        />
                        {canAdjust && (
                          <Action
                            label={t('inventory.stockCount')}
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
                {historyFor === null
                  ? t('inventory.recentMovements')
                  : `Movements — ${historyFor.name}`}
              </h2>
              {historyFor !== null && (
                <button
                  type="button"
                  className="text-sm font-medium text-brand-700 hover:underline"
                  onClick={() => {
                    setHistoryFor(null);
                  }}
                >
                  {t('inventory.showEvery')}
                </button>
              )}
            </div>

            <div className="card overflow-x-auto">
              <table className="w-full text-start text-sm">
                <thead>
                  <tr className="border-b border-line text-xs tracking-wider text-ink-muted uppercase">
                    <th className="px-4 py-3 font-semibold">{t('field.when')}</th>
                    <th className="px-4 py-3 font-semibold">{t('term.material')}</th>
                    <th className="px-4 py-3 font-semibold">{t('inventory.movement')}</th>
                    <th className="px-4 py-3 text-end font-semibold">
                      {t('inventory.quantity')}
                    </th>
                    <th className="px-4 py-3 font-semibold">By</th>
                    <th className="px-4 py-3 font-semibold">{t('field.note')}</th>
                  </tr>
                </thead>
                <tbody>
                  {movements.isPending && (
                    <tr>
                      <td colSpan={6} className="px-4 py-6 text-center text-ink-muted">
                        {t('common.loading')}
                      </td>
                    </tr>
                  )}
                  {sectionMoves?.length === 0 && (
                    <tr>
                      <td colSpan={6} className="px-4 py-8 text-center text-ink-muted">
                        {t('inventory.nothingMoved')}
                      </td>
                    </tr>
                  )}
                  {sectionMoves?.map((move) => (
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
                          'px-4 py-3 text-end font-semibold tabular-nums',
                          move.direction > 0 ? 'text-ok' : 'text-bad',
                        ].join(' ')}
                      >
                        {move.direction > 0 ? '+' : '−'}
                        {move.quantity}{' '}
                        <span className="text-ink-muted">{move.baseUnitSymbol}</span>
                      </td>
                      <td className="px-4 py-3 text-ink-soft">{move.userName}</td>
                      <td className="max-w-md px-4 py-3 text-ink-muted">
                        {move.notes ?? '—'}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </section>
        </>
      )}

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

function Tab({
  label,
  active,
  onClick,
}: {
  label: string;
  active: boolean;
  onClick: () => void;
}): ReactElement {
  return (
    <button
      type="button"
      onClick={onClick}
      className={[
        '-mb-px border-b-2 px-4 py-3 text-sm font-semibold whitespace-nowrap transition-colors',
        active
          ? 'border-brand-600 text-brand-700'
          : 'border-transparent text-ink-muted hover:text-ink-soft',
      ].join(' ')}
    >
      {label}
    </button>
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

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useState, type ReactElement } from 'react';
import { PageHeader } from '../../components/ui/PageHeader';
import { useAuth } from '../../hooks/useAuth';
import { ApiError } from '../../lib/apiClient';
import { RoleNames } from '../../lib/roles';
import { shiftReportsApi } from '../shifts/api';
import { formatDate } from '../shifts/shiftFormat';
import { palletsApi } from './api';
import { PalletCard } from './PalletCard';
import { PalletScanBox } from './PalletScanBox';
import { PalletStatusBadge } from './PalletStatusBadge';
import { ReverseScanDialog } from './ReverseScanDialog';

/**
 * Pallets (specification section 10).
 *
 * Cards on the left, the chosen pallet on the right. A pallet is picked once and then
 * scanned into many times, so the scan box lives in the panel rather than in a dialog
 * that would have to be opened for every one of a couple of dozen bags.
 *
 * The panel never asks for the colour or the product. The first bag scanned decides
 * both, in the factory's own words, so there is nothing here to pick wrongly.
 */
export function PalletsPage(): ReactElement {
  const queryClient = useQueryClient();
  const { hasRole } = useAuth();
  const canPack = hasRole(RoleNames.Administrator, RoleNames.PackagingOperator);
  const canReverse = hasRole(RoleNames.Administrator, RoleNames.Supervisor);

  const [openOnly, setOpenOnly] = useState(true);
  const [selectedId, setSelectedId] = useState<number | null>(null);
  const [reversing, setReversing] = useState<{ id: number; barcode: string } | null>(null);
  const [actionError, setActionError] = useState<string | null>(null);

  const pallets = useQuery({
    queryKey: ['pallets', openOnly],
    queryFn: () => palletsApi.list(openOnly),
  });

  const selected = useQuery({
    queryKey: ['pallet', selectedId],
    queryFn: () => palletsApi.get(selectedId ?? 0),
    enabled: selectedId !== null,
  });

  const availableBags = useQuery({
    queryKey: ['available-bags', selectedId],
    queryFn: () => palletsApi.availableBags(selectedId ?? undefined),
  });

  // Pallets are built where the bags come off (specification section 4).
  const packingLines = useQuery({
    queryKey: ['shift-reports', 'packing-lines'],
    queryFn: async () => {
      const open = await shiftReportsApi.list(undefined, true);
      const full = await Promise.all(open.map((s) => shiftReportsApi.get(s.id)));
      return full.flatMap((shift) =>
        shift.lines
          .filter((line) => line.formsBags)
          .map((line) => ({
            shiftLineId: line.id,
            label: `${line.productionLineName} — shift ${shift.shiftName}, ${formatDate(shift.productionDate)}`,
          })),
      );
    },
  });

  function invalidate(): void {
    void queryClient.invalidateQueries({ queryKey: ['pallets'] });
    void queryClient.invalidateQueries({ queryKey: ['pallet'] });
    void queryClient.invalidateQueries({ queryKey: ['available-bags'] });
  }

  const startPallet = useMutation({
    mutationFn: (shiftLineId: number) => palletsApi.start(shiftLineId, null),
    onSuccess: (pallet) => {
      setActionError(null);
      setSelectedId(pallet.id);
    },
    onError: (caught: unknown) => {
      setActionError(caught instanceof ApiError ? caught.message : 'Something went wrong.');
    },
    onSettled: invalidate,
  });

  if (pallets.isPending) {
    return <p className="p-6 text-ink-muted">Loading…</p>;
  }

  if (pallets.isError) {
    return <p className="p-6 text-bad">Could not load the pallets.</p>;
  }

  const lines = packingLines.data ?? [];
  const open = selected.data ?? null;

  return (
    <>
      <PageHeader
        title="Pallets"
        subtitle="The first bag scanned decides the pallet's colour and product. Every later bag must match, and the product itself says how many fill it."
        actions={
          canPack && lines.length > 0 ? (
            <select
              aria-label="Start a pallet on"
              className="field-input h-touch w-auto py-0"
              value=""
              onChange={(event) => {
                if (event.target.value !== '') {
                  startPallet.mutate(Number(event.target.value));
                }
              }}
            >
              <option value="">Start a pallet on…</option>
              {lines.map((line) => (
                <option key={line.shiftLineId} value={line.shiftLineId}>
                  {line.label}
                </option>
              ))}
            </select>
          ) : undefined
        }
      />

      {canPack && lines.length === 0 && (
        <p className="mb-4 rounded-control border border-line bg-canvas px-4 py-3 text-sm text-ink-soft">
          No packing line is open. Pallets are built on the line that makes the bags.
        </p>
      )}

      {actionError !== null && (
        <p
          role="alert"
          className="mb-4 rounded-control border border-l-4 border-bad/30 border-l-bad bg-bad-soft px-4 py-3 text-sm font-medium text-bad"
        >
          {actionError}
        </p>
      )}

      <section className="mb-6 flex flex-wrap gap-2">
        <Chip
          label="Being built"
          active={openOnly}
          onClick={() => {
            setOpenOnly(true);
          }}
        />
        <Chip
          label="Every pallet"
          active={!openOnly}
          onClick={() => {
            setOpenOnly(false);
          }}
        />
      </section>

      <div className="grid gap-6 lg:grid-cols-3">
        <div className="lg:col-span-2">
          {pallets.data.length === 0 ? (
            <p className="card p-8 text-center text-ink-muted">
              {openOnly ? 'No pallet is being built.' : 'No pallet has been started yet.'}
            </p>
          ) : (
            <div className="grid gap-4 sm:grid-cols-2">
              {pallets.data.map((pallet) => (
                <PalletCard
                  key={pallet.id}
                  pallet={pallet}
                  isSelected={selectedId === pallet.id}
                  onSelect={() => {
                    setSelectedId((current) => (current === pallet.id ? null : pallet.id));
                  }}
                />
              ))}
            </div>
          )}
        </div>

        <aside className="lg:sticky lg:top-6 lg:self-start">
          {open === null ? (
            <div className="card p-6 text-sm text-ink-muted">
              Choose a pallet to scan bags onto it.
            </div>
          ) : (
            <div className="card p-5">
              <h2 className="mb-1 font-mono text-lg font-bold text-ink">{open.barcode}</h2>
              <p className="mb-5 text-sm text-ink-muted">
                Pallet {open.palletNumber} · {open.productionLineName}, shift{' '}
                {open.shiftName}
              </p>

              {canPack && (
                <PalletScanBox
                  pallet={open}
                  bags={availableBags.data ?? []}
                  onScanned={invalidate}
                />
              )}

              <dl className="rounded-control bg-canvas px-4 py-3 text-sm">
                <Row label="Bags">
                  {open.bagCount}
                  {open.capacity === null ? '' : ` / ${String(open.capacity)}`}
                </Row>
                <Row label="Total pieces">{open.pieceCount.toLocaleString('en-GB')}</Row>
                <Row label="Weight">{open.weight} kg</Row>
                <Row label="Colour">
                  {open.colorName ?? <span className="text-ink-muted">not set yet</span>}
                </Row>
                <Row label="Product">
                  {open.productName ?? <span className="text-ink-muted">not set yet</span>}
                </Row>
                <Row label="Status">
                  <PalletStatusBadge status={open.status} />
                </Row>
              </dl>
            </div>
          )}
        </aside>
      </div>

      {open !== null && (
        <section className="mt-8">
          <h2 className="mb-3 text-lg font-bold text-ink">
            What is on {open.barcode}
          </h2>

          <div className="card overflow-x-auto">
            <table className="w-full text-left text-sm">
              <thead>
                <tr className="border-b border-line text-xs tracking-wider text-ink-muted uppercase">
                  <th className="px-4 py-3 font-semibold">Bag</th>
                  <th className="px-4 py-3 font-semibold">From roll</th>
                  <th className="px-4 py-3 text-right font-semibold">Pieces</th>
                  <th className="px-4 py-3 text-right font-semibold">Weight</th>
                  <th className="px-4 py-3 font-semibold">Scanned by</th>
                  <th className="px-4 py-3" />
                </tr>
              </thead>
              <tbody>
                {open.bags.length === 0 && (
                  <tr>
                    <td colSpan={6} className="px-4 py-8 text-center text-ink-muted">
                      Nothing on it yet. The first bag decides what this pallet is.
                    </td>
                  </tr>
                )}
                {open.bags.map((bag) => (
                  <tr
                    key={bag.assignmentId}
                    className={[
                      'border-b border-line last:border-0',
                      bag.isActive ? '' : 'text-ink-muted',
                    ].join(' ')}
                  >
                    <td className="px-4 py-3 font-mono font-semibold">
                      <span className={bag.isActive ? 'text-ink' : 'line-through'}>
                        {bag.barcode}
                      </span>
                      {/* Kept for ever, with the reason it was undone. */}
                      {!bag.isActive && (
                        <span className="ml-2 font-sans text-xs font-normal">
                          taken off by {bag.reversedByName} — {bag.reversalReason}
                        </span>
                      )}
                    </td>
                    <td className="px-4 py-3 font-mono text-xs">{bag.rollCode}</td>
                    <td className="px-4 py-3 text-right tabular-nums">{bag.pieceCount}</td>
                    <td className="px-4 py-3 text-right tabular-nums">{bag.weight}</td>
                    <td className="px-4 py-3">{bag.assignedByName}</td>
                    <td className="px-4 py-3">
                      <div className="flex justify-end">
                        {bag.isActive && canReverse && open.status !== 'Shipped' && (
                          <button
                            type="button"
                            className="min-h-9 rounded-control border border-line px-3 text-sm font-medium whitespace-nowrap text-ink-soft transition-colors hover:border-bad/40 hover:bg-bad-soft hover:text-bad"
                            onClick={() => {
                              setReversing({
                                id: bag.assignmentId,
                                barcode: bag.barcode,
                              });
                            }}
                          >
                            Take it off
                          </button>
                        )}
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </section>
      )}

      {reversing !== null && (
        <ReverseScanDialog
          assignmentId={reversing.id}
          barcode={reversing.barcode}
          onClose={() => {
            setReversing(null);
          }}
          onReversed={invalidate}
        />
      )}
    </>
  );
}

function Row({
  label,
  children,
}: {
  label: string;
  children: React.ReactNode;
}): ReactElement {
  return (
    <div className="flex items-center justify-between gap-3 border-b border-line py-2 last:border-0">
      <dt className="text-ink-soft">{label}</dt>
      <dd className="font-semibold text-ink">{children}</dd>
    </div>
  );
}

function Chip({
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
        'min-h-9 rounded-full border px-4 text-sm font-medium transition-colors',
        active
          ? 'border-brand-600 bg-brand-50 text-brand-700'
          : 'border-line text-ink-soft hover:border-brand-200 hover:bg-brand-50',
      ].join(' ')}
    >
      {label}
    </button>
  );
}

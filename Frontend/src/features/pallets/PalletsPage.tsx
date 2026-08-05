import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useState, type ReactElement } from 'react';
import { PageHeader } from '../../components/ui/PageHeader';
import { useAuth } from '../../hooks/useAuth';
import { ApiError } from '../../lib/apiClient';
import { RoleNames } from '../../lib/roles';
import { shiftReportsApi } from '../shifts/api';
import { formatDate } from '../shifts/shiftFormat';
import { palletsApi } from './api';
import { PalletScanBox } from './PalletScanBox';
import { PalletStatusBadge } from './PalletStatusBadge';
import { ReverseScanDialog } from './ReverseScanDialog';

/**
 * Pallets (specification section 10).
 *
 * A pallet is chosen once and then scanned into, so the screen is a list on top and the
 * open pallet underneath — not a dialog per bag. The pallet's colour and product are
 * shown but never chosen: the first bag scanned decides them, in the factory's own words.
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

      <div className="card mb-8 overflow-x-auto">
        <table className="w-full text-left text-sm">
          <thead>
            <tr className="border-b border-line text-xs tracking-wider text-ink-muted uppercase">
              <th className="px-4 py-3 font-semibold">Pallet</th>
              <th className="px-4 py-3 font-semibold">Barcode</th>
              <th className="px-4 py-3 font-semibold">Holding</th>
              <th className="px-4 py-3 font-semibold">Shift</th>
              <th className="px-4 py-3">Status</th>
              <th className="px-4 py-3 text-right font-semibold">Bags</th>
              <th className="px-4 py-3 text-right font-semibold">Pieces</th>
              <th className="px-4 py-3 text-right font-semibold">Weight</th>
              <th className="px-4 py-3" />
            </tr>
          </thead>
          <tbody>
            {pallets.data.length === 0 && (
              <tr>
                <td colSpan={9} className="px-4 py-8 text-center text-ink-muted">
                  {openOnly ? 'No pallet is being built.' : 'No pallet has been started yet.'}
                </td>
              </tr>
            )}
            {pallets.data.map((pallet) => (
              <tr
                key={pallet.id}
                className={[
                  'border-b border-line last:border-0',
                  selectedId === pallet.id ? 'bg-brand-50' : '',
                ].join(' ')}
              >
                <td className="px-4 py-3 font-bold text-ink">{pallet.palletNumber}</td>
                <td className="px-4 py-3 font-mono text-xs text-ink-muted">
                  {pallet.barcode}
                </td>
                <td className="px-4 py-3 text-ink-soft">
                  {pallet.productName === null ? (
                    <span className="text-ink-muted">nothing yet</span>
                  ) : (
                    <>
                      {pallet.colorName} {pallet.productName}
                    </>
                  )}
                </td>
                <td className="px-4 py-3 whitespace-nowrap text-ink-soft">
                  {pallet.shiftName} · {formatDate(pallet.productionDate)}
                </td>
                <td className="px-4 py-3">
                  <PalletStatusBadge status={pallet.status} />
                </td>
                <td className="px-4 py-3 text-right tabular-nums text-ink-soft">
                  {pallet.bagCount}
                  {pallet.capacity !== null && (
                    <span className="text-ink-muted"> / {pallet.capacity}</span>
                  )}
                </td>
                <td className="px-4 py-3 text-right tabular-nums text-ink-soft">
                  {pallet.pieceCount.toLocaleString('en-GB')}
                </td>
                <td className="px-4 py-3 text-right tabular-nums text-ink-soft">
                  {pallet.weight}
                </td>
                <td className="px-4 py-3">
                  <div className="flex justify-end">
                    <button
                      type="button"
                      className="min-h-9 rounded-control border border-line px-3 text-sm font-medium whitespace-nowrap text-ink-soft transition-colors hover:border-brand-200 hover:bg-brand-50 hover:text-brand-700"
                      onClick={() => {
                        setSelectedId((current) =>
                          current === pallet.id ? null : pallet.id,
                        );
                      }}
                    >
                      {selectedId === pallet.id ? 'Close' : 'Open'}
                    </button>
                  </div>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {open !== null && (
        <section>
          <h2 className="mb-3 text-lg font-bold text-ink">
            Pallet {open.palletNumber}
            {open.productName !== null && (
              <span className="ml-2 text-base font-normal text-ink-soft">
                — {open.colorName} {open.productName}
              </span>
            )}
          </h2>

          {canPack && <PalletScanBox pallet={open} bags={availableBags.data ?? []} onScanned={invalidate} />}

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

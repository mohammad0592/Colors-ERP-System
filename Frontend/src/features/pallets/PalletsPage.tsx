import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useState, type ReactElement } from 'react';
import { useTranslation } from '../../hooks/useTranslation';
import { PageHeader } from '../../components/ui/PageHeader';
import { StartOnLineButton } from '../../components/ui/StartOnLineButton';
import { useAuth } from '../../hooks/useAuth';
import { ApiError } from '../../lib/apiClient';
import { RoleNames } from '../../lib/roles';
import { LabelPrintScreen } from '../labels/LabelPrintScreen';
import { shiftReportsApi } from '../shifts/api';
import { formatDate } from '../shifts/shiftFormat';
import { palletsApi } from './api';
import { CancelPalletDialog } from './CancelPalletDialog';
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
  const { t } = useTranslation();
  const queryClient = useQueryClient();
  const { hasRole } = useAuth();
  const canPack = hasRole(RoleNames.Administrator, RoleNames.PackagingOperator);
  const canReverse = hasRole(RoleNames.Administrator, RoleNames.Supervisor);

  const [openOnly, setOpenOnly] = useState(true);
  const [selectedId, setSelectedId] = useState<number | null>(null);
  const [reversing, setReversing] = useState<{ id: number; barcode: string } | null>(
    null,
  );
  const [cancelling, setCancelling] = useState<{ id: number; number: number } | null>(
    null,
  );
  const [labelFor, setLabelFor] = useState<string | null>(null);
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
            lineName: line.productionLineName,
            shiftLabel: `shift ${shift.shiftName}, ${formatDate(shift.productionDate)}`,
          })),
      );
    },
  });

  function invalidate(): void {
    void queryClient.invalidateQueries({ queryKey: ['pallets'] });
    void queryClient.invalidateQueries({ queryKey: ['pallet'] });
    void queryClient.invalidateQueries({ queryKey: ['available-bags'] });

    // Starting a pallet takes a wooden pallet out of the store and cancelling puts it
    // back, so the store's figure is now wrong until it is asked again. Without this the
    // inventory screen serves its cached number and the movement seems not to have
    // happened (specification section 10).
    void queryClient.invalidateQueries({ queryKey: ['inventory'] });
    void queryClient.invalidateQueries({ queryKey: ['inventory-movements'] });
  }

  const startPallet = useMutation({
    mutationFn: (shiftLineId: number) => palletsApi.start(shiftLineId, null),
    onSuccess: (pallet) => {
      setActionError(null);
      setSelectedId(pallet.id);
      // The label goes on the empty pallet before the first bag does, so it comes up
      // the moment the pallet is started.
      setLabelFor(pallet.barcode);
    },
    onError: (caught: unknown) => {
      setActionError(
        caught instanceof ApiError ? caught.message : t('common.somethingWrong'),
      );
    },
    onSettled: invalidate,
  });

  if (pallets.isPending) {
    return <p className="p-6 text-ink-muted">{t('common.loading')}</p>;
  }

  if (pallets.isError) {
    return <p className="p-6 text-bad">{t('pallets.loadFailed')}</p>;
  }

  const lines = packingLines.data ?? [];
  const open = selected.data ?? null;

  return (
    <>
      <PageHeader
        title={t('page.pallets.title')}
        subtitle={t('page.pallets.subtitle')}
        actions={
          canPack ? (
            <StartOnLineButton
              lines={lines}
              action="Start a pallet"
              onStart={(shiftLineId) => {
                startPallet.mutate(shiftLineId);
              }}
            />
          ) : undefined
        }
      />

      {canPack && lines.length === 0 && (
        <p className="mb-4 rounded-control border border-line bg-canvas px-4 py-3 text-sm text-ink-soft">
          {t('msg.noPackingLineOpen')}
        </p>
      )}

      {actionError !== null && (
        <p
          role="alert"
          className="mb-4 rounded-control border border-s-4 border-bad/30 border-s-bad bg-bad-soft px-4 py-3 text-sm font-medium text-bad"
        >
          {actionError}
        </p>
      )}

      <section className="mb-6 flex flex-wrap gap-2">
        <Chip
          label={t('pallets.beingBuilt')}
          active={openOnly}
          onClick={() => {
            setOpenOnly(true);
          }}
        />
        <Chip
          label={t('pallets.everyPallet')}
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
              {openOnly ? t('pallets.noneBeingBuilt') : t('pallets.noneStarted')}
            </p>
          ) : (
            <div className="grid gap-4 sm:grid-cols-2">
              {pallets.data.map((pallet) => (
                <PalletCard
                  key={pallet.id}
                  pallet={pallet}
                  isSelected={selectedId === pallet.id}
                  onSelect={() => {
                    setSelectedId((current) =>
                      current === pallet.id ? null : pallet.id,
                    );
                  }}
                />
              ))}
            </div>
          )}
        </div>

        <aside className="lg:sticky lg:top-6 lg:self-start">
          {open === null ? (
            <div className="card p-6 text-sm text-ink-muted">
              {t('pallets.choosePallet')}
            </div>
          ) : (
            <div className="card p-5">
              <h2 className="mb-1 font-mono text-lg font-bold text-ink">
                {open.barcode}
              </h2>
              <p className="mb-3 text-sm text-ink-muted">
                Pallet {open.palletNumber} · {open.productionLineName}, shift{' '}
                {open.shiftName}
              </p>
              <div className="mb-5 flex flex-wrap items-center gap-4">
                <button
                  type="button"
                  className="text-sm font-medium text-brand-700 hover:underline"
                  onClick={() => {
                    setLabelFor(open.barcode);
                  }}
                >
                  {t('pallets.printLabel')}
                </button>

                {/* Only while it is empty. After the first bag the wood is under the
                    bags, and the way back is to take the bags off. */}
                {canPack && open.status === 'Empty' && (
                  <button
                    type="button"
                    className="text-sm font-medium text-ink-soft hover:text-bad hover:underline"
                    onClick={() => {
                      setCancelling({ id: open.id, number: open.palletNumber });
                    }}
                  >
                    {t('pallets.cancelThis')}
                  </button>
                )}
              </div>

              {open.cancelledAt !== null && (
                <p className="mb-5 rounded-control border border-line bg-canvas px-4 py-3 text-sm text-ink-soft">
                  Cancelled by {open.cancelledByName} — {open.cancellationReason}. Its
                  wooden pallet went back to the store.
                </p>
              )}

              {canPack && open.status !== 'Cancelled' && (
                <PalletScanBox
                  pallet={open}
                  bags={availableBags.data ?? []}
                  onScanned={invalidate}
                />
              )}

              <dl className="rounded-control bg-canvas px-4 py-3 text-sm">
                <Row label={t('term.bags')}>
                  {open.bagCount}
                  {open.capacity === null ? '' : ` / ${String(open.capacity)}`}
                </Row>
                <Row label={t('pallets.totalPieces')}>{open.pieceCount.toLocaleString('en-GB')}</Row>
                <Row label={t('field.weight')}>{open.weight} kg</Row>
                <Row label={t('term.colour')}>
                  {open.colorName ?? <span className="text-ink-muted">not set yet</span>}
                </Row>
                <Row label={t('term.product')}>
                  {open.productName ?? (
                    <span className="text-ink-muted">not set yet</span>
                  )}
                </Row>
                <Row label={t('field.status')}>
                  <PalletStatusBadge status={open.status} />
                </Row>
              </dl>
            </div>
          )}
        </aside>
      </div>

      {open !== null && (
        <section className="mt-8">
          <h2 className="mb-3 text-lg font-bold text-ink">What is on {open.barcode}</h2>

          <div className="card overflow-x-auto">
            <table className="w-full text-start text-sm">
              <thead>
                <tr className="border-b border-line text-xs tracking-wider text-ink-muted uppercase">
                  <th className="px-4 py-3 font-semibold">{t('term.bag')}</th>
                  <th className="px-4 py-3 font-semibold">{t('term.fromRoll')}</th>
                  <th className="px-4 py-3 text-end font-semibold">{t('field.pieces')}</th>
                  <th className="px-4 py-3 text-end font-semibold">{t('field.weight')}</th>
                  <th className="px-4 py-3 font-semibold">{t('pallets.scannedBy')}</th>
                  <th className="px-4 py-3" />
                </tr>
              </thead>
              <tbody>
                {open.bags.length === 0 && (
                  <tr>
                    <td colSpan={6} className="px-4 py-8 text-center text-ink-muted">
                      {t('pallets.nothingOnItLong')}
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
                      <button
                        type="button"
                        className={[
                          'underline-offset-2 hover:underline',
                          bag.isActive ? 'text-ink' : 'line-through',
                        ].join(' ')}
                        onClick={() => {
                          setLabelFor(bag.barcode);
                        }}
                      >
                        {bag.barcode}
                      </button>
                      {/* Kept for ever, with the reason it was undone. */}
                      {!bag.isActive && (
                        <span className="ms-2 font-sans text-xs font-normal">
                          taken off by {bag.reversedByName} — {bag.reversalReason}
                        </span>
                      )}
                    </td>
                    <td className="px-4 py-3 font-mono text-xs">{bag.rollCode}</td>
                    <td className="px-4 py-3 text-end tabular-nums">{bag.pieceCount}</td>
                    <td className="px-4 py-3 text-end tabular-nums">{bag.weight}</td>
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
                            {t('pallets.takeItOff')}
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

      {labelFor !== null && (
        <LabelPrintScreen
          barcodes={[labelFor]}
          onClose={() => {
            setLabelFor(null);
          }}
        />
      )}

      {cancelling !== null && (
        <CancelPalletDialog
          palletId={cancelling.id}
          palletNumber={cancelling.number}
          onClose={() => {
            setCancelling(null);
          }}
          onCancelled={invalidate}
        />
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

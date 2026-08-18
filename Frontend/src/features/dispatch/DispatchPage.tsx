import { useQuery, useQueryClient } from '@tanstack/react-query';
import { useState, type ReactElement } from 'react';
import { PageHeader } from '../../components/ui/PageHeader';
import { ScanField } from '../../components/ui/ScanField';
import { ApiError } from '../../lib/apiClient';
import type { EntryMethod } from '../../lib/barcodeScanner';
import { palletsApi, type PalletSummaryDto } from '../pallets/api';
import { PalletStatusBadge } from '../pallets/PalletStatusBadge';
import { formatDate } from '../shifts/shiftFormat';
import { isStale, waitingLabel } from './dispatchFormat';
import { UnshipDialog } from './UnshipDialog';

/**
 * Dispatch — sending a finished pallet out of the factory (specification section 10).
 *
 * Until this screen existed the Shipped state was defined and unreachable: `ShippedAt`
 * was a column nothing ever set.
 *
 * Two lists, because they answer two different questions. **In the factory** is what is
 * finished and still here — the only place the system says what finished goods it holds,
 * and the list the man loading the lorry works down. **Gone** is the last few that left,
 * carried on screen only so a wrong scan can be undone by the man who made it, while he
 * still remembers.
 *
 * The box itself is `ScanField`, shared with every other screen that takes a code, so
 * the camera, the typing and the list behave here exactly as they do at the pallet.
 */
export function DispatchPage(): ReactElement {
  const queryClient = useQueryClient();

  const [barcode, setBarcode] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [lastShipped, setLastShipped] = useState<string | null>(null);
  const [isSaving, setIsSaving] = useState(false);
  const [unshipping, setUnshipping] = useState<{ id: number; number: number } | null>(
    null,
  );

  const inStock = useQuery({
    queryKey: ['pallets', 'in-stock'],
    queryFn: () => palletsApi.inStock(),
  });

  // Everything, filtered here rather than on the server: there is no "shipped only"
  // question anywhere else, and the list is already capped at 300 rows.
  const shipped = useQuery({
    queryKey: ['pallets', false],
    queryFn: () => palletsApi.list(false),
  });

  async function refresh(): Promise<void> {
    await queryClient.invalidateQueries({ queryKey: ['pallets'] });
  }

  async function send(code: string, entry: EntryMethod): Promise<void> {
    if (code === '') {
      return;
    }

    setError(null);
    setIsSaving(true);
    try {
      const pallet = await palletsApi.ship(
        { palletBarcode: code, palletId: null },
        entry,
      );
      setLastShipped(`Pallet ${String(pallet.palletNumber)}`);
      setBarcode('');
      await refresh();
    } catch (caught) {
      setError(
        caught instanceof ApiError ? caught.message : 'Something went wrong. Try again.',
      );
      // A refused scan keeps the code on screen so the man can see what he scanned.
    } finally {
      setIsSaving(false);
    }
  }

  const now = new Date();
  const waiting = inStock.data ?? [];
  const gone = (shipped.data ?? [])
    .filter((p) => p.status === 'Shipped')
    .slice(0, 10);

  return (
    <div>
      <PageHeader
        title="Dispatch"
        subtitle="Scan a pallet as it goes on the lorry."
      />

      <div className="mb-6 max-w-xl">
        <ScanField
          label="Scan a pallet"
          placeholder="P000123"
          value={barcode}
          onChange={setBarcode}
          onSubmit={(code, entry) => {
            void send(code, entry);
          }}
          // The list is every finished pallet still here, which is exactly what may be
          // sent out — so a torn label costs nothing.
          options={waiting.map((pallet) => ({
            value: pallet.barcode,
            label: `${pallet.barcode} — pallet ${String(pallet.palletNumber)}, ${pallet.colorName ?? ''} ${pallet.productName ?? ''}`,
          }))}
          optionsHint="finished pallets still in the factory"
          submitLabel="Send out"
          busy={isSaving}
        />

        {error !== null && (
          <p
            role="alert"
            className="mt-3 rounded-control border border-l-4 border-bad/30 border-l-bad bg-bad-soft px-4 py-3 text-sm font-medium text-bad"
          >
            {error}
          </p>
        )}

        {error === null && lastShipped !== null && (
          <p className="mt-3 rounded-control border border-l-4 border-ok/30 border-l-ok bg-ok-soft px-4 py-3 text-sm font-medium text-ok">
            <strong>{lastShipped}</strong> has gone.
          </p>
        )}
      </div>

      <section className="mb-8">
        <h2 className="mb-1 text-lg font-semibold text-ink">In the factory</h2>
        <p className="mb-3 text-sm text-ink-muted">
          Oldest first — load these before the newer ones.
        </p>

        {inStock.isPending && <p className="text-sm text-ink-muted">Loading…</p>}

        {!inStock.isPending && waiting.length === 0 && (
          <p className="rounded-control border border-line bg-canvas px-4 py-3 text-sm text-ink-soft">
            Nothing finished is waiting.
          </p>
        )}

        {waiting.length > 0 && (
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead>
                <tr className="border-b border-line text-left text-ink-muted">
                  <th className="py-2 pr-4 font-medium">Pallet</th>
                  <th className="py-2 pr-4 font-medium">What is on it</th>
                  <th className="py-2 pr-4 font-medium">Bags</th>
                  <th className="py-2 pr-4 font-medium">Weight</th>
                  <th className="py-2 pr-4 font-medium">Finished</th>
                  <th className="py-2 pr-4 font-medium">Waiting</th>
                </tr>
              </thead>
              <tbody>
                {waiting.map((pallet) => (
                  <Row key={pallet.id} pallet={pallet} now={now} />
                ))}
              </tbody>
            </table>
          </div>
        )}
      </section>

      <section>
        <h2 className="mb-1 text-lg font-semibold text-ink">Gone</h2>
        <p className="mb-3 text-sm text-ink-muted">
          The last ten to leave.
        </p>

        {gone.length === 0 && (
          <p className="rounded-control border border-line bg-canvas px-4 py-3 text-sm text-ink-soft">
            Nothing has been sent out yet.
          </p>
        )}

        {gone.length > 0 && (
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead>
                <tr className="border-b border-line text-left text-ink-muted">
                  <th className="py-2 pr-4 font-medium">Pallet</th>
                  <th className="py-2 pr-4 font-medium">What was on it</th>
                  <th className="py-2 pr-4 font-medium">Bags</th>
                  <th className="py-2 pr-4 font-medium">Went</th>
                  <th className="py-2 pr-4 font-medium" />
                </tr>
              </thead>
              <tbody>
                {gone.map((pallet) => (
                  <tr key={pallet.id} className="border-b border-line/60">
                    <td className="py-2 pr-4 font-mono">{pallet.palletNumber}</td>
                    <td className="py-2 pr-4">
                      {pallet.colorName ?? '—'} {pallet.productName ?? ''}
                    </td>
                    <td className="py-2 pr-4">{pallet.bagCount}</td>
                    <td className="py-2 pr-4">
                      {pallet.shippedAt === null ? '—' : formatDate(pallet.shippedAt)}
                    </td>
                    <td className="py-2 pr-4">
                      <button
                        type="button"
                        className="text-sm font-medium text-brand-700 underline"
                        onClick={() => {
                          setUnshipping({
                            id: pallet.id,
                            number: pallet.palletNumber,
                          });
                        }}
                      >
                        It did not go
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </section>

      {unshipping !== null && (
        <UnshipDialog
          palletId={unshipping.id}
          palletNumber={unshipping.number}
          onClose={() => {
            setUnshipping(null);
          }}
          onReversed={() => {
            void refresh();
          }}
        />
      )}
    </div>
  );
}

function Row({
  pallet,
  now,
}: {
  pallet: PalletSummaryDto;
  now: Date;
}): ReactElement {
  const stale = pallet.completedAt !== null && isStale(pallet.completedAt, now);

  return (
    <tr className="border-b border-line/60">
      <td className="py-2 pr-4 font-mono">
        {pallet.palletNumber} <PalletStatusBadge status={pallet.status} />
      </td>
      <td className="py-2 pr-4">
        {pallet.colorName ?? '—'} {pallet.productName ?? ''}
      </td>
      <td className="py-2 pr-4">{pallet.bagCount}</td>
      <td className="py-2 pr-4">{pallet.weight.toFixed(1)} kg</td>
      <td className="py-2 pr-4">
        {pallet.completedAt === null ? '—' : formatDate(pallet.completedAt)}
      </td>
      <td className={`py-2 pr-4 ${stale ? 'font-semibold text-bad' : 'text-ink-muted'}`}>
        {pallet.completedAt === null ? '—' : waitingLabel(pallet.completedAt, now)}
      </td>
    </tr>
  );
}

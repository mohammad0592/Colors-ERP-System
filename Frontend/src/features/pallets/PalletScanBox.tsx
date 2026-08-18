import { useState, type ReactElement } from 'react';
import { ScanField } from '../../components/ui/ScanField';
import { ApiError } from '../../lib/apiClient';
import type { EntryMethod } from '../../lib/barcodeScanner';
import { palletsApi, type AvailableBagDto, type PalletDto } from './api';

interface PalletScanBoxProps {
  pallet: PalletDto;
  bags: AvailableBagDto[];
  onScanned: (pallet: PalletDto) => void;
}

/**
 * The box the packer scans into, over and over (specification section 10).
 *
 * Built as a box that stays on screen rather than a dialog per bag, because this is the
 * most repeated action in the factory — a dialog would mean two extra clicks on every
 * one of a couple of dozen bags per pallet.
 *
 * Scanning, typing and picking off the list are one field now (`ScanField`), which is
 * also what carries the camera. Before that this screen had a box and a separate
 * dropdown beneath it, and every other screen taking a code had its own arrangement of
 * the same two things.
 */
export function PalletScanBox({
  pallet,
  bags,
  onScanned,
}: PalletScanBoxProps): ReactElement {
  const [barcode, setBarcode] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [lastAdded, setLastAdded] = useState<string | null>(null);
  const [isSaving, setIsSaving] = useState(false);

  const full = !pallet.isOpen;

  async function send(code: string, entry: EntryMethod): Promise<void> {
    setError(null);
    setIsSaving(true);
    try {
      // Always by barcode. The list offers barcodes too, so there is one path through
      // the server whether the man scanned the label, typed it, or chose it.
      const updated = await palletsApi.scanBag(
        pallet.id,
        { bagBarcode: code, producedBagId: null },
        entry,
      );
      const added = updated.bags.find((b) => b.isActive);
      setLastAdded(added?.barcode ?? null);
      setBarcode('');
      onScanned(updated);
    } catch (caught) {
      setError(
        caught instanceof ApiError ? caught.message : 'Something went wrong. Try again.',
      );
      // A refused scan keeps the code on screen so the packer can see what he scanned.
    } finally {
      setIsSaving(false);
    }
  }

  return (
    <div className="mb-5">
      <ScanField
        label="Scan a bag"
        placeholder="B000123"
        value={barcode}
        onChange={setBarcode}
        onSubmit={(code, entry) => {
          void send(code, entry);
        }}
        options={bags.map((bag) => ({
          value: bag.barcode,
          label: `${bag.barcode} — ${bag.colorName} ${bag.productName}, from roll ${bag.rollCode}`,
        }))}
        optionsHint={
          pallet.colorId === null
            ? 'the first bag decides what this pallet is'
            : `only ${pallet.colorName ?? ''} ${pallet.productName ?? ''} fits`
        }
        submitLabel="Add bag"
        disabled={full}
        busy={isSaving}
      />

      {full && (
        <p className="mt-3 rounded-control border border-line bg-canvas px-4 py-3 text-sm text-ink-soft">
          This pallet is {pallet.status === 'Shipped' ? 'gone' : 'full'}. Start a new one
          for the next bag.
        </p>
      )}

      {bags.length === 0 && !full && (
        <p className="mt-3 rounded-control border border-line bg-canvas px-4 py-3 text-sm text-ink-soft">
          No bag is waiting{pallet.colorId === null ? '' : ' that fits this pallet'}.
        </p>
      )}

      {error !== null && (
        <p
          role="alert"
          className="mt-3 rounded-control border border-s-4 border-bad/30 border-s-bad bg-bad-soft px-4 py-3 text-sm font-medium text-bad"
        >
          {error}
        </p>
      )}

      {error === null && lastAdded !== null && (
        <p className="mt-3 rounded-control border border-s-4 border-ok/30 border-s-ok bg-ok-soft px-4 py-3 text-sm font-medium text-ok">
          <strong className="font-mono">{lastAdded}</strong> is on the pallet —{' '}
          {pallet.capacity === null
            ? `${String(pallet.bagCount)} bag${pallet.bagCount === 1 ? '' : 's'}`
            : `${String(pallet.bagCount)} of ${String(pallet.capacity)} bags`}
          .
        </p>
      )}
    </div>
  );
}

import { useEffect, useRef, useState, type ReactElement } from 'react';
import { ApiError } from '../../lib/apiClient';
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
 * A scanner types the code then presses Enter, so the box submits on Enter, clears
 * itself, and takes the focus straight back. The packer never touches the tablet.
 */
export function PalletScanBox({
  pallet,
  bags,
  onScanned,
}: PalletScanBoxProps): ReactElement {
  const [barcode, setBarcode] = useState('');
  const [picked, setPicked] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [lastAdded, setLastAdded] = useState<string | null>(null);
  const [isSaving, setIsSaving] = useState(false);
  const box = useRef<HTMLInputElement>(null);

  // Back to the box after every scan, and whenever the packer changes pallet.
  //
  // It has to be an effect, not a line in the save handler: the box is disabled while
  // the scan is in flight, and focusing a disabled input does nothing. Waiting for the
  // render that re-enables it is what keeps the scan-scan-scan rhythm going.
  useEffect(() => {
    if (!isSaving) {
      box.current?.focus();
      box.current?.select();
    }
  }, [pallet.id, isSaving]);

  const full = !pallet.isOpen;

  async function send(body: {
    bagBarcode: string | null;
    producedBagId: number | null;
  }): Promise<void> {
    setError(null);
    setIsSaving(true);
    try {
      const updated = await palletsApi.scanBag(pallet.id, body);
      const added = updated.bags.find((b) => b.isActive);
      setLastAdded(added?.barcode ?? null);
      setBarcode('');
      setPicked('');
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
    <div className="card mb-6 p-4">
      <form
        onSubmit={(event) => {
          event.preventDefault();
          if (barcode.trim() !== '') {
            void send({ bagBarcode: barcode.trim(), producedBagId: null });
          }
        }}
        noValidate
      >
        <label className="field-label" htmlFor="pallet-scan">
          Scan a bag onto pallet {pallet.palletNumber}
        </label>
        <div className="flex flex-wrap gap-3">
          <input
            id="pallet-scan"
            ref={box}
            className="field-input flex-1 font-mono text-lg"
            placeholder="B000123"
            autoComplete="off"
            value={barcode}
            disabled={isSaving || full}
            onChange={(event) => {
              setBarcode(event.target.value);
            }}
          />
          <button
            type="submit"
            className="btn-primary w-auto"
            disabled={isSaving || full || barcode.trim() === ''}
          >
            Add
          </button>
        </div>
      </form>

      {/* For the office, and for a label too torn to scan. */}
      <div className="mt-3">
        <label className="field-label" htmlFor="pallet-pick">
          Or pick one{' '}
          <span className="font-normal text-ink-muted">
            {pallet.colorId === null
              ? '(the first bag decides what this pallet is)'
              : `(only ${pallet.colorName ?? ''} ${pallet.productName ?? ''})`}
          </span>
        </label>
        <select
          id="pallet-pick"
          className="field-input"
          value={picked}
          disabled={isSaving || full}
          onChange={(event) => {
            const value = event.target.value;
            setPicked(value);
            if (value !== '') {
              void send({ bagBarcode: null, producedBagId: Number(value) });
            }
          }}
        >
          <option value="">Choose a bag…</option>
          {bags.map((bag) => (
            <option key={bag.id} value={bag.id}>
              {bag.barcode} — {bag.colorName} {bag.productName}, from roll {bag.rollCode}
            </option>
          ))}
        </select>
      </div>

      {full && (
        <p className="mt-3 rounded-control border border-line bg-canvas px-4 py-3 text-sm text-ink-soft">
          This pallet is {pallet.status === 'Shipped' ? 'gone' : 'full'}. Start a new one
          for the next bag.
        </p>
      )}

      {bags.length === 0 && !full && (
        <p className="mt-3 rounded-control border border-line bg-canvas px-4 py-3 text-sm text-ink-soft">
          No bag is waiting{pallet.colorId === null ? '' : ' that fits this pallet'}. Bags
          are created when the thermo test person saves his form.
        </p>
      )}

      {error !== null && (
        <p
          role="alert"
          className="mt-3 rounded-control border border-l-4 border-bad/30 border-l-bad bg-bad-soft px-4 py-3 text-sm font-medium text-bad"
        >
          {error}
        </p>
      )}

      {error === null && lastAdded !== null && (
        <p className="mt-3 rounded-control border border-l-4 border-ok/30 border-l-ok bg-ok-soft px-4 py-3 text-sm font-medium text-ok">
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

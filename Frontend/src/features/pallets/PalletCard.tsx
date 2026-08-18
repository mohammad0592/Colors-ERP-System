import type { ReactElement } from 'react';
import type { PalletSummaryDto } from './api';
import { PalletStatusBadge } from './PalletStatusBadge';

interface PalletCardProps {
  pallet: PalletSummaryDto;
  isSelected: boolean;
  onSelect: () => void;
}

/**
 * One pallet, as a card with a slot for every bag it can hold
 * (specification section 10).
 *
 * The slots are the point: "9 of 15" is read faster as nine filled squares than as a
 * number, and the packer is looking at this from a step away with his hands full.
 *
 * How many slots there are comes from the product the pallet took off its first bag —
 * 15 for plates, about 21 for meal boxes. An empty pallet has no product yet, so it
 * honestly has no slots to draw.
 */
export function PalletCard({
  pallet,
  isSelected,
  onSelect,
}: PalletCardProps): ReactElement {
  const capacity = pallet.capacity;
  const filled = pallet.bagCount;
  const isFull = pallet.status === 'Completed' || pallet.status === 'Shipped';

  return (
    <button
      type="button"
      onClick={onSelect}
      aria-pressed={isSelected}
      className={[
        'card w-full p-5 text-start transition-colors',
        isSelected
          ? 'border-brand-600 ring-1 ring-brand-600'
          : 'hover:border-brand-200 hover:bg-brand-50/30',
      ].join(' ')}
    >
      <div className="mb-1 flex items-start justify-between gap-3">
        <span className="font-mono text-lg font-bold text-ink">{pallet.barcode}</span>
        <PalletStatusBadge status={pallet.status} />
      </div>

      {/* One line, as in the design. Which shift built it is on the panel, not here —
          the packer already knows, and the card is read from a step away. */}
      <p className="mb-4 text-sm text-ink-soft">
        {pallet.productName === null ? (
          <span className="text-ink-muted">Nothing on it yet</span>
        ) : (
          <>
            {pallet.productName} · {pallet.colorName}
          </>
        )}
      </p>

      {capacity === null ? (
        <p className="mb-3 rounded-control border border-dashed border-line px-3 py-4 text-center text-xs text-ink-muted">
          The first bag scanned decides what this pallet is, and how many fill it.
        </p>
      ) : (
        <>
          <div className="mb-2 flex flex-wrap gap-1">
            {Array.from({ length: capacity }, (_, slot) => (
              <span
                key={slot}
                className={[
                  'h-5 w-5 rounded-sm border',
                  slot < filled
                    ? isFull
                      ? 'border-ok bg-ok'
                      : 'border-brand-600 bg-brand-600'
                    : 'border-line bg-canvas',
                ].join(' ')}
              />
            ))}
          </div>

          <div className="mb-3 h-1.5 w-full overflow-hidden rounded-full bg-line">
            <div
              className={`h-full rounded-full ${isFull ? 'bg-ok' : 'bg-brand-600'}`}
              style={{ width: `${String(Math.min(100, (filled / capacity) * 100))}%` }}
            />
          </div>
        </>
      )}

      <div className="flex items-baseline justify-between gap-3 text-sm">
        <span className="font-semibold text-ink">
          {capacity === null
            ? `${String(filled)} bag${filled === 1 ? '' : 's'}`
            : `${String(filled)}/${String(capacity)} bags`}
        </span>
        <span className="text-ink-muted">
          {pallet.pieceCount.toLocaleString('en-GB')} pieces
        </span>
      </div>
    </button>
  );
}

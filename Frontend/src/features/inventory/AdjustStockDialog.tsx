import { useState, type ReactElement } from 'react';
import { Modal } from '../../components/ui/Modal';
import { ApiError } from '../../lib/apiClient';
import { inventoryApi, type MaterialStockDto } from './api';

interface AdjustStockDialogProps {
  material: MaterialStockDto;
  onClose: () => void;
  onAdjusted: () => void;
}

/**
 * Corrects a balance after a stock count.
 *
 * The supervisor types **what he counted**, not the difference — that is what he has
 * in his hand, and working out the difference is exactly the sort of mental arithmetic
 * that gets a number wrong. The system posts the difference as a movement.
 */
export function AdjustStockDialog({
  material,
  onClose,
  onAdjusted,
}: AdjustStockDialogProps): ReactElement {
  const [counted, setCounted] = useState('');
  const [reason, setReason] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [isSaving, setIsSaving] = useState(false);

  const countedNumber = Number(counted);
  const isNumber = counted.trim() !== '' && Number.isFinite(countedNumber);
  const difference = isNumber ? countedNumber - material.currentQuantity : 0;

  async function save(): Promise<void> {
    setError(null);
    setIsSaving(true);
    try {
      await inventoryApi.adjust({
        materialId: material.materialId,
        countedQuantity: countedNumber,
        reason,
      });
      onAdjusted();
      onClose();
    } catch (caught) {
      setError(
        caught instanceof ApiError ? caught.message : 'Something went wrong. Try again.',
      );
    } finally {
      setIsSaving(false);
    }
  }

  return (
    <Modal title={`Stock count — ${material.name}`} onClose={onClose}>
      <form
        onSubmit={(event) => {
          event.preventDefault();
          void save();
        }}
        noValidate
      >
        <div className="mb-4 rounded-control bg-canvas px-4 py-3">
          <div className="flex items-baseline justify-between gap-3">
            <span className="text-sm font-medium text-ink-soft">The system says</span>
            <span className="text-lg font-bold text-ink">
              {material.currentQuantity} {material.baseUnitSymbol}
            </span>
          </div>
        </div>

        <div className="mb-4">
          <label className="field-label" htmlFor="adjust-counted">
            What did you count?
          </label>
          <input
            id="adjust-counted"
            type="number"
            step="0.001"
            min="0"
            className="field-input"
            value={counted}
            disabled={isSaving}
            onChange={(event) => {
              setCounted(event.target.value);
            }}
          />
          <p className="mt-1 text-xs text-ink-muted">
            In {material.baseUnitName.toLowerCase()}. Type what is actually there — the
            system works out the difference.
          </p>
        </div>

        {isNumber && difference !== 0 && (
          <p
            className={[
              'mb-4 rounded-control px-4 py-3 text-sm font-medium',
              difference > 0 ? 'bg-ok-soft text-ok' : 'bg-warn-soft text-warn',
            ].join(' ')}
          >
            {difference > 0
              ? `${String(difference)} ${material.baseUnitSymbol} more than the system thought.`
              : `${String(Math.abs(difference))} ${material.baseUnitSymbol} less than the system thought.`}
          </p>
        )}

        <div className="mb-4">
          <label className="field-label" htmlFor="adjust-reason">
            Why is it different?
          </label>
          <textarea
            id="adjust-reason"
            rows={2}
            maxLength={400}
            className="field-input"
            value={reason}
            disabled={isSaving}
            placeholder="Spillage, a bag found behind the door, a delivery booked in twice…"
            onChange={(event) => {
              setReason(event.target.value);
            }}
          />
          <p className="mt-1 text-xs text-ink-muted">
            Kept on the record for good. A correction nobody explained is a mystery a
            month later.
          </p>
        </div>

        {error !== null && (
          <p
            role="alert"
            className="mb-4 rounded-control border border-l-4 border-bad/30 border-l-bad bg-bad-soft px-4 py-3 text-sm font-medium text-bad"
          >
            {error}
          </p>
        )}

        <button
          type="submit"
          className="btn-primary"
          disabled={isSaving || !isNumber || difference === 0 || reason.trim() === ''}
        >
          {isSaving ? 'Saving…' : 'Correct the balance'}
        </button>
      </form>
    </Modal>
  );
}

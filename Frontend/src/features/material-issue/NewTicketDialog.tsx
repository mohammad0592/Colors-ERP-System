import { useState, type ReactElement } from 'react';
import { Modal } from '../../components/ui/Modal';
import { ApiError } from '../../lib/apiClient';
import type { MaterialStockDto } from '../inventory/api';
import type { ShiftReportSummaryDto } from '../shifts/api';
import { formatDate } from '../shifts/shiftFormat';
import { materialIssueApi, type IssueTicketDto } from './api';

interface OpenLine {
  shiftLineId: number;
  label: string;
}

interface NewTicketDialogProps {
  /** Only lines of shifts that are still open — material cannot go to a finished one. */
  openLines: OpenLine[];
  shifts: ShiftReportSummaryDto[];
  /** Raw material only; the caller has already filtered out packaging. */
  stock: MaterialStockDto[];
  onClose: () => void;
  onCreated: (ticket: IssueTicketDto) => void;
}

/**
 * Issuing material (specification section 7).
 *
 * Every weight typed here leaves the store the moment the ticket is saved, so the
 * stock figure is true while the shift is still running rather than corrected
 * afterwards. The store balance is shown beside each material, because issuing more
 * than there is should be obvious before the button is pressed, not after.
 */
export function NewTicketDialog({
  openLines,
  shifts,
  stock,
  onClose,
  onCreated,
}: NewTicketDialogProps): ReactElement {
  const [shiftLineId, setShiftLineId] = useState(() => openLines[0]?.shiftLineId ?? 0);
  const [notes, setNotes] = useState('');
  const [quantities, setQuantities] = useState<Record<number, string>>({});
  const [error, setError] = useState<string | null>(null);
  const [isSaving, setIsSaving] = useState(false);

  const lines = Object.entries(quantities)
    .map(([materialId, value]) => ({
      materialId: Number(materialId),
      quantity: Number(value),
    }))
    .filter((line) => Number.isFinite(line.quantity) && line.quantity > 0);

  async function save(): Promise<void> {
    setError(null);
    setIsSaving(true);
    try {
      const ticket = await materialIssueApi.create({
        shiftLineId,
        notes: notes.trim() === '' ? null : notes.trim(),
        lines,
      });
      onCreated(ticket);
      onClose();
    } catch (caught) {
      setError(
        caught instanceof ApiError ? caught.message : 'Something went wrong. Try again.',
      );
    } finally {
      setIsSaving(false);
    }
  }

  if (openLines.length === 0) {
    return (
      <Modal title="Issue material" onClose={onClose}>
        <p className="text-sm leading-relaxed text-ink-soft">
          No shift is open, so there is nothing to issue material to.
        </p>
        {shifts.length > 0 && (
          <p className="mt-3 text-sm text-ink-muted">
            The most recent shift was {formatDate(shifts[0]?.productionDate ?? '')}, and
            it is {shifts[0]?.status.toLowerCase()}.
          </p>
        )}
      </Modal>
    );
  }

  return (
    <Modal title="Issue material" onClose={onClose}>
      <form
        onSubmit={(event) => {
          event.preventDefault();
          void save();
        }}
        noValidate
      >
        <div className="mb-4">
          <label className="field-label" htmlFor="ticket-line">
            Going to
          </label>
          <select
            id="ticket-line"
            className="field-input"
            value={shiftLineId}
            disabled={isSaving}
            onChange={(event) => {
              setShiftLineId(Number(event.target.value));
            }}
          >
            {openLines.map((line) => (
              <option key={line.shiftLineId} value={line.shiftLineId}>
                {line.label}
              </option>
            ))}
          </select>
        </div>

        <p className="field-label">What is going out, weighed</p>
        <p className="mb-2 text-xs text-ink-muted">
          Raw material only.
        </p>
        <div className="mb-4 max-h-72 overflow-y-auto rounded-control border border-line">
          <table className="w-full text-left text-sm">
            <tbody>
              {stock.map((material) => (
                <tr
                  key={material.materialId}
                  className="border-b border-line last:border-0"
                >
                  <td className="px-3 py-2">
                    <span className="font-medium text-ink">{material.name}</span>
                    <span className="ml-2 text-xs text-ink-muted">
                      {material.currentQuantity} {material.baseUnitSymbol} in store
                    </span>
                  </td>
                  <td className="px-3 py-2 text-right">
                    <input
                      type="number"
                      step="0.001"
                      min="0"
                      aria-label={`Weight of ${material.name}`}
                      className="field-input h-9 w-28 py-0 text-right"
                      value={quantities[material.materialId] ?? ''}
                      disabled={isSaving}
                      onChange={(event) => {
                        setQuantities((current) => ({
                          ...current,
                          [material.materialId]: event.target.value,
                        }));
                      }}
                    />
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>

        <div className="mb-4">
          <label className="field-label" htmlFor="ticket-notes">
            Note <span className="font-normal text-ink-muted">(optional)</span>
          </label>
          <input
            id="ticket-notes"
            className="field-input"
            maxLength={300}
            value={notes}
            disabled={isSaving}
            placeholder="Which mix it is for…"
            onChange={(event) => {
              setNotes(event.target.value);
            }}
          />
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
          disabled={isSaving || lines.length === 0}
        >
          {isSaving
            ? 'Issuing…'
            : `Issue ${String(lines.length)} material${lines.length === 1 ? '' : 's'}`}
        </button>
      </form>
    </Modal>
  );
}

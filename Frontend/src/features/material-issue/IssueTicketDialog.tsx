import { useState, type ReactElement } from 'react';
import { ConfirmDialog, type ConfirmRequest } from '../../components/ui/ConfirmDialog';
import { Modal } from '../../components/ui/Modal';
import { ApiError } from '../../lib/apiClient';
import { materialIssueApi, type IssueTicketDto } from './api';

interface IssueTicketDialogProps {
  ticket: IssueTicketDto;
  canIssue: boolean;
  onClose: () => void;
  onChanged: (ticket: IssueTicketDto) => void;
}

/**
 * One ticket: what went out, what came back, and what that means was used.
 *
 * The returns are typed as **weights**, one per material, because that is what the
 * storekeeper reads off the scale. Nobody types "net used" — it is the subtraction,
 * and a typed number could disagree with the two weighings it comes from.
 */
export function IssueTicketDialog({
  ticket,
  canIssue,
  onClose,
  onChanged,
}: IssueTicketDialogProps): ReactElement {
  const [returns, setReturns] = useState<Record<number, string>>({});
  const [error, setError] = useState<string | null>(null);
  const [isSaving, setIsSaving] = useState(false);
  const [confirm, setConfirm] = useState<ConfirmRequest | null>(null);

  const typed = Object.entries(returns)
    .map(([materialId, value]) => ({
      materialId: Number(materialId),
      quantity: Number(value),
    }))
    .filter((line) => value(line.quantity));

  function value(quantity: number): boolean {
    return Number.isFinite(quantity) && quantity > 0;
  }

  async function run(action: () => Promise<IssueTicketDto>): Promise<void> {
    setError(null);
    setIsSaving(true);
    try {
      onChanged(await action());
      setReturns({});
    } catch (caught) {
      setError(
        caught instanceof ApiError ? caught.message : 'Something went wrong. Try again.',
      );
    } finally {
      setIsSaving(false);
    }
  }

  const totalIssued = ticket.lines.reduce((sum, l) => sum + l.issuedQuantity, 0);
  const totalUsed = ticket.lines.reduce((sum, l) => sum + l.netUsed, 0);

  return (
    <Modal title={`Ticket ${String(ticket.ticketNumber)}`} onClose={onClose}>
      <div className="mb-5 flex flex-wrap items-center gap-3 border-b border-line pb-4 text-sm text-ink-muted">
        <span
          className={[
            'rounded-full px-2.5 py-0.5 text-xs font-semibold',
            ticket.isOpen ? 'bg-warn-soft text-warn' : 'bg-line text-ink-muted',
          ].join(' ')}
        >
          {ticket.status}
        </span>
        <span>
          {ticket.productionLineName} · shift {ticket.shiftName}
        </span>
        <span>· issued by {ticket.issuedByName}</span>
      </div>

      {ticket.notes !== null && (
        <p className="mb-4 rounded-control bg-canvas px-4 py-3 text-sm text-ink-soft">
          {ticket.notes}
        </p>
      )}

      <div className="mb-4 overflow-x-auto">
        <table className="w-full text-left text-sm">
          <thead>
            <tr className="border-b border-line text-xs tracking-wider text-ink-muted uppercase">
              <th className="py-2 pr-3 font-semibold">Material</th>
              <th className="px-3 py-2 text-right font-semibold">Out</th>
              <th className="px-3 py-2 text-right font-semibold">Back</th>
              <th className="px-3 py-2 text-right font-semibold">Used</th>
              {ticket.isOpen && canIssue && (
                <th className="py-2 pl-3 text-right font-semibold">Weigh back in</th>
              )}
            </tr>
          </thead>
          <tbody>
            {ticket.lines.map((line) => {
              const outstanding = line.issuedQuantity - line.returnedQuantity;
              return (
                <tr key={line.id} className="border-b border-line last:border-0">
                  <td className="py-2 pr-3 font-medium text-ink">
                    {line.materialName}
                    <span className="ml-2 font-mono text-xs text-ink-muted">
                      {line.materialCode}
                    </span>
                  </td>
                  <td className="px-3 py-2 text-right tabular-nums text-ink-soft">
                    {line.issuedQuantity}
                  </td>
                  <td className="px-3 py-2 text-right tabular-nums text-ink-soft">
                    {line.returnedQuantity}
                  </td>
                  <td className="px-3 py-2 text-right font-semibold tabular-nums text-ink">
                    {line.netUsed}{' '}
                    <span className="text-ink-muted">{line.baseUnitSymbol}</span>
                  </td>
                  {ticket.isOpen && canIssue && (
                    <td className="py-2 pl-3">
                      <input
                        type="number"
                        step="0.001"
                        min="0"
                        max={outstanding}
                        aria-label={`Leftover of ${line.materialName}`}
                        className="field-input h-9 w-28 py-0 text-right"
                        value={returns[line.materialId] ?? ''}
                        disabled={isSaving || outstanding <= 0}
                        onChange={(event) => {
                          setReturns((current) => ({
                            ...current,
                            [line.materialId]: event.target.value,
                          }));
                        }}
                      />
                    </td>
                  )}
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>

      <div className="mb-4 rounded-control bg-canvas px-4 py-3">
        <div className="flex items-baseline justify-between gap-3">
          <span className="text-sm font-medium text-ink-soft">
            Used so far, of {totalIssued} issued
          </span>
          <span className="text-lg font-bold text-ink">{totalUsed}</span>
        </div>
      </div>

      {error !== null && (
        <p
          role="alert"
          className="mb-4 rounded-control border border-l-4 border-bad/30 border-l-bad bg-bad-soft px-4 py-3 text-sm font-medium text-bad"
        >
          {error}
        </p>
      )}

      {ticket.isOpen && canIssue && (
        <div className="flex flex-wrap gap-3">
          <button
            type="button"
            className="btn-primary w-auto px-6"
            disabled={isSaving || typed.length === 0}
            onClick={() => {
              void run(() => materialIssueApi.recordReturns(ticket.id, typed));
            }}
          >
            {isSaving ? 'Saving…' : 'Record what came back'}
          </button>

          <button
            type="button"
            className="min-h-touch rounded-control border border-line px-5 text-sm font-semibold text-ink-soft transition-colors hover:bg-canvas"
            disabled={isSaving}
            onClick={() => {
              setConfirm({
                title: `Close ticket ${String(ticket.ticketNumber)}?`,
                message: (
                  <>
                    Whatever has not come back counts as <strong>used</strong> —{' '}
                    {totalUsed} of {totalIssued} issued.
                    <br />
                    <br />
                    The figures are then fixed, and the shift can be closed.
                  </>
                ),
                confirmLabel: 'Close ticket',
                tone: 'primary',
                onConfirm: () => {
                  void run(() => materialIssueApi.close(ticket.id));
                },
              });
            }}
          >
            Close ticket
          </button>
        </div>
      )}

      {confirm !== null && (
        <ConfirmDialog
          request={confirm}
          onCancel={() => {
            setConfirm(null);
          }}
        />
      )}
    </Modal>
  );
}

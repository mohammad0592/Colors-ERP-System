import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useState, type ReactElement } from 'react';
import { PageHeader } from '../../components/ui/PageHeader';
import { useAuth } from '../../hooks/useAuth';
import { ApiError } from '../../lib/apiClient';
import { RoleNames } from '../../lib/roles';
import { inventoryApi } from '../inventory/api';
import { shiftReportsApi } from '../shifts/api';
import { formatDate } from '../shifts/shiftFormat';
import { materialIssueApi, type IssueTicketDto } from './api';
import { IssueTicketDialog } from './IssueTicketDialog';
import { NewTicketDialog } from './NewTicketDialog';

/**
 * Material issue and return (specification section 7).
 *
 * The heart of the waste control: material out is weighed, leftover back is weighed,
 * and the difference is what was really used. A ticket left open is what stops its
 * shift from closing, so the open ones lead.
 */
export function MaterialIssuePage(): ReactElement {
  const queryClient = useQueryClient();
  const { hasRole } = useAuth();
  const canIssue = hasRole(RoleNames.Administrator, RoleNames.InventoryManager);

  const [openOnly, setOpenOnly] = useState(true);
  const [creating, setCreating] = useState(false);
  const [viewing, setViewing] = useState<IssueTicketDto | null>(null);
  const [actionError, setActionError] = useState<string | null>(null);

  const tickets = useQuery({
    queryKey: ['issue-tickets', openOnly],
    queryFn: () => materialIssueApi.list(openOnly),
  });

  const shifts = useQuery({
    queryKey: ['shift-reports', 'all', false],
    queryFn: () => shiftReportsApi.list(undefined, false),
  });

  const stock = useQuery({
    queryKey: ['inventory', false],
    queryFn: () => inventoryApi.stock(false),
  });

  // Only lines of shifts that are still open — material issued to a finished shift
  // could never be returned against it.
  const openShifts = useQuery({
    queryKey: ['shift-reports', 'open-detail'],
    queryFn: async () => {
      const open = await shiftReportsApi.list(undefined, true);
      return Promise.all(open.map((s) => shiftReportsApi.get(s.id)));
    },
  });

  // Only the lines that take raw material. Which those are is a tick box in Master
  // Data, not a rule about a line's name (specification section 4).
  const openLines = (openShifts.data ?? []).flatMap((shift) =>
    shift.lines
      .filter((line) => line.takesRawMaterial)
      .map((line) => ({
        shiftLineId: line.id,
        label: `${line.productionLineName} — shift ${shift.shiftName}, ${formatDate(shift.productionDate)}`,
      })),
  );

  function invalidate(): void {
    void queryClient.invalidateQueries({ queryKey: ['issue-tickets'] });
    void queryClient.invalidateQueries({ queryKey: ['inventory'] });
    // Issuing and returning both post movements, so the history is stale too.
    void queryClient.invalidateQueries({ queryKey: ['inventory-movements'] });
  }

  const open = useMutation({
    mutationFn: (id: number) => materialIssueApi.get(id),
    onSuccess: (full) => {
      setActionError(null);
      setViewing(full);
    },
    onError: (caught) => {
      setActionError(
        caught instanceof ApiError ? caught.message : 'Something went wrong.',
      );
    },
  });

  if (tickets.isPending || shifts.isPending || stock.isPending) {
    return <p className="p-6 text-ink-muted">Loading…</p>;
  }

  if (tickets.isError || shifts.isError || stock.isError) {
    return <p className="p-6 text-bad">Could not load the tickets.</p>;
  }

  return (
    <>
      <PageHeader
        title="Material Issue"
        subtitle="Material out is weighed, leftover back is weighed, and the difference is what was really used. A shift cannot close while a ticket is open."
        actions={
          canIssue ? (
            <button
              type="button"
              className="btn-primary h-touch w-auto px-5 text-base"
              onClick={() => {
                setCreating(true);
              }}
            >
              Issue material
            </button>
          ) : undefined
        }
      />

      <section className="mb-6 flex flex-wrap gap-2">
        <Chip
          label="Open tickets"
          active={openOnly}
          onClick={() => {
            setOpenOnly(true);
          }}
        />
        <Chip
          label="All tickets"
          active={!openOnly}
          onClick={() => {
            setOpenOnly(false);
          }}
        />
      </section>

      {actionError !== null && (
        <p
          role="alert"
          className="mb-4 rounded-control border border-l-4 border-bad/30 border-l-bad bg-bad-soft px-4 py-3 text-sm font-medium text-bad"
        >
          {actionError}
        </p>
      )}

      <div className="card overflow-x-auto">
        <table className="w-full text-left text-sm">
          <thead>
            <tr className="border-b border-line text-xs tracking-wider text-ink-muted uppercase">
              <th className="px-4 py-3 font-semibold">Ticket</th>
              <th className="px-4 py-3 font-semibold">Going to</th>
              <th className="px-4 py-3 font-semibold">Shift</th>
              <th className="px-4 py-3 font-semibold">Status</th>
              <th className="px-4 py-3 font-semibold">Materials</th>
              <th className="px-4 py-3 text-right font-semibold">Out</th>
              <th className="px-4 py-3 text-right font-semibold">Back</th>
              <th className="px-4 py-3 text-right font-semibold">Used</th>
              <th className="px-4 py-3" />
            </tr>
          </thead>
          <tbody>
            {tickets.data.length === 0 && (
              <tr>
                <td colSpan={9} className="px-4 py-8 text-center text-ink-muted">
                  {openOnly ? 'Nothing is outstanding.' : 'No tickets yet.'}
                </td>
              </tr>
            )}
            {tickets.data.map((ticket) => (
              <tr key={ticket.id} className="border-b border-line last:border-0">
                <td className="px-4 py-3 font-bold text-ink">{ticket.ticketNumber}</td>
                <td className="px-4 py-3 text-ink-soft">{ticket.productionLineName}</td>
                <td className="px-4 py-3 text-ink-soft">
                  {ticket.shiftName} · {formatDate(ticket.productionDate)}
                </td>
                <td className="px-4 py-3">
                  <span
                    className={[
                      'rounded-full px-2.5 py-0.5 text-xs font-semibold',
                      ticket.isOpen ? 'bg-warn-soft text-warn' : 'bg-line text-ink-muted',
                    ].join(' ')}
                  >
                    {ticket.status}
                  </span>
                </td>
                <td className="px-4 py-3 text-ink-soft">{ticket.lineCount}</td>
                <td className="px-4 py-3 text-right tabular-nums text-ink-soft">
                  {ticket.totalIssued}
                </td>
                <td className="px-4 py-3 text-right tabular-nums text-ink-soft">
                  {ticket.totalReturned}
                </td>
                <td className="px-4 py-3 text-right font-semibold tabular-nums text-ink">
                  {ticket.totalIssued - ticket.totalReturned}
                </td>
                <td className="px-4 py-3">
                  <div className="flex justify-end">
                    <button
                      type="button"
                      className="min-h-9 rounded-control border border-line px-3 text-sm font-medium whitespace-nowrap text-ink-soft transition-colors hover:border-brand-200 hover:bg-brand-50 hover:text-brand-700"
                      onClick={() => {
                        open.mutate(ticket.id);
                      }}
                    >
                      {ticket.isOpen && canIssue ? 'Weigh back in' : 'View'}
                    </button>
                  </div>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {creating && (
        <NewTicketDialog
          openLines={openLines}
          shifts={shifts.data}
          // Raw material only. Offering packaging would invite a double count
          // against the end-of-shift figures the system works out itself.
          stock={stock.data.filter((m) => m.issuedOnTickets)}
          onClose={() => {
            setCreating(false);
          }}
          onCreated={(ticket) => {
            invalidate();
            setViewing(ticket);
          }}
        />
      )}

      {viewing !== null && (
        <IssueTicketDialog
          ticket={viewing}
          canIssue={canIssue}
          onClose={() => {
            setViewing(null);
          }}
          onChanged={(ticket) => {
            setViewing(ticket);
            invalidate();
          }}
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

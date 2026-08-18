import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useState, type ReactElement } from 'react';
import { ConfirmDialog, type ConfirmRequest } from '../../components/ui/ConfirmDialog';
import { useTranslation } from '../../hooks/useTranslation';
import { PageHeader } from '../../components/ui/PageHeader';
import { useAuth } from '../../hooks/useAuth';
import { ApiError } from '../../lib/apiClient';
import { RoleNames } from '../../lib/roles';
import { mouldsApi, productionLinesApi, shiftsApi } from '../master-data/api';
import { peopleApi } from '../people/api';
import { shiftReportsApi, type ShiftReportDto, type ShiftReportSummaryDto } from './api';
import { OpenShiftDialog } from './OpenShiftDialog';
import { ReopenShiftDialog } from './ReopenShiftDialog';
import { ShiftReportDialog } from './ShiftReportDialog';
import { formatDate, orDash } from './shiftFormat';
import { ShiftStatusBadge } from './ShiftStatusBadge';

/**
 * Shifts — one record per date and shift for the whole factory, with the lines that
 * ran hanging underneath (specification section 2).
 *
 * A shift is opened when work starts, filled in as it runs, and closed at the end.
 * Closing is what makes "did every shift get its readings?" a question with an
 * answer, so it is the action the screen leads with.
 */
export function ShiftsPage(): ReactElement {
  const { t } = useTranslation();
  const queryClient = useQueryClient();
  const { hasRole } = useAuth();
  const isAdministrator = hasRole(RoleNames.Administrator);

  const [lineFilter, setLineFilter] = useState<number | 'all'>('all');
  const [openOnly, setOpenOnly] = useState(false);
  const [opening, setOpening] = useState(false);
  const [editing, setEditing] = useState<ShiftReportDto | null>(null);
  const [reopening, setReopening] = useState<ShiftReportSummaryDto | null>(null);
  const [confirm, setConfirm] = useState<ConfirmRequest | null>(null);
  const [actionError, setActionError] = useState<string | null>(null);

  const lines = useQuery({
    queryKey: ['production-lines', 'active'],
    queryFn: () => productionLinesApi.list(false),
  });

  const shifts = useQuery({
    queryKey: ['shifts', 'active'],
    queryFn: () => shiftsApi.list(false),
  });

  const people = useQuery({
    queryKey: ['people'],
    queryFn: () => peopleApi.list(false),
  });

  const roles = useQuery({
    queryKey: ['roles'],
    queryFn: () => peopleApi.roles(),
  });

  // Only active moulds — a retired template must not be mountable on a new shift.
  const moulds = useQuery({
    queryKey: ['moulds', 'active'],
    queryFn: () => mouldsApi.list(false),
  });

  const reports = useQuery({
    queryKey: ['shift-reports', lineFilter, openOnly],
    queryFn: () =>
      shiftReportsApi.list(lineFilter === 'all' ? undefined : lineFilter, openOnly),
  });

  function invalidate(): void {
    void queryClient.invalidateQueries({ queryKey: ['shift-reports'] });
  }

  function onActionError(caught: unknown): void {
    setActionError(caught instanceof ApiError ? caught.message : 'Something went wrong.');
  }

  const open = useMutation({
    mutationFn: (id: number) => shiftReportsApi.get(id),
    onSuccess: (full) => {
      setActionError(null);
      setEditing(full);
    },
    onError: onActionError,
  });

  const close = useMutation({
    mutationFn: (id: number) => shiftReportsApi.close(id),
    onSuccess: () => {
      setActionError(null);
    },
    onError: onActionError,
    onSettled: invalidate,
  });

  const remove = useMutation({
    mutationFn: (id: number) => shiftReportsApi.remove(id),
    onSuccess: () => {
      setActionError(null);
    },
    onError: onActionError,
    onSettled: invalidate,
  });

  if (
    lines.isPending ||
    shifts.isPending ||
    people.isPending ||
    roles.isPending ||
    moulds.isPending ||
    reports.isPending
  ) {
    return <p className="p-6 text-ink-muted">Loading…</p>;
  }

  if (
    lines.isError ||
    shifts.isError ||
    people.isError ||
    roles.isError ||
    moulds.isError
  ) {
    return <p className="p-6 text-bad">Could not load the shift screen.</p>;
  }

  if (reports.isError) {
    return <p className="p-6 text-bad">Could not load the shifts.</p>;
  }

  const openCount = reports.data.filter((report) => report.status === 'Open').length;

  return (
    <>
      <PageHeader
        title={t('page.shifts.title')}
        subtitle={t('page.shifts.subtitle')}
        actions={
          <button
            type="button"
            className="btn-primary h-touch w-auto px-5 text-base"
            onClick={() => {
              setOpening(true);
            }}
          >
            Open a shift
          </button>
        }
      />

      {/* Lines, as a filter — "which shifts did the thermo run in?" */}
      <section className="mb-6 flex flex-wrap gap-2">
        <FilterChip
          label="All lines"
          active={lineFilter === 'all'}
          onClick={() => {
            setLineFilter('all');
          }}
        />
        {lines.data.map((line) => (
          <FilterChip
            key={line.id}
            label={line.name}
            active={lineFilter === line.id}
            onClick={() => {
              setLineFilter((current) => (current === line.id ? 'all' : line.id));
            }}
          />
        ))}
        <FilterChip
          label={`Open only${openOnly ? '' : ` (${String(openCount)})`}`}
          active={openOnly}
          onClick={() => {
            setOpenOnly((current) => !current);
          }}
        />
      </section>

      {actionError !== null && (
        <p
          role="alert"
          className="mb-4 rounded-control border border-s-4 border-bad/30 border-s-bad bg-bad-soft px-4 py-3 text-sm font-medium text-bad"
        >
          {actionError}
        </p>
      )}

      <p className="mb-3 text-sm text-ink-muted">
        {reports.data.length} shift{reports.data.length === 1 ? '' : 's'}
      </p>

      <div className="card overflow-x-auto">
        <table className="w-full text-start text-sm">
          <thead>
            <tr className="border-b border-line text-xs tracking-wider text-ink-muted uppercase">
              <th className="px-4 py-3 font-semibold">Date</th>
              <th className="px-4 py-3 font-semibold">Shift</th>
              <th className="px-4 py-3 font-semibold">Status</th>
              <th className="px-4 py-3 font-semibold">Lines running</th>
              <th className="px-4 py-3 font-semibold">Supervisor</th>
              <th className="px-4 py-3 font-semibold">Electricity</th>
              <th className="px-4 py-3 font-semibold">Crew</th>
              <th className="px-4 py-3" />
            </tr>
          </thead>
          <tbody>
            {reports.data.length === 0 && (
              <tr>
                <td colSpan={8} className="px-4 py-8 text-center text-ink-muted">
                  No shifts here yet.
                </td>
              </tr>
            )}
            {reports.data.map((report) => (
              <tr key={report.id} className="border-b border-line last:border-0">
                <td className="px-4 py-3 font-semibold text-ink">
                  {formatDate(report.productionDate)}
                </td>
                <td className="px-4 py-3 text-ink-soft">{report.shiftName}</td>
                <td className="px-4 py-3">
                  <ShiftStatusBadge status={report.status} />
                </td>
                <td className="px-4 py-3 text-ink-soft">{report.lineNames.join(', ')}</td>
                <td className="px-4 py-3 text-ink-soft">
                  {report.supervisorName ?? '—'}
                </td>
                <td className="px-4 py-3 text-ink-soft">
                  {orDash(report.electricityUsed)}
                </td>
                <td className="px-4 py-3 text-ink-soft">{report.workerCount}</td>
                <td className="px-4 py-3">
                  <div className="flex justify-end gap-2">
                    <Action
                      label={report.canEdit ? 'Open report' : 'View'}
                      onClick={() => {
                        open.mutate(report.id);
                      }}
                    />

                    {report.canEdit && (
                      <>
                        <Action
                          label="Close shift"
                          tone="primary"
                          onClick={() => {
                            setConfirm({
                              title: 'Close this shift?',
                              message: (
                                <>
                                  Shift {report.shiftName} on{' '}
                                  {formatDate(report.productionDate)} will be finished,
                                  with all {report.lineCount} line
                                  {report.lineCount === 1 ? '' : 's'} on it.
                                  <br />
                                  <br />
                                  Nothing more can be recorded against it, and its figures
                                  are fixed. Only an administrator can reopen it.
                                </>
                              ),
                              confirmLabel: 'Close shift',
                              tone: 'primary',
                              onConfirm: () => {
                                close.mutate(report.id);
                              },
                            });
                          }}
                        />

                        {/* Only an empty shift can be removed — one opened on the
                            wrong day. */}
                        {report.workerCount === 0 && (
                          <Action
                            label="Discard"
                            tone="danger"
                            onClick={() => {
                              setConfirm({
                                title: 'Discard this shift?',
                                message: (
                                  <>
                                    Nothing has been recorded on shift {report.shiftName},{' '}
                                    {formatDate(report.productionDate)}, so it can be
                                    removed. Use this when a shift was opened by mistake.
                                  </>
                                ),
                                confirmLabel: 'Discard',
                                onConfirm: () => {
                                  remove.mutate(report.id);
                                },
                              });
                            }}
                          />
                        )}
                      </>
                    )}

                    {!report.canEdit && isAdministrator && (
                      <Action
                        label="Reopen"
                        onClick={() => {
                          setReopening(report);
                        }}
                      />
                    )}
                  </div>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {opening && (
        <OpenShiftDialog
          lines={lines.data}
          shifts={shifts.data}
          people={people.data}
          onClose={() => {
            setOpening(false);
          }}
          onOpened={(report) => {
            invalidate();
            // Straight into the report: opening a shift is the start of filling it in.
            setEditing(report);
          }}
        />
      )}

      {editing !== null && (
        <ShiftReportDialog
          report={editing}
          allLines={lines.data}
          people={people.data}
          roles={roles.data}
          moulds={moulds.data}
          onClose={() => {
            setEditing(null);
          }}
          onChanged={(report) => {
            // The dialog stays open on the fresh copy, so the calculated hours and
            // the tabs update the moment a line is saved.
            setEditing(report);
            invalidate();
          }}
        />
      )}

      {reopening !== null && (
        <ReopenShiftDialog
          report={reopening}
          onClose={() => {
            setReopening(null);
          }}
          onReopened={invalidate}
        />
      )}

      {confirm !== null && (
        <ConfirmDialog
          request={confirm}
          onCancel={() => {
            setConfirm(null);
          }}
        />
      )}
    </>
  );
}

function FilterChip({
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

function Action({
  label,
  onClick,
  tone = 'normal',
}: {
  label: string;
  onClick: () => void;
  tone?: 'normal' | 'primary' | 'danger';
}): ReactElement {
  const tones = {
    normal:
      'border-line text-ink-soft hover:border-brand-200 hover:bg-brand-50 hover:text-brand-700',
    primary: 'border-brand-600 bg-brand-600 text-white hover:bg-brand-700',
    danger:
      'border-line text-ink-muted hover:border-bad/40 hover:bg-bad-soft hover:text-bad',
  };

  return (
    <button
      type="button"
      onClick={onClick}
      className={`min-h-9 rounded-control border px-3 text-sm font-medium whitespace-nowrap transition-colors ${tones[tone]}`}
    >
      {label}
    </button>
  );
}

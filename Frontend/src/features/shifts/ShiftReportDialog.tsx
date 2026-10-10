import { useState, type ReactElement } from 'react';
import { Link } from 'react-router-dom';
import { useTranslation } from '../../hooks/useTranslation';
import { ConfirmDialog, type ConfirmRequest } from '../../components/ui/ConfirmDialog';
import { Modal } from '../../components/ui/Modal';
import { ApiError } from '../../lib/apiClient';
import type { ProductionLineDto } from '../master-data/api';
import type { PersonDto } from '../people/api';
import { shiftReportsApi, type ShiftLineDto, type ShiftReportDto } from './api';
import { formatDate } from './shiftFormat';
import { ShiftStatusBadge } from './ShiftStatusBadge';

interface ShiftReportDialogProps {
  report: ShiftReportDto;
  allLines: ProductionLineDto[];
  people: PersonDto[];
  onClose: () => void;
  onChanged: (report: ShiftReportDto) => void;
}

/**
 * The lines on a shift, and who runs each one (specification section 2).
 *
 * That is all the supervisor sets here. Each line's times, crew, notes and machine
 * settings are its operator's to fill in, on the Shift Configurations screen — and the
 * meter is shared there by every line. The supervisor himself is chosen when the shift
 * is opened.
 */
export function ShiftReportDialog({
  report,
  allLines,
  people,
  onClose,
  onChanged,
}: ShiftReportDialogProps): ReactElement {
  const { t } = useTranslation();
  // A shift being corrected is not running, but its record is still open to change.
  const locked = !report.canEdit;
  const [error, setError] = useState<string | null>(null);
  const [isSaving, setIsSaving] = useState(false);
  const [confirm, setConfirm] = useState<ConfirmRequest | null>(null);

  const missing = allLines.filter(
    (line) => !report.lines.some((onShift) => onShift.productionLineId === line.id),
  );

  async function run(action: () => Promise<ShiftReportDto>): Promise<void> {
    setError(null);
    setIsSaving(true);
    try {
      onChanged(await action());
    } catch (caught) {
      setError(
        caught instanceof ApiError ? caught.message : t('common.somethingWentWrong'),
      );
    } finally {
      setIsSaving(false);
    }
  }

  /** Only the people who hold the job that runs this line. */
  function candidates(line: ShiftLineDto): PersonDto[] {
    const role = line.operatorRole;
    return role === null
      ? people
      : people.filter((person) => person.roles.includes(role));
  }

  return (
    <Modal
      title={`Shift ${report.shiftName} — ${formatDate(report.productionDate)}`}
      onClose={onClose}
    >
      <div className="mb-5 flex flex-wrap items-center gap-3 border-b border-line pb-4 text-sm text-ink-muted">
        <ShiftStatusBadge status={report.status} />
        <span>Opened by {report.openedByName}</span>
        {report.supervisorName !== null && (
          <span>
            · {t('term.supervisor')}: {report.supervisorName}
          </span>
        )}
        {report.closedByName !== null && <span>· Closed by {report.closedByName}</span>}
      </div>

      {locked && (
        <p className="mb-5 rounded-control border border-line bg-canvas px-4 py-3 text-sm text-ink-soft">
          {t('shifts.closedNote')}
        </p>
      )}

      <h3 className="mb-1 text-sm font-bold tracking-wider text-ink-muted uppercase">
        {t('shifts.lineOperators')}
      </h3>
      <p className="mb-4 text-xs text-ink-muted">{t('shifts.operatorsHint')}</p>

      <ul className="mb-4 space-y-3">
        {report.lines.map((line) => (
          <li
            key={line.id}
            className="flex flex-wrap items-end gap-3 rounded-control border border-line p-3"
          >
            <div className="min-w-48 flex-1">
              <label className="field-label" htmlFor={`operator-${String(line.id)}`}>
                {line.productionLineName}
              </label>
              <select
                id={`operator-${String(line.id)}`}
                className="field-input"
                value={line.operatorUserId ?? ''}
                disabled={locked || isSaving}
                onChange={(event) => {
                  const chosen =
                    event.target.value === '' ? null : Number(event.target.value);
                  void run(() =>
                    shiftReportsApi.setLineOperator(report.id, line.id, chosen),
                  );
                }}
              >
                <option value="">{t('shifts.chooseOperator')}</option>
                {candidates(line).map((person) => (
                  <option key={person.id} value={person.id}>
                    {person.fullName} ({person.employeeNumber})
                  </option>
                ))}
              </select>
            </div>

            {/* A line with nothing recorded can come off the shift — the usual reason
                is that it turned out not to run. */}
            {!locked && report.lines.length > 1 && line.workers.length === 0 && (
              <button
                type="button"
                disabled={isSaving}
                className="min-h-touch rounded-control border border-line px-3 text-sm font-medium text-ink-muted transition-colors hover:border-bad/40 hover:bg-bad-soft hover:text-bad"
                onClick={() => {
                  setConfirm({
                    title: `Take ${line.productionLineName} off this shift?`,
                    message: (
                      <>
                        Nothing has been recorded for{' '}
                        <strong>{line.productionLineName}</strong>, so it can be removed.
                        Use this when the line turned out not to run.
                      </>
                    ),
                    confirmLabel: t('shifts.removeLine'),
                    onConfirm: () => {
                      void run(() => shiftReportsApi.removeLine(report.id, line.id));
                    },
                  });
                }}
              >
                {t('shifts.removeLine')}
              </button>
            )}
          </li>
        ))}
      </ul>

      {!locked && missing.length > 0 && (
        <div className="mb-4 flex flex-wrap gap-2">
          {missing.map((line) => (
            <button
              key={line.id}
              type="button"
              disabled={isSaving}
              onClick={() => {
                void run(() => shiftReportsApi.addLine(report.id, line.id));
              }}
              className="min-h-touch rounded-control border border-dashed border-line px-4 text-sm font-medium text-ink-muted transition-colors hover:border-brand-200 hover:bg-brand-50 hover:text-brand-700"
            >
              + {line.name}
            </button>
          ))}
        </div>
      )}

      {error !== null && (
        <p
          role="alert"
          className="mb-4 rounded-control border border-s-4 border-bad/30 border-s-bad bg-bad-soft px-4 py-3 text-sm font-medium text-bad"
        >
          {error}
        </p>
      )}

      <div className="mt-6 flex flex-wrap gap-3">
        <Link
          to={`/production/shift-config?shift=${String(report.id)}`}
          className="btn-primary grid w-auto place-items-center px-6"
        >
          {t('shifts.openConfig')}
        </Link>
        <button
          type="button"
          className="min-h-touch rounded-control border border-line px-6 text-sm font-semibold text-ink-soft transition-colors hover:bg-canvas"
          onClick={onClose}
        >
          {t('common.close')}
        </button>
      </div>

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

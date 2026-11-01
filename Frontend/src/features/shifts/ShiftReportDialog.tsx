import { useState, type ReactElement } from 'react';
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
 * That is all the supervisor sets here. Each line's times, staff, notes and machine
 * settings are its operator's to fill in, on that line's Shift Configuration — and the
 * meter is shared there by every line. The supervisor himself is chosen when the shift
 * is opened.
 *
 * The operators are chosen first and saved together with one Save, which closes the
 * window (section 19.24). Adding or taking off a line happens at once, as it changes the
 * list being chosen from.
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
  // The operators picked but not yet saved, by line. A line not in here keeps its own.
  const [chosen, setChosen] = useState<Record<number, number | null>>({});

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

  /** The operator a line will have: the one picked here, or the one it has. */
  function operatorOf(line: ShiftLineDto): number | null {
    return line.id in chosen ? (chosen[line.id] ?? null) : line.operatorUserId;
  }

  /** Saves every operator that was changed, one line after another, and closes. */
  async function save(): Promise<void> {
    const changed = report.lines.filter(
      (line) => operatorOf(line) !== line.operatorUserId,
    );
    if (changed.length === 0) {
      onClose();
      return;
    }

    setError(null);
    setIsSaving(true);
    try {
      let latest = report;
      for (const line of changed) {
        latest = await shiftReportsApi.setLineOperator(
          report.id,
          line.id,
          operatorOf(line),
        );
      }
      onChanged(latest);
      onClose();
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
                {/* Why the roll numbers start again at 01 on this shift (19.16). */}
                {line.isStartingShift && (
                  <span className="ms-2 rounded-full bg-brand-50 px-2 py-0.5 text-xs font-semibold text-brand-700">
                    {t('shifts.startingShiftBadge')}
                  </span>
                )}
              </label>
              <select
                id={`operator-${String(line.id)}`}
                className="field-input"
                value={operatorOf(line) ?? ''}
                disabled={locked || isSaving}
                onChange={(event) => {
                  const picked =
                    event.target.value === '' ? null : Number(event.target.value);
                  setChosen((current) => ({ ...current, [line.id]: picked }));
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

      {/* One Save for the operators chosen above. A shift that can no longer be changed
          has nothing to save, and closes with the cross. */}
      {!locked && (
        <div className="mt-6">
          <button
            type="button"
            className="btn-primary"
            disabled={isSaving}
            onClick={() => {
              void save();
            }}
          >
            {isSaving ? t('common.saving') : t('common.save')}
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

import { useState, type ReactElement } from 'react';
import { ConfirmDialog, type ConfirmRequest } from '../../components/ui/ConfirmDialog';
import { Modal } from '../../components/ui/Modal';
import { ApiError } from '../../lib/apiClient';
import type { LookupDto, ProductionLineDto } from '../master-data/api';
import type { PersonDto, RoleDto } from '../people/api';
import { shiftReportsApi, type ShiftReportDto } from './api';
import { formatDate, orDash, toField, toNumberOrNull } from './shiftFormat';
import { ShiftLineForm } from './ShiftLineForm';
import { ShiftStatusBadge } from './ShiftStatusBadge';

interface ShiftReportDialogProps {
  report: ShiftReportDto;
  allLines: ProductionLineDto[];
  people: PersonDto[];
  roles: RoleDto[];
  moulds: LookupDto[];
  onClose: () => void;
  onChanged: (report: ShiftReportDto) => void;
}

/**
 * A whole shift: who is answerable for it, and one tab per line that ran.
 *
 * A closed shift is shown in exactly the same layout with every box locked. Seeing
 * the fields greyed out says "this is finished" far more clearly than hiding them.
 */
export function ShiftReportDialog({
  report,
  allLines,
  people,
  roles,
  moulds,
  onClose,
  onChanged,
}: ShiftReportDialogProps): ReactElement {
  // A shift being corrected is not running, but its record is still open to
  // change — that is the whole point of reopening it.
  const locked = !report.canEdit;

  const [activeLineId, setActiveLineId] = useState(() => report.lines[0]?.id ?? 0);
  const [supervisorUserId, setSupervisorUserId] = useState(report.supervisorUserId);
  const [meterStart, setMeterStart] = useState(toField(report.electricityStartMeter));
  const [meterEnd, setMeterEnd] = useState(toField(report.electricityEndMeter));
  const [notes, setNotes] = useState(report.notes ?? '');
  const [error, setError] = useState<string | null>(null);
  const [isSaving, setIsSaving] = useState(false);
  const [savedDetails, setSavedDetails] = useState(false);
  const [confirm, setConfirm] = useState<ConfirmRequest | null>(null);

  const active = report.lines.find((line) => line.id === activeLineId) ?? report.lines[0];
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
        caught instanceof ApiError ? caught.message : 'Something went wrong. Try again.',
      );
    } finally {
      setIsSaving(false);
    }
  }

  return (
    <Modal
      title={`Shift ${report.shiftName} — ${formatDate(report.productionDate)}`}
      onClose={onClose}
    >
      <div className="mb-5 flex flex-wrap items-center gap-3 border-b border-line pb-4 text-sm text-ink-muted">
        <ShiftStatusBadge status={report.status} />
        <span>Opened by {report.openedByName}</span>
        {report.closedByName !== null && <span>· Closed by {report.closedByName}</span>}
      </div>

      {locked && (
        <p className="mb-5 rounded-control border border-line bg-canvas px-4 py-3 text-sm text-ink-soft">
          This shift is closed. An administrator can reopen it.
        </p>
      )}

      {/* The shift's own details — one supervisor, one meter reading and one set of
          notes for the day, not one per line. */}
      <form
        className="mb-5"
        onSubmit={(event) => {
          event.preventDefault();
          setSavedDetails(false);
          void run(async () => {
            const saved = await shiftReportsApi.update(report.id, {
              supervisorUserId,
              electricityStartMeter: toNumberOrNull(meterStart),
              electricityEndMeter: toNumberOrNull(meterEnd),
              notes: notes.trim() === '' ? null : notes.trim(),
            });
            setSavedDetails(true);
            return saved;
          });
        }}
        noValidate
      >
        <div className="mb-4">
          <label className="field-label" htmlFor="shift-supervisor">
            Supervisor
          </label>
          <select
            id="shift-supervisor"
            className="field-input"
            value={supervisorUserId ?? ''}
            disabled={locked || isSaving}
            onChange={(event) => {
              setSupervisorUserId(
                event.target.value === '' ? null : Number(event.target.value),
              );
            }}
          >
            <option value="">Not decided yet</option>
            {people.map((person) => (
              <option key={person.id} value={person.id}>
                {person.fullName} ({person.employeeNumber})
              </option>
            ))}
          </select>
        </div>

        {/* One meter for the whole factory, so it is read once for the shift —
            never per line, which would count the same meter two or three times. */}
        <div className="grid gap-4 sm:grid-cols-2">
          <div className="mb-4">
            <label className="field-label" htmlFor="shift-meter-start">
              Electricity meter at start
            </label>
            <input
              id="shift-meter-start"
              type="number"
              step="0.01"
              className="field-input"
              value={meterStart}
              disabled={locked || isSaving}
              onChange={(event) => {
                setMeterStart(event.target.value);
              }}
            />
          </div>
          <div className="mb-4">
            <label className="field-label" htmlFor="shift-meter-end">
              Electricity meter at end
            </label>
            <input
              id="shift-meter-end"
              type="number"
              step="0.01"
              className="field-input"
              value={meterEnd}
              disabled={locked || isSaving}
              onChange={(event) => {
                setMeterEnd(event.target.value);
              }}
            />
          </div>
        </div>

        <div className="mb-4 rounded-control bg-canvas px-4 py-3">
          <div className="flex items-baseline justify-between gap-3">
            <span className="text-sm font-medium text-ink-soft">Electricity used</span>
            <span className="text-lg font-bold text-ink">
              {orDash(report.electricityUsed)}
            </span>
          </div>
          <p className="mt-1 text-xs text-ink-muted">
            End meter minus start meter, for the whole factory.
          </p>
        </div>

        <div className="mb-4">
          <label className="field-label" htmlFor="shift-notes">
            Notes for the shift
          </label>
          <textarea
            id="shift-notes"
            rows={2}
            maxLength={1000}
            className="field-input"
            value={notes}
            disabled={locked || isSaving}
            onChange={(event) => {
              setNotes(event.target.value);
            }}
          />
        </div>

        {!locked && (
          <div className="flex items-center gap-3">
            <button type="submit" className="btn-primary w-auto px-6" disabled={isSaving}>
              {isSaving ? 'Saving…' : 'Save shift details'}
            </button>
            {savedDetails && error === null && !isSaving && (
              <span className="text-sm font-medium text-ok">Saved</span>
            )}
          </div>
        )}
      </form>

      {/* One tab per line that ran. */}
      <div className="mb-4 flex flex-wrap items-center gap-2 border-b border-line pb-3">
        {report.lines.map((line) => (
          <button
            key={line.id}
            type="button"
            onClick={() => {
              setActiveLineId(line.id);
            }}
            className={[
              'min-h-touch rounded-control border px-4 text-sm font-semibold transition-colors',
              line.id === active?.id
                ? 'border-brand-600 bg-brand-600 text-white'
                : 'border-line text-ink-soft hover:border-brand-200 hover:bg-brand-50',
            ].join(' ')}
          >
            {line.productionLineName}
            {line.workers.length > 0 && (
              <span className="ms-2 text-xs font-normal opacity-80">
                {line.workers.length}
              </span>
            )}
          </button>
        ))}

        {!locked &&
          missing.map((line) => (
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

      {error !== null && (
        <p
          role="alert"
          className="mb-4 rounded-control border border-s-4 border-bad/30 border-s-bad bg-bad-soft px-4 py-3 text-sm font-medium text-bad"
        >
          {error}
        </p>
      )}

      {active !== undefined && (
        <>
          {!locked && report.lines.length > 1 && active.workers.length === 0 && (
            <div className="mb-4 flex justify-end">
              <button
                type="button"
                className="min-h-9 rounded-control border border-line px-3 text-sm font-medium text-ink-muted transition-colors hover:border-bad/40 hover:bg-bad-soft hover:text-bad"
                onClick={() => {
                  setConfirm({
                    title: `Take ${active.productionLineName} off this shift?`,
                    message: (
                      <>
                        Nothing has been recorded for{' '}
                        <strong>{active.productionLineName}</strong>, so it can be
                        removed. Use this when the line turned out not to run.
                      </>
                    ),
                    confirmLabel: 'Remove line',
                    onConfirm: () => {
                      setActiveLineId(0);
                      void run(() => shiftReportsApi.removeLine(report.id, active.id));
                    },
                  });
                }}
              >
                Remove {active.productionLineName} from this shift
              </button>
            </div>
          )}

          {/* Keyed on the line, so switching tabs starts from that line's own
              saved values rather than carrying the last one's over. */}
          <ShiftLineForm
            key={active.id}
            reportId={report.id}
            line={active}
            people={people}
            roles={roles}
            moulds={moulds}
            locked={locked}
            onSaved={onChanged}
          />
        </>
      )}

      {locked && (
        <button
          type="button"
          className="mt-6 min-h-touch w-full rounded-control border border-line text-sm font-semibold text-ink-soft transition-colors hover:bg-canvas"
          onClick={onClose}
        >
          Close
        </button>
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

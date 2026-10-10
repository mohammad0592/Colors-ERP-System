import { useState, type ReactElement } from 'react';
import { useTranslation } from '../../hooks/useTranslation';
import { ApiError } from '../../lib/apiClient';
import type { PersonDto, RoleDto } from '../people/api';
import {
  shiftReportsApi,
  type SaveShiftWorker,
  type ShiftLineDto,
  type ShiftReportDto,
} from './api';
import { CrewEditor } from './CrewEditor';
import { orDash, toField, toNumberOrNull } from './shiftFormat';

interface ShiftLineFormProps {
  reportId: number;
  line: ShiftLineDto;
  people: PersonDto[];
  roles: RoleDto[];
  locked: boolean;
  onSaved: (report: ShiftReportDto) => void;
}

/**
 * One line's part of a shift — the screen version of the paper form headed "Daily
 * Production Report for the Forming Department". Shown on the Shift Configurations
 * screen, one tab per line.
 *
 * Each line saves on its own, so the extruder operator writing his hours cannot
 * overwrite what the thermo operator typed a minute earlier.
 */
export function ShiftLineForm({
  reportId,
  line,
  people,
  roles,
  locked,
  onSaved,
}: ShiftLineFormProps): ReactElement {
  const { t } = useTranslation();
  const [startTime, setStartTime] = useState(line.productionStartTime ?? '');
  const [endTime, setEndTime] = useState(line.productionEndTime ?? '');
  const [downtime, setDowntime] = useState(toField(line.downtimeHours));
  const [machineSpeed, setMachineSpeed] = useState(toField(line.machineSpeed));
  const [feedDistance, setFeedDistance] = useState(toField(line.feedDistanceMm));
  const [cycleTime, setCycleTime] = useState(toField(line.cycleTimeSeconds));
  const [notes, setNotes] = useState(line.notes ?? '');
  const [workers, setWorkers] = useState<SaveShiftWorker[]>(() =>
    line.workers.map((worker) => ({
      userId: worker.userId,
      roleInShiftIds: worker.roleInShiftIds,
      isTrainee: worker.isTrainee,
    })),
  );

  const [error, setError] = useState<string | null>(null);
  const [isSaving, setIsSaving] = useState(false);
  const [savedAt, setSavedAt] = useState<number | null>(null);

  async function save(): Promise<void> {
    setError(null);
    setIsSaving(true);
    try {
      const report = await shiftReportsApi.updateLine(reportId, line.id, {
        productionStartTime: startTime === '' ? null : startTime,
        productionEndTime: endTime === '' ? null : endTime,
        downtimeHours: toNumberOrNull(downtime),
        // Never sent for a line that has no such settings — the server refuses them
        // there, and an old value left in state must not sneak through.
        machineSpeed: line.recordsMachineSettings ? toNumberOrNull(machineSpeed) : null,
        feedDistanceMm: line.recordsMachineSettings ? toNumberOrNull(feedDistance) : null,
        cycleTimeSeconds: line.recordsMachineSettings ? toNumberOrNull(cycleTime) : null,
        workers,
        notes: notes.trim() === '' ? null : notes.trim(),
      });
      setSavedAt(Date.now());
      onSaved(report);
    } catch (caught) {
      setError(
        caught instanceof ApiError ? caught.message : t('common.somethingWentWrong'),
      );
    } finally {
      setIsSaving(false);
    }
  }

  const disabled = locked || isSaving;

  return (
    <form
      onSubmit={(event) => {
        event.preventDefault();
        void save();
      }}
      noValidate
    >
      <Section title={t('shifts.times')} />

      <div className="grid gap-4 sm:grid-cols-3">
        <Field label={t('shifts.productionStarted')} htmlFor={`start-${String(line.id)}`}>
          <input
            id={`start-${String(line.id)}`}
            type="time"
            className="field-input"
            value={startTime}
            disabled={disabled}
            onChange={(event) => {
              setStartTime(event.target.value);
            }}
          />
        </Field>
        <Field label={t('shifts.productionEnded')} htmlFor={`end-${String(line.id)}`}>
          <input
            id={`end-${String(line.id)}`}
            type="time"
            className="field-input"
            value={endTime}
            disabled={disabled}
            onChange={(event) => {
              setEndTime(event.target.value);
            }}
          />
        </Field>
        <Field label={t('shifts.downtimeHours')} htmlFor={`down-${String(line.id)}`}>
          <input
            id={`down-${String(line.id)}`}
            type="number"
            step="0.25"
            min="0"
            className="field-input"
            value={downtime}
            disabled={disabled}
            onChange={(event) => {
              setDowntime(event.target.value);
            }}
          />
        </Field>
      </div>

      <Calculated
        label={t('shifts.hoursProducing')}
        value={orDash(line.actualProductionHours, ' h')}
        hint="End minus start, less downtime. Worked out by the server, not stored."
      />

      {/* No electricity here: the factory has one meter for the whole building, so
          it is read once per shift, above the tabs. */}

      {/* Only the thermo line has forming settings. Elsewhere the section is left
          out altogether rather than shown empty. */}
      {line.recordsMachineSettings && (
        <>
          <Section title={t('shifts.machineSettings')} />

          <div className="grid gap-4 sm:grid-cols-3">
            <Field label={t('shifts.speed')} htmlFor={`speed-${String(line.id)}`}>
              <input
                id={`speed-${String(line.id)}`}
                type="number"
                className="field-input"
                value={machineSpeed}
                disabled={disabled}
                onChange={(event) => {
                  setMachineSpeed(event.target.value);
                }}
              />
            </Field>
            <Field label={t('shifts.feedDistance')} htmlFor={`feed-${String(line.id)}`}>
              <input
                id={`feed-${String(line.id)}`}
                type="number"
                className="field-input"
                value={feedDistance}
                disabled={disabled}
                onChange={(event) => {
                  setFeedDistance(event.target.value);
                }}
              />
            </Field>
            <Field label={t('shifts.cycleTime')} htmlFor={`cycle-${String(line.id)}`}>
              <input
                id={`cycle-${String(line.id)}`}
                type="number"
                step="0.1"
                className="field-input"
                value={cycleTime}
                disabled={disabled}
                onChange={(event) => {
                  setCycleTime(event.target.value);
                }}
              />
            </Field>
          </div>
        </>
      )}

      <Section title={t('term.crew')} />

      <div className="mb-4">
        <CrewEditor
          workers={workers}
          people={people}
          roles={roles}
          disabled={disabled}
          onChange={setWorkers}
        />
      </div>

      <Section title={t('shiftCfg.lineNotes')} />

      <div className="mb-4">
        <textarea
          aria-label={t('shiftCfg.lineNotes')}
          rows={3}
          maxLength={1000}
          className="field-input"
          value={notes}
          disabled={disabled}
          onChange={(event) => {
            setNotes(event.target.value);
          }}
        />
      </div>

      {error !== null && (
        <p
          role="alert"
          className="mb-4 rounded-control border border-s-4 border-bad/30 border-s-bad bg-bad-soft px-4 py-3 text-sm font-medium text-bad"
        >
          {error}
        </p>
      )}

      {!locked && (
        <div className="flex items-center gap-3">
          <button type="submit" className="btn-primary w-auto px-6" disabled={isSaving}>
            {isSaving ? 'Saving…' : `Save ${line.productionLineName}`}
          </button>
          {savedAt !== null && error === null && !isSaving && (
            <span className="text-sm font-medium text-ok">{t('state.saved')}</span>
          )}
        </div>
      )}
    </form>
  );
}

function Field({
  label,
  htmlFor,
  children,
}: {
  label: string;
  htmlFor: string;
  children: ReactElement;
}): ReactElement {
  return (
    <div className="mb-4">
      <label className="field-label" htmlFor={htmlFor}>
        {label}
      </label>
      {children}
    </div>
  );
}

function Section({ title }: { title: string }): ReactElement {
  return (
    <h3 className="mt-6 mb-4 border-b border-line pb-2 text-sm font-bold tracking-wider text-ink-muted uppercase first:mt-0">
      {title}
    </h3>
  );
}

/**
 * A figure the server worked out. Shown as text, never as a box, because typing into
 * it would let it disagree with the two readings it comes from.
 */
function Calculated({
  label,
  value,
  hint,
}: {
  label: string;
  value: string;
  hint: string;
}): ReactElement {
  return (
    <div className="mb-4 rounded-control bg-canvas px-4 py-3">
      <div className="flex items-baseline justify-between gap-3">
        <span className="text-sm font-medium text-ink-soft">{label}</span>
        <span className="text-lg font-bold text-ink">{value}</span>
      </div>
      <p className="mt-1 text-xs text-ink-muted">{hint}</p>
    </div>
  );
}

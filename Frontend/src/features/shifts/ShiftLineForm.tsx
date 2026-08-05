import { useState, type ReactElement } from 'react';
import { ApiError } from '../../lib/apiClient';
import type { LookupDto } from '../master-data/api';
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
  /** Empty for a line that takes no mould, so the picker is simply not shown. */
  moulds: LookupDto[];
  locked: boolean;
  onSaved: (report: ShiftReportDto) => void;
}

/**
 * One line's part of a shift — the screen version of the paper form headed "Daily
 * Production Report for the Forming Department".
 *
 * Each line saves on its own, so the extruder operator writing his hours cannot
 * overwrite what the thermo operator typed a minute earlier.
 */
export function ShiftLineForm({
  reportId,
  line,
  people,
  roles,
  moulds,
  locked,
  onSaved,
}: ShiftLineFormProps): ReactElement {
  const [mouldId, setMouldId] = useState<number | null>(line.mouldId);
  const [startTime, setStartTime] = useState(line.productionStartTime ?? '');
  const [endTime, setEndTime] = useState(line.productionEndTime ?? '');
  const [downtime, setDowntime] = useState(toField(line.downtimeHours));
  const [machineSpeed, setMachineSpeed] = useState(toField(line.machineSpeed));
  const [feedDistance, setFeedDistance] = useState(toField(line.feedDistanceMm));
  const [cycleTime, setCycleTime] = useState(toField(line.cycleTimeSeconds));
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
        // Only the forming line takes one; the server refuses it elsewhere.
        mouldId: line.recordsMachineSettings ? mouldId : null,
        productionStartTime: startTime === '' ? null : startTime,
        productionEndTime: endTime === '' ? null : endTime,
        downtimeHours: toNumberOrNull(downtime),
        // Never sent for a line that has no such settings — the server refuses them
        // there, and an old value left in state must not sneak through.
        machineSpeed: line.recordsMachineSettings ? toNumberOrNull(machineSpeed) : null,
        feedDistanceMm: line.recordsMachineSettings
          ? toNumberOrNull(feedDistance)
          : null,
        cycleTimeSeconds: line.recordsMachineSettings
          ? toNumberOrNull(cycleTime)
          : null,
        workers,
      });
      setSavedAt(Date.now());
      onSaved(report);
    } catch (caught) {
      setError(
        caught instanceof ApiError ? caught.message : 'Something went wrong. Try again.',
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
      {/* Only the forming line takes a mould, and it is chosen once for the shift —
          everything made on the line inherits it. */}
      {line.recordsMachineSettings && (
        <>
          <Section title="Template in the machine" />

          <Field label="Mould" htmlFor={`mould-${String(line.id)}`}>
            <select
              id={`mould-${String(line.id)}`}
              className="field-input"
              value={mouldId ?? ''}
              disabled={disabled}
              onChange={(event) => {
                setMouldId(
                  event.target.value === '' ? null : Number(event.target.value),
                );
              }}
            >
              <option value="">Not mounted yet</option>
              {moulds.map((mould) => (
                <option key={mould.id} value={mould.id}>
                  {mould.name}
                </option>
              ))}
            </select>
          </Field>

          <p className="-mt-2 mb-4 text-xs text-ink-muted">
            Changing a template is heavy work, so one is mounted for the whole shift.
            Everything formed on this line takes its product from it and from the
            roll&rsquo;s recipe — nobody types a product.
          </p>
        </>
      )}

      <Section title="Times" />

      <div className="grid gap-4 sm:grid-cols-3">
        <Field label="Production started" htmlFor={`start-${String(line.id)}`}>
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
        <Field label="Production ended" htmlFor={`end-${String(line.id)}`}>
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
        <Field label="Downtime (hours)" htmlFor={`down-${String(line.id)}`}>
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
        label="Hours actually producing"
        value={orDash(line.actualProductionHours, ' h')}
        hint="End minus start, less downtime. Worked out by the server, not stored."
      />

      {/* No electricity here: the factory has one meter for the whole building, so
          it is read once per shift, above the lines. */}

      {/* Only the thermo line has forming settings. Elsewhere the section is left
          out altogether rather than shown empty. */}
      {line.recordsMachineSettings && (
        <>
          <Section title="Machine settings" />

          <div className="grid gap-4 sm:grid-cols-3">
            <Field label="Speed (cycles/hour)" htmlFor={`speed-${String(line.id)}`}>
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
            <Field label="Feed distance (mm)" htmlFor={`feed-${String(line.id)}`}>
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
            <Field label="Cycle time (seconds)" htmlFor={`cycle-${String(line.id)}`}>
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

      <Section title="Crew" />

      <div className="mb-4">
        <CrewEditor
          workers={workers}
          people={people}
          roles={roles}
          disabled={disabled}
          onChange={setWorkers}
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

      {!locked && (
        <div className="flex items-center gap-3">
          <button type="submit" className="btn-primary w-auto px-6" disabled={isSaving}>
            {isSaving ? 'Saving…' : `Save ${line.productionLineName}`}
          </button>
          {savedAt !== null && error === null && !isSaving && (
            <span className="text-sm font-medium text-ok">Saved</span>
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

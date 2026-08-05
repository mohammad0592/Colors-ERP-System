import { useState, type ReactElement } from 'react';
import type { PersonDto, RoleDto } from '../people/api';
import type { SaveShiftWorker } from './api';

interface CrewEditorProps {
  workers: SaveShiftWorker[];
  people: PersonDto[];
  roles: RoleDto[];
  disabled: boolean;
  onChange: (workers: SaveShiftWorker[]) => void;
}

/**
 * Who worked the shift.
 *
 * The role is asked for separately from the person's own roles on purpose: the same
 * man is both extruder operator and extruder test person, so what he *did* on this
 * shift is a fact about the shift, not about him.
 */
export function CrewEditor({
  workers,
  people,
  roles,
  disabled,
  onChange,
}: CrewEditorProps): ReactElement {
  const [toAdd, setToAdd] = useState<string>('');

  const byId = new Map(people.map((person) => [person.id, person]));
  const alreadyOn = new Set(workers.map((worker) => worker.userId));
  const available = people.filter((person) => !alreadyOn.has(person.id));

  function replace(index: number, changed: Partial<SaveShiftWorker>): void {
    onChange(
      workers.map((worker, i) => (i === index ? { ...worker, ...changed } : worker)),
    );
  }

  return (
    <div>
      <p className="field-label">Workers on this shift</p>

      {workers.length === 0 && (
        <p className="mb-3 text-sm text-ink-muted">Nobody added yet.</p>
      )}

      <div className="mb-3 space-y-2">
        {workers.map((worker, index) => {
          const person = byId.get(worker.userId);
          return (
            <div
              key={worker.userId}
              className="flex flex-wrap items-center gap-2 rounded-control border border-line px-3 py-2"
            >
              <span className="min-w-40 flex-1 text-sm font-medium text-ink">
                {person?.fullName ?? `User ${String(worker.userId)}`}
                <span className="ml-2 text-xs font-normal text-ink-muted">
                  {person?.employeeNumber}
                </span>
              </span>

              <select
                aria-label="Job on this shift"
                className="field-input h-9 w-auto min-w-44 py-0 text-sm"
                value={worker.roleInShiftId ?? ''}
                disabled={disabled}
                onChange={(event) => {
                  replace(index, {
                    roleInShiftId:
                      event.target.value === '' ? null : Number(event.target.value),
                  });
                }}
              >
                <option value="">Job not recorded</option>
                {roles.map((role) => (
                  <option key={role.id} value={role.id}>
                    {role.name}
                  </option>
                ))}
              </select>

              <label className="flex items-center gap-2 text-sm text-ink-soft">
                <input
                  type="checkbox"
                  className="size-4"
                  checked={worker.isTrainee}
                  disabled={disabled}
                  onChange={(event) => {
                    replace(index, { isTrainee: event.target.checked });
                  }}
                />
                Trainee
              </label>

              {!disabled && (
                <button
                  type="button"
                  className="min-h-9 rounded-control border border-line px-3 text-sm font-medium text-ink-muted transition-colors hover:border-bad/40 hover:bg-bad-soft hover:text-bad"
                  onClick={() => {
                    onChange(workers.filter((_, i) => i !== index));
                  }}
                >
                  Remove
                </button>
              )}
            </div>
          );
        })}
      </div>

      {!disabled && (
        <div className="flex flex-wrap gap-2">
          <select
            aria-label="Add a worker"
            className="field-input h-touch w-auto min-w-56 py-0"
            value={toAdd}
            onChange={(event) => {
              setToAdd(event.target.value);
            }}
          >
            <option value="">Add a worker…</option>
            {available.map((person) => (
              <option key={person.id} value={person.id}>
                {person.fullName} ({person.employeeNumber})
              </option>
            ))}
          </select>
          <button
            type="button"
            className="min-h-touch rounded-control border border-line px-4 text-sm font-semibold text-ink-soft transition-colors hover:border-brand-200 hover:bg-brand-50 hover:text-brand-700 disabled:opacity-50"
            disabled={toAdd === ''}
            onClick={() => {
              onChange([
                ...workers,
                { userId: Number(toAdd), roleInShiftId: null, isTrainee: false },
              ]);
              setToAdd('');
            }}
          >
            Add
          </button>
        </div>
      )}
    </div>
  );
}

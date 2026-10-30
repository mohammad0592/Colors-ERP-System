import { useQuery } from '@tanstack/react-query';
import { useState, type ReactElement } from 'react';
import { useTranslation } from '../../hooks/useTranslation';
import type { PersonDto } from '../people/api';
import { staffApi } from '../staff/api';
import type { SaveShiftWorker } from './api';

interface StaffEditorProps {
  workers: SaveShiftWorker[];
  /** The system's users. */
  people: PersonDto[];
  disabled: boolean;
  onChange: (workers: SaveShiftWorker[]) => void;
}

/** One person on the list, whichever kind: "u12" a system user, "s5" staff only. */
function keyOf(worker: Pick<SaveShiftWorker, 'userId' | 'staffMemberId'>): string {
  return worker.userId === null
    ? `s${String(worker.staffMemberId)}`
    : `u${String(worker.userId)}`;
}

/**
 * Who worked the line this shift — its staff and workers (specification section 19.19).
 *
 * Two kinds of people, picked from one list: **staff**, the system's users with an
 * account, and **workers**, who have no account and are only recorded. Only chosen here —
 * adding somebody to the factory's people, either kind, is the administrator's, on the
 * Users screen.
 *
 * Each person is recorded with whether he was a trainee, and nothing more: the jobs he
 * did are no longer asked (section 19.20). Those recorded on earlier shifts are kept as
 * they were — a person loaded with jobs is saved back with the same ones.
 */
export function StaffEditor({
  workers,
  people,
  disabled,
  onChange,
}: StaffEditorProps): ReactElement {
  const { t } = useTranslation();
  const [toAdd, setToAdd] = useState<string>('');

  // Everyone, those who have left too, so a past shift still names them.
  const staff = useQuery({
    queryKey: ['staff', 'all'],
    queryFn: () => staffApi.list(true),
  });

  const users = new Map(people.map((person) => [person.id, person]));
  const staffById = new Map((staff.data ?? []).map((member) => [member.id, member]));
  const alreadyOn = new Set(workers.map(keyOf));
  const availableUsers = people.filter(
    (person) => !alreadyOn.has(`u${String(person.id)}`),
  );
  const availableStaff = (staff.data ?? []).filter(
    (member) => member.isActive && !alreadyOn.has(`s${String(member.id)}`),
  );

  function replace(index: number, changed: Partial<SaveShiftWorker>): void {
    onChange(
      workers.map((worker, i) => (i === index ? { ...worker, ...changed } : worker)),
    );
  }

  function add(choice: string): void {
    const id = Number(choice.slice(1));
    onChange([
      ...workers,
      choice.startsWith('u')
        ? { userId: id, staffMemberId: null, roleInShiftIds: [], isTrainee: false }
        : { userId: null, staffMemberId: id, roleInShiftIds: [], isTrainee: false },
    ]);
  }

  return (
    <div>
      <p className="field-label">{t('shifts.crewTitle')}</p>

      {workers.length === 0 && (
        <p className="mb-3 text-sm text-ink-muted">{t('shifts.nobodyYet')}</p>
      )}

      <div className="mb-3 space-y-2">
        {workers.map((worker, index) => {
          const user = worker.userId === null ? undefined : users.get(worker.userId);
          const member =
            worker.staffMemberId === null
              ? undefined
              : staffById.get(worker.staffMemberId);
          const name = user?.fullName ?? member?.fullName ?? '…';
          const number = user?.employeeNumber ?? member?.employeeNumber ?? '';

          return (
            <div
              key={keyOf(worker)}
              className="rounded-control border border-line px-3 py-2"
            >
              <div className="flex flex-wrap items-center gap-2">
                <span className="min-w-40 flex-1 text-sm font-medium text-ink">
                  {name}
                  <span className="ms-2 text-xs font-normal text-ink-muted">
                    {number}
                  </span>
                  {/* Somebody with no account is only recorded, and says so. */}
                  {worker.staffMemberId !== null && (
                    <span className="ms-2 rounded-full bg-canvas px-2 py-0.5 text-xs font-semibold text-ink-muted">
                      {t('staff.noAccount')}
                    </span>
                  )}
                </span>

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
                  {t('shifts.trainee')}
                </label>

                {!disabled && (
                  <button
                    type="button"
                    className="min-h-9 rounded-control border border-line px-3 text-sm font-medium text-ink-muted transition-colors hover:border-bad/40 hover:bg-bad-soft hover:text-bad"
                    onClick={() => {
                      onChange(workers.filter((_, i) => i !== index));
                    }}
                  >
                    {t('shifts.remove')}
                  </button>
                )}
              </div>
            </div>
          );
        })}
      </div>

      {!disabled && (
        <>
          <div className="flex flex-wrap gap-2">
            <select
              aria-label={t('shifts.addWorkerEllipsis')}
              className="field-input h-touch w-auto min-w-56 py-0"
              value={toAdd}
              onChange={(event) => {
                setToAdd(event.target.value);
              }}
            >
              <option value="">{t('shifts.addWorkerEllipsis')}</option>
              <optgroup label={t('staff.systemUsers')}>
                {availableUsers.map((person) => (
                  <option key={`u${String(person.id)}`} value={`u${String(person.id)}`}>
                    {person.fullName} ({person.employeeNumber})
                  </option>
                ))}
              </optgroup>
              <optgroup label={t('staff.withoutAccount')}>
                {availableStaff.map((member) => (
                  <option key={`s${String(member.id)}`} value={`s${String(member.id)}`}>
                    {member.fullName}
                    {member.employeeNumber === null ? '' : ` (${member.employeeNumber})`}
                  </option>
                ))}
              </optgroup>
            </select>
            <button
              type="button"
              className="min-h-touch rounded-control border border-line px-4 text-sm font-semibold text-ink-soft transition-colors hover:border-brand-200 hover:bg-brand-50 hover:text-brand-700 disabled:opacity-50"
              disabled={toAdd === ''}
              onClick={() => {
                add(toAdd);
                setToAdd('');
              }}
            >
              {t('shifts.add')}
            </button>
          </div>

          {/* Nobody is added to the factory's people here: that is the administrator's,
              on the Users screen (section 19.19). This list only chooses from them. */}
          <p className="mt-1.5 text-xs text-ink-muted">{t('staff.addedByAdmin')}</p>
        </>
      )}
    </div>
  );
}

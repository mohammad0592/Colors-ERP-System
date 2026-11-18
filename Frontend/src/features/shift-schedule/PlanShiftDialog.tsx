import { useState, type ReactElement } from 'react';
import { Modal } from '../../components/ui/Modal';
import { useTranslation } from '../../hooks/useTranslation';
import { ApiError } from '../../lib/apiClient';
import type { PersonDto } from '../people/api';
import { formatDate } from '../shifts/shiftFormat';
import {
  shiftScheduleApi,
  type ScheduledShiftDto,
  type ScheduleLineOptionDto,
} from './api';

interface PlanShiftDialogProps {
  /** "yyyy-MM-dd" — always tomorrow for a new plan. */
  productionDate: string;
  shift: { id: number; name: string; startTime: string; endTime: string };
  lines: ScheduleLineOptionDto[];
  people: PersonDto[];
  /** The plan being changed, or null for a new one. */
  existing: ScheduledShiftDto | null;
  onClose: () => void;
  onSaved: () => void;
}

/**
 * Plans one of tomorrow's shifts: who supervises, which lines run, and who operates
 * each (specification section 19.30). The workers on each line are not planned — they
 * are added on the day, the same as on a shift opened by hand.
 *
 * Each line's operator is picked from the people holding that line's job only. The
 * server refuses anyone else anyway; a list without them never offers the mistake.
 */
export function PlanShiftDialog({
  productionDate,
  shift,
  lines,
  people,
  existing,
  onClose,
  onSaved,
}: PlanShiftDialogProps): ReactElement {
  const { t } = useTranslation();

  const [supervisorUserId, setSupervisorUserId] = useState<number | null>(
    existing?.supervisorUserId ?? null,
  );
  // A new plan starts with every line ticked, as opening a shift by hand does: most
  // days everything runs.
  const [operators, setOperators] = useState<Map<number, number | null>>(
    () =>
      new Map(
        existing === null
          ? lines.map((line) => [line.id, null])
          : existing.lines.map((line) => [line.productionLineId, line.operatorUserId]),
      ),
  );
  const [startingShift, setStartingShift] = useState(
    existing?.extruderStartingShift ?? false,
  );
  const [error, setError] = useState<string | null>(null);
  const [isSaving, setIsSaving] = useState(false);

  const extruderChosen = lines.some((line) => line.makesRolls && operators.has(line.id));

  // Anyone may be named as the supervisor, but the supervisors come first because that
  // is who it usually is.
  const supervisors = [...people].sort((a, b) => {
    const rank = (person: PersonDto): number =>
      person.roles.includes('Supervisor') ? 0 : 1;
    return rank(a) - rank(b) || a.fullName.localeCompare(b.fullName);
  });

  function setLine(lineId: number, on: boolean): void {
    setOperators((current) => {
      const next = new Map(current);
      if (on) {
        next.set(lineId, null);
      } else {
        next.delete(lineId);
      }
      return next;
    });
  }

  function setOperator(lineId: number, userId: number | null): void {
    setOperators((current) => new Map(current).set(lineId, userId));
  }

  async function save(): Promise<void> {
    setError(null);
    setIsSaving(true);

    const request = {
      supervisorUserId,
      lines: [...operators].map(([productionLineId, operatorUserId]) => ({
        productionLineId,
        operatorUserId,
      })),
      extruderStartingShift: startingShift && extruderChosen,
    };

    try {
      if (existing === null) {
        await shiftScheduleApi.schedule({
          ...request,
          productionDate,
          shiftId: shift.id,
        });
      } else {
        await shiftScheduleApi.update(existing.id, request);
      }
      onSaved();
      onClose();
    } catch (caught) {
      setError(
        caught instanceof ApiError ? caught.message : t('common.somethingWentWrong'),
      );
    } finally {
      setIsSaving(false);
    }
  }

  return (
    <Modal
      title={`${t('schedule.planShift')} ${shift.name} · ${formatDate(productionDate)}`}
      onClose={onClose}
    >
      <form
        onSubmit={(event) => {
          event.preventDefault();
          void save();
        }}
        noValidate
      >
        <p className="mb-4 text-sm text-ink-muted">
          <span dir="ltr">
            {shift.startTime}–{shift.endTime}
          </span>{' '}
          {t('schedule.opensItself')}
        </p>

        <div className="mb-4">
          <label className="field-label" htmlFor="plan-supervisor">
            {t('term.supervisor')}
          </label>
          <select
            id="plan-supervisor"
            className="field-input"
            value={supervisorUserId ?? ''}
            disabled={isSaving}
            onChange={(event) => {
              setSupervisorUserId(
                event.target.value === '' ? null : Number(event.target.value),
              );
            }}
          >
            <option value="">{t('state.notDecidedYet')}</option>
            {supervisors.map((person) => (
              <option key={person.id} value={person.id}>
                {person.fullName} ({person.employeeNumber})
              </option>
            ))}
          </select>
        </div>

        <fieldset className="mb-4">
          <legend className="field-label">{t('schedule.whichLines')}</legend>
          <div className="space-y-2">
            {lines.map((line) => {
              const on = operators.has(line.id);
              const candidates = people
                .filter(
                  (person) =>
                    line.operatorRole === null ||
                    person.roles.includes(line.operatorRole),
                )
                .sort((a, b) => a.fullName.localeCompare(b.fullName));

              return (
                <div key={line.id} className="rounded-control border border-line">
                  <label className="flex min-h-touch items-center gap-3 px-3 text-sm font-medium text-ink">
                    <input
                      type="checkbox"
                      className="size-5"
                      checked={on}
                      disabled={isSaving}
                      onChange={(event) => {
                        setLine(line.id, event.target.checked);
                      }}
                    />
                    {line.name}
                  </label>

                  {on && (
                    <div className="border-t border-line bg-canvas px-3 py-2.5">
                      <label
                        className="mb-1 block text-xs font-medium text-ink-muted"
                        htmlFor={`plan-operator-${String(line.id)}`}
                      >
                        {t('schedule.operator')}
                      </label>
                      <select
                        id={`plan-operator-${String(line.id)}`}
                        className="field-input"
                        value={operators.get(line.id) ?? ''}
                        disabled={isSaving}
                        onChange={(event) => {
                          setOperator(
                            line.id,
                            event.target.value === '' ? null : Number(event.target.value),
                          );
                        }}
                      >
                        <option value="">{t('schedule.nameOnTheDay')}</option>
                        {candidates.map((person) => (
                          <option key={person.id} value={person.id}>
                            {person.fullName} ({person.employeeNumber})
                          </option>
                        ))}
                      </select>

                      {line.makesRolls && (
                        <label className="mt-2.5 flex items-start gap-3 text-sm text-ink">
                          <input
                            type="checkbox"
                            className="mt-0.5 size-5 shrink-0"
                            checked={startingShift}
                            disabled={isSaving}
                            onChange={(event) => {
                              setStartingShift(event.target.checked);
                            }}
                          />
                          <span>
                            <span className="font-medium">
                              {t('shifts.startingShift')}
                            </span>
                            <span className="mt-0.5 block text-xs text-ink-muted">
                              {t('shifts.startingShiftNote')}
                            </span>
                          </span>
                        </label>
                      )}
                    </div>
                  )}
                </div>
              );
            })}
          </div>
          <p className="mt-1 text-xs text-ink-muted">{t('schedule.crewOnTheDay')}</p>
        </fieldset>

        {error !== null && (
          <p
            role="alert"
            className="mb-4 rounded-control border border-s-4 border-bad/30 border-s-bad bg-bad-soft px-4 py-3 text-sm font-medium text-bad"
          >
            {error}
          </p>
        )}

        <button
          type="submit"
          className="btn-primary"
          disabled={isSaving || operators.size === 0}
        >
          {isSaving
            ? t('common.saving')
            : existing === null
              ? t('schedule.savePlan')
              : t('schedule.saveChanges')}
        </button>
      </form>
    </Modal>
  );
}

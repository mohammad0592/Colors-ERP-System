import { useState, type ReactElement } from 'react';
import { useTranslation } from '../../hooks/useTranslation';
import { Modal } from '../../components/ui/Modal';
import { ApiError } from '../../lib/apiClient';
import type { ProductionLineDto, ShiftDto } from '../master-data/api';
import type { PersonDto } from '../people/api';
import { shiftReportsApi, type ShiftReportDto } from './api';
import { todayIso } from './shiftFormat';

interface OpenShiftDialogProps {
  lines: ProductionLineDto[];
  shifts: ShiftDto[];
  people: PersonDto[];
  onClose: () => void;
  onOpened: (report: ShiftReportDto) => void;
}

/**
 * Starts a shift.
 *
 * One shift for the whole factory, with the lines that are running ticked. That is how
 * the floor talks about it — "shift A on 4 August" — and it means the electricity is
 * asked for once, not once per line. A line that starts late can be added afterwards.
 */
export function OpenShiftDialog({
  lines,
  shifts,
  people,
  onClose,
  onOpened,
}: OpenShiftDialogProps): ReactElement {
  const { t } = useTranslation();
  const [shiftId, setShiftId] = useState(() => shifts[0]?.id ?? 0);
  const [productionDate, setProductionDate] = useState(todayIso);
  const [supervisorUserId, setSupervisorUserId] = useState<number | null>(null);
  // Most days everything runs, so everything starts ticked.
  const [lineIds, setLineIds] = useState<number[]>(() => lines.map((line) => line.id));
  const [error, setError] = useState<string | null>(null);
  const [isSaving, setIsSaving] = useState(false);

  // Anyone may be named as the supervisor of a shift, but the supervisors come first
  // because that is who it usually is.
  const supervisors = [...people].sort((a, b) => {
    const rank = (person: PersonDto): number =>
      person.roles.includes('Supervisor') ? 0 : 1;
    return rank(a) - rank(b) || a.fullName.localeCompare(b.fullName);
  });

  async function save(): Promise<void> {
    setError(null);
    setIsSaving(true);
    try {
      const report = await shiftReportsApi.open({
        productionDate,
        shiftId,
        supervisorUserId,
        productionLineIds: lineIds,
      });
      onOpened(report);
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
    <Modal title={t('action.openShift')} onClose={onClose}>
      <form
        onSubmit={(event) => {
          event.preventDefault();
          void save();
        }}
        noValidate
      >
        <div className="mb-4">
          <label className="field-label" htmlFor="open-date">
            {t('shifts.productionDate')}
          </label>
          <input
            id="open-date"
            type="date"
            className="field-input"
            value={productionDate}
            disabled={isSaving}
            onChange={(event) => {
              setProductionDate(event.target.value);
            }}
          />
          <p className="mt-1 text-xs text-ink-muted">
            {t('shifts.dateNote')}
          </p>
        </div>

        <div className="mb-4">
          <label className="field-label" htmlFor="open-shift">
            {t('term.shift')}
          </label>
          <select
            id="open-shift"
            className="field-input"
            value={shiftId}
            disabled={isSaving}
            onChange={(event) => {
              setShiftId(Number(event.target.value));
            }}
          >
            {shifts.map((shift) => (
              <option key={shift.id} value={shift.id}>
                {shift.name} ({shift.startTime}–{shift.endTime})
              </option>
            ))}
          </select>
        </div>

        <fieldset className="mb-4">
          <legend className="field-label">{t('shifts.whichLines')}</legend>
          <div className="space-y-2">
            {lines.map((line) => (
              <label
                key={line.id}
                className="flex min-h-touch items-center gap-3 rounded-control border border-line px-3 text-sm font-medium text-ink"
              >
                <input
                  type="checkbox"
                  className="size-5"
                  checked={lineIds.includes(line.id)}
                  disabled={isSaving}
                  onChange={(event) => {
                    setLineIds((current) =>
                      event.target.checked
                        ? [...current, line.id]
                        : current.filter((id) => id !== line.id),
                    );
                  }}
                />
                {line.name}
              </label>
            ))}
          </div>
          <p className="mt-1 text-xs text-ink-muted">
            {t('shifts.lineLater')}
          </p>
        </fieldset>

        <div className="mb-4">
          <label className="field-label" htmlFor="open-supervisor">
            {t('term.supervisor')}
          </label>
          <select
            id="open-supervisor"
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
          disabled={isSaving || lineIds.length === 0}
        >
          {isSaving ? 'Opening…' : t('shifts.openShiftBtn')}
        </button>
      </form>
    </Modal>
  );
}

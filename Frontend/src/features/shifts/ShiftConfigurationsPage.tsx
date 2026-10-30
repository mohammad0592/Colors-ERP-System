import { useQuery, useQueryClient } from '@tanstack/react-query';
import { useState, type ReactElement } from 'react';
import { Navigate, useSearchParams } from 'react-router-dom';
import { PageHeader } from '../../components/ui/PageHeader';
import { useAuth } from '../../hooks/useAuth';
import { useTranslation } from '../../hooks/useTranslation';
import { ApiError } from '../../lib/apiClient';
import { RoleNames } from '../../lib/roles';
import { rolesFor } from '../../routes/access';
import { peopleApi } from '../people/api';
import { shiftReportsApi, type ShiftReportDto } from './api';
import { shiftConfigLines as kinds, type ShiftConfigKind } from './shiftConfigLines';
import { ShiftLineForm } from './ShiftLineForm';
import { formatDate, orDash, toField, toNumberOrNull } from './shiftFormat';

/**
 * The address the three screens used to share. A bookmark to it lands on the first of
 * the three this man may open — for an operator, his own line's — with the shift it
 * named, if any.
 */
export function ShiftConfigurationRedirect(): ReactElement {
  const { hasRole } = useAuth();
  const [params] = useSearchParams();
  const allowed = kinds.find((kind) => {
    const roles = rolesFor(kind.path);
    return roles === undefined || hasRole(...roles);
  });
  const query = params.toString();

  return (
    <Navigate
      to={
        allowed === undefined ? '/' : `${allowed.path}${query === '' ? '' : `?${query}`}`
      }
      replace
    />
  );
}

/**
 * What one line records for the shift — its times, its crew, its notes, and on the
 * thermo the machine settings (specification section 2).
 *
 * One screen per line, each in its line's part of the menu and opened by that line's
 * operator (section 19.18). The meter is the whole factory's, so it heads all three and
 * every operator sees the same reading and who entered it — polled, so a reading entered
 * on another line's screen shows up here too.
 *
 * This line only — nothing of the other two, for the administrator as for anyone. The
 * open shift by default; a closed or corrected shift is reached from the Shifts screen,
 * whose shift window links to each of its lines' screens with the shift's id.
 */
export function ShiftConfigurationsPage({
  kind: active,
}: {
  kind: ShiftConfigKind;
}): ReactElement {
  const { t } = useTranslation();
  const { user, hasRole } = useAuth();
  const queryClient = useQueryClient();
  const [params] = useSearchParams();
  const requested = Number(params.get('shift'));
  const isManager = hasRole(RoleNames.Administrator, RoleNames.Supervisor);

  const open = useQuery({
    queryKey: ['shift-reports', 'open'],
    queryFn: () => shiftReportsApi.list(undefined, true),
    enabled: !(requested > 0),
  });
  const shiftId = requested > 0 ? requested : open.data?.[0]?.id;

  // Polled, so a meter reading another operator enters shows up here without anybody
  // reloading the page.
  const report = useQuery({
    queryKey: ['shift-report', shiftId],
    queryFn: () => shiftReportsApi.get(shiftId ?? 0),
    enabled: shiftId !== undefined,
    refetchInterval: 30_000,
  });

  const people = useQuery({ queryKey: ['people'], queryFn: () => peopleApi.list(false) });
  const roles = useQuery({ queryKey: ['roles'], queryFn: () => peopleApi.roles() });

  const kind = kinds.find((k) => k.key === active) ?? kinds[0];

  function saved(fresh: ShiftReportDto): void {
    queryClient.setQueryData(['shift-report', fresh.id], fresh);
    void queryClient.invalidateQueries({ queryKey: ['shift-reports'] });
  }

  if (open.isError || report.isError || people.isError || roles.isError) {
    return <p className="p-6 text-bad">{t('shifts.loadScreenFailed')}</p>;
  }

  if (open.isPending && !(requested > 0)) {
    return <p className="p-6 text-ink-muted">{t('common.loading')}</p>;
  }

  if (shiftId === undefined) {
    return (
      <>
        <PageHeader title={t(kind?.label ?? 'shiftCfg.extruder')} />
        <p className="rounded-control border border-line bg-canvas px-4 py-3 text-sm text-ink-soft">
          {t('shiftCfg.noOpenShift')}
        </p>
      </>
    );
  }

  if (report.isPending || people.isPending || roles.isPending) {
    return <p className="p-6 text-ink-muted">{t('common.loading')}</p>;
  }

  const shift = report.data;
  const line = shift.lines.find((l) => kind?.matches(l) === true);
  const canEditLine =
    line !== undefined && (isManager || line.operatorUserId === user?.id);
  const onShift = isManager || shift.lines.some((l) => l.operatorUserId === user?.id);

  return (
    <>
      <PageHeader
        title={t(kind?.label ?? 'shiftCfg.extruder')}
        subtitle={`${t('term.shift')} ${shift.shiftName} — ${formatDate(shift.productionDate)}`}
      />

      {!shift.canEdit && (
        <p className="mb-5 rounded-control border border-line bg-canvas px-4 py-3 text-sm text-ink-soft">
          {t('shifts.closedNote')}
        </p>
      )}

      <ElectricityPanel
        key={`${String(shift.id)}-${String(shift.electricityStartMeter)}-${String(shift.electricityEndMeter)}`}
        shift={shift}
        canEnter={shift.canEdit && onShift}
        onSaved={saved}
      />

      {line === undefined ? (
        <p className="rounded-control border border-line bg-canvas px-4 py-3 text-sm text-ink-soft">
          {t('shiftCfg.lineNotRunning')}
        </p>
      ) : (
        <section className="card p-5">
          <div className="mb-5 flex flex-wrap items-baseline justify-between gap-3 border-b border-line pb-4">
            <h2 className="text-lg font-bold text-ink">{line.productionLineName}</h2>
            <p className="text-sm text-ink-soft">
              {t('shiftCfg.operator')}:{' '}
              {line.operatorName === null ? (
                <span className="text-ink-muted">{t('shiftCfg.noOperator')}</span>
              ) : (
                <strong className="text-ink">{line.operatorName}</strong>
              )}
            </p>
          </div>

          {shift.canEdit && !canEditLine && (
            <p className="mb-5 rounded-control border border-line bg-canvas px-4 py-3 text-sm text-ink-soft">
              {t('shiftCfg.readOnly')}
            </p>
          )}

          {/* Keyed on the line only: the page refreshes every half minute, and a form
              rebuilt from the server mid-typing would throw away what was typed. */}
          <ShiftLineForm
            key={line.id}
            reportId={shift.id}
            line={line}
            people={people.data}
            roles={roles.data}
            locked={!shift.canEdit || !canEditLine}
            onSaved={saved}
          />
        </section>
      )}
    </>
  );
}

/**
 * The factory's one meter. Any of the shift's operators may enter or correct it, and
 * every screen shows who entered each reading last, so it is not read three times.
 */
function ElectricityPanel({
  shift,
  canEnter,
  onSaved,
}: {
  shift: ShiftReportDto;
  canEnter: boolean;
  onSaved: (fresh: ShiftReportDto) => void;
}): ReactElement {
  const { t } = useTranslation();
  const [start, setStart] = useState(toField(shift.electricityStartMeter));
  const [end, setEnd] = useState(toField(shift.electricityEndMeter));
  const [error, setError] = useState<string | null>(null);
  const [isSaving, setIsSaving] = useState(false);

  async function save(): Promise<void> {
    setError(null);
    setIsSaving(true);
    try {
      onSaved(
        await shiftReportsApi.recordElectricity(shift.id, {
          electricityStartMeter: toNumberOrNull(start),
          electricityEndMeter: toNumberOrNull(end),
        }),
      );
    } catch (caught) {
      setError(
        caught instanceof ApiError ? caught.message : t('common.somethingWentWrong'),
      );
    } finally {
      setIsSaving(false);
    }
  }

  return (
    <form
      className="card mb-6 p-5"
      onSubmit={(event) => {
        event.preventDefault();
        void save();
      }}
      noValidate
    >
      <h2 className="mb-4 text-sm font-bold tracking-wider text-ink-muted uppercase">
        {t('shiftCfg.electricityTitle')}
      </h2>

      <div className="grid gap-4 sm:grid-cols-3">
        <Reading
          id="meter-start"
          label={t('shifts.meterStart')}
          value={start}
          onChange={setStart}
          disabled={!canEnter || isSaving}
          by={shift.electricityStartRecordedBy}
          at={shift.electricityStartRecordedAt}
        />
        <Reading
          id="meter-end"
          label={t('shifts.meterEnd')}
          value={end}
          onChange={setEnd}
          disabled={!canEnter || isSaving}
          by={shift.electricityEndRecordedBy}
          at={shift.electricityEndRecordedAt}
        />
        <div className="mb-4 rounded-control bg-canvas px-4 py-3">
          <p className="text-sm font-medium text-ink-soft">
            {t('shifts.electricityUsed')}
          </p>
          <p className="text-lg font-bold text-ink">{orDash(shift.electricityUsed)}</p>
        </div>
      </div>

      {error !== null && (
        <p
          role="alert"
          className="mb-4 rounded-control border border-s-4 border-bad/30 border-s-bad bg-bad-soft px-4 py-3 text-sm font-medium text-bad"
        >
          {error}
        </p>
      )}

      {canEnter ? (
        <button type="submit" className="btn-primary w-auto px-6" disabled={isSaving}>
          {isSaving ? t('common.saving') : t('shiftCfg.saveElectricity')}
        </button>
      ) : (
        <p className="text-xs text-ink-muted">{t('shiftCfg.electricityReadOnly')}</p>
      )}
    </form>
  );
}

function Reading({
  id,
  label,
  value,
  onChange,
  disabled,
  by,
  at,
}: {
  id: string;
  label: string;
  value: string;
  onChange: (value: string) => void;
  disabled: boolean;
  by: string | null;
  at: string | null;
}): ReactElement {
  const { t } = useTranslation();

  return (
    <div className="mb-4">
      <label className="field-label" htmlFor={id}>
        {label}
      </label>
      <input
        id={id}
        type="number"
        step="0.01"
        className="field-input"
        value={value}
        disabled={disabled}
        onChange={(event) => {
          onChange(event.target.value);
        }}
      />
      <p className="mt-1 text-xs text-ink-muted">
        {by === null || at === null ? (
          t('shiftCfg.notEntered')
        ) : (
          <>
            {t('shiftCfg.enteredBy')} <strong className="text-ink-soft">{by}</strong> ·{' '}
            {new Date(at).toLocaleTimeString('en-GB', {
              hour: '2-digit',
              minute: '2-digit',
            })}
          </>
        )}
      </p>
    </div>
  );
}

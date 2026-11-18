import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useState, type ReactElement } from 'react';
import { ConfirmDialog, type ConfirmRequest } from '../../components/ui/ConfirmDialog';
import { PageHeader } from '../../components/ui/PageHeader';
import { useTranslation } from '../../hooks/useTranslation';
import { ApiError } from '../../lib/apiClient';
import type { TranslationKey } from '../../lib/i18n/en';
import { shiftsApi, type ShiftDto } from '../master-data/api';
import { peopleApi } from '../people/api';
import { formatDate } from '../shifts/shiftFormat';
import {
  shiftScheduleApi,
  type ScheduledShiftDto,
  type ScheduledShiftStatus,
  type ShiftScheduleDto,
} from './api';
import { PlanShiftDialog } from './PlanShiftDialog';

/** The shift a card stands for — a planned one, or one of the factory's shifts not yet planned. */
interface Slot {
  id: number;
  name: string;
  startTime: string;
  endTime: string;
}

/**
 * Shift Scheduling — tomorrow's shifts, planned the day before (specification section
 * 19.30).
 *
 * Tomorrow comes first, because planning it is what the screen is for. Today is below
 * it: a plan can still be changed until its shift starts, and today is where a shift is
 * seen opening, running past its end, or never opening at all.
 *
 * "Today" and "tomorrow" are the factory's, sent by the server, not the tablet's — a
 * tablet with its clock wrong would otherwise plan the wrong day.
 */
export function ShiftSchedulePage(): ReactElement {
  const { t } = useTranslation();
  const queryClient = useQueryClient();

  const [planning, setPlanning] = useState<{
    date: string;
    slot: Slot;
    existing: ScheduledShiftDto | null;
  } | null>(null);
  const [confirm, setConfirm] = useState<ConfirmRequest | null>(null);
  const [actionError, setActionError] = useState<string | null>(null);

  const schedule = useQuery({
    queryKey: ['shift-schedule'],
    queryFn: () => shiftScheduleApi.get(),
    // A shift opens by itself at its time, and the card should say so without a reload.
    refetchInterval: 60_000,
  });

  const shifts = useQuery({
    queryKey: ['shifts', 'active'],
    queryFn: () => shiftsApi.list(false),
  });

  const people = useQuery({
    queryKey: ['people'],
    queryFn: () => peopleApi.list(false),
  });

  function invalidate(): void {
    void queryClient.invalidateQueries({ queryKey: ['shift-schedule'] });
    void queryClient.invalidateQueries({ queryKey: ['shift-reports'] });
  }

  const cancel = useMutation({
    mutationFn: (id: number) => shiftScheduleApi.cancel(id),
    onSuccess: () => {
      setActionError(null);
    },
    onError: (caught: unknown) => {
      setActionError(
        caught instanceof ApiError ? caught.message : t('common.somethingWrong'),
      );
    },
    onSettled: invalidate,
  });

  if (schedule.isPending || shifts.isPending || people.isPending) {
    return <p className="p-6 text-ink-muted">{t('common.loading')}</p>;
  }

  if (schedule.isError || shifts.isError || people.isError) {
    return <p className="p-6 text-bad">{t('schedule.loadFailed')}</p>;
  }

  const data = schedule.data;

  function edit(date: string, slot: Slot, existing: ScheduledShiftDto | null): void {
    setActionError(null);
    setPlanning({ date, slot, existing });
  }

  function askCancel(plan: ScheduledShiftDto): void {
    setConfirm({
      title: t('schedule.cancelThis'),
      message: t('schedule.cancelMessage')
        .replace('{0}', plan.shiftName)
        .replace('{1}', formatDate(plan.productionDate)),
      confirmLabel: t('schedule.cancelPlan'),
      tone: 'danger',
      onConfirm: () => {
        setConfirm(null);
        cancel.mutate(plan.id);
      },
    });
  }

  return (
    <>
      <PageHeader
        title={t('page.schedule.title')}
        subtitle={t('page.schedule.subtitle')}
      />

      {actionError !== null && (
        <p
          role="alert"
          className="mb-4 rounded-control border border-s-4 border-bad/30 border-s-bad bg-bad-soft px-4 py-3 text-sm font-medium text-bad"
        >
          {actionError}
        </p>
      )}

      <Day
        heading={t('schedule.tomorrow')}
        date={data.tomorrow}
        canPlan
        data={data}
        shifts={shifts.data}
        onPlan={edit}
        onCancel={askCancel}
      />

      <Day
        heading={t('schedule.today')}
        date={data.today}
        canPlan={false}
        data={data}
        shifts={shifts.data}
        onPlan={edit}
        onCancel={askCancel}
      />

      {planning !== null && (
        <PlanShiftDialog
          productionDate={planning.date}
          shift={planning.slot}
          lines={data.lines}
          people={people.data}
          existing={planning.existing}
          onClose={() => {
            setPlanning(null);
          }}
          onSaved={invalidate}
        />
      )}

      {confirm !== null && (
        <ConfirmDialog
          request={confirm}
          onCancel={() => {
            setConfirm(null);
          }}
        />
      )}
    </>
  );
}

/**
 * One day: a card for each of the factory's shifts, planned or not, in the order they
 * run. A plan for a shift since made inactive still shows — it will still open.
 */
function Day({
  heading,
  date,
  canPlan,
  data,
  shifts,
  onPlan,
  onCancel,
}: {
  heading: string;
  date: string;
  canPlan: boolean;
  data: ShiftScheduleDto;
  shifts: ShiftDto[];
  onPlan: (date: string, slot: Slot, existing: ScheduledShiftDto | null) => void;
  onCancel: (plan: ScheduledShiftDto) => void;
}): ReactElement {
  const { t } = useTranslation();

  const plans = data.shifts.filter((plan) => plan.productionDate === date);
  const slots: Slot[] = [
    ...shifts,
    ...plans
      .filter((plan) => !shifts.some((shift) => shift.id === plan.shiftId))
      .map((plan) => ({
        id: plan.shiftId,
        name: plan.shiftName,
        startTime: plan.startTime,
        endTime: plan.endTime,
      })),
  ].sort((a, b) => a.startTime.localeCompare(b.startTime));

  return (
    <section className="mb-8">
      <h2 className="mb-3 text-lg font-bold text-ink">
        {heading} <span className="font-normal text-ink-muted">· {formatDate(date)}</span>
      </h2>

      <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
        {slots.map((slot) => {
          const plan = plans.find((p) => p.shiftId === slot.id) ?? null;

          return (
            <article key={slot.id} className="card flex flex-col p-4">
              <header className="mb-3 flex items-start justify-between gap-2">
                <div>
                  <p className="text-base font-bold text-ink">
                    {t('term.shift')} {slot.name}
                  </p>
                  {/* Left to right in Arabic too, or 16:00–00:00 reads as 00:00–16:00. */}
                  <p className="text-sm text-ink-muted">
                    <span dir="ltr">
                      {slot.startTime}–{slot.endTime}
                    </span>
                  </p>
                </div>
                {plan !== null && <StatusBadge status={plan.status} />}
              </header>

              {plan === null ? (
                <NotPlanned
                  canPlan={canPlan}
                  onPlan={() => {
                    onPlan(date, slot, null);
                  }}
                />
              ) : (
                <PlanDetails
                  plan={plan}
                  onEdit={() => {
                    onPlan(date, slot, plan);
                  }}
                  onCancel={() => {
                    onCancel(plan);
                  }}
                />
              )}
            </article>
          );
        })}
      </div>
    </section>
  );
}

function NotPlanned({
  canPlan,
  onPlan,
}: {
  canPlan: boolean;
  onPlan: () => void;
}): ReactElement {
  const { t } = useTranslation();

  return canPlan ? (
    <div className="flex flex-1 flex-col justify-between gap-3">
      <p className="text-sm text-ink-muted">{t('schedule.notPlannedYet')}</p>
      <button type="button" className="btn-primary" onClick={onPlan}>
        {t('schedule.planShift')}
      </button>
    </div>
  ) : (
    // Today can no longer be planned; a shift needed today is opened by hand.
    <p className="text-sm text-ink-muted">{t('schedule.notPlannedToday')}</p>
  );
}

function PlanDetails({
  plan,
  onEdit,
  onCancel,
}: {
  plan: ScheduledShiftDto;
  onEdit: () => void;
  onCancel: () => void;
}): ReactElement {
  const { t } = useTranslation();

  return (
    <div className="flex flex-1 flex-col gap-3">
      <dl className="space-y-1.5 text-sm">
        <div className="flex justify-between gap-3">
          <dt className="text-ink-muted">{t('term.supervisor')}</dt>
          <dd className="text-end font-medium text-ink">
            {plan.supervisorName ?? t('state.notDecidedYet')}
          </dd>
        </div>
        {plan.lines.map((line) => (
          <div key={line.productionLineId} className="flex justify-between gap-3">
            <dt className="text-ink-muted">{line.productionLineName}</dt>
            <dd className="text-end font-medium text-ink">
              {line.operatorName ?? t('schedule.nameOnTheDay')}
            </dd>
          </div>
        ))}
      </dl>

      {plan.extruderStartingShift && (
        <p className="w-fit rounded-full bg-brand-50 px-2.5 py-0.5 text-xs font-semibold text-brand-700">
          {t('shifts.startingShift')}
        </p>
      )}

      {plan.status === 'Overrunning' && (
        <p className="rounded-control border border-s-4 border-warn/30 border-s-warn bg-warn-soft px-3 py-2 text-xs font-medium text-warn">
          {t('schedule.overrunningNote')}
        </p>
      )}

      {plan.notOpenedReason !== null && (
        <p className="rounded-control border border-s-4 border-bad/30 border-s-bad bg-bad-soft px-3 py-2 text-xs font-medium text-bad">
          {plan.notOpenedReason}
        </p>
      )}

      <p className="mt-auto text-xs text-ink-muted">
        {t('schedule.plannedBy')} {plan.scheduledByName}
        {plan.changedByName !== null && (
          <>
            {' · '}
            {t('schedule.changedBy')} {plan.changedByName}
          </>
        )}
      </p>

      {plan.canChange && (
        <div className="flex gap-2">
          <button
            type="button"
            className="min-h-touch flex-1 rounded-control border border-line px-3 text-sm font-medium text-ink-soft transition-colors hover:border-brand-200 hover:bg-brand-50 hover:text-brand-700"
            onClick={onEdit}
          >
            {t('action.edit')}
          </button>
          <button
            type="button"
            className="min-h-touch flex-1 rounded-control border border-line px-3 text-sm font-medium text-ink-muted transition-colors hover:border-bad/40 hover:bg-bad-soft hover:text-bad"
            onClick={onCancel}
          >
            {t('schedule.cancelPlan')}
          </button>
        </div>
      )}
    </div>
  );
}

const statusStyles: Record<
  ScheduledShiftStatus,
  { label: TranslationKey; className: string }
> = {
  // Waiting for its start time. Can still be changed.
  Planned: { label: 'schedule.status.planned', className: 'bg-brand-50 text-brand-700' },
  // Opened by the schedule and running now.
  Running: { label: 'schedule.status.running', className: 'bg-ok-soft text-ok' },
  // Past its end, kept open because work is still open on it.
  Overrunning: {
    label: 'schedule.status.overrunning',
    className: 'bg-warn-soft text-warn',
  },
  Finished: { label: 'schedule.status.finished', className: 'bg-line text-ink-muted' },
  NotOpened: { label: 'schedule.status.notOpened', className: 'bg-bad-soft text-bad' },
};

function StatusBadge({ status }: { status: ScheduledShiftStatus }): ReactElement {
  const { t } = useTranslation();
  const tone = statusStyles[status];

  return (
    <span
      className={`shrink-0 rounded-full px-2.5 py-0.5 text-xs font-semibold ${tone.className}`}
    >
      {t(tone.label)}
    </span>
  );
}

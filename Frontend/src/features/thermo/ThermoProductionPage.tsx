import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useState, type ReactElement } from 'react';
import { ConfirmDialog, type ConfirmRequest } from '../../components/ui/ConfirmDialog';
import { ReasonDialog } from '../../components/ui/ReasonDialog';
import { useTranslation } from '../../hooks/useTranslation';
import { PageHeader } from '../../components/ui/PageHeader';
import { StartOnLineButton } from '../../components/ui/StartOnLineButton';
import { useAuth } from '../../hooks/useAuth';
import { ApiError } from '../../lib/apiClient';
import { RoleNames } from '../../lib/roles';
import { shiftReportsApi } from '../shifts/api';
import { formatDate } from '../shifts/shiftFormat';
import { thermoApi, type ThermoRunDto } from './api';
import { LeaveUnfinishedDialog } from './LeaveUnfinishedDialog';
import { StartRunDialog } from './StartRunDialog';

/**
 * Line 2 — thermoforming (specification section 9).
 *
 * One roll goes in whole and is never split, so the screen is a list of runs: one roll,
 * one row. What is being made is not chosen here — the roll named its product at the
 * extruder, and the bags are that product.
 */
export function ThermoProductionPage(): ReactElement {
  const { t } = useTranslation();
  const queryClient = useQueryClient();
  const { hasRole } = useAuth();
  const canForm = hasRole(RoleNames.Administrator, RoleNames.ThermoOperator);
  // The wrong roll scanned in: the operator's own fresh mistake, or a supervisor's (19.27).
  const canUndoStart = hasRole(
    RoleNames.Administrator,
    RoleNames.Supervisor,
    RoleNames.ThermoOperator,
  );
  const [undoing, setUndoing] = useState<{ id: number; rollCode: string } | null>(null);

  const [openOnly, setOpenOnly] = useState(true);
  const [starting, setStarting] = useState<{
    shiftLineId: number;
    lineName: string;
    shiftLabel: string;
  } | null>(null);
  const [confirm, setConfirm] = useState<ConfirmRequest | null>(null);
  const [actionError, setActionError] = useState<string | null>(null);
  const [justStarted, setJustStarted] = useState<ThermoRunDto | null>(null);
  // Taking a roll out before it is finished, which asks why (section 19.23).
  const [leaving, setLeaving] = useState<{ id: number; rollCode: string } | null>(null);

  const runs = useQuery({
    queryKey: ['thermo-runs', openOnly, 'current-shift'],
    // Still-to-count is a queue and crosses shifts; the full list is this shift (19.6).
    queryFn: () => thermoApi.runs(openOnly, !openOnly),
  });

  const availableRolls = useQuery({
    queryKey: ['thermo-available-rolls'],
    queryFn: () => thermoApi.availableRolls(),
  });

  // Only the forming lines of shifts still open. The server refuses the rest anyway,
  // but offering them would put the man in that position for nothing.
  const formingLines = useQuery({
    queryKey: ['shift-reports', 'forming-lines'],
    queryFn: async () => {
      const open = await shiftReportsApi.list(undefined, true);
      const full = await Promise.all(open.map((s) => shiftReportsApi.get(s.id)));
      return full.flatMap((shift) =>
        shift.lines
          .filter((line) => line.formsBags)
          .map((line) => ({
            shiftLineId: line.id,
            lineName: line.productionLineName,
            shiftLabel: `shift ${shift.shiftName}, ${formatDate(shift.productionDate)}`,
          })),
      );
    },
  });

  function invalidate(): void {
    void queryClient.invalidateQueries({ queryKey: ['thermo-runs'] });
    void queryClient.invalidateQueries({ queryKey: ['thermo-available-rolls'] });
    void queryClient.invalidateQueries({ queryKey: ['rolls'] });
  }

  const finishRun = useMutation({
    mutationFn: (id: number) => thermoApi.finishRun(id, null),
    onSuccess: () => {
      setActionError(null);
      // The roll is out, so "it is in the machine" no longer holds.
      setJustStarted(null);
    },
    onError: (caught: unknown) => {
      setActionError(
        caught instanceof ApiError ? caught.message : t('common.somethingWrong'),
      );
    },
    onSettled: invalidate,
  });

  if (runs.isPending) {
    return <p className="p-6 text-ink-muted">{t('common.loading')}</p>;
  }

  if (runs.isError) {
    return <p className="p-6 text-bad">{t('thermo.loadFailed2')}</p>;
  }

  const lines = formingLines.data ?? [];

  return (
    <>
      <PageHeader
        title={t('page.thermo.title')}
        subtitle={t('page.thermo.subtitle')}
        actions={
          canForm ? (
            <StartOnLineButton
              lines={lines}
              action={t('thermo.addRoll')}
              onStart={(shiftLineId) => {
                const line = lines.find((l) => l.shiftLineId === shiftLineId);
                if (line !== undefined) {
                  setStarting(line);
                }
              }}
            />
          ) : undefined
        }
      />

      {canForm && lines.length === 0 && (
        <p className="mb-4 rounded-control border border-line bg-canvas px-4 py-3 text-sm text-ink-soft">
          No forming line is open. Open a shift with the thermo line on it.
        </p>
      )}

      <section className="mb-6 flex flex-wrap gap-2">
        <Chip
          label={t('thermo.stillToCount')}
          active={openOnly}
          onClick={() => {
            setOpenOnly(true);
          }}
        />
        <Chip
          label={t('common.thisShift')}
          active={!openOnly}
          onClick={() => {
            setOpenOnly(false);
          }}
        />
      </section>

      {actionError !== null && (
        <p
          role="alert"
          className="mb-4 rounded-control border border-s-4 border-bad/30 border-s-bad bg-bad-soft px-4 py-3 text-sm font-medium text-bad"
        >
          {actionError}
        </p>
      )}

      {justStarted !== null && (
        <p className="mb-4 rounded-control border border-s-4 border-ok/30 border-s-ok bg-ok-soft px-4 py-3 text-sm font-medium text-ok">
          {t('term.roll')} <strong className="font-mono">{justStarted.rollCode}</strong>{' '}
          is in the machine, making {justStarted.productName ?? 'its product'}. Press{' '}
          {t('thermo.takeItOut')} when the run is done, then count what it made.
        </p>
      )}

      <div className="card overflow-x-auto">
        <table className="w-full text-start text-sm">
          <thead>
            <tr className="border-b border-line text-xs tracking-wider text-ink-muted uppercase">
              <th className="px-4 py-3 font-semibold">{t('term.roll')}</th>
              <th className="px-4 py-3 font-semibold">{t('term.colourRecipe')}</th>
              <th className="px-4 py-3 font-semibold">{t('term.shift')}</th>
              <th className="px-4 py-3 font-semibold">{t('thermo.operator')}</th>
              <th className="px-4 py-3 font-semibold">{t('thermo.whereItIs')}</th>
              <th className="px-4 py-3 font-semibold">{t('thermo.timeIn')}</th>
              <th className="px-4 py-3 font-semibold">{t('thermo.timeOut')}</th>
              <th className="px-4 py-3 text-end font-semibold">{t('field.minutes')}</th>
              <th className="px-4 py-3 text-end font-semibold">{t('term.bags')}</th>
              <th className="px-4 py-3" />
            </tr>
          </thead>
          <tbody>
            {runs.data.length === 0 && (
              <tr>
                <td colSpan={10} className="px-4 py-8 text-center text-ink-muted">
                  {openOnly ? t('thermo.nothingToCount') : t('thermo.noneFormed')}
                </td>
              </tr>
            )}
            {runs.data.map((run) => (
              <tr key={run.id} className="border-b border-line last:border-0">
                <td className="px-4 py-3 font-mono font-semibold text-ink">
                  {run.rollCode}
                  {/* Why it came out unfinished, where the next man will look. */}
                  {run.unfinishedReason !== null && (
                    <span className="block font-sans text-xs font-normal text-warn">
                      {run.unfinishedReason}
                    </span>
                  )}
                </td>
                <td className="px-4 py-3 text-ink-soft">
                  {run.colorName}
                  <span className="ms-2 text-xs text-ink-muted">
                    {run.recipeNumber} {run.recipeFamilyName}
                  </span>
                </td>
                <td className="px-4 py-3 whitespace-nowrap text-ink-soft">
                  {run.shiftName} · {formatDate(run.productionDate)}
                </td>
                <td className="px-4 py-3 text-ink-soft">{run.operatorName}</td>
                <td className="px-4 py-3">
                  <RunStage run={run} />
                </td>
                <td className="px-4 py-3 tabular-nums text-ink-soft">
                  <ClockTime at={run.startedAt} />
                </td>
                <td className="px-4 py-3 tabular-nums text-ink-soft">
                  {run.finishedAt === null ? '—' : <ClockTime at={run.finishedAt} />}
                </td>
                <td className="px-4 py-3 text-end tabular-nums text-ink-soft">
                  {run.totalTimeMinutes ?? '—'}
                </td>
                <td className="px-4 py-3 text-end tabular-nums text-ink-soft">
                  {run.bagCount ?? '—'}
                </td>
                <td className="px-4 py-3">
                  <div className="flex justify-end gap-2">
                    {canUndoStart && !run.isFinished && (
                      <button
                        type="button"
                        className="min-h-9 rounded-control border border-line px-3 text-sm font-medium whitespace-nowrap text-ink-soft transition-colors hover:border-bad/40 hover:bg-bad-soft hover:text-bad"
                        onClick={() => {
                          setUndoing({ id: run.id, rollCode: run.rollCode });
                        }}
                      >
                        {t('correct.wrongRoll')}
                      </button>
                    )}
                    {canForm && !run.isFinished && (
                      <button
                        type="button"
                        className="min-h-9 rounded-control border border-warn/40 px-3 text-sm font-medium whitespace-nowrap text-warn transition-colors hover:bg-warn-soft"
                        onClick={() => {
                          setLeaving({ id: run.id, rollCode: run.rollCode });
                        }}
                      >
                        {t('thermo.notFinished')}
                      </button>
                    )}
                    {canForm && !run.isFinished && (
                      <button
                        type="button"
                        className="min-h-9 rounded-control border border-brand-600 bg-brand-600 px-3 text-sm font-medium whitespace-nowrap text-white transition-colors hover:bg-brand-700"
                        onClick={() => {
                          setConfirm({
                            title: `Roll ${run.rollCode} finished?`,
                            message: (
                              <>
                                The roll comes out now, and this is its time out. Counting
                                what it made comes next, on the Thermo Tests screen — and
                                that is what creates the bags.
                              </>
                            ),
                            confirmLabel: t('thermo.takeItOut'),
                            tone: 'primary',
                            onConfirm: () => {
                              finishRun.mutate(run.id);
                            },
                          });
                        }}
                      >
                        {t('thermo.takeItOut')}
                      </button>
                    )}
                  </div>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {starting !== null && (
        <StartRunDialog
          line={starting}
          rolls={availableRolls.data ?? []}
          onClose={() => {
            setStarting(null);
          }}
          onStarted={(run) => {
            setJustStarted(run);
            invalidate();
          }}
        />
      )}

      {leaving !== null && (
        <LeaveUnfinishedDialog
          runId={leaving.id}
          rollCode={leaving.rollCode}
          onClose={() => {
            setLeaving(null);
          }}
          onLeft={() => {
            // Out of the machine, so "it is in the machine" no longer holds.
            setJustStarted(null);
            invalidate();
          }}
        />
      )}

      {undoing !== null && (
        <ReasonDialog
          title={`${t('correct.wrongRoll')} — ${undoing.rollCode}`}
          explanation={t('correct.wrongRollExplain')}
          question={t('correct.whyWrongRoll')}
          confirmLabel={t('correct.takeBack')}
          onConfirm={async (reason) => {
            await thermoApi.undoStart(undoing.id, reason);
            setJustStarted(null);
            invalidate();
          }}
          onClose={() => {
            setUndoing(null);
          }}
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
 * Three stages, not a status column: in the machine, out and waiting to be counted, or
 * done. The middle one is the one the test person is looking for.
 */
function RunStage({
  run,
}: {
  run: { isFinished: boolean; needsTest: boolean; leftUnfinished: boolean };
}): ReactElement {
  const { t } = useTranslation();
  const [label, tone] = !run.isFinished
    ? ['In the machine', 'bg-brand-50 text-brand-700']
    : run.leftUnfinished
      ? // Out early: back in stock, and what it made is still counted (section 19.23).
        [
          run.needsTest ? t('thermo.notFinishedToCount') : t('thermo.notFinishedCounted'),
          'bg-warn-soft text-warn',
        ]
      : run.needsTest
        ? ['Waiting to be counted', 'bg-warn-soft text-warn']
        : ['Counted', 'bg-ok-soft text-ok'];

  return (
    <span className={`rounded-full px-2.5 py-0.5 text-xs font-semibold ${tone}`}>
      {label}
    </span>
  );
}

/**
 * When the roll went in or came out, on the clock of the tablet showing it. The day goes
 * on a second line only when it is not today: the still-to-count list carries rolls over
 * from earlier shifts, and "23:40" under a roll counted the next morning would read as
 * tonight.
 */
function ClockTime({ at }: { at: string }): ReactElement {
  const when = new Date(at);
  const time = when.toLocaleTimeString('en-GB', { hour: '2-digit', minute: '2-digit' });
  const day = when.toLocaleDateString('en-GB');
  return (
    <span className="whitespace-nowrap">
      {time}
      {day !== new Date().toLocaleDateString('en-GB') && (
        <span className="block text-xs text-ink-muted">{day}</span>
      )}
    </span>
  );
}

function Chip({
  label,
  active,
  onClick,
}: {
  label: string;
  active: boolean;
  onClick: () => void;
}): ReactElement {
  return (
    <button
      type="button"
      onClick={onClick}
      className={[
        'min-h-9 rounded-full border px-4 text-sm font-medium transition-colors',
        active
          ? 'border-brand-600 bg-brand-50 text-brand-700'
          : 'border-line text-ink-soft hover:border-brand-200 hover:bg-brand-50',
      ].join(' ')}
    >
      {label}
    </button>
  );
}

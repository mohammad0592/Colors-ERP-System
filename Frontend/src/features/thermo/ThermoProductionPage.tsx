import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useState, type ReactElement } from 'react';
import { ConfirmDialog, type ConfirmRequest } from '../../components/ui/ConfirmDialog';
import { PageHeader } from '../../components/ui/PageHeader';
import { StartOnLineButton } from '../../components/ui/StartOnLineButton';
import { useAuth } from '../../hooks/useAuth';
import { ApiError } from '../../lib/apiClient';
import { RoleNames } from '../../lib/roles';
import { shiftReportsApi } from '../shifts/api';
import { formatDate } from '../shifts/shiftFormat';
import { thermoApi, type ThermoRunDto } from './api';
import { StartRunDialog } from './StartRunDialog';

/**
 * Line 2 — thermoforming (specification section 9).
 *
 * One roll goes in whole and is never split, so the screen is a list of runs: one roll,
 * one row. What is being made is not on this screen at all, because nobody chooses it —
 * the mould on the line and the roll's recipe decide it between them.
 */
export function ThermoProductionPage(): ReactElement {
  const queryClient = useQueryClient();
  const { hasRole } = useAuth();
  const canForm = hasRole(RoleNames.Administrator, RoleNames.ThermoOperator);

  const [openOnly, setOpenOnly] = useState(true);
  const [starting, setStarting] = useState<{
    shiftLineId: number;
    lineName: string;
    shiftLabel: string;
    mouldName: string | null;
  } | null>(null);
  const [confirm, setConfirm] = useState<ConfirmRequest | null>(null);
  const [actionError, setActionError] = useState<string | null>(null);
  const [justStarted, setJustStarted] = useState<ThermoRunDto | null>(null);

  const runs = useQuery({
    queryKey: ['thermo-runs', openOnly],
    queryFn: () => thermoApi.runs(openOnly),
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
            mouldName: line.mouldName,
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
    },
    onError: (caught: unknown) => {
      setActionError(
        caught instanceof ApiError ? caught.message : 'Something went wrong.',
      );
    },
    onSettled: invalidate,
  });

  if (runs.isPending) {
    return <p className="p-6 text-ink-muted">Loading…</p>;
  }

  if (runs.isError) {
    return <p className="p-6 text-bad">Could not load line 2.</p>;
  }

  const lines = formingLines.data ?? [];

  return (
    <>
      <PageHeader
        title="Thermoforming"
        subtitle="One roll goes in whole. The mould and the recipe decide what comes out."
        actions={
          canForm ? (
            <StartOnLineButton
              lines={lines}
              action="Put a roll in"
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
          No forming line is open. Open a shift with the thermo line on it, and set the
          mould — without a mould there is no way to know what is being made.
        </p>
      )}

      <section className="mb-6 flex flex-wrap gap-2">
        <Chip
          label="Still to be counted"
          active={openOnly}
          onClick={() => {
            setOpenOnly(true);
          }}
        />
        <Chip
          label="Every run"
          active={!openOnly}
          onClick={() => {
            setOpenOnly(false);
          }}
        />
      </section>

      {actionError !== null && (
        <p
          role="alert"
          className="mb-4 rounded-control border border-l-4 border-bad/30 border-l-bad bg-bad-soft px-4 py-3 text-sm font-medium text-bad"
        >
          {actionError}
        </p>
      )}

      {justStarted !== null && (
        <p className="mb-4 rounded-control border border-l-4 border-ok/30 border-l-ok bg-ok-soft px-4 py-3 text-sm font-medium text-ok">
          Roll <strong className="font-mono">{justStarted.rollCode}</strong> is in the
          machine on the {justStarted.mouldName ?? 'mounted'} mould. Take it out when the
          run is done, then count what it made.
        </p>
      )}

      <div className="card overflow-x-auto">
        <table className="w-full text-left text-sm">
          <thead>
            <tr className="border-b border-line text-xs tracking-wider text-ink-muted uppercase">
              <th className="px-4 py-3 font-semibold">Roll</th>
              <th className="px-4 py-3 font-semibold">Colour · recipe</th>
              <th className="px-4 py-3 font-semibold">Shift</th>
              <th className="px-4 py-3 font-semibold">Operator</th>
              <th className="px-4 py-3 font-semibold">Where it is</th>
              <th className="px-4 py-3 text-right font-semibold">Minutes</th>
              <th className="px-4 py-3 text-right font-semibold">Bags</th>
              <th className="px-4 py-3" />
            </tr>
          </thead>
          <tbody>
            {runs.data.length === 0 && (
              <tr>
                <td colSpan={8} className="px-4 py-8 text-center text-ink-muted">
                  {openOnly
                    ? 'Nothing is waiting to be counted.'
                    : 'No roll has been formed yet.'}
                </td>
              </tr>
            )}
            {runs.data.map((run) => (
              <tr key={run.id} className="border-b border-line last:border-0">
                <td className="px-4 py-3 font-mono font-semibold text-ink">
                  {run.rollCode}
                </td>
                <td className="px-4 py-3 text-ink-soft">
                  {run.colorName}
                  <span className="ml-2 text-xs text-ink-muted">
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
                <td className="px-4 py-3 text-right tabular-nums text-ink-soft">
                  {run.totalTimeMinutes ?? '—'}
                </td>
                <td className="px-4 py-3 text-right tabular-nums text-ink-soft">
                  {run.bagCount ?? '—'}
                </td>
                <td className="px-4 py-3">
                  <div className="flex justify-end">
                    {canForm && !run.isFinished && (
                      <button
                        type="button"
                        className="min-h-9 rounded-control border border-brand-600 bg-brand-600 px-3 text-sm font-medium whitespace-nowrap text-white transition-colors hover:bg-brand-700"
                        onClick={() => {
                          setConfirm({
                            title: `Take roll ${run.rollCode} out?`,
                            message: (
                              <>
                                The run ends now. Counting what it made comes next, on the
                                Thermo Tests screen — and that is what creates the bags.
                              </>
                            ),
                            confirmLabel: 'Take it out',
                            tone: 'primary',
                            onConfirm: () => {
                              finishRun.mutate(run.id);
                            },
                          });
                        }}
                      >
                        Take it out
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
  run: { isFinished: boolean; needsTest: boolean };
}): ReactElement {
  const [label, tone] = !run.isFinished
    ? ['In the machine', 'bg-brand-50 text-brand-700']
    : run.needsTest
      ? ['Waiting to be counted', 'bg-warn-soft text-warn']
      : ['Counted', 'bg-ok-soft text-ok'];

  return (
    <span className={`rounded-full px-2.5 py-0.5 text-xs font-semibold ${tone}`}>
      {label}
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

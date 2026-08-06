import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useState, type ReactElement } from 'react';
import { ConfirmDialog, type ConfirmRequest } from '../../components/ui/ConfirmDialog';
import { PageHeader } from '../../components/ui/PageHeader';
import { StartOnLineButton } from '../../components/ui/StartOnLineButton';
import { useAuth } from '../../hooks/useAuth';
import { ApiError } from '../../lib/apiClient';
import { RoleNames } from '../../lib/roles';
import { LabelPrintScreen } from '../labels/LabelPrintScreen';
import { colorsApi } from '../master-data/api';
import { recipesApi } from '../recipes/api';
import { shiftReportsApi } from '../shifts/api';
import { formatDate } from '../shifts/shiftFormat';
import { productionApi, type BatchSummaryDto, type RollDto } from './api';
import { NewRollDialog } from './NewRollDialog';
import { RollStatusBadge } from './RollStatusBadge';

/**
 * Line 1 — the mixer and the extruder (specification section 8).
 *
 * A batch is the smallest thing that knows its materials, so the screen leads with
 * the mix and hangs its rolls underneath. Each roll's code and barcode are generated
 * and shown, never asked for.
 */
export function RollProductionPage(): ReactElement {
  const queryClient = useQueryClient();
  const { hasRole } = useAuth();
  const canProduce = hasRole(RoleNames.Administrator, RoleNames.ExtruderOperator);

  const [openOnly, setOpenOnly] = useState(true);
  const [selected, setSelected] = useState<BatchSummaryDto | null>(null);
  const [logging, setLogging] = useState<BatchSummaryDto | null>(null);
  const [confirm, setConfirm] = useState<ConfirmRequest | null>(null);
  const [actionError, setActionError] = useState<string | null>(null);
  const [justLogged, setJustLogged] = useState<RollDto | null>(null);

  // The labels to print. Set the moment a roll is logged, so the sticker comes up by
  // itself — nobody should have to go and find a roll in a list to label it.
  const [printing, setPrinting] = useState<{ barcodes: string[]; headline?: string } | null>(
    null,
  );

  const batches = useQuery({
    queryKey: ['batches', openOnly],
    queryFn: () => productionApi.batches(openOnly),
  });

  const rolls = useQuery({
    queryKey: ['rolls', selected?.id ?? null],
    queryFn: () => productionApi.rolls(selected?.id),
  });

  const recipes = useQuery({
    queryKey: ['recipe-versions', 'all'],
    queryFn: () => recipesApi.versions(),
  });

  const colors = useQuery({
    queryKey: ['colors', 'active'],
    queryFn: () => colorsApi.list(false),
  });

  // Lines of shifts still open, and only the ones that mix — a batch never crosses a
  // shift, and the thermo does not mix at all (specification section 4).
  const openLines = useQuery({
    queryKey: ['shift-reports', 'mixing-lines'],
    queryFn: async () => {
      const open = await shiftReportsApi.list(undefined, true);
      const full = await Promise.all(open.map((s) => shiftReportsApi.get(s.id)));
      return full.flatMap((shift) =>
        shift.lines
          .filter((line) => line.makesRolls)
          .map((line) => ({
            shiftLineId: line.id,
            lineName: line.productionLineName,
            shiftLabel: `shift ${shift.shiftName}, ${formatDate(shift.productionDate)}`,
          })),
      );
    },
  });

  function invalidate(): void {
    void queryClient.invalidateQueries({ queryKey: ['batches'] });
    void queryClient.invalidateQueries({ queryKey: ['rolls'] });
  }

  function onError(caught: unknown): void {
    setActionError(caught instanceof ApiError ? caught.message : 'Something went wrong.');
  }

  const startBatch = useMutation({
    mutationFn: (shiftLineId: number) => productionApi.startBatch(shiftLineId, null),
    onSuccess: (batch) => {
      setActionError(null);
      setSelected(batch);
    },
    onError,
    onSettled: invalidate,
  });

  const finishBatch = useMutation({
    mutationFn: (id: number) => productionApi.finishBatch(id),
    onSuccess: () => {
      setActionError(null);
    },
    onError,
    onSettled: invalidate,
  });

  if (batches.isPending || recipes.isPending || colors.isPending) {
    return <p className="p-6 text-ink-muted">Loading…</p>;
  }

  if (batches.isError || recipes.isError || colors.isError) {
    return <p className="p-6 text-bad">Could not load line 1.</p>;
  }

  // A draft may still change, so a roll could never be reproduced from it.
  const usableRecipes = recipes.data.filter((r) => r.status !== 'Draft');

  return (
    <>
      <PageHeader
        title="Roll Production"
        subtitle="One mix makes fifteen to seventeen rolls. The batch knows the materials; each roll knows its own recipe and colour."
        actions={
          canProduce ? (
            <StartOnLineButton
              lines={openLines.data ?? []}
              action="Start a batch"
              onStart={(shiftLineId) => {
                startBatch.mutate(shiftLineId);
              }}
            />
          ) : undefined
        }
      />

      {canProduce && (openLines.data?.length ?? 0) === 0 && (
        <p className="mb-4 rounded-control border border-line bg-canvas px-4 py-3 text-sm text-ink-soft">
          No shift is open, so a batch cannot be started. A batch never crosses a
          shift, because all material goes back to the store at shift end.
        </p>
      )}

      <section className="mb-6 flex flex-wrap gap-2">
        <Chip
          label="Running batches"
          active={openOnly}
          onClick={() => {
            setOpenOnly(true);
          }}
        />
        <Chip
          label="All batches"
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

      {justLogged !== null && (
        <p className="mb-4 rounded-control border border-l-4 border-ok/30 border-l-ok bg-ok-soft px-4 py-3 text-sm font-medium text-ok">
          Roll <strong className="font-mono">{justLogged.rollCode}</strong> logged —
          barcode <strong className="font-mono">{justLogged.barcode}</strong>.{' '}
          <button
            type="button"
            className="font-semibold underline"
            onClick={() => {
              setPrinting({ barcodes: [justLogged.barcode] });
            }}
          >
            Print the label again
          </button>
        </p>
      )}

      <div className="card mb-8 overflow-x-auto">
        <table className="w-full text-left text-sm">
          <thead>
            <tr className="border-b border-line text-xs tracking-wider text-ink-muted uppercase">
              <th className="px-4 py-3 font-semibold">Batch</th>
              <th className="px-4 py-3 font-semibold">Line</th>
              <th className="px-4 py-3 font-semibold">Shift</th>
              <th className="px-4 py-3 font-semibold">Status</th>
              <th className="px-4 py-3 text-right font-semibold">Rolls</th>
              <th className="px-4 py-3 text-right font-semibold">Measured kg</th>
              <th className="px-4 py-3" />
            </tr>
          </thead>
          <tbody>
            {batches.data.length === 0 && (
              <tr>
                <td colSpan={7} className="px-4 py-8 text-center text-ink-muted">
                  {openOnly ? 'No batch is running.' : 'No batches yet.'}
                </td>
              </tr>
            )}
            {batches.data.map((batch) => (
              <tr
                key={batch.id}
                className={[
                  'border-b border-line last:border-0',
                  selected?.id === batch.id ? 'bg-brand-50' : '',
                ].join(' ')}
              >
                <td className="px-4 py-3 font-bold text-ink">{batch.batchNumber}</td>
                <td className="px-4 py-3 text-ink-soft">{batch.productionLineName}</td>
                <td className="px-4 py-3 text-ink-soft">
                  {batch.shiftName} · {formatDate(batch.productionDate)}
                </td>
                <td className="px-4 py-3">
                  <span
                    className={[
                      'rounded-full px-2.5 py-0.5 text-xs font-semibold',
                      batch.isFinished ? 'bg-line text-ink-muted' : 'bg-ok-soft text-ok',
                    ].join(' ')}
                  >
                    {batch.isFinished ? 'Finished' : 'Running'}
                  </span>
                </td>
                <td className="px-4 py-3 text-right tabular-nums text-ink-soft">
                  {batch.rollCount}
                </td>
                <td className="px-4 py-3 text-right tabular-nums text-ink-soft">
                  {batch.totalRollWeight ?? '—'}
                </td>
                <td className="px-4 py-3">
                  <div className="flex justify-end gap-2">
                    <Action
                      label={selected?.id === batch.id ? 'Hide rolls' : 'Show rolls'}
                      onClick={() => {
                        setSelected((current) =>
                          current?.id === batch.id ? null : batch,
                        );
                      }}
                    />
                    {canProduce && !batch.isFinished && (
                      <>
                        <Action
                          label="Log a roll"
                          tone="primary"
                          onClick={() => {
                            setLogging(batch);
                          }}
                        />
                        <Action
                          label="Finish"
                          onClick={() => {
                            setConfirm({
                              title: `Finish batch ${String(batch.batchNumber)}?`,
                              message: (
                                <>
                                  No more rolls can be drawn from this mix. It made{' '}
                                  {batch.rollCount} roll
                                  {batch.rollCount === 1 ? '' : 's'}.
                                </>
                              ),
                              confirmLabel: 'Finish batch',
                              tone: 'primary',
                              onConfirm: () => {
                                finishBatch.mutate(batch.id);
                              },
                            });
                          }}
                        />
                      </>
                    )}
                  </div>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      <section>
        <div className="mb-3 flex flex-wrap items-center justify-between gap-3">
          <h2 className="text-lg font-bold text-ink">
            {selected === null
              ? 'Recent rolls'
              : `Rolls from batch ${String(selected.batchNumber)}`}
          </h2>
          {selected !== null && (
            <button
              type="button"
              className="text-sm font-medium text-brand-700 hover:underline"
              onClick={() => {
                setSelected(null);
              }}
            >
              Show every roll
            </button>
          )}
        </div>

        <div className="card overflow-x-auto">
          <table className="w-full text-left text-sm">
            <thead>
              <tr className="border-b border-line text-xs tracking-wider text-ink-muted uppercase">
                <th className="px-4 py-3 font-semibold">Roll code</th>
                <th className="px-4 py-3 font-semibold">Barcode</th>
                <th className="px-4 py-3 font-semibold">Recipe</th>
                <th className="px-4 py-3 font-semibold">Colour</th>
                <th className="px-4 py-3 font-semibold">Status</th>
                <th className="px-4 py-3 text-right font-semibold">Weight</th>
                <th className="px-4 py-3 font-semibold">Made by</th>
                <th className="px-4 py-3 font-semibold">Out at</th>
              </tr>
            </thead>
            <tbody>
              {rolls.isPending && (
                <tr>
                  <td colSpan={8} className="px-4 py-6 text-center text-ink-muted">
                    Loading…
                  </td>
                </tr>
              )}
              {rolls.data?.length === 0 && (
                <tr>
                  <td colSpan={8} className="px-4 py-8 text-center text-ink-muted">
                    No rolls yet.
                  </td>
                </tr>
              )}
              {rolls.data?.map((roll) => (
                <tr key={roll.id} className="border-b border-line last:border-0">
                  <td className="px-4 py-3 font-mono font-semibold text-ink">
                    {roll.rollCode}
                  </td>
                  <td className="px-4 py-3">
                    {/* The label is reachable from the roll itself, not only from the
                        banner that appears once when it is logged. */}
                    <button
                      type="button"
                      className="font-mono text-xs text-ink-muted underline-offset-2 hover:text-brand-700 hover:underline"
                      onClick={() => {
                        setPrinting({ barcodes: [roll.barcode] });
                      }}
                    >
                      {roll.barcode}
                    </button>
                  </td>
                  <td className="px-4 py-3 text-ink-soft">
                    {roll.recipeNumber}
                    <span className="ml-2 text-xs text-ink-muted">
                      {roll.recipeFamilyName}
                    </span>
                  </td>
                  <td className="px-4 py-3 text-ink-soft">{roll.colorName}</td>
                  <td className="px-4 py-3">
                    <RollStatusBadge status={roll.status} />
                  </td>
                  <td className="px-4 py-3 text-right tabular-nums text-ink-soft">
                    {roll.weight ?? '—'}
                  </td>
                  <td className="px-4 py-3 text-ink-soft">{roll.producedByName}</td>
                  <td className="px-4 py-3 whitespace-nowrap text-ink-muted">
                    {new Date(roll.producedAt).toLocaleString('en-GB', {
                      day: '2-digit',
                      month: '2-digit',
                      hour: '2-digit',
                      minute: '2-digit',
                    })}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </section>

      {logging !== null && (
        <NewRollDialog
          batch={logging}
          recipes={usableRecipes}
          colors={colors.data}
          onClose={() => {
            setLogging(null);
          }}
          onCreated={(roll) => {
            setJustLogged(roll);
            setSelected(logging);
            setPrinting({
              barcodes: [roll.barcode],
              headline: `Roll ${roll.rollCode} logged. Print this and stick it on the roll.`,
            });
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

      {printing !== null && (
        <LabelPrintScreen
          barcodes={printing.barcodes}
          {...(printing.headline === undefined ? {} : { headline: printing.headline })}
          onClose={() => {
            setPrinting(null);
          }}
        />
      )}
    </>
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

function Action({
  label,
  onClick,
  tone = 'normal',
}: {
  label: string;
  onClick: () => void;
  tone?: 'normal' | 'primary';
}): ReactElement {
  const tones = {
    normal:
      'border-line text-ink-soft hover:border-brand-200 hover:bg-brand-50 hover:text-brand-700',
    primary: 'border-brand-600 bg-brand-600 text-white hover:bg-brand-700',
  };

  return (
    <button
      type="button"
      onClick={onClick}
      className={`min-h-9 rounded-control border px-3 text-sm font-medium whitespace-nowrap transition-colors ${tones[tone]}`}
    >
      {label}
    </button>
  );
}

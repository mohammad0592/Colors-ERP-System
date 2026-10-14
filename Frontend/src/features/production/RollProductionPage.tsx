import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useState, type ReactElement } from 'react';
import { useTranslation } from '../../hooks/useTranslation';
import { ConfirmDialog, type ConfirmRequest } from '../../components/ui/ConfirmDialog';
import { PageHeader } from '../../components/ui/PageHeader';
import {
  StartOnLineButton,
  type StartableLine,
} from '../../components/ui/StartOnLineButton';
import { useAuth } from '../../hooks/useAuth';
import { RoleNames } from '../../lib/roles';
import { LabelPrintScreen } from '../labels/LabelPrintScreen';
import { colorsApi, productsApi } from '../master-data/api';
import { recipesApi } from '../recipes/api';
import { shiftReportsApi } from '../shifts/api';
import { formatDate } from '../shifts/shiftFormat';
import { productionApi, type RollDto, type RollSummaryDto } from './api';
import { EditRollDialog } from './EditRollDialog';
import { MeasureRollDialog } from './MeasureRollDialog';
import { NewRollDialog } from './NewRollDialog';
import { RollStatusBadge } from './RollStatusBadge';
import { ThicknessVerdict } from './ThicknessVerdict';

/**
 * Line 1 — the mixer and the extruder (specification section 8).
 *
 * The screen is rolls, and nothing else. There is no batch here because the operator
 * does not have one: the mixer is filled once a shift, so the mix *is* the extruder's
 * part of the shift, and the first roll opens it without anybody being asked. Making him
 * declare it was asking him to state something already true — and it cost a deadlock,
 * because an empty mix could be left behind on a closed shift and never cleared.
 */
export function RollProductionPage(): ReactElement {
  const { t } = useTranslation();
  const queryClient = useQueryClient();
  const { hasRole } = useAuth();
  const canProduce = hasRole(RoleNames.Administrator, RoleNames.ExtruderOperator);
  // Correcting the shift's record is the supervisor's job too.
  const canCorrect = hasRole(
    RoleNames.Administrator,
    RoleNames.Supervisor,
    RoleNames.ExtruderOperator,
  );
  const [correcting, setCorrecting] = useState<RollSummaryDto | null>(null);
  // Weighing moved here from the roll test (section 19.12).
  const canMeasure = hasRole(
    RoleNames.Administrator,
    RoleNames.Supervisor,
    RoleNames.ExtruderOperator,
    RoleNames.ExtruderTestPerson,
  );
  const canReset = hasRole(RoleNames.Administrator, RoleNames.Supervisor);
  const [measuring, setMeasuring] = useState<RollDto | RollSummaryDto | null>(null);
  const [confirm, setConfirm] = useState<ConfirmRequest | null>(null);

  const [logging, setLogging] = useState<StartableLine | null>(null);
  const [justLogged, setJustLogged] = useState<RollDto | null>(null);

  // The labels to print. Set the moment a roll is weighed and measured, so the sticker
  // comes up by itself with the weight and length on it (section 19.15) — nobody should
  // have to go and find a roll in a list to label it.
  const [printing, setPrinting] = useState<{
    barcodes: string[];
    headline?: string;
  } | null>(null);

  // The extruder's own screen: this shift only. Everything older is in inventory
  // (specification section 19.6).
  const rolls = useQuery({
    queryKey: ['rolls', 'current-shift'],
    queryFn: () => productionApi.rolls(undefined, false, true),
  });

  const recipes = useQuery({
    queryKey: ['recipe-versions', 'all'],
    queryFn: () => recipesApi.versions(),
  });

  const colors = useQuery({
    queryKey: ['colors', 'active'],
    queryFn: () => colorsApi.list(false),
  });

  const products = useQuery({
    queryKey: ['products', 'active'],
    queryFn: () => productsApi.list(false),
  });

  // The factory's running roll number, which goes back to 01 only when reset.
  const rollNumber = useQuery({
    queryKey: ['roll-number'],
    queryFn: () => productionApi.rollNumber(),
  });

  const reset = useMutation({
    mutationFn: () => productionApi.resetRollNumber(),
    onSuccess: (fresh) => {
      queryClient.setQueryData(['roll-number'], fresh);
      void queryClient.invalidateQueries({ queryKey: ['next-roll-code'] });
    },
  });

  // Lines of the open shift that actually mix (specification section 4).
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
    void queryClient.invalidateQueries({ queryKey: ['rolls'] });
    void queryClient.invalidateQueries({ queryKey: ['batches'] });
    void queryClient.invalidateQueries({ queryKey: ['roll-number'] });
    // The single roll the edit and test windows open with.
    void queryClient.invalidateQueries({ queryKey: ['roll'] });
  }

  if (rolls.isPending || recipes.isPending || colors.isPending || products.isPending) {
    return <p className="p-6 text-ink-muted">{t('common.loading')}</p>;
  }

  if (rolls.isError || recipes.isError || colors.isError || products.isError) {
    return <p className="p-6 text-bad">{t('rolls.loadFailed1')}</p>;
  }

  // A draft may still change, so a roll could never be reproduced from it.
  const usableRecipes = recipes.data.filter((r) => r.status !== 'Draft');

  // The list is this shift's rolls, newest first (section 19.6), so the product the
  // operator last used is simply the first roll's. No second request, and it follows
  // the shift: the first roll of a new shift has nothing before it and asks.
  const lastProductId = rolls.data.find((r) => r.productId !== null)?.productId ?? null;
  const lines = openLines.data ?? [];

  return (
    <>
      <PageHeader
        title={t('page.rolls.title')}
        subtitle={t('page.rolls.subtitle')}
        actions={
          <div className="flex flex-wrap items-center gap-3">
            {rollNumber.data !== undefined && (
              <span className="text-sm text-ink-muted">
                {t('rolls.nextNumber')}{' '}
                <strong className="font-mono text-base text-ink">
                  {String(rollNumber.data.nextNumber).padStart(2, '0')}
                </strong>
              </span>
            )}
            {canReset && (
              <button
                type="button"
                className="h-touch rounded-control border border-line px-4 text-sm font-semibold text-ink-soft transition-colors hover:border-brand-200 hover:bg-brand-50 hover:text-brand-700"
                disabled={reset.isPending}
                onClick={() => {
                  setConfirm({
                    title: t('rolls.resetTitle'),
                    message: t('rolls.resetMessage'),
                    confirmLabel: t('rolls.resetNumber'),
                    onConfirm: () => {
                      reset.mutate();
                    },
                  });
                }}
              >
                {t('rolls.resetNumber')}
              </button>
            )}
            {canProduce && (
              <StartOnLineButton
                lines={lines}
                action="Log a roll"
                onStart={(shiftLineId) => {
                  const line = lines.find((l) => l.shiftLineId === shiftLineId);
                  if (line !== undefined) {
                    setLogging(line);
                  }
                }}
              />
            )}
          </div>
        }
      />

      {canProduce && lines.length === 0 && (
        <p className="mb-4 rounded-control border border-line bg-canvas px-4 py-3 text-sm text-ink-soft">
          {t('rolls.noShift')}
        </p>
      )}

      {justLogged !== null && (
        <p className="mb-4 flex flex-wrap items-center gap-x-3 gap-y-2 rounded-control border border-s-4 border-ok/30 border-s-ok bg-ok-soft px-4 py-3 text-sm font-medium text-ok">
          <span>
            {t('term.roll')} <strong className="font-mono">{justLogged.rollCode}</strong>{' '}
            {t('rolls.loggedMeasureNext')}
          </span>
          {canMeasure && (
            <button
              type="button"
              className="min-h-9 rounded-control border border-brand-600 bg-brand-600 px-3 text-sm font-semibold text-white hover:bg-brand-700"
              onClick={() => {
                setMeasuring(justLogged);
              }}
            >
              {t('rolls.measure')}
            </button>
          )}
        </p>
      )}

      <div className="card overflow-x-auto">
        <table className="w-full text-start text-sm">
          <thead>
            <tr className="border-b border-line text-xs tracking-wider text-ink-muted uppercase">
              <th className="px-4 py-3 font-semibold">{t('term.rollCode')}</th>
              <th className="px-4 py-3 font-semibold">{t('term.recipe')}</th>
              <th className="px-4 py-3 font-semibold">{t('rolls.madeFor')}</th>
              <th className="px-4 py-3 font-semibold">{t('term.colour')}</th>
              <th className="px-4 py-3 font-semibold">{t('field.status')}</th>
              <th className="px-4 py-3 text-end font-semibold">{t('field.weight')}</th>
              <th className="px-4 py-3 text-end font-semibold">{t('field.length')}</th>
              <th className="px-4 py-3 font-semibold">{t('rolls.madeBy')}</th>
              <th className="px-4 py-3 font-semibold">{t('rolls.outAt')}</th>
              <th className="px-4 py-3" />
            </tr>
          </thead>
          <tbody>
            {rolls.data.length === 0 && (
              <tr>
                <td colSpan={11} className="px-4 py-8 text-center text-ink-muted">
                  {t('rolls.none')}
                </td>
              </tr>
            )}
            {rolls.data.map((roll) => (
              <tr key={roll.id} className="border-b border-line last:border-0">
                <td className="px-4 py-3">
                  {/* One code, which is the barcode too (section 19.12). The label is
                      reachable from the roll itself, not only from the banner that
                      appears once when it is logged. */}
                  <button
                    type="button"
                    title={t('rolls.printLabel')}
                    className="font-mono font-semibold whitespace-nowrap text-ink underline-offset-2 hover:text-brand-700 hover:underline"
                    onClick={() => {
                      setPrinting({ barcodes: [roll.barcode] });
                    }}
                  >
                    {roll.rollCode}
                  </button>
                </td>
                <td className="px-4 py-3 text-ink-soft">
                  {roll.recipeNumber}
                  <span className="ms-2 text-xs text-ink-muted">
                    {roll.recipeFamilyName}
                  </span>
                </td>
                <td className="px-4 py-3 text-ink-soft">
                  {roll.productName ?? '—'}
                  <ThicknessVerdict inSpec={roll.thicknessInSpec} />
                </td>
                <td className="px-4 py-3 text-ink-soft">{roll.colorName}</td>
                <td className="px-4 py-3">
                  <RollStatusBadge
                    status={roll.status}
                    needsTest={roll.needsTest}
                    needsMeasure={roll.needsMeasure}
                  />
                </td>
                <td className="px-4 py-3 text-end tabular-nums text-ink-soft">
                  {roll.weight ?? '—'}
                </td>
                <td className="px-4 py-3 text-end tabular-nums text-ink-soft">
                  {roll.length ?? '—'}
                </td>
                <td className="px-4 py-3 text-ink-soft">{roll.producedByName}</td>
                <td className="px-4 py-3 whitespace-nowrap text-ink-muted">
                  {/* The time only; the date is kept behind it (section 19.12). */}
                  {new Date(roll.producedAt).toLocaleTimeString('en-GB', {
                    hour: '2-digit',
                    minute: '2-digit',
                  })}
                </td>
                <td className="px-4 py-3 text-end whitespace-nowrap">
                  {/* The label, whenever it is wanted — torn, lost, or printed before
                      the weight was in (section 19.15). */}
                  <button
                    type="button"
                    className="me-2 min-h-9 rounded-control border border-line px-3 text-sm font-medium whitespace-nowrap text-ink-soft transition-colors hover:border-brand-200 hover:bg-brand-50 hover:text-brand-700"
                    onClick={() => {
                      setPrinting({ barcodes: [roll.barcode] });
                    }}
                  >
                    {t('rolls.label')}
                  </button>
                  {canMeasure &&
                    (roll.status === 'NeedsTest' || roll.status === 'Available') && (
                      <button
                        type="button"
                        className={[
                          'me-2 min-h-9 rounded-control border px-3 text-sm font-medium whitespace-nowrap transition-colors',
                          roll.needsMeasure
                            ? 'border-brand-600 bg-brand-600 text-white hover:bg-brand-700'
                            : 'border-line text-ink-soft hover:border-brand-200 hover:bg-brand-50 hover:text-brand-700',
                        ].join(' ')}
                        onClick={() => {
                          setMeasuring(roll);
                        }}
                      >
                        {t('rolls.measure')}
                      </button>
                    )}
                  {canCorrect && (
                    <button
                      type="button"
                      className="min-h-9 rounded-control border border-line px-3 text-sm font-medium whitespace-nowrap text-ink-soft transition-colors hover:border-brand-200 hover:bg-brand-50 hover:text-brand-700"
                      onClick={() => {
                        setCorrecting(roll);
                      }}
                    >
                      {t('action.edit')}
                    </button>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {logging !== null && (
        <NewRollDialog
          shiftLine={logging}
          recipes={usableRecipes}
          colors={colors.data}
          products={products.data}
          lastProductId={lastProductId}
          onClose={() => {
            setLogging(null);
          }}
          onCreated={(roll) => {
            // No label yet: it carries the weight and length, which come next.
            setJustLogged(roll);
            invalidate();
          }}
        />
      )}

      {measuring !== null && (
        <MeasureRollDialog
          roll={measuring}
          onClose={() => {
            setMeasuring(null);
          }}
          onSaved={(roll) => {
            // Weighed and measured: now the label has everything to print. Measured
            // again, the old label shows the old figures and is replaced.
            const again = measuring.weight !== null;
            setJustLogged(null);
            setPrinting({
              barcodes: [roll.barcode],
              headline: `${t('term.roll')} ${roll.rollCode} — ${t(
                again ? 'rolls.remeasuredPrint' : 'rolls.measuredPrint',
              )}`,
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

      {correcting !== null && (
        <EditRollDialog
          roll={correcting}
          recipes={usableRecipes}
          colors={colors.data}
          products={products.data}
          onClose={() => {
            setCorrecting(null);
          }}
          onSaved={(roll, labelChanged) => {
            invalidate();
            // The label shows the code, the colour and the product, and for a roll logged
            // since 19.12 the barcode is the code — so any of them changing needs a new label.
            if (labelChanged) {
              setPrinting({
                barcodes: [roll.barcode],
                headline: `${t('term.roll')} ${roll.rollCode} — ${t('rolls.changedPrint')}`,
              });
            }
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

import { useQuery, useQueryClient } from '@tanstack/react-query';
import { useState, type ReactElement } from 'react';
import { useTranslation } from '../../hooks/useTranslation';
import { PageHeader } from '../../components/ui/PageHeader';
import {
  StartOnLineButton,
  type StartableLine,
} from '../../components/ui/StartOnLineButton';
import { useAuth } from '../../hooks/useAuth';
import { RoleNames } from '../../lib/roles';
import { LabelPrintScreen } from '../labels/LabelPrintScreen';
import { colorsApi } from '../master-data/api';
import { recipesApi } from '../recipes/api';
import { shiftReportsApi } from '../shifts/api';
import { formatDate } from '../shifts/shiftFormat';
import { productionApi, type RollDto } from './api';
import { NewRollDialog } from './NewRollDialog';
import { RollStatusBadge } from './RollStatusBadge';

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

  const [logging, setLogging] = useState<StartableLine | null>(null);
  const [justLogged, setJustLogged] = useState<RollDto | null>(null);

  // The labels to print. Set the moment a roll is logged, so the sticker comes up by
  // itself — nobody should have to go and find a roll in a list to label it.
  const [printing, setPrinting] = useState<{
    barcodes: string[];
    headline?: string;
  } | null>(null);

  const rolls = useQuery({
    queryKey: ['rolls', 'all'],
    queryFn: () => productionApi.rolls(),
  });

  const recipes = useQuery({
    queryKey: ['recipe-versions', 'all'],
    queryFn: () => recipesApi.versions(),
  });

  const colors = useQuery({
    queryKey: ['colors', 'active'],
    queryFn: () => colorsApi.list(false),
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
  }

  if (rolls.isPending || recipes.isPending || colors.isPending) {
    return <p className="p-6 text-ink-muted">Loading…</p>;
  }

  if (rolls.isError || recipes.isError || colors.isError) {
    return <p className="p-6 text-bad">Could not load line 1.</p>;
  }

  // A draft may still change, so a roll could never be reproduced from it.
  const usableRecipes = recipes.data.filter((r) => r.status !== 'Draft');
  const lines = openLines.data ?? [];

  return (
    <>
      <PageHeader
        title={t('page.rolls.title')}
        subtitle={t('page.rolls.subtitle')}
        actions={
          canProduce ? (
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
          ) : undefined
        }
      />

      {canProduce && lines.length === 0 && (
        <p className="mb-4 rounded-control border border-line bg-canvas px-4 py-3 text-sm text-ink-soft">
          No shift is open, so a roll cannot be logged.
        </p>
      )}

      {justLogged !== null && (
        <p className="mb-4 rounded-control border border-s-4 border-ok/30 border-s-ok bg-ok-soft px-4 py-3 text-sm font-medium text-ok">
          {t('term.roll')} <strong className="font-mono">{justLogged.rollCode}</strong> logged —
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

      <div className="card overflow-x-auto">
        <table className="w-full text-start text-sm">
          <thead>
            <tr className="border-b border-line text-xs tracking-wider text-ink-muted uppercase">
              <th className="px-4 py-3 font-semibold">{t('term.rollCode')}</th>
              <th className="px-4 py-3 font-semibold">{t('term.barcode')}</th>
              <th className="px-4 py-3 font-semibold">{t('term.recipe')}</th>
              <th className="px-4 py-3 font-semibold">{t('term.colour')}</th>
              <th className="px-4 py-3 font-semibold">{t('field.status')}</th>
              <th className="px-4 py-3 text-end font-semibold">{t('field.weight')}</th>
              <th className="px-4 py-3 text-end font-semibold">{t('field.length')}</th>
              <th className="px-4 py-3 font-semibold">Made by</th>
              <th className="px-4 py-3 font-semibold">Out at</th>
            </tr>
          </thead>
          <tbody>
            {rolls.data.length === 0 && (
              <tr>
                <td colSpan={9} className="px-4 py-8 text-center text-ink-muted">
                  No rolls yet.
                </td>
              </tr>
            )}
            {rolls.data.map((roll) => (
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
                  <span className="ms-2 text-xs text-ink-muted">
                    {roll.recipeFamilyName}
                  </span>
                </td>
                <td className="px-4 py-3 text-ink-soft">{roll.colorName}</td>
                <td className="px-4 py-3">
                  <RollStatusBadge status={roll.status} />
                </td>
                <td className="px-4 py-3 text-end tabular-nums text-ink-soft">
                  {roll.weight ?? '—'}
                </td>
                <td className="px-4 py-3 text-end tabular-nums text-ink-soft">
                  {roll.length ?? '—'}
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

      {logging !== null && (
        <NewRollDialog
          shiftLine={logging}
          recipes={usableRecipes}
          colors={colors.data}
          onClose={() => {
            setLogging(null);
          }}
          onCreated={(roll) => {
            setJustLogged(roll);
            setPrinting({
              barcodes: [roll.barcode],
              headline: `Roll ${roll.rollCode} logged. Print this and stick it on the roll.`,
            });
            invalidate();
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

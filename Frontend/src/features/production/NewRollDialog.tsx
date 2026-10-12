import { useQuery } from '@tanstack/react-query';
import { useState, type ReactElement } from 'react';
import { useTranslation } from '../../hooks/useTranslation';
import { Modal } from '../../components/ui/Modal';
import { ApiError } from '../../lib/apiClient';
import type { ColorDto, ProductDto } from '../master-data/api';
import type { RecipeVersionSummaryDto } from '../recipes/api';
import { productionApi, type RollDto } from './api';
import { fromOutTimeField, toOutTimeField } from './outTime';

interface NewRollDialogProps {
  /** The extruder's part of the open shift. The mix underneath is created by the
   *  first roll and never mentioned (specification section 8). */
  shiftLine: { shiftLineId: number; lineName: string; shiftLabel: string };
  /** Only recipes in production — a draft may still change. */
  recipes: RecipeVersionSummaryDto[];
  colors: ColorDto[];
  /** Active products. Narrowed here to the ones the chosen recipe can make. */
  products: ProductDto[];
  /**
   * The product of the last roll logged on this shift, or null if this is the first.
   * The operator picks the product once and then only when it changes — so the
   * first roll of a shift asks, and every roll after it is already filled in.
   */
  lastProductId: number | null;
  onClose: () => void;
  onCreated: (roll: RollDto) => void;
}

/**
 * Logs a roll off the extruder (specification section 8).
 *
 * The recipe and colour are asked per roll rather than taken from the batch, because
 * both change while a mix is still running: the colouring agent is fed separately at
 * the extruder, so the operator switches colour without stopping.
 *
 * The roll code is worked out and shown already filled in, so nobody has to type it.
 * It can still be typed over, for a roll labelled by hand before it was logged. The
 * barcode is never asked for: it names the roll, whatever code is written on it.
 */
/** " — 2.8–3.0 mm", " — 3.0 mm or more", or nothing when no range is set. */
function thicknessRange(min: number | null, max: number | null): string {
  if (min !== null && max !== null) {
    return ` — ${String(min)}–${String(max)} mm`;
  }
  if (min !== null) {
    return ` — ≥ ${String(min)} mm`;
  }
  if (max !== null) {
    return ` — ≤ ${String(max)} mm`;
  }
  return '';
}

export function NewRollDialog({
  shiftLine,
  recipes,
  colors,
  products,
  lastProductId,
  onClose,
  onCreated,
}: NewRollDialogProps): ReactElement {
  const { t } = useTranslation();
  const [recipeVersionId, setRecipeVersionId] = useState(() => recipes[0]?.id ?? 0);
  const [colorId, setColorId] = useState(() => colors[0]?.id ?? 0);

  // Deliberately no default on the first roll of a shift. Quietly picking the first
  // product in the list would turn a forgotten choice into a wrong declaration, and a
  // wrong declaration is refused at the thermo an hour later, far from where it happened.
  const [productId, setProductId] = useState(() => lastProductId ?? 0);

  // Each product is made from one main recipe and a roll for any other is refused, so
  // only the products this recipe makes are offered at all. A product not yet given a
  // main recipe falls back to matching on absorbency, as the server does.
  const recipe = recipes.find((r) => r.id === recipeVersionId);
  const fitting = products.filter((p) =>
    recipe === undefined
      ? true
      : p.recipeFamilyId === null
        ? p.isAbsorbent === recipe.isAbsorbent
        : p.recipeFamilyId === recipe.recipeFamilyId,
  );
  const chosen = fitting.some((p) => p.id === productId) ? productId : 0;
  const [notes, setNotes] = useState('');

  // The code the system would give. It follows the recipe and colour until somebody
  // types over it; from then on what he typed is kept.
  const suggestion = useQuery({
    queryKey: ['next-roll-code', shiftLine.shiftLineId, recipeVersionId, colorId],
    queryFn: () =>
      productionApi.nextRollCode(shiftLine.shiftLineId, recipeVersionId, colorId),
    enabled: recipeVersionId !== 0 && colorId !== 0,
  });
  const [typedCode, setTypedCode] = useState<string | null>(null);
  const rollCode = typedCode ?? suggestion.data ?? '';

  // Now, unless he changes it — a roll logged a few minutes after it came off.
  const [outAt, setOutAt] = useState(() => toOutTimeField(new Date()));
  const [outAtChanged, setOutAtChanged] = useState(false);

  const [error, setError] = useState<string | null>(null);
  const [isSaving, setIsSaving] = useState(false);

  async function save(): Promise<void> {
    setError(null);
    setIsSaving(true);
    try {
      const roll = await productionApi.createRoll({
        shiftLineId: shiftLine.shiftLineId,
        recipeVersionId,
        colorId,
        productId: chosen === 0 ? null : chosen,
        // Null means now. He is usually standing at the machine.
        producedAt: outAtChanged ? fromOutTimeField(outAt) : null,
        notes: notes.trim() === '' ? null : notes.trim(),
        // Sent only when typed over. Otherwise the server works it out again at the
        // moment of saving, in case another roll took the suggested number meanwhile.
        rollCode:
          typedCode !== null && typedCode.trim() !== (suggestion.data ?? '')
            ? typedCode.trim()
            : null,
      });
      onCreated(roll);
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
      title={`Log a roll — ${shiftLine.lineName}, ${shiftLine.shiftLabel}`}
      onClose={onClose}
    >
      <form
        onSubmit={(event) => {
          event.preventDefault();
          void save();
        }}
        noValidate
      >
        <div className="mb-4">
          <label className="field-label" htmlFor="roll-recipe">
            {t('term.recipe')}
          </label>
          <select
            id="roll-recipe"
            className="field-input"
            value={recipeVersionId}
            disabled={isSaving}
            onChange={(event) => {
              setRecipeVersionId(Number(event.target.value));
            }}
          >
            {recipes.map((recipe) => (
              <option key={recipe.id} value={recipe.id}>
                {recipe.recipeNumber} — {recipe.familyName}
              </option>
            ))}
          </select>
          <p className="mt-1 text-xs text-ink-muted">
            Asked per roll, not once for the shift: the recipe can change while the same
            mix is running.
          </p>
        </div>

        <div className="mb-4">
          <label className="field-label" htmlFor="roll-product">
            {t('rolls.madeFor')}
          </label>
          <select
            id="roll-product"
            className="field-input"
            value={chosen}
            disabled={isSaving}
            onChange={(event) => {
              setProductId(Number(event.target.value));
            }}
          >
            <option value={0}>{t('action.choose')}</option>
            {fitting.map((product) => (
              <option key={product.id} value={product.id}>
                {product.name}
                {thicknessRange(product.minThickness, product.maxThickness)}
              </option>
            ))}
          </select>
          {lastProductId !== null && chosen === lastProductId && (
            <p className="mt-1 text-xs text-ink-muted">{t('rolls.sameAsLast')}</p>
          )}
        </div>

        <div className="mb-4">
          <label className="field-label" htmlFor="roll-colour">
            {t('term.colour')}
          </label>
          <select
            id="roll-colour"
            className="field-input"
            value={colorId}
            disabled={isSaving}
            onChange={(event) => {
              setColorId(Number(event.target.value));
            }}
          >
            {colors.map((colour) => (
              <option key={colour.id} value={colour.id}>
                {colour.name} ({colour.code})
              </option>
            ))}
          </select>
        </div>

        <div className="mb-4">
          <label className="field-label" htmlFor="roll-notes">
            {t('field.note')}{' '}
            <span className="font-normal text-ink-muted">(optional)</span>
          </label>
          <input
            id="roll-notes"
            className="field-input"
            maxLength={300}
            value={notes}
            disabled={isSaving}
            onChange={(event) => {
              setNotes(event.target.value);
            }}
          />
        </div>

        <div className="grid gap-4 sm:grid-cols-2">
          <div className="mb-4">
            <label className="field-label" htmlFor="roll-code">
              {t('term.rollCode')}
            </label>
            <input
              id="roll-code"
              className="field-input font-mono"
              maxLength={30}
              autoComplete="off"
              value={rollCode}
              disabled={isSaving}
              onChange={(event) => {
                setTypedCode(event.target.value);
              }}
            />
            <p className="mt-1 text-xs text-ink-muted">{t('rolls.codeHint')}</p>
          </div>

          <div className="mb-4">
            <label className="field-label" htmlFor="roll-out">
              {t('rolls.outAt')}
            </label>
            <input
              id="roll-out"
              type="datetime-local"
              className="field-input"
              value={outAt}
              disabled={isSaving}
              onChange={(event) => {
                setOutAt(event.target.value);
                setOutAtChanged(true);
              }}
            />
            <p className="mt-1 text-xs text-ink-muted">{t('rolls.outAtHint')}</p>
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

        <button
          type="submit"
          className="btn-primary"
          disabled={isSaving || recipeVersionId === 0 || colorId === 0 || chosen === 0}
        >
          {isSaving ? 'Logging…' : t('rolls.logTheRoll')}
        </button>
      </form>
    </Modal>
  );
}

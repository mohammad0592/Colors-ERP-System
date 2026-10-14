import { useQuery } from '@tanstack/react-query';
import { useState, type ReactElement } from 'react';
import { Modal } from '../../components/ui/Modal';
import { useTranslation } from '../../hooks/useTranslation';
import { ApiError } from '../../lib/apiClient';
import type { ColorDto, ProductDto } from '../master-data/api';
import type { RecipeVersionSummaryDto } from '../recipes/api';
import { productionApi, type RollDto, type RollSummaryDto } from './api';
import { fromTimeField, toOffsetIso, toTimeField } from './outTime';
import { productsFor, thicknessRange } from './rollOptions';

interface EditRollDialogProps {
  roll: RollSummaryDto;
  /** Recipes in production. The roll's own is offered too, even if it is not. */
  recipes: RecipeVersionSummaryDto[];
  colors: ColorDto[];
  products: ProductDto[];
  onClose: () => void;
  /** With whether anything printed on the label changed, so the screen can offer a new one. */
  onSaved: (roll: RollDto, labelChanged: boolean) => void;
}

/**
 * Corrects anything chosen when the roll was logged — the recipe, the product, the colour,
 * the out time, the code and the note (specification section 19.15). Weight and length
 * are corrected with Measure, which prints the label again.
 *
 * The code follows the corrections: it is worked out again from the roll's own number and
 * the new recipe, time and colour, and shown before saving. Typed over, what was typed is
 * kept. For a roll whose barcode is its code, the barcode follows the code too.
 */
export function EditRollDialog({
  roll,
  recipes,
  colors,
  products,
  onClose,
  onSaved,
}: EditRollDialogProps): ReactElement {
  const { t } = useTranslation();

  // The note is not in the list, so the whole roll is fetched for it.
  const full = useQuery({
    queryKey: ['roll', roll.id],
    queryFn: () => productionApi.roll(roll.id),
  });

  return (
    <Modal title={`${t('rolls.editTitle')} — ${roll.rollCode}`} onClose={onClose}>
      {full.isPending ? (
        <p className="text-ink-muted">{t('common.loading')}</p>
      ) : full.isError ? (
        <p className="text-bad">{t('rolls.loadFailed')}</p>
      ) : (
        <EditRollForm
          roll={full.data}
          recipes={recipes}
          colors={colors}
          products={products}
          onClose={onClose}
          onSaved={onSaved}
        />
      )}
    </Modal>
  );
}

function EditRollForm({
  roll,
  recipes,
  colors,
  products,
  onClose,
  onSaved,
}: Omit<EditRollDialogProps, 'roll'> & { roll: RollDto }): ReactElement {
  const { t } = useTranslation();
  const [recipeVersionId, setRecipeVersionId] = useState(roll.recipeVersionId);
  const [colorId, setColorId] = useState(roll.colorId);
  const [productId, setProductId] = useState(roll.productId ?? 0);
  const [notes, setNotes] = useState(roll.notes ?? '');
  // The time only; the date stays the day the roll came off (section 19.12).
  const originalTime = toTimeField(new Date(roll.producedAt));
  const [outAt, setOutAt] = useState(originalTime);
  const outMoment = fromTimeField(outAt, new Date(roll.producedAt));
  const outAtIso = outMoment === null ? null : toOffsetIso(outMoment);

  // What the roll was made to stays on offer, even retired or switched off since: the
  // roll was made to it. The server accepts it unchanged and refuses it as a change.
  const inProduction = recipes.map((r) => ({
    id: r.id,
    label: `${String(r.recipeNumber)} — ${r.familyName}`,
  }));
  const recipeOptions = recipes.some((r) => r.id === roll.recipeVersionId)
    ? inProduction
    : [
        {
          id: roll.recipeVersionId,
          label: `${String(roll.recipeNumber)} — ${roll.recipeFamilyName}`,
        },
        ...inProduction,
      ];
  const colourOptions = colors.some((c) => c.id === roll.colorId)
    ? colors.map((c) => ({ id: c.id, label: `${c.name} (${c.code})` }))
    : [
        { id: roll.colorId, label: roll.colorName },
        ...colors.map((c) => ({ id: c.id, label: `${c.name} (${c.code})` })),
      ];

  const recipe = recipes.find((r) => r.id === recipeVersionId);
  const fitting = productsFor(products, recipe).map((p) => ({
    id: p.id,
    label: `${p.name}${thicknessRange(p.minThickness, p.maxThickness)}`,
  }));
  const productOptions =
    roll.productId !== null &&
    recipeVersionId === roll.recipeVersionId &&
    !fitting.some((p) => p.id === roll.productId)
      ? [{ id: roll.productId, label: roll.productName ?? '' }, ...fitting]
      : fitting;
  const chosenProduct = productOptions.some((p) => p.id === productId) ? productId : 0;

  // The code follows what it is built from — recipe, time and colour — until somebody
  // types over it. Unchanged, the roll keeps the code it has.
  const partsChanged =
    recipeVersionId !== roll.recipeVersionId ||
    colorId !== roll.colorId ||
    outAt !== originalTime;
  const suggestion = useQuery({
    queryKey: ['roll-code-for', roll.id, recipeVersionId, colorId, outAtIso],
    queryFn: () =>
      productionApi.nextRollCode(
        { rollId: roll.id },
        recipeVersionId,
        colorId,
        outAtIso ?? roll.producedAt,
      ),
    enabled: partsChanged && outAtIso !== null,
  });
  const [typedCode, setTypedCode] = useState<string | null>(null);
  const workedOut = partsChanged ? (suggestion.data ?? '') : roll.rollCode;
  const rollCode = typedCode ?? workedOut;

  const [error, setError] = useState<string | null>(null);
  const [isSaving, setIsSaving] = useState(false);

  async function save(): Promise<void> {
    if (outAtIso === null) {
      setError(t('rolls.outAtHint'));
      return;
    }

    setError(null);
    setIsSaving(true);
    try {
      const saved = await productionApi.updateRoll(roll.id, {
        // Typed over: what was typed. Otherwise the server works it out again from the
        // roll's number, or keeps the code it has when nothing it is built from changed.
        rollCode:
          typedCode !== null && typedCode.trim() !== workedOut
            ? typedCode.trim()
            : partsChanged
              ? null
              : roll.rollCode,
        producedAt: outAtIso,
        recipeVersionId,
        colorId,
        productId: chosenProduct === 0 ? null : chosenProduct,
        notes: notes.trim() === '' ? null : notes.trim(),
      });
      // The label prints the code, the colour and the product.
      onSaved(
        saved,
        saved.rollCode !== roll.rollCode ||
          saved.colorId !== roll.colorId ||
          saved.productId !== roll.productId,
      );
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
    <form
      onSubmit={(event) => {
        event.preventDefault();
        void save();
      }}
      noValidate
    >
      <div className="mb-4">
        <label className="field-label" htmlFor="edit-roll-recipe">
          {t('term.recipe')}
        </label>
        <select
          id="edit-roll-recipe"
          className="field-input"
          value={recipeVersionId}
          disabled={isSaving}
          onChange={(event) => {
            setRecipeVersionId(Number(event.target.value));
          }}
        >
          {recipeOptions.map((option) => (
            <option key={option.id} value={option.id}>
              {option.label}
            </option>
          ))}
        </select>
      </div>

      <div className="mb-4">
        <label className="field-label" htmlFor="edit-roll-product">
          {t('rolls.madeFor')}
        </label>
        <select
          id="edit-roll-product"
          className="field-input"
          value={chosenProduct}
          disabled={isSaving}
          onChange={(event) => {
            setProductId(Number(event.target.value));
          }}
        >
          <option value={0}>{t('action.choose')}</option>
          {productOptions.map((option) => (
            <option key={option.id} value={option.id}>
              {option.label}
            </option>
          ))}
        </select>
      </div>

      <div className="mb-4">
        <label className="field-label" htmlFor="edit-roll-colour">
          {t('term.colour')}
        </label>
        <select
          id="edit-roll-colour"
          className="field-input"
          value={colorId}
          disabled={isSaving}
          onChange={(event) => {
            setColorId(Number(event.target.value));
          }}
        >
          {colourOptions.map((option) => (
            <option key={option.id} value={option.id}>
              {option.label}
            </option>
          ))}
        </select>
      </div>

      <div className="grid gap-4 sm:grid-cols-2">
        <div className="mb-4">
          <label className="field-label" htmlFor="edit-roll-out">
            {t('rolls.outAt')}
          </label>
          <input
            id="edit-roll-out"
            type="time"
            className="field-input"
            value={outAt}
            disabled={isSaving}
            onChange={(event) => {
              setOutAt(event.target.value);
            }}
          />
        </div>

        <div className="mb-4">
          <label className="field-label" htmlFor="edit-roll-code">
            {t('term.rollCode')}
          </label>
          <input
            id="edit-roll-code"
            className="field-input font-mono"
            maxLength={50}
            autoComplete="off"
            value={rollCode}
            disabled={isSaving}
            onChange={(event) => {
              setTypedCode(event.target.value);
            }}
          />
        </div>
      </div>

      <div className="mb-4">
        <label className="field-label" htmlFor="edit-roll-notes">
          {t('field.note')} <span className="font-normal text-ink-muted">(optional)</span>
        </label>
        <input
          id="edit-roll-notes"
          className="field-input"
          maxLength={300}
          value={notes}
          disabled={isSaving}
          onChange={(event) => {
            setNotes(event.target.value);
          }}
        />
      </div>

      <p className="mb-4 rounded-control bg-canvas px-4 py-3 text-sm text-ink-soft">
        {t('rolls.editNote')}
      </p>

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
        disabled={
          isSaving ||
          rollCode.trim() === '' ||
          (chosenProduct === 0 && roll.productId !== null)
        }
      >
        {isSaving ? t('common.saving') : t('common.save')}
      </button>
    </form>
  );
}

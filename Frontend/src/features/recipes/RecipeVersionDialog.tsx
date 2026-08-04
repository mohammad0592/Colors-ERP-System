import { useState, type ReactElement } from 'react';
import { Modal } from '../../components/ui/Modal';
import { ApiError } from '../../lib/apiClient';
import type { MaterialDto } from '../master-data/api';
import {
  recipesApi,
  type RecipeFamilyDto,
  type RecipeVersionDto,
  type SaveRecipeIngredient,
} from './api';
import { IngredientEditor } from './IngredientEditor';
import { emptyRow, type IngredientRow } from './ingredientRow';
import { RecipeStatusBadge } from './RecipeStatusBadge';

interface RecipeVersionDialogProps {
  /** The version being opened, or null when writing a new one. */
  version: RecipeVersionDto | null;
  families: RecipeFamilyDto[];
  materials: MaterialDto[];
  onClose: () => void;
  onSaved: () => void;
}

/**
 * Writes a recipe, or shows a frozen one read-only.
 *
 * A Current or Archived version can be looked at but never changed: rolls point at
 * it, and the formula that made them must stay true (specification section 5). The
 * dialog says so plainly and offers the copy path instead.
 */
export function RecipeVersionDialog({
  version,
  families,
  materials,
  onClose,
  onSaved,
}: RecipeVersionDialogProps): ReactElement {
  const isNew = version === null;
  const readOnly = version !== null && !version.isEditable;

  const [familyId, setFamilyId] = useState(
    version === null ? '' : String(version.recipeFamilyId),
  );
  const [notes, setNotes] = useState(version?.notes ?? '');
  const [rows, setRows] = useState<IngredientRow[]>(() =>
    version === null
      ? [emptyRow()]
      : version.ingredients.map((i) => ({
          materialId: String(i.materialId),
          isBaseResin: i.isBaseResin,
          target: String(i.targetPercentage),
          min: String(i.minPercentage),
          max: String(i.maxPercentage),
        })),
  );
  const [error, setError] = useState<string | null>(null);
  const [isSaving, setIsSaving] = useState(false);

  /** The ingredients when the form converts cleanly, or the message to show. */
  function buildIngredients(): SaveRecipeIngredient[] | string {
    if (rows.length === 0) {
      return 'A recipe needs at least one material.';
    }

    const ingredients: SaveRecipeIngredient[] = [];

    for (const row of rows) {
      const materialId = Number(row.materialId);
      if (!Number.isInteger(materialId) || materialId <= 0) {
        return 'Choose a material for every row.';
      }

      const target = Number(row.target);
      const min = Number(row.min);
      const max = Number(row.max);

      if (![target, min, max].every(Number.isFinite)) {
        return 'Every percentage must be a number.';
      }

      ingredients.push({
        materialId,
        isBaseResin: row.isBaseResin,
        targetPercentage: target,
        minPercentage: min,
        maxPercentage: max,
      });
    }

    return ingredients;
  }

  async function save(): Promise<void> {
    const ingredients = buildIngredients();
    if (typeof ingredients === 'string') {
      setError(ingredients);
      return;
    }

    const trimmedNotes = notes.trim() === '' ? null : notes.trim();

    setError(null);
    setIsSaving(true);
    try {
      if (isNew) {
        const family = Number(familyId);
        if (!Number.isInteger(family) || family <= 0) {
          setError('Choose a recipe family.');
          return;
        }

        await recipesApi.createVersion({
          recipeFamilyId: family,
          notes: trimmedNotes,
          ingredients,
        });
      } else {
        await recipesApi.updateVersion(version.id, { notes: trimmedNotes, ingredients });
      }

      onSaved();
      onClose();
    } catch (caught) {
      // The server owns the real rules — the base resin total, frozen versions,
      // duplicate materials — so its message is shown as it is.
      setError(
        caught instanceof ApiError ? caught.message : 'Something went wrong. Try again.',
      );
    } finally {
      setIsSaving(false);
    }
  }

  const title = isNew
    ? 'New recipe'
    : `Recipe ${String(version.recipeNumber)} — ${version.familyName}`;

  return (
    <Modal title={title} onClose={onClose}>
      {version !== null && (
        <div className="mb-4 flex flex-wrap items-center gap-3 text-sm text-ink-muted">
          <RecipeStatusBadge status={version.status} />
          <span>version {version.versionNumber}</span>
          <span>·</span>
          <span>written by {version.createdByName}</span>
        </div>
      )}

      {readOnly && (
        <p className="mb-4 rounded-control border border-line bg-canvas px-4 py-3 text-sm text-ink-soft">
          This recipe is <strong>{version.status.toLowerCase()}</strong> and can no longer
          be changed — the rolls made with it must keep their exact formula. Use{' '}
          <strong>Copy</strong> to try a change under a new recipe number.
        </p>
      )}

      <form
        onSubmit={(event) => {
          event.preventDefault();
          void save();
        }}
        noValidate
      >
        {isNew && (
          <div className="mb-4">
            <label className="field-label" htmlFor="rec-family">
              Recipe family
            </label>
            <select
              id="rec-family"
              className="field-input"
              value={familyId}
              onChange={(e) => {
                setFamilyId(e.target.value);
              }}
              disabled={isSaving}
            >
              <option value="">Choose…</option>
              {families.map((f) => (
                <option key={f.id} value={f.id}>
                  {f.name}
                </option>
              ))}
            </select>
          </div>
        )}

        <div className="mb-5">
          <IngredientEditor
            rows={rows}
            materials={materials}
            disabled={readOnly || isSaving}
            onChange={setRows}
          />
        </div>

        <div className="mb-4">
          <label className="field-label" htmlFor="rec-notes">
            Notes
          </label>
          <input
            id="rec-notes"
            type="text"
            className="field-input"
            placeholder="What changed, and why"
            value={notes}
            onChange={(e) => {
              setNotes(e.target.value);
            }}
            disabled={readOnly || isSaving}
          />
        </div>

        {error !== null && (
          <p
            role="alert"
            className="mb-4 rounded-control border border-l-4 border-bad/30 border-l-bad bg-bad-soft px-4 py-3 text-sm font-medium text-bad"
          >
            {error}
          </p>
        )}

        {!readOnly && (
          <button type="submit" className="btn-primary" disabled={isSaving}>
            {isSaving ? 'Saving…' : isNew ? 'Create draft' : 'Save draft'}
          </button>
        )}
      </form>
    </Modal>
  );
}

import { useState, type ReactElement } from 'react';
import { Modal } from '../../components/ui/Modal';
import { ApiError } from '../../lib/apiClient';
import type { ColorDto } from '../master-data/api';
import type { RecipeVersionSummaryDto } from '../recipes/api';
import { productionApi, type RollDto } from './api';

interface NewRollDialogProps {
  /** The extruder's part of the open shift. The mix underneath is created by the
   *  first roll and never mentioned (specification section 8). */
  shiftLine: { shiftLineId: number; lineName: string; shiftLabel: string };
  /** Only recipes in production — a draft may still change. */
  recipes: RecipeVersionSummaryDto[];
  colors: ColorDto[];
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
 * The roll code and the barcode are not asked for. They are generated — the whole
 * point is that nobody types them.
 */
export function NewRollDialog({
  shiftLine,
  recipes,
  colors,
  onClose,
  onCreated,
}: NewRollDialogProps): ReactElement {
  const [recipeVersionId, setRecipeVersionId] = useState(() => recipes[0]?.id ?? 0);
  const [colorId, setColorId] = useState(() => colors[0]?.id ?? 0);
  const [notes, setNotes] = useState('');
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
        // Null means now. He is usually standing at the machine.
        producedAt: null,
        notes: notes.trim() === '' ? null : notes.trim(),
      });
      onCreated(roll);
      onClose();
    } catch (caught) {
      setError(
        caught instanceof ApiError ? caught.message : 'Something went wrong. Try again.',
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
            Recipe
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
          <label className="field-label" htmlFor="roll-colour">
            Colour
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
            Note <span className="font-normal text-ink-muted">(optional)</span>
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

        <p className="mb-4 rounded-control bg-canvas px-4 py-3 text-sm text-ink-soft">
          The roll code and barcode are printed by the system. Nobody types them, so two
          rolls can never share one.
        </p>

        {error !== null && (
          <p
            role="alert"
            className="mb-4 rounded-control border border-l-4 border-bad/30 border-l-bad bg-bad-soft px-4 py-3 text-sm font-medium text-bad"
          >
            {error}
          </p>
        )}

        <button
          type="submit"
          className="btn-primary"
          disabled={isSaving || recipeVersionId === 0 || colorId === 0}
        >
          {isSaving ? 'Logging…' : 'Log the roll'}
        </button>
      </form>
    </Modal>
  );
}

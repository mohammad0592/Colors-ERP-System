/**
 * One ingredient row while it is being edited.
 *
 * Everything is a string, straight from the inputs: a half-typed "1." is not a
 * number yet, and forcing it through Number() on every keystroke would fight the
 * person typing. Conversion happens once, on save.
 *
 * Kept apart from the editor component so editing that component does not force a
 * full page reload during development.
 */
export interface IngredientRow {
  materialId: string;
  isBaseResin: boolean;
  target: string;
  min: string;
  max: string;
}

export function emptyRow(): IngredientRow {
  return { materialId: '', isBaseResin: false, target: '', min: '', max: '' };
}

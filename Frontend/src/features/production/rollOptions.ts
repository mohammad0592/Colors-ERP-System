import type { ProductDto } from '../master-data/api';
import type { RecipeVersionSummaryDto } from '../recipes/api';

/**
 * The products a recipe can make, for the roll dialogs (specification section 19.1).
 *
 * Each product is made from one main recipe and a roll for any other is refused, so only
 * these are offered at all. A product not yet given a main recipe falls back to matching
 * on absorbency, as the server does.
 */
export function productsFor(
  products: ProductDto[],
  recipe: Pick<RecipeVersionSummaryDto, 'recipeFamilyId' | 'isAbsorbent'> | undefined,
): ProductDto[] {
  return products.filter((p) =>
    recipe === undefined
      ? true
      : p.recipeFamilyId === null
        ? p.isAbsorbent === recipe.isAbsorbent
        : p.recipeFamilyId === recipe.recipeFamilyId,
  );
}

/** " — 2.8–3.0 mm", " — ≥ 3.0 mm", or nothing when no range is set. */
export function thicknessRange(min: number | null, max: number | null): string {
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

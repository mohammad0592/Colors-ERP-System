import type { ProductDto } from '../master-data/api';
import type { RecipeVersionSummaryDto } from '../recipes/api';

/**
 * The products a recipe can make — offered when a roll goes into the thermo, where its
 * product is named (specification section 19.17).
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

import { apiRequest } from '../../lib/apiClient';

/**
 * Recipes, mirroring the C# records in Colors.Application.Features.Recipes.
 * Specification section 5.
 */

/** Draft may be edited; Current and Archived are frozen for ever. */
export type RecipeStatus = 'Draft' | 'Current' | 'Archived';

export interface RecipeFamilyDto {
  id: number;
  name: string;
  productTypeId: number;
  productTypeName: string;
  usesRecycle: boolean;
  /**
   * Which colours it may be made in: a black-only recipe needs black, and every other
   * recipe refuses black.
   */
  blackOnly: boolean;
  isAbsorbent: boolean;
  description: string | null;
  isActive: boolean;
  /** The number of the version in production, when the family has one. */
  currentRecipeNumber: number | null;
  versionCount: number;
}

export interface SaveRecipeFamily {
  name: string;
  productTypeId: number;
  usesRecycle: boolean;
  blackOnly: boolean;
  isAbsorbent: boolean;
  description: string | null;
}

export interface RecipeIngredientDto {
  materialId: number;
  materialCode: string;
  materialName: string;
  /** GPPS and Recycle — the polymer that forms the 100% base. */
  isBaseResin: boolean;
  targetPercentage: number;
  minPercentage: number;
  maxPercentage: number;
}

export interface SaveRecipeIngredient {
  materialId: number;
  isBaseResin: boolean;
  targetPercentage: number;
  minPercentage: number;
  maxPercentage: number;
}

export interface RecipeVersionSummaryDto {
  id: number;
  recipeNumber: number;
  recipeFamilyId: number;
  familyName: string;
  versionNumber: number;
  status: RecipeStatus;
  isEditable: boolean;
  createdByName: string;
  createdAt: string;
  notes: string | null;
  ingredientCount: number;
}

export interface RecipeVersionDto extends Omit<
  RecipeVersionSummaryDto,
  'ingredientCount'
> {
  ingredients: RecipeIngredientDto[];
}

export const recipesApi = {
  families: (includeInactive = false): Promise<RecipeFamilyDto[]> =>
    apiRequest<RecipeFamilyDto[]>(
      `/api/recipes/families?includeInactive=${String(includeInactive)}`,
    ),

  createFamily: (body: SaveRecipeFamily): Promise<RecipeFamilyDto> =>
    apiRequest<RecipeFamilyDto>('/api/recipes/families', { method: 'POST', body }),

  updateFamily: (id: number, body: SaveRecipeFamily): Promise<RecipeFamilyDto> =>
    apiRequest<RecipeFamilyDto>(`/api/recipes/families/${String(id)}`, {
      method: 'PUT',
      body,
    }),

  setFamilyActive: (id: number, isActive: boolean): Promise<RecipeFamilyDto> =>
    apiRequest<RecipeFamilyDto>(`/api/recipes/families/${String(id)}/active`, {
      method: 'PUT',
      body: { isActive },
    }),

  versions: (familyId?: number): Promise<RecipeVersionSummaryDto[]> =>
    apiRequest<RecipeVersionSummaryDto[]>(
      familyId === undefined
        ? '/api/recipes/versions'
        : `/api/recipes/versions?familyId=${String(familyId)}`,
    ),

  version: (id: number): Promise<RecipeVersionDto> =>
    apiRequest<RecipeVersionDto>(`/api/recipes/versions/${String(id)}`),

  createVersion: (body: {
    recipeFamilyId: number;
    notes: string | null;
    ingredients: SaveRecipeIngredient[];
  }): Promise<RecipeVersionDto> =>
    apiRequest<RecipeVersionDto>('/api/recipes/versions', { method: 'POST', body }),

  updateVersion: (
    id: number,
    body: { notes: string | null; ingredients: SaveRecipeIngredient[] },
  ): Promise<RecipeVersionDto> =>
    apiRequest<RecipeVersionDto>(`/api/recipes/versions/${String(id)}`, {
      method: 'PUT',
      body,
    }),

  /** Copies any version into a new draft — the "try a small change" path. */
  copyVersion: (id: number, notes: string | null): Promise<RecipeVersionDto> =>
    apiRequest<RecipeVersionDto>(`/api/recipes/versions/${String(id)}/copy`, {
      method: 'POST',
      body: { notes },
    }),

  /** Puts a draft into production and archives the family's previous one. */
  promoteVersion: (id: number): Promise<RecipeVersionDto> =>
    apiRequest<RecipeVersionDto>(`/api/recipes/versions/${String(id)}/promote`, {
      method: 'POST',
    }),

  deleteDraft: (id: number): Promise<undefined> =>
    apiRequest<undefined>(`/api/recipes/versions/${String(id)}`, { method: 'DELETE' }),
};

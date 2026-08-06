import { apiRequest } from '../../lib/apiClient';

/**
 * Reports, mirroring Colors.Application.Features.Reports.
 * Specification section 13.
 *
 * Every figure here is read from records that already exist. Nothing is stored, so a
 * report cannot disagree with the data underneath it.
 */

export interface MaterialWasteLineDto {
  materialId: number;
  materialCode: string;
  materialName: string;
  unitSymbol: string;
  /** True for the polymer that forms the recipe's 100% base — GPPS and recycle. */
  isBaseResin: boolean;
  issued: number;
  returned: number;
  netUsed: number;
  /** The recipe's target for this material, or null where it names none. */
  targetPercentage: number | null;
  /** Target percentage of the resin actually used. Null where unknown. */
  required: number | null;
  /** Used less required. Positive means more went in than the recipe asks. */
  difference: number | null;
  differencePercentage: number | null;
  /** True where the used share falls outside the min–max the supervisor set. */
  outsideRange: boolean;
}

export interface MaterialWasteReportDto {
  shiftReportId: number;
  productionDate: string;
  shiftName: string;
  status: string;
  recipeNumber: number | null;
  recipeFamilyName: string | null;
  recipeVersionNumber: number | null;
  /** More than one and there is no single requirement to compare against. */
  recipeCount: number;
  resinUsed: number;
  rollsProduced: number;
  rollWeightProduced: number;
  lines: MaterialWasteLineDto[];
}

export interface ShiftProductLineDto {
  productId: number;
  productName: string;
  rollsUsed: number;
  rollWeightUsed: number;
  bagCount: number;
  pieceCount: number;
  /** Pieces × each roll's own measured plate weight, never one shared figure. */
  productWeight: number;
  lossWeight: number;
  lossPercentage: number | null;
}

export interface ShiftSummaryReportDto {
  shiftReportId: number;
  productionDate: string;
  shiftName: string;
  status: string;
  supervisorName: string | null;
  electricityUsed: number | null;
  rollsProduced: number;
  rollWeightProduced: number;
  rollsFormed: number;
  rollWeightUsed: number;
  bagCount: number;
  pieceCount: number;
  productWeight: number;
  lossWeight: number;
  lossPercentage: number | null;
  palletsBuilt: number;
  palletsCompleted: number;
  recycledMaterialProduced: number;
  products: ShiftProductLineDto[];
}

export interface ConsumptionMaterialDto {
  materialId: number;
  materialCode: string;
  materialName: string;
  unitSymbol: string;
  issued: number;
  returned: number;
  netUsed: number;
  /** Used per kilogram of roll, so a long shift and a short one can be compared. */
  perKilogramOfRoll: number | null;
}

export interface ConsumptionGroupDto {
  label: string;
  shiftReportId: number | null;
  productionDate: string | null;
  shiftName: string | null;
  recipeNumber: number | null;
  recipeFamilyName: string | null;
  /** How many shifts are behind this row. Always 1 when grouped by shift. */
  shifts: number;
  rollsProduced: number;
  rollWeightProduced: number;
  totalUsed: number;
  materials: ConsumptionMaterialDto[];
}

export interface ConsumptionReportDto {
  from: string;
  to: string;
  groupedBy: string;
  groups: ConsumptionGroupDto[];
  /** Shifts left out of a by-recipe report because they ran more than one recipe. */
  mixedRecipeShifts: number;
}

export interface PalletProductLineDto {
  productId: number;
  productName: string;
  palletsCompleted: number;
  bags: number;
  pieces: number;
  weight: number;
  /** The product's own bags-per-pallet, for reading the bag count against. */
  bagsPerPallet: number;
}

export interface PalletProductionReportDto {
  from: string;
  to: string;
  palletsStarted: number;
  palletsCompleted: number;
  /** Started and given up on. Their wood went back, so they consumed nothing. */
  palletsCancelled: number;
  /** Started and still being filled — no product until the first bag lands. */
  palletsStillOpen: number;
  products: PalletProductLineDto[];
}

export interface RecycledShiftLineDto {
  shiftReportId: number;
  productionDate: string;
  shiftName: string;
  productionLineName: string;
  produced: number;
  recordedByName: string;
  notes: string | null;
}

export interface RecycledMaterialReportDto {
  from: string;
  to: string;
  materialName: string | null;
  totalProduced: number;
  /** How much the mixer took back out — the black recipes are all that consume it. */
  totalConsumed: number;
  /** Produced less consumed. Negative means the pile is being drawn down. */
  difference: number;
  /** What the store holds now, which no date range changes. */
  inStock: number;
  shifts: RecycledShiftLineDto[];
}

export const reportsApi = {
  materialWaste: (shiftReportId: number): Promise<MaterialWasteReportDto> =>
    apiRequest<MaterialWasteReportDto>(
      `/api/reports/material-waste/${String(shiftReportId)}`,
    ),

  shiftSummary: (shiftReportId: number): Promise<ShiftSummaryReportDto> =>
    apiRequest<ShiftSummaryReportDto>(
      `/api/reports/shift-summary/${String(shiftReportId)}`,
    ),

  consumption: (
    from: string,
    to: string,
    groupBy: 'Shift' | 'Recipe',
  ): Promise<ConsumptionReportDto> =>
    apiRequest<ConsumptionReportDto>(
      `/api/reports/consumption?from=${from}&to=${to}&groupBy=${groupBy}`,
    ),

  palletProduction: (from: string, to: string): Promise<PalletProductionReportDto> =>
    apiRequest<PalletProductionReportDto>(
      `/api/reports/pallet-production?from=${from}&to=${to}`,
    ),

  recycledMaterial: (from: string, to: string): Promise<RecycledMaterialReportDto> =>
    apiRequest<RecycledMaterialReportDto>(
      `/api/reports/recycled-material?from=${from}&to=${to}`,
    ),
};

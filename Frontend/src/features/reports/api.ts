import { apiDownload, apiRequest } from '../../lib/apiClient';

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

/** One roll formed on the shift — a row of the thermoforming form. */
export interface ThermoReportRollDto {
  rollCode: string;
  colorName: string;
  rollWeight: number | null;
  rollLength: number | null;
  averageThickness: number | null;
  totalTimeMinutes: number | null;
  productName: string | null;
  piecesPerBag: number | null;
  /** Null until the bags have been counted. */
  bagCount: number | null;
  pieceCount: number | null;
  /** Grams, as weighed. */
  pieceWeight: number | null;
  bagWeight: number | null;
  isAbsorbent: boolean;
  absorbentPercentage: number | null;
  /** The share of recycled material in the roll's recipe, 0–100. */
  recycledPercentage: number | null;
  /** The shift the roll was made on at the extruder. */
  rollShiftName: string;
  scrapWeight: number | null;
}

export interface ThermoReportProductDto {
  productName: string;
  productCode: string;
  rollWeight: number;
  pieceCount: number;
  productWeight: number;
  scrapWeight: number;
  scrapPercentage: number | null;
}

export interface ThermoReportPalletDto {
  palletNumber: number;
  status: string;
  bagCount: number;
  piecesPerBag: number | null;
  pieceCount: number | null;
  productName: string | null;
  colorName: string | null;
  rollCodes: string[];
}

export interface ThermoReportPackagingDto {
  materialName: string;
  unitSymbol: string;
  quantity: number;
  weight: number | null;
}

/** The thermoforming line's report for one shift (specification section 13). */
export interface ThermoShiftReportDto {
  shiftReportId: number;
  productionDate: string;
  shiftName: string;
  status: string;
  lineName: string;
  operatorName: string | null;
  operatorEmployeeNumber: string | null;
  electricityStartMeter: number | null;
  electricityEndMeter: number | null;
  electricityUsed: number | null;
  cycleTimeSeconds: number | null;
  feedDistanceMm: number | null;
  machineSpeed: number | null;
  productionStartTime: string | null;
  productionEndTime: string | null;
  downtimeHours: number | null;
  actualProductionHours: number | null;
  workers: string[];
  trainees: string[];
  notes: string | null;
  rolls: ThermoReportRollDto[];
  totalRollWeight: number;
  totalBagCount: number;
  totalPieceCount: number;
  products: ThermoReportProductDto[];
  pallets: ThermoReportPalletDto[];
  packagingRecorded: boolean;
  packaging: ThermoReportPackagingDto[];
}

export const reportsApi = {
  materialWaste: (shiftReportId: number): Promise<MaterialWasteReportDto> =>
    apiRequest<MaterialWasteReportDto>(
      `/api/reports/material-waste/${String(shiftReportId)}`,
    ),

  thermoShift: (shiftReportId: number): Promise<ThermoShiftReportDto> =>
    apiRequest<ThermoShiftReportDto>(
      `/api/reports/thermo-shift/${String(shiftReportId)}`,
    ),

  /** The same report as the factory's Excel form, saved by the browser. */
  thermoShiftExcel: (report: ThermoShiftReportDto): Promise<void> =>
    apiDownload(
      `/api/reports/thermo-shift/${String(report.shiftReportId)}/excel`,
      `Thermo report ${report.productionDate} shift ${report.shiftName}.xlsx`,
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

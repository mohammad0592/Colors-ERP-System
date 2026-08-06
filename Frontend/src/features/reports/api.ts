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

export const reportsApi = {
  materialWaste: (shiftReportId: number): Promise<MaterialWasteReportDto> =>
    apiRequest<MaterialWasteReportDto>(
      `/api/reports/material-waste/${String(shiftReportId)}`,
    ),

  shiftSummary: (shiftReportId: number): Promise<ShiftSummaryReportDto> =>
    apiRequest<ShiftSummaryReportDto>(
      `/api/reports/shift-summary/${String(shiftReportId)}`,
    ),
};

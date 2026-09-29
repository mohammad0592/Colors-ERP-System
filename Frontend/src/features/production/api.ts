import { apiRequest } from '../../lib/apiClient';

/**
 * Line 1 — the mixer and the extruder, mirroring
 * Colors.Application.Features.Production. Specification section 8.
 */

export type RollStatus =
  'NeedsTest' | 'Available' | 'InThermo' | 'Processed' | 'Scrapped';

export interface BatchSummaryDto {
  id: number;
  batchNumber: number;
  shiftLineId: number;
  productionLineName: string;
  shiftName: string;
  productionDate: string;
  createdByName: string;
  isFinished: boolean;
  rollCount: number;
  /** Only measured rolls contribute — the rest have no weight yet. */
  totalRollWeight: number | null;
  startedAt: string;
  finishedAt: string | null;
}

export interface RollTestReportDto {
  id: number;
  weight: number;
  length: number;
  plateWeight: number;
  thicknessRs: number;
  thicknessRm: number;
  thicknessLm: number;
  thicknessLs: number;
  /** The mean of the four. Worked out on the server, never stored. */
  averageThickness: number;
  /**
   * Whether that average fell inside the product's range when it was measured, and kept
   * (specification section 19.1). Null where the product had no range — which is not a
   * failure and must never be shown as one.
   */
  thicknessInSpec: boolean | null;
  testedByName: string;
  testedAt: string;
  notes: string | null;
}

export interface RollSummaryDto {
  id: number;
  rollCode: string;
  barcode: string;
  dailySerial: number;
  productionDate: string;
  batchId: number;
  batchNumber: number;
  recipeVersionId: number;
  recipeNumber: number;
  recipeFamilyName: string;
  colorId: number;
  colorName: string;
  status: RollStatus;
  needsTest: boolean;
  producedByName: string;
  producedAt: string;
  weight: number | null;
  length: number | null;
  averageThickness: number | null;
  /** What it was made for. Null only on rolls made before products were declared. */
  productId: number | null;
  productName: string | null;
  /** The range that product asks for. Either end may be missing, and both usually are. */
  minThickness: number | null;
  maxThickness: number | null;
  /** Null until measured, and null for a product with no range. */
  thicknessInSpec: boolean | null;
}

export interface RollDto extends Omit<
  RollSummaryDto,
  'weight' | 'length' | 'averageThickness' | 'thicknessInSpec'
> {
  notes: string | null;
  testReport: RollTestReportDto | null;
}

export const productionApi = {
  batches: (openOnly = false): Promise<BatchSummaryDto[]> =>
    apiRequest<BatchSummaryDto[]>(`/api/production/batches?openOnly=${String(openOnly)}`),

  /**
   * Rolls. `currentShiftOnly` is the line screen's view — this shift and nothing older,
   * because the past belongs to inventory (specification section 19.6).
   */
  rolls: (
    batchId?: number,
    needsTestOnly = false,
    currentShiftOnly = false,
  ): Promise<RollSummaryDto[]> => {
    const query = new URLSearchParams({
      needsTestOnly: String(needsTestOnly),
      currentShiftOnly: String(currentShiftOnly),
    });
    if (batchId !== undefined) {
      query.set('batchId', String(batchId));
    }
    return apiRequest<RollSummaryDto[]>(`/api/production/rolls?${query.toString()}`);
  },

  roll: (id: number): Promise<RollDto> =>
    apiRequest<RollDto>(`/api/production/rolls/${String(id)}`),

  createRoll: (body: {
    shiftLineId: number;
    recipeVersionId: number;
    colorId: number;
    /** What the roll is being made for (section 19.1). Required by the server. */
    productId: number | null;
    producedAt: string | null;
    notes: string | null;
  }): Promise<RollDto> =>
    apiRequest<RollDto>('/api/production/rolls', { method: 'POST', body }),

  /** Saving the measurements is what makes the roll usable by the thermo. */
  saveTest: (
    rollId: number,
    body: {
      weight: number;
      length: number;
      plateWeight: number;
      thicknessRs: number;
      thicknessRm: number;
      thicknessLm: number;
      thicknessLs: number;
      notes: string | null;
    },
  ): Promise<RollDto> =>
    apiRequest<RollDto>(`/api/production/rolls/${String(rollId)}/test`, {
      method: 'POST',
      body,
    }),
};

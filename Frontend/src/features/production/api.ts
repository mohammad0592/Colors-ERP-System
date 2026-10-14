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
  /** Absorbent rolls only: the sample dry and after ten minutes in water, grams. */
  absorbencyDryWeight: number | null;
  absorbencyWetWeight: number | null;
  /** (wet − dry) ÷ dry × 100, worked out on the server. */
  absorbencyPercentage: number | null;
  testedByName: string;
  testedAt: string;
  notes: string | null;
}

export interface RollSummaryDto {
  id: number;
  rollCode: string;
  /** For rolls logged since 19.12, the code itself in capitals. */
  barcode: string;
  rollNumber: number;
  productionDate: string;
  batchId: number;
  batchNumber: number;
  recipeVersionId: number;
  recipeNumber: number;
  recipeFamilyName: string;
  colorId: number;
  colorName: string;
  status: RollStatus;
  /** Still to be done before the thermo may take it, in either order. */
  needsTest: boolean;
  needsMeasure: boolean;
  producedByName: string;
  producedAt: string;
  /** Measured on the production screen. Null until then. */
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
  /** Its recipe is absorbent, so the test asks for the absorbency (section 19.14). */
  isAbsorbent: boolean;
  absorbencyPercentage: number | null;
}

export interface RollDto extends Omit<
  RollSummaryDto,
  'averageThickness' | 'thicknessInSpec' | 'isAbsorbent' | 'absorbencyPercentage'
> {
  measuredByName: string | null;
  measuredAt: string | null;
  notes: string | null;
  testReport: RollTestReportDto | null;
}

/** The factory's running roll number (section 19.12). */
export interface RollNumberDto {
  nextNumber: number;
  lastResetAt: string | null;
  lastResetByName: string | null;
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
    /** Null for the code the system works out. */
    rollCode: string | null;
  }): Promise<RollDto> =>
    apiRequest<RollDto>('/api/production/rolls', { method: 'POST', body }),

  /** The code the next roll would get. A suggestion: another roll logged first takes it. */
  nextRollCode: (
    shiftLineId: number,
    recipeVersionId: number,
    colorId: number,
    /** The out time with the tablet's offset — the code carries the factory's clock. */
    at: string,
  ): Promise<string> =>
    apiRequest<string>(
      `/api/production/rolls/next-code?shiftLineId=${String(shiftLineId)}` +
        `&recipeVersionId=${String(recipeVersionId)}&colorId=${String(colorId)}` +
        `&at=${encodeURIComponent(at)}`,
    ),

  /** The roll's weight and length, taken on the production screen (section 19.12). */
  measureRoll: (id: number, body: { weight: number; length: number }): Promise<RollDto> =>
    apiRequest<RollDto>(`/api/production/rolls/${String(id)}/measure`, {
      method: 'POST',
      body,
    }),

  /** The number the next roll will get, and when it was last put back to 1. */
  rollNumber: (): Promise<RollNumberDto> =>
    apiRequest<RollNumberDto>('/api/production/roll-number'),

  /** Supervisor and administrator only. */
  resetRollNumber: (): Promise<RollNumberDto> =>
    apiRequest<RollNumberDto>('/api/production/roll-number/reset', {
      method: 'POST',
      body: {},
    }),

  /** Corrects a roll's code or out time while its shift is still open. */
  updateRoll: (
    id: number,
    body: { rollCode: string; producedAt: string },
  ): Promise<RollDto> =>
    apiRequest<RollDto>(`/api/production/rolls/${String(id)}`, { method: 'PUT', body }),

  /** Saving the measurements is what makes the roll usable by the thermo. */
  saveTest: (
    rollId: number,
    body: {
      plateWeight: number;
      thicknessRs: number;
      thicknessRm: number;
      thicknessLm: number;
      thicknessLs: number;
      notes: string | null;
      /** Absorbent rolls only; left out for every other. */
      absorbencyDryWeight?: number | null;
      absorbencyWetWeight?: number | null;
    },
  ): Promise<RollDto> =>
    apiRequest<RollDto>(`/api/production/rolls/${String(rollId)}/test`, {
      method: 'POST',
      body,
    }),
};

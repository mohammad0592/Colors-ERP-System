import { apiRequest } from '../../lib/apiClient';

/**
 * Line 1 — the mixer and the extruder, mirroring
 * Colors.Application.Features.Production. Specification section 8.
 */

export type RollStatus =
  | 'NeedsTest'
  | 'Available'
  | 'InThermo'
  | 'Processed'
  | 'Scrapped';

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
  averageThickness: number | null;
}

export interface RollDto extends Omit<RollSummaryDto, 'weight' | 'averageThickness'> {
  notes: string | null;
  testReport: RollTestReportDto | null;
}

export const productionApi = {
  batches: (openOnly = false): Promise<BatchSummaryDto[]> =>
    apiRequest<BatchSummaryDto[]>(`/api/production/batches?openOnly=${String(openOnly)}`),

  startBatch: (shiftLineId: number, notes: string | null): Promise<BatchSummaryDto> =>
    apiRequest<BatchSummaryDto>('/api/production/batches', {
      method: 'POST',
      body: { shiftLineId, notes },
    }),

  finishBatch: (id: number): Promise<BatchSummaryDto> =>
    apiRequest<BatchSummaryDto>(`/api/production/batches/${String(id)}/finish`, {
      method: 'POST',
      body: {},
    }),

  rolls: (batchId?: number, needsTestOnly = false): Promise<RollSummaryDto[]> => {
    const query = new URLSearchParams({ needsTestOnly: String(needsTestOnly) });
    if (batchId !== undefined) {
      query.set('batchId', String(batchId));
    }
    return apiRequest<RollSummaryDto[]>(`/api/production/rolls?${query.toString()}`);
  },

  roll: (id: number): Promise<RollDto> =>
    apiRequest<RollDto>(`/api/production/rolls/${String(id)}`),

  createRoll: (body: {
    batchId: number;
    recipeVersionId: number;
    colorId: number;
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

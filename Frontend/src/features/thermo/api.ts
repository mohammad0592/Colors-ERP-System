import { apiRequest } from '../../lib/apiClient';

/**
 * Line 2 — thermoforming, mirroring Colors.Application.Features.Thermo.
 * Specification section 9.
 */

export type ProducedBagStatus = 'Available' | 'Assigned' | 'Defective';

export interface ThermoRunSummaryDto {
  id: number;
  rollId: number;
  rollCode: string;
  rollBarcode: string;
  colorName: string;
  recipeNumber: number;
  recipeFamilyName: string;
  isAbsorbent: boolean;
  shiftLineId: number;
  productionLineName: string;
  shiftName: string;
  productionDate: string;
  operatorName: string;
  startedAt: string;
  finishedAt: string | null;
  /** Worked out from the two timestamps, never stored. */
  totalTimeMinutes: number | null;
  isFinished: boolean;
  needsTest: boolean;
  productName: string | null;
  bagCount: number | null;
  pieceCount: number | null;
}

export interface ThermoTestReportDto {
  id: number;
  productId: number;
  productName: string;
  bagCount: number;
  /** Bags × the product's pieces per bag, frozen at save. */
  pieceCount: number;
  pieceWeight: number;
  bagWeight: number;
  absorbentPercentage: number;
  testedByName: string;
  testedAt: string;
  notes: string | null;
}

/** The roll's own measurements, shown read-only — never asked for again. */
export interface RollReadingsDto {
  weight: number;
  length: number;
  plateWeight: number;
  averageThickness: number;
}

export interface ProducedBagDto {
  id: number;
  barcode: string;
  colorName: string;
  productName: string;
  weight: number;
  pieceCount: number;
  status: ProducedBagStatus;
  createdAt: string;
}

export interface ThermoRunDto {
  id: number;
  rollId: number;
  rollCode: string;
  rollBarcode: string;
  colorId: number;
  colorName: string;
  recipeNumber: number;
  recipeFamilyName: string;
  isAbsorbent: boolean;
  shiftLineId: number;
  productionLineName: string;
  shiftName: string;
  productionDate: string;
  mouldName: string | null;
  operatorName: string;
  startedAt: string;
  finishedAt: string | null;
  totalTimeMinutes: number | null;
  notes: string | null;
  rollReadings: RollReadingsDto | null;
  testReport: ThermoTestReportDto | null;
  bags: ProducedBagDto[];
}

export interface AvailableRollDto {
  id: number;
  rollCode: string;
  barcode: string;
  colorName: string;
  recipeNumber: number;
  recipeFamilyName: string;
  isAbsorbent: boolean;
  productionDate: string;
  weight: number | null;
}

export const thermoApi = {
  runs: (openOnly = false): Promise<ThermoRunSummaryDto[]> =>
    apiRequest<ThermoRunSummaryDto[]>(`/api/thermo/runs?openOnly=${String(openOnly)}`),

  run: (id: number): Promise<ThermoRunDto> =>
    apiRequest<ThermoRunDto>(`/api/thermo/runs/${String(id)}`),

  availableRolls: (): Promise<AvailableRollDto[]> =>
    apiRequest<AvailableRollDto[]>('/api/thermo/available-rolls'),

  /** Scan a roll to start. Everything else is inherited from it. */
  startRun: (body: {
    rollBarcode: string | null;
    rollId: number | null;
    shiftLineId: number;
    startedAt: string | null;
    notes: string | null;
  }): Promise<ThermoRunDto> =>
    apiRequest<ThermoRunDto>('/api/thermo/runs', { method: 'POST', body }),

  finishRun: (id: number, finishedAt: string | null): Promise<ThermoRunDto> =>
    apiRequest<ThermoRunDto>(`/api/thermo/runs/${String(id)}/finish`, {
      method: 'POST',
      body: { finishedAt },
    }),

  /** Saving the counts creates the bags and prints their labels. */
  saveTest: (
    runId: number,
    body: {
      bagCount: number;
      pieceWeight: number;
      bagWeight: number;
      absorbentPercentage: number;
      notes: string | null;
    },
  ): Promise<ThermoRunDto> =>
    apiRequest<ThermoRunDto>(`/api/thermo/runs/${String(runId)}/test`, {
      method: 'POST',
      body,
    }),
};

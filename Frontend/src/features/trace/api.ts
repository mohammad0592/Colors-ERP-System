import { apiRequest } from '../../lib/apiClient';

/**
 * Where one thing came from, and what it became — mirroring
 * Colors.Application.Features.Trace. Specification section 13.
 */

export interface TraceMaterialDto {
  ticketNumber: number;
  material: string;
  issued: number;
  returned: number;
  /** What the shift actually consumed. Both ends are weighed, so neither is a memory. */
  used: number;
  unitSymbol: string;
}

export interface TraceMixDto {
  batchNumber: number;
  shiftName: string;
  productionDate: string;
  productionLineName: string;
  materials: TraceMaterialDto[];
  /**
   * The ticket names the shift line, not the mix. With one mix per shift that is the
   * same set of materials — but the screen says which sentence is true rather than
   * claiming more than it knows.
   */
  issuedToShiftNotMix: boolean;
}

export interface TraceRollDto {
  id: number;
  rollCode: string;
  barcode: string;
  recipeNumber: number;
  recipeFamilyName: string;
  colorName: string;
  shiftName: string;
  productionDate: string;
  producedByName: string;
  producedAt: string;
  status: string;
  weight: number | null;
  length: number | null;
  plateWeight: number | null;
  averageThickness: number | null;
}

export interface TraceThermoDto {
  id: number;
  shiftName: string;
  productionDate: string;
  operatorName: string;
  startedAt: string;
  finishedAt: string | null;
  totalTimeMinutes: number | null;
  mouldName: string | null;
  productName: string | null;
  bagCount: number | null;
  pieceCount: number | null;
  pieceWeight: number | null;
  bagWeight: number | null;
  absorbentPercentage: number | null;
}

export interface TraceBagDto {
  id: number;
  barcode: string;
  /** Always carried, because which roll a bag came from is the whole point. */
  rollCode: string;
  productName: string;
  colorName: string;
  weight: number;
  pieceCount: number;
  status: string;
  palletNumber: number | null;
}

export interface TracePalletDto {
  id: number;
  palletNumber: number;
  barcode: string;
  productName: string | null;
  colorName: string | null;
  status: string;
  bagCount: number;
  capacity: number | null;
  pieceCount: number;
  weight: number;
  shiftName: string;
  productionDate: string;
  createdAt: string;
  completedAt: string | null;
}

export interface TraceDto {
  barcode: string;
  kind: 'Roll' | 'Bag' | 'Pallet';
  headline: string;
  mix: TraceMixDto | null;
  roll: TraceRollDto | null;
  thermo: TraceThermoDto | null;
  bag: TraceBagDto | null;
  pallet: TracePalletDto | null;
  /** A roll's bags, or a pallet's — each naming its own roll. */
  bags: TraceBagDto[];
}

export const traceApi = {
  /** Any barcode, or a roll code — the line printed large on the label. */
  get: (code: string): Promise<TraceDto> =>
    apiRequest<TraceDto>(`/api/trace/${encodeURIComponent(code)}`),
};

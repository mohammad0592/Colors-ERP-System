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

/** The roll's own test, every reading on it, and who took them. */
export interface TraceRollTestDto {
  testedByName: string;
  testedAt: string;
  plateWeight: number | null;
  /** The four corners as the test sheet has them: right side, right middle, left middle, left side. */
  thicknessRs: number | null;
  thicknessRm: number | null;
  thicknessLm: number | null;
  thicknessLs: number | null;
  averageThickness: number | null;
  thicknessInSpec: boolean | null;
  absorbencyDryWeight: number | null;
  absorbencyWetWeight: number | null;
  absorbencyPercentage: number | null;
  notes: string | null;
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
  /** Named at the thermo (section 19.17), so empty until the roll has gone in. */
  productName: string | null;
  notes: string | null;
  measuredByName: string | null;
  measuredAt: string | null;
  test: TraceRollTestDto | null;
  /** Cancelled or scrapped (section 19.27). */
  removedAt: string | null;
  removedByName: string | null;
  removalReason: string | null;
}

export interface TraceThermoDto {
  id: number;
  shiftName: string;
  productionDate: string;
  operatorName: string;
  startedAt: string;
  finishedAt: string | null;
  totalTimeMinutes: number | null;
  productName: string | null;
  bagCount: number | null;
  pieceCount: number | null;
  pieceWeight: number | null;
  bagWeight: number | null;
  absorbentPercentage: number | null;
  productionLineName: string;
  notes: string | null;
  /** Taken out before it was finished (section 19.23), and why. */
  unfinishedReason: string | null;
  countedByName: string | null;
  countedAt: string | null;
  countNotes: string | null;
  /** Counts corrected after they were saved (section 19.27). */
  correctedAt: string | null;
  correctedByName: string | null;
  correctionReason: string | null;
  /** Roll weight less the plates it made. Calculated, never stored. */
  scrapWeight: number | null;
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
  /** AB500B: the kind of bag, as its label prints it. */
  productCode: string;
  createdAt: string;
  notes: string | null;
  /** Marked defective, or cancelled by a count correction (section 19.27). */
  removedAt: string | null;
  removedByName: string | null;
  removalReason: string | null;
  palletBarcode: string | null;
}

/** One time a bag went onto a pallet, kept when it came off again. */
export interface TracePalletMoveDto {
  palletNumber: number;
  palletBarcode: string | null;
  assignedAt: string;
  assignedByName: string;
  reversedAt: string | null;
  reversedByName: string | null;
  reversalReason: string | null;
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
  /** P in front of its bags' code — PAB500B. Empty while nothing is on it. */
  generalCode: string | null;
  createdByName: string;
  closedAt: string | null;
  closedByName: string | null;
  shippedAt: string | null;
  shippedByName: string | null;
  shippingReversedAt: string | null;
  shippingReversedByName: string | null;
  shippingReversalReason: string | null;
  cancelledAt: string | null;
  cancelledByName: string | null;
  cancellationReason: string | null;
  notes: string | null;
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
  /** A roll's runs through the thermo, first to last — more than one if it went in again. */
  runs: TraceThermoDto[];
  /** A bag's pallets, the ones it came off included. */
  palletHistory: TracePalletMoveDto[];
}

export const traceApi = {
  /** Any barcode, or a roll code — the line printed large on the label. */
  get: (code: string): Promise<TraceDto> =>
    apiRequest<TraceDto>(`/api/trace/${encodeURIComponent(code)}`),
};

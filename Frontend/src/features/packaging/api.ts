import { apiRequest } from '../../lib/apiClient';

/**
 * Packaging consumption, mirroring Colors.Application.Features.Packaging.
 * Specification section 10.
 */

export type CountedAs = 'None' | 'LargeBag' | 'SmallBag' | 'WoodenPallet';

export interface PackagingLineDto {
  materialId: number;
  materialCode: string;
  materialName: string;
  unitSymbol: string;
  countedAs: CountedAs;
  /** True when the quantity comes from what the shift produced, not from a person. */
  isCounted: boolean;
  quantity: number;
  weight: number | null;
  /** Quantity × the material's unit weight, where it has one. */
  expectedWeight: number | null;
  /** Weighed minus expected. The gap is packaging torn, wasted or used elsewhere. */
  weightDifference: number | null;
  inStock: number;
}

export interface PackagingConsumptionDto {
  id: number;
  shiftLineId: number;
  productionLineName: string;
  shiftName: string;
  productionDate: string;
  recordedByName: string;
  recordedAt: string;
  notes: string | null;
  lines: PackagingLineDto[];
}

export interface PackagingDraftDto {
  shiftLineId: number;
  productionLineName: string;
  shiftName: string;
  productionDate: string;
  /** Shown so the operator can see why the counts are what they are. */
  bagsProduced: number;
  /**
   * Shown for the shift's shape only. The wood for these pallets is already out of the
   * store — it left as each one was started, so it is not a line on this form.
   */
  palletsStarted: number;
  alreadyRecorded: boolean;
  lines: PackagingLineDto[];
}

export const packagingApi = {
  list: (shiftReportId?: number): Promise<PackagingConsumptionDto[]> => {
    const query =
      shiftReportId === undefined ? '' : `?shiftReportId=${String(shiftReportId)}`;
    return apiRequest<PackagingConsumptionDto[]>(`/api/packaging${query}`);
  },

  /** The form, with the three counted materials already worked out. */
  draft: (shiftLineId: number): Promise<PackagingDraftDto> =>
    apiRequest<PackagingDraftDto>(`/api/packaging/draft/${String(shiftLineId)}`),

  save: (body: {
    shiftLineId: number;
    lines: { materialId: number; quantity: number; weight: number | null }[];
    notes: string | null;
  }): Promise<PackagingConsumptionDto> =>
    apiRequest<PackagingConsumptionDto>('/api/packaging', { method: 'POST', body }),
};

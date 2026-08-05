import { apiRequest } from '../../lib/apiClient';

/**
 * Produced stock and its labels, mirroring
 * Colors.Application.Features.Inventory. Specification sections 8 to 12.
 */

export type ProducedKind = 'Roll' | 'Bag' | 'Pallet';

export interface ProducedStockItemDto {
  kind: ProducedKind;
  id: number;
  barcode: string;
  /** What a person reads: the roll code, the product code, or the pallet number. */
  code: string;
  description: string;
  status: string;
  isAvailable: boolean;
  /** Its batch, the pallet it sits on, or the line that built it. */
  whereabouts: string;
  weight: number | null;
  pieceCount: number | null;
  productionDate: string;
  createdAt: string;
}

export interface BarcodeLabelDto {
  barcode: string;
  kind: ProducedKind;
  headlineCode: string;
  /** The roll a bag came from — the factory prints this today. */
  rollCode: string | null;
  /** AB500B: the kind of bag, not the bag. Text only, never the barcode. */
  productCode: string | null;
  productName: string | null;
  colorName: string | null;
  pieceCount: number | null;
  weight: number | null;
  shiftName: string | null;
  productionDate: string;
  createdAt: string;
}

export const producedStockApi = {
  list: (filters: {
    kind?: ProducedKind;
    status?: string;
    search?: string;
    availableOnly?: boolean;
  }): Promise<ProducedStockItemDto[]> => {
    const query = new URLSearchParams();
    if (filters.kind !== undefined) query.set('kind', filters.kind);
    if (filters.status !== undefined && filters.status !== '') {
      query.set('status', filters.status);
    }
    if (filters.search !== undefined && filters.search !== '') {
      query.set('search', filters.search);
    }
    if (filters.availableOnly === true) query.set('availableOnly', 'true');

    return apiRequest<ProducedStockItemDto[]>(
      `/api/inventory/produced?${query.toString()}`,
    );
  },

  label: (barcode: string): Promise<BarcodeLabelDto> =>
    apiRequest<BarcodeLabelDto>(
      `/api/inventory/produced/label/${encodeURIComponent(barcode)}`,
    ),
};

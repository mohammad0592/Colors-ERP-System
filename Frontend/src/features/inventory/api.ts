import { apiRequest } from '../../lib/apiClient';

/**
 * The store, mirroring the C# records in Colors.Application.Features.Inventory.
 * Specification section 6.
 */

export interface MaterialStockDto {
  materialId: number;
  code: string;
  name: string;
  categoryName: string;
  /**
   * True only for raw material. Packaging never goes out on a ticket — it is counted
   * at the end of the shift from what was produced.
   */
  issuedOnTickets: boolean;
  baseUnitName: string;
  baseUnitSymbol: string;
  /** Always in the material's base unit. */
  currentQuantity: number;
  minQuantity: number;
  /** Worked out on the server, so the screen and the reports agree on what is low. */
  isBelowMinimum: boolean;
  lastUpdated: string | null;
}

export interface InventoryMovementDto {
  id: number;
  materialId: number;
  materialCode: string;
  materialName: string;
  movementTypeName: string;
  /** +1 in, −1 out. The quantity itself never carries a sign. */
  direction: number;
  quantity: number;
  baseUnitSymbol: string;
  userName: string;
  movementDate: string;
  notes: string | null;
}

export interface ReceivingUnitDto {
  unitId: number;
  unitName: string;
  unitSymbol: string;
  /** What one of these is worth in the base unit — a pallet might be 750 kg. */
  quantityInBaseUnit: number;
  isDefault: boolean;
}

export const inventoryApi = {
  stock: (belowMinimumOnly = false): Promise<MaterialStockDto[]> =>
    apiRequest<MaterialStockDto[]>(
      `/api/inventory?belowMinimumOnly=${String(belowMinimumOnly)}`,
    ),

  receivingUnits: (materialId: number): Promise<ReceivingUnitDto[]> =>
    apiRequest<ReceivingUnitDto[]>(
      `/api/inventory/materials/${String(materialId)}/units`,
    ),

  movements: (materialId?: number, take = 100): Promise<InventoryMovementDto[]> => {
    const query = new URLSearchParams({ take: String(take) });
    if (materialId !== undefined) {
      query.set('materialId', String(materialId));
    }
    return apiRequest<InventoryMovementDto[]>(
      `/api/inventory/movements?${query.toString()}`,
    );
  },

  receive: (body: {
    materialId: number;
    unitId: number;
    quantity: number;
    notes: string | null;
  }): Promise<MaterialStockDto> =>
    apiRequest<MaterialStockDto>('/api/inventory/receive', { method: 'POST', body }),

  /** Corrects a balance to what a stock count found. Always with a reason. */
  adjust: (body: {
    materialId: number;
    countedQuantity: number;
    reason: string;
  }): Promise<MaterialStockDto> =>
    apiRequest<MaterialStockDto>('/api/inventory/adjust', { method: 'POST', body }),
};

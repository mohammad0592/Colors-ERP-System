import { apiRequest } from '../../lib/apiClient';
import type { EntryMethod } from '../../lib/barcodeScanner';

/**
 * Pallets, mirroring Colors.Application.Features.Pallets.
 * Specification section 10.
 */

/** ClosedNotFull: holding bags, not full, put away in the warehouse (section 19.21). */
export type PalletStatus =
  'Empty' | 'Opened' | 'Completed' | 'ClosedNotFull' | 'Shipped' | 'Cancelled';

export interface PalletSummaryDto {
  id: number;
  palletNumber: number;
  barcode: string;
  shiftLineId: number;
  productionLineName: string;
  shiftName: string;
  productionDate: string;
  /** Both null until the first bag is scanned. */
  colorId: number | null;
  colorName: string | null;
  productId: number | null;
  productName: string | null;
  /** Worked out from two dates and the bags on it, never stored. */
  status: PalletStatus;
  isOpen: boolean;
  bagCount: number;
  pieceCount: number;
  weight: number;
  /** From the product the pallet took off its first bag. Null while empty. */
  capacity: number | null;
  createdByName: string;
  createdAt: string;
  completedAt: string | null;
  /** Set while it stands closed, not full, in the warehouse (section 19.21). */
  closedAt: string | null;
  shippedAt: string | null;
}

export interface PalletBagDto {
  assignmentId: number;
  producedBagId: number;
  barcode: string;
  rollCode: string;
  weight: number;
  pieceCount: number;
  assignedByName: string;
  assignedAt: string;
  /** A bag taken off stays here, saying who undid it and why. */
  isActive: boolean;
  reversedByName: string | null;
  reversedAt: string | null;
  reversalReason: string | null;
}

export interface PalletDto extends PalletSummaryDto {
  shippedByName: string | null;
  /** Set only while a pallet is back in the factory after a shipping was undone. */
  shippingReversedAt: string | null;
  shippingReversedByName: string | null;
  shippingReversalReason: string | null;
  /** Set only on a cancelled pallet. Its wooden pallet went back to the store. */
  cancelledAt: string | null;
  cancelledByName: string | null;
  cancellationReason: string | null;
  closedByName: string | null;
  notes: string | null;
  bags: PalletBagDto[];
}

export interface AvailableBagDto {
  id: number;
  barcode: string;
  rollCode: string;
  colorId: number;
  colorName: string;
  productId: number;
  productName: string;
  weight: number;
  pieceCount: number;
  createdAt: string;
}

/**
 * What to call a pallet on screen: its code once it has one, and its number while it is
 * still being filled. A pallet is given its code when it is finished, full or closed,
 * because the code carries the time it was finished (section 19.26).
 */
export function palletName(pallet: { barcode: string; palletNumber: number }): string {
  return pallet.barcode === '' ? `Pallet ${String(pallet.palletNumber)}` : pallet.barcode;
}

/**
 * The tablet's distance from UTC. Finishing a pallet gives it its code, with the day's
 * number and the time on the factory's clock, and the server keeps time in UTC.
 */
function utcOffsetMinutes(): number {
  return -new Date().getTimezoneOffset();
}

export const palletsApi = {
  list: (openOnly = false): Promise<PalletSummaryDto[]> =>
    apiRequest<PalletSummaryDto[]>(`/api/pallets?openOnly=${String(openOnly)}`),

  get: (id: number): Promise<PalletDto> =>
    apiRequest<PalletDto>(`/api/pallets/${String(id)}`),

  /** Pass a pallet and only the bags it can actually take come back. */
  availableBags: (palletId?: number): Promise<AvailableBagDto[]> => {
    const query = palletId === undefined ? '' : `?palletId=${String(palletId)}`;
    return apiRequest<AvailableBagDto[]>(`/api/pallets/available-bags${query}`);
  },

  /** Also takes the wooden pallet out of the store. Refused when there is none. */
  start: (shiftLineId: number, notes: string | null): Promise<PalletDto> =>
    apiRequest<PalletDto>('/api/pallets', {
      method: 'POST',
      body: { shiftLineId, notes },
    }),

  /** Cancels an empty pallet and sends its wooden pallet back to the store. */
  /** Closes a pallet that is not full, to store it as it is (section 19.21). */
  close: (palletId: number): Promise<PalletDto> =>
    apiRequest<PalletDto>(`/api/pallets/${String(palletId)}/close`, {
      method: 'POST',
      body: { utcOffsetMinutes: utcOffsetMinutes() },
    }),

  /** Opens a closed pallet again, to take more bags. */
  reopen: (palletId: number): Promise<PalletDto> =>
    apiRequest<PalletDto>(`/api/pallets/${String(palletId)}/reopen`, {
      method: 'POST',
      body: {},
    }),

  cancel: (palletId: number, reason: string): Promise<PalletDto> =>
    apiRequest<PalletDto>(`/api/pallets/${String(palletId)}/cancel`, {
      method: 'POST',
      body: { reason },
    }),

  /** The first bag decides what the pallet is; every later one must match. */
  scanBag: (
    palletId: number,
    body: { bagBarcode: string | null; producedBagId: number | null },
    entry?: EntryMethod,
  ): Promise<PalletDto> =>
    apiRequest<PalletDto>(`/api/pallets/${String(palletId)}/bags`, {
      method: 'POST',
      body: { ...body, utcOffsetMinutes: utcOffsetMinutes() },
      ...(entry === undefined ? {} : { entry }),
    }),

  /** Takes a bag back off. The scan stays in the history with its reason. */
  reverse: (assignmentId: number, reason: string): Promise<PalletDto> =>
    apiRequest<PalletDto>(`/api/pallets/assignments/${String(assignmentId)}/reverse`, {
      method: 'POST',
      body: { reason },
    }),

  /** Finished pallets still standing in the factory, oldest first. */
  inStock: (): Promise<PalletSummaryDto[]> =>
    apiRequest<PalletSummaryDto[]>('/api/pallets/in-stock'),

  /** Sends a finished pallet out. Refused on anything not full. */
  ship: (
    body: { palletBarcode: string | null; palletId: number | null },
    entry?: EntryMethod,
  ): Promise<PalletDto> =>
    apiRequest<PalletDto>('/api/pallets/ship', {
      method: 'POST',
      body,
      ...(entry === undefined ? {} : { entry }),
    }),

  /** Puts a pallet shipped by mistake back in the factory. */
  unship: (palletId: number, reason: string): Promise<PalletDto> =>
    apiRequest<PalletDto>(`/api/pallets/${String(palletId)}/unship`, {
      method: 'POST',
      body: { reason },
    }),
};

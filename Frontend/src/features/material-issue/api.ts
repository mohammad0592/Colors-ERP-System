import { apiRequest } from '../../lib/apiClient';

/**
 * Material issue and return, mirroring Colors.Application.Features.MaterialIssue.
 * Specification section 7.
 */

export type IssueTicketStatus = 'Open' | 'Closed';

export interface IssueTicketLineDto {
  id: number;
  materialId: number;
  materialCode: string;
  materialName: string;
  baseUnitSymbol: string;
  issuedQuantity: number;
  returnedQuantity: number;
  /** Issued minus returned — what was really used. Calculated, never stored. */
  netUsed: number;
}

export interface IssueTicketSummaryDto {
  id: number;
  ticketNumber: number;
  shiftLineId: number;
  productionLineName: string;
  shiftName: string;
  productionDate: string;
  status: IssueTicketStatus;
  isOpen: boolean;
  issuedByName: string;
  lineCount: number;
  totalIssued: number;
  totalReturned: number;
  createdAt: string;
  closedAt: string | null;
}

export interface IssueTicketDto {
  id: number;
  ticketNumber: number;
  shiftLineId: number;
  productionLineName: string;
  shiftName: string;
  productionDate: string;
  status: IssueTicketStatus;
  isOpen: boolean;
  issuedByName: string;
  closedByName: string | null;
  createdAt: string;
  closedAt: string | null;
  notes: string | null;
  lines: IssueTicketLineDto[];
}

export const materialIssueApi = {
  list: (openOnly = false): Promise<IssueTicketSummaryDto[]> =>
    apiRequest<IssueTicketSummaryDto[]>(
      `/api/material-issue?openOnly=${String(openOnly)}`,
    ),

  get: (id: number): Promise<IssueTicketDto> =>
    apiRequest<IssueTicketDto>(`/api/material-issue/${String(id)}`),

  create: (body: {
    shiftLineId: number;
    notes: string | null;
    lines: { materialId: number; quantity: number }[];
  }): Promise<IssueTicketDto> =>
    apiRequest<IssueTicketDto>('/api/material-issue', { method: 'POST', body }),

  /** Weighs the leftover back in. May be called again as more comes back. */
  recordReturns: (
    id: number,
    lines: { materialId: number; quantity: number }[],
  ): Promise<IssueTicketDto> =>
    apiRequest<IssueTicketDto>(`/api/material-issue/${String(id)}/returns`, {
      method: 'POST',
      body: { lines },
    }),

  close: (id: number): Promise<IssueTicketDto> =>
    apiRequest<IssueTicketDto>(`/api/material-issue/${String(id)}/close`, {
      method: 'POST',
      body: {},
    }),
};

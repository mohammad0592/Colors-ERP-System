import { apiRequest } from '../../lib/apiClient';

/**
 * The audit log, mirroring Colors.Application.Features.Audit.
 * Specification section 15.
 *
 * Reading only. Nothing writes a line from a screen, and nothing edits or deletes one.
 */

export interface AuditEntryDto {
  id: number;
  userId: number | null;
  userName: string | null;
  shiftReportId: number | null;
  shiftLabel: string | null;
  /** `Added` / `Modified` / `Deleted` for a success; the endpoint for a refusal. */
  action: string;
  objectType: string;
  objectId: number | null;
  result: 'Success' | 'Rejected';
  details: string | null;
  timestamp: string;
}

export const auditApi = {
  list: (query: {
    shiftReportId?: number;
    objectType?: string;
    refusalsOnly?: boolean;
    take?: number;
  }): Promise<AuditEntryDto[]> => {
    const parts: string[] = [];

    if (query.shiftReportId !== undefined) {
      parts.push(`shiftReportId=${String(query.shiftReportId)}`);
    }
    if (query.objectType !== undefined && query.objectType !== '') {
      parts.push(`objectType=${encodeURIComponent(query.objectType)}`);
    }
    if (query.refusalsOnly === true) {
      parts.push('refusalsOnly=true');
    }

    parts.push(`take=${String(query.take ?? 200)}`);

    return apiRequest<AuditEntryDto[]>(`/api/audit?${parts.join('&')}`);
  },
};

import { apiRequest } from '../../lib/apiClient';

/**
 * The label printer's queue, mirroring Colors.Application.Features.Printing
 * (specification section 19.13).
 */

export type PrintJobStatus = 'Pending' | 'Printed' | 'Cancelled';

export interface PrintJobDto {
  id: number;
  barcodes: string[];
  labelCount: number;
  headline: string | null;
  /** The screen it was sent from — a path such as /production/rolls. */
  source: string;
  requestedByName: string;
  requestedAt: string;
  status: PrintJobStatus;
  doneByName: string | null;
  doneAt: string | null;
}

export const printJobsApi = {
  /** From any screen that makes a label. */
  queue: (body: {
    barcodes: string[];
    headline: string | null;
    source: string;
  }): Promise<PrintJobDto> =>
    apiRequest<PrintJobDto>('/api/print-jobs', { method: 'POST', body }),

  /** Waiting jobs, oldest first. */
  pending: (): Promise<PrintJobDto[]> =>
    apiRequest<PrintJobDto[]>('/api/print-jobs?pendingOnly=true'),

  /** The most recent jobs of any status, newest first. */
  recent: (take = 30): Promise<PrintJobDto[]> =>
    apiRequest<PrintJobDto[]>(`/api/print-jobs?pendingOnly=false&take=${String(take)}`),

  markPrinted: (id: number): Promise<PrintJobDto> =>
    apiRequest<PrintJobDto>(`/api/print-jobs/${String(id)}/printed`, {
      method: 'POST',
      body: {},
    }),

  cancel: (id: number): Promise<PrintJobDto> =>
    apiRequest<PrintJobDto>(`/api/print-jobs/${String(id)}/cancel`, {
      method: 'POST',
      body: {},
    }),
};

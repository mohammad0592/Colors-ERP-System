import { apiRequest } from '../../lib/apiClient';
import type { ShiftSummaryReportDto } from '../reports/api';

/**
 * The home screen, mirroring Colors.Application.Features.Dashboard.
 * Specification section 13.
 */

export interface DashboardAlertDto {
  /** What kind of thing is waiting, so the screen knows where to send the reader. */
  kind: string;
  /** What to call one, and what to call several — English plurals cannot be derived. */
  label: string;
  labelPlural: string;
  count: number;
  detail: string;
  /** True where this stops a shift from closing. */
  blocksShiftClose: boolean;
}

export interface DashboardShiftDto {
  shiftReportId: number;
  productionDate: string;
  shiftName: string;
  supervisorName: string | null;
  openedAt: string;
  lineNames: string[];
}

export interface DashboardDto {
  /** The shift running now, or null when the factory is between shifts. */
  openShift: DashboardShiftDto | null;
  /** What that shift has made so far, read through the same code as the shift report. */
  summary: ShiftSummaryReportDto | null;
  needsAttention: DashboardAlertDto[];
}

export const dashboardApi = {
  get: (): Promise<DashboardDto> => apiRequest<DashboardDto>('/api/dashboard'),
};

import { apiRequest } from '../../lib/apiClient';

/**
 * Shift reports, mirroring the C# records in Colors.Application.Features.ShiftReports.
 * Specification section 2.
 *
 * A shift is one date and one shift for the whole factory; the lines that ran hang
 * underneath it.
 */

/**
 * `Correcting` is a shift reopened to fix its record while another one is running.
 * It takes edits but no production (specification section 2).
 */
export type ShiftReportStatus = 'Open' | 'Closed' | 'Correcting';

export interface ShiftWorkerDto {
  userId: number;
  employeeNumber: string;
  fullName: string;
  /**
   * The jobs they did on this shift — a list, because one man commonly does two.
   * Not the same as the roles they hold.
   */
  roleInShiftIds: number[];
  roleInShiftNames: string[];
  isTrainee: boolean;
}

export interface SaveShiftWorker {
  userId: number;
  roleInShiftIds: number[];
  isTrainee: boolean;
}

export interface ShiftLineDto {
  id: number;
  productionLineId: number;
  productionLineName: string;
  /**
   * From the line itself: true only for the thermo, and it decides whether the screen
   * shows the machine settings at all.
   */
  recordsMachineSettings: boolean;
  /**
   * What the line does. The server refuses the wrong line anyway, but a screen that
   * offers only the lines that can do the job never puts the man in that position.
   */
  makesRolls: boolean;
  formsBags: boolean;
  takesRawMaterial: boolean;
  recycles: boolean;
  /**
   * Which template is bolted in this shift. Everything formed on the line inherits it,
   * so it is chosen once rather than per roll.
   */
  mouldId: number | null;
  mouldName: string | null;
  /** "HH:mm" */
  productionStartTime: string | null;
  productionEndTime: string | null;
  downtimeHours: number | null;
  /** Worked out by the server, so the screen shows what the reports will show. */
  actualProductionHours: number | null;
  machineSpeed: number | null;
  feedDistanceMm: number | null;
  cycleTimeSeconds: number | null;
  workers: ShiftWorkerDto[];
}

export interface UpdateShiftLine {
  mouldId: number | null;
  productionStartTime: string | null;
  productionEndTime: string | null;
  downtimeHours: number | null;
  machineSpeed: number | null;
  feedDistanceMm: number | null;
  cycleTimeSeconds: number | null;
  workers: SaveShiftWorker[];
}

export interface ShiftReportSummaryDto {
  id: number;
  /** "yyyy-MM-dd" — a plain date, never a moment in time. */
  productionDate: string;
  shiftId: number;
  shiftName: string;
  status: ShiftReportStatus;
  isOpen: boolean;
  /**
   * Its record may still be changed. True while it is running, and also while it is
   * being corrected after a reopen — a correcting shift takes edits but no production.
   */
  canEdit: boolean;
  supervisorName: string | null;
  lineNames: string[];
  lineCount: number;
  workerCount: number;
  electricityUsed: number | null;
  openedAt: string;
  closedAt: string | null;
}

export interface ShiftReportDto {
  id: number;
  productionDate: string;
  shiftId: number;
  shiftName: string;
  status: ShiftReportStatus;
  isOpen: boolean;
  /**
   * Its record may still be changed. True while it is running, and also while it is
   * being corrected after a reopen — a correcting shift takes edits but no production.
   */
  canEdit: boolean;
  supervisorUserId: number | null;
  supervisorName: string | null;
  /** One meter for the whole building, so it is read once per shift. */
  electricityStartMeter: number | null;
  electricityEndMeter: number | null;
  electricityUsed: number | null;
  notes: string | null;
  openedByName: string;
  openedAt: string;
  closedByName: string | null;
  closedAt: string | null;
  lines: ShiftLineDto[];
}

export interface OpenShiftReport {
  productionDate: string;
  shiftId: number;
  supervisorUserId: number | null;
  productionLineIds: number[];
}

export const shiftReportsApi = {
  list: (
    productionLineId?: number,
    openOnly = false,
  ): Promise<ShiftReportSummaryDto[]> => {
    const query = new URLSearchParams();
    if (productionLineId !== undefined) {
      query.set('productionLineId', String(productionLineId));
    }
    if (openOnly) {
      query.set('openOnly', 'true');
    }
    const suffix = query.size === 0 ? '' : `?${query.toString()}`;
    return apiRequest<ShiftReportSummaryDto[]>(`/api/shift-reports${suffix}`);
  },

  get: (id: number): Promise<ShiftReportDto> =>
    apiRequest<ShiftReportDto>(`/api/shift-reports/${String(id)}`),

  open: (body: OpenShiftReport): Promise<ShiftReportDto> =>
    apiRequest<ShiftReportDto>('/api/shift-reports', { method: 'POST', body }),

  update: (
    id: number,
    body: {
      supervisorUserId: number | null;
      electricityStartMeter: number | null;
      electricityEndMeter: number | null;
      notes: string | null;
    },
  ): Promise<ShiftReportDto> =>
    apiRequest<ShiftReportDto>(`/api/shift-reports/${String(id)}`, {
      method: 'PUT',
      body,
    }),

  /** Adds a line that started after the shift was opened. */
  addLine: (id: number, productionLineId: number): Promise<ShiftReportDto> =>
    apiRequest<ShiftReportDto>(`/api/shift-reports/${String(id)}/lines`, {
      method: 'POST',
      body: { productionLineId },
    }),

  updateLine: (
    id: number,
    lineId: number,
    body: UpdateShiftLine,
  ): Promise<ShiftReportDto> =>
    apiRequest<ShiftReportDto>(
      `/api/shift-reports/${String(id)}/lines/${String(lineId)}`,
      { method: 'PUT', body },
    ),

  removeLine: (id: number, lineId: number): Promise<ShiftReportDto> =>
    apiRequest<ShiftReportDto>(
      `/api/shift-reports/${String(id)}/lines/${String(lineId)}`,
      { method: 'DELETE' },
    ),

  /** Ends the shift and every line on it. */
  close: (id: number): Promise<ShiftReportDto> =>
    apiRequest<ShiftReportDto>(`/api/shift-reports/${String(id)}/close`, {
      method: 'POST',
      body: {},
    }),

  /** Administrator only, and the reason is kept on the record. */
  reopen: (id: number, reason: string): Promise<ShiftReportDto> =>
    apiRequest<ShiftReportDto>(`/api/shift-reports/${String(id)}/reopen`, {
      method: 'POST',
      body: { reason },
    }),

  remove: (id: number): Promise<undefined> =>
    apiRequest<undefined>(`/api/shift-reports/${String(id)}`, { method: 'DELETE' }),
};

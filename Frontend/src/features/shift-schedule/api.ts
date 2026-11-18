import { apiRequest } from '../../lib/apiClient';

/**
 * The shift schedule, mirroring the C# records in
 * Colors.Application.Features.ShiftSchedule. Specification section 19.30.
 *
 * Tomorrow's shifts are planned the day before. At its start time the system opens
 * each one with its supervisor, lines and operators, and closes it at its end.
 */

/**
 * What has become of a plan. `Overrunning` is a shift past its end that could not
 * close itself: a roll is still in the thermo or a ticket is still out, and no shift
 * came after to take them over.
 */
export type ScheduledShiftStatus =
  'Planned' | 'Running' | 'Overrunning' | 'Finished' | 'NotOpened';

export interface ScheduledShiftLineDto {
  productionLineId: number;
  productionLineName: string;
  /** The job the operator must hold, so the picker offers only those people. */
  operatorRole: string | null;
  operatorUserId: number | null;
  operatorName: string | null;
}

export interface ScheduledShiftDto {
  id: number;
  /** "yyyy-MM-dd" */
  productionDate: string;
  shiftId: number;
  shiftName: string;
  startTime: string;
  endTime: string;
  supervisorUserId: number | null;
  supervisorName: string | null;
  extruderStartingShift: boolean;
  lines: ScheduledShiftLineDto[];
  status: ScheduledShiftStatus;
  /** Still a plan and not yet started, so it can be changed or cancelled. */
  canChange: boolean;
  shiftReportId: number | null;
  notOpenedReason: string | null;
  scheduledByName: string;
  scheduledAt: string;
  changedByName: string | null;
  changedAt: string | null;
}

export interface ScheduleLineOptionDto {
  id: number;
  name: string;
  makesRolls: boolean;
  operatorRole: string | null;
}

/** Today and tomorrow, on the factory's clock rather than the tablet's. */
export interface ShiftScheduleDto {
  today: string;
  tomorrow: string;
  shifts: ScheduledShiftDto[];
  lines: ScheduleLineOptionDto[];
}

export interface ScheduleShiftLine {
  productionLineId: number;
  operatorUserId: number | null;
}

export interface PlanShiftRequest {
  supervisorUserId: number | null;
  lines: ScheduleShiftLine[];
  extruderStartingShift: boolean;
}

export const shiftScheduleApi = {
  get: (): Promise<ShiftScheduleDto> =>
    apiRequest<ShiftScheduleDto>('/api/shift-schedule'),

  schedule: (
    request: PlanShiftRequest & { productionDate: string; shiftId: number },
  ): Promise<ScheduledShiftDto> =>
    apiRequest<ScheduledShiftDto>('/api/shift-schedule', {
      method: 'POST',
      body: request,
    }),

  update: (id: number, request: PlanShiftRequest): Promise<ScheduledShiftDto> =>
    apiRequest<ScheduledShiftDto>(`/api/shift-schedule/${String(id)}`, {
      method: 'PUT',
      body: request,
    }),

  cancel: (id: number): Promise<undefined> =>
    apiRequest<undefined>(`/api/shift-schedule/${String(id)}`, { method: 'DELETE' }),
};

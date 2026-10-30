import { apiRequest } from '../../lib/apiClient';

/**
 * Workers: people who work the floor without using the system — no account, no
 * password, no role (specification section 19.19). Staff are the ones with an account.
 * Mirrors Colors.Application.Features.Staff, which keeps its older name for them.
 */

export interface StaffMemberDto {
  id: number;
  fullName: string;
  /** The factory's number for him, where he has one. */
  employeeNumber: string | null;
  /** Off when he has left. Never deleted: the shifts he worked go on naming him. */
  isActive: boolean;
}

export interface SaveStaffMember {
  fullName: string;
  employeeNumber: string | null;
  isActive: boolean;
}

export const staffApi = {
  list: (includeInactive = false): Promise<StaffMemberDto[]> =>
    apiRequest<StaffMemberDto[]>(`/api/staff?includeInactive=${String(includeInactive)}`),

  /** Whoever fills in a line's staff may add somebody missing from the list. */
  create: (body: SaveStaffMember): Promise<StaffMemberDto> =>
    apiRequest<StaffMemberDto>('/api/staff', { method: 'POST', body }),

  /** The supervisor and the administrator. */
  update: (id: number, body: SaveStaffMember): Promise<StaffMemberDto> =>
    apiRequest<StaffMemberDto>(`/api/staff/${String(id)}`, { method: 'PUT', body }),
};

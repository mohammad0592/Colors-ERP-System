import { apiRequest } from '../../lib/apiClient';

/**
 * User administration, mirroring Colors.Application.Features.Users.
 * Specification section 3.
 */

export interface UserDto {
  id: number;
  employeeNumber: string;
  fullName: string;
  isActive: boolean;
  createdAt: string;
  /** Every job this person may do. One man often holds several. */
  roles: string[];
  /** True while wrong passwords are still locking the account. */
  isLockedOut: boolean;
  lockedOutUntil: string | null;
}

export const usersApi = {
  list: (): Promise<UserDto[]> => apiRequest<UserDto[]>('/api/users'),

  create: (body: {
    employeeNumber: string;
    fullName: string;
    password: string;
    roles: string[];
  }): Promise<UserDto> => apiRequest<UserDto>('/api/users', { method: 'POST', body }),

  update: (
    id: number,
    body: {
      employeeNumber: string;
      fullName: string;
      roles: string[];
      isActive: boolean;
    },
  ): Promise<UserDto> =>
    apiRequest<UserDto>(`/api/users/${String(id)}`, { method: 'PUT', body }),

  resetPassword: (id: number, newPassword: string): Promise<UserDto> =>
    apiRequest<UserDto>(`/api/users/${String(id)}/password`, {
      method: 'POST',
      body: { newPassword },
    }),

  unlock: (id: number): Promise<UserDto> =>
    apiRequest<UserDto>(`/api/users/${String(id)}/unlock`, { method: 'POST' }),
};

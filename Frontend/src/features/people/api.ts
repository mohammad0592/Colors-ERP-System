import { apiRequest } from '../../lib/apiClient';

/**
 * People and roles, read only — for every screen that has to name somebody.
 * Mirrors Colors.Application.Features.People.
 */

export interface PersonDto {
  id: number;
  employeeNumber: string;
  fullName: string;
  isActive: boolean;
  roles: string[];
}

export interface RoleDto {
  id: number;
  name: string;
}

export const peopleApi = {
  list: (includeInactive = false): Promise<PersonDto[]> =>
    apiRequest<PersonDto[]>(`/api/people?includeInactive=${String(includeInactive)}`),

  roles: (): Promise<RoleDto[]> => apiRequest<RoleDto[]>('/api/people/roles'),
};

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useState, type ReactElement } from 'react';
import { PageHeader } from '../../components/ui/PageHeader';
import { ApiError } from '../../lib/apiClient';
import { labelForRole } from '../../lib/roles';
import { usersApi, type UserDto } from './api';
import { ResetPasswordDialog } from './ResetPasswordDialog';
import { UserDialog } from './UserDialog';

/**
 * Users and roles (specification section 3).
 *
 * The administrator's screen: add a worker, change what he may do, set a new password
 * when one is forgotten.
 *
 * <b>Nobody is deleted.</b> The shifts, rolls and pallets a man recorded name him for
 * ever, so leaving is recorded by unticking "still works here" — he keeps his history and
 * simply cannot sign in.
 */
export function UsersPage(): ReactElement {
  const queryClient = useQueryClient();
  const [editing, setEditing] = useState<UserDto | null>(null);
  const [adding, setAdding] = useState(false);
  const [resetting, setResetting] = useState<UserDto | null>(null);
  const [actionError, setActionError] = useState<string | null>(null);

  const users = useQuery({
    queryKey: ['users'],
    queryFn: () => usersApi.list(),
  });

  function invalidate(): void {
    void queryClient.invalidateQueries({ queryKey: ['users'] });
    // Every screen that names somebody reads the same people.
    void queryClient.invalidateQueries({ queryKey: ['people'] });
  }

  const unlock = useMutation({
    mutationFn: (id: number) => usersApi.unlock(id),
    onSuccess: () => {
      setActionError(null);
      invalidate();
    },
    onError: (caught: unknown) => {
      setActionError(
        caught instanceof ApiError ? caught.message : 'Something went wrong.',
      );
    },
  });

  return (
    <>
      <PageHeader
        title="Users"
        subtitle="Who may sign in, and what each of them may do."
        actions={
          <button
            type="button"
            className="btn-primary"
            onClick={() => {
              setAdding(true);
            }}
          >
            Add a worker
          </button>
        }
      />

      {actionError !== null && (
        <p
          role="alert"
          className="mb-4 rounded-control border border-l-4 border-bad/30 border-l-bad bg-bad-soft px-4 py-3 text-sm font-medium text-bad"
        >
          {actionError}
        </p>
      )}

      {users.isPending && <p className="p-6 text-ink-muted">Loading…</p>}
      {users.isError && <p className="p-6 text-bad">Could not load the people.</p>}

      {users.data !== undefined && (
        <div className="card overflow-x-auto">
          <table className="w-full text-left text-sm">
            <thead>
              <tr className="border-b border-line text-xs tracking-wider text-ink-muted uppercase">
                <th className="px-4 py-3 font-semibold">Employee number</th>
                <th className="px-4 py-3 font-semibold">Name</th>
                <th className="px-4 py-3 font-semibold">What he may do</th>
                <th className="px-4 py-3" />
              </tr>
            </thead>
            <tbody>
              {users.data.map((user) => (
                <tr
                  key={user.id}
                  className={[
                    'border-b border-line last:border-0',
                    user.isActive ? '' : 'text-ink-muted',
                  ].join(' ')}
                >
                  <td className="px-4 py-3 font-mono font-semibold text-ink">
                    {user.employeeNumber}
                  </td>
                  <td className="px-4 py-3">
                    <span className={user.isActive ? 'text-ink' : ''}>
                      {user.fullName}
                    </span>
                    {!user.isActive && (
                      <span className="ml-2 rounded-full bg-canvas px-2 py-0.5 text-xs font-semibold text-ink-muted">
                        left
                      </span>
                    )}
                    {user.isLockedOut && (
                      <span className="ml-2 rounded-full bg-warn-soft px-2 py-0.5 text-xs font-semibold text-warn">
                        locked out
                      </span>
                    )}
                  </td>
                  <td className="px-4 py-3">
                    {user.roles.length === 0 ? (
                      <span className="text-ink-muted">nothing yet</span>
                    ) : (
                      <span className="flex flex-wrap gap-1">
                        {user.roles.map((role) => (
                          <span
                            key={role}
                            className="rounded-full bg-canvas px-2 py-0.5 text-xs font-medium text-ink-soft"
                          >
                            {labelForRole(role)}
                          </span>
                        ))}
                      </span>
                    )}
                  </td>
                  <td className="px-4 py-3">
                    <div className="flex flex-wrap justify-end gap-2">
                      {user.isLockedOut && (
                        <button
                          type="button"
                          className="min-h-9 rounded-control border border-line px-3 text-sm font-medium whitespace-nowrap text-ink-soft transition-colors hover:border-brand-200 hover:bg-brand-50 hover:text-brand-700"
                          disabled={unlock.isPending}
                          onClick={() => {
                            unlock.mutate(user.id);
                          }}
                        >
                          Let him back in
                        </button>
                      )}
                      <button
                        type="button"
                        className="min-h-9 rounded-control border border-line px-3 text-sm font-medium whitespace-nowrap text-ink-soft transition-colors hover:border-brand-200 hover:bg-brand-50 hover:text-brand-700"
                        onClick={() => {
                          setResetting(user);
                        }}
                      >
                        New password
                      </button>
                      <button
                        type="button"
                        className="min-h-9 rounded-control border border-line px-3 text-sm font-medium whitespace-nowrap text-ink-soft transition-colors hover:border-brand-200 hover:bg-brand-50 hover:text-brand-700"
                        onClick={() => {
                          setEditing(user);
                        }}
                      >
                        Edit
                      </button>
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {(adding || editing !== null) && (
        <UserDialog
          user={editing}
          onClose={() => {
            setAdding(false);
            setEditing(null);
          }}
          onSaved={invalidate}
        />
      )}

      {resetting !== null && (
        <ResetPasswordDialog
          user={resetting}
          onClose={() => {
            setResetting(null);
          }}
          onSaved={invalidate}
        />
      )}
    </>
  );
}

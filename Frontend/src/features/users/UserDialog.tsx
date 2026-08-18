import { useState, type ReactElement } from 'react';
import { useTranslation } from '../../hooks/useTranslation';
import { Modal } from '../../components/ui/Modal';
import { ApiError } from '../../lib/apiClient';
import { RoleNames, labelForRole, type RoleName } from '../../lib/roles';
import { usersApi, type UserDto } from './api';

/**
 * Adding a worker, or changing one (specification section 3).
 *
 * The roles are tick boxes rather than a single choice, because one man is the extruder
 * operator and its test person today and the factory wants to split those later without
 * anything being rebuilt.
 *
 * There is no delete. Production records name people for ever, so somebody who leaves is
 * marked as no longer working here and keeps their history.
 */
export function UserDialog({
  user,
  onClose,
  onSaved,
}: {
  /** Null when adding somebody new. */
  user: UserDto | null;
  onClose: () => void;
  onSaved: () => void;
}): ReactElement {
  const { t } = useTranslation();
  const [employeeNumber, setEmployeeNumber] = useState(user?.employeeNumber ?? '');
  const [fullName, setFullName] = useState(user?.fullName ?? '');
  const [password, setPassword] = useState('');
  const [isActive, setIsActive] = useState(user?.isActive ?? true);
  const [roles, setRoles] = useState<string[]>(user?.roles ?? []);
  const [error, setError] = useState<string | null>(null);
  const [isSaving, setIsSaving] = useState(false);

  const isNew = user === null;

  async function save(): Promise<void> {
    setError(null);
    setIsSaving(true);
    try {
      if (isNew) {
        await usersApi.create({
          employeeNumber: employeeNumber.trim(),
          fullName: fullName.trim(),
          password,
          roles,
        });
      } else {
        await usersApi.update(user.id, {
          employeeNumber: employeeNumber.trim(),
          fullName: fullName.trim(),
          roles,
          isActive,
        });
      }

      onSaved();
      onClose();
    } catch (caught) {
      setError(
        caught instanceof ApiError ? caught.message : t('common.somethingWentWrong'),
      );
    } finally {
      setIsSaving(false);
    }
  }

  function toggle(role: string): void {
    setRoles((current) =>
      current.includes(role) ? current.filter((r) => r !== role) : [...current, role],
    );
  }

  const ready =
    employeeNumber.trim() !== '' && fullName.trim() !== '' && (!isNew || password !== '');

  return (
    <Modal title={isNew ? t('action.addWorker') : `Edit ${user.fullName}`} onClose={onClose}>
      <form
        onSubmit={(event) => {
          event.preventDefault();
          void save();
        }}
        noValidate
      >
        <div className="mb-4 grid gap-4 sm:grid-cols-2">
          <div>
            <label className="field-label" htmlFor="user-number">
              {t('field.employeeNumber')}
            </label>
            <input
              id="user-number"
              className="field-input"
              maxLength={20}
              value={employeeNumber}
              disabled={isSaving}
              onChange={(event) => {
                setEmployeeNumber(event.target.value);
              }}
            />
            <p className="mt-1 text-xs text-ink-muted">
              {t('users.numberIsLogin')}
            </p>
          </div>

          <div>
            <label className="field-label" htmlFor="user-name">
              {t('users.fullName')}
            </label>
            <input
              id="user-name"
              className="field-input"
              maxLength={100}
              value={fullName}
              disabled={isSaving}
              onChange={(event) => {
                setFullName(event.target.value);
              }}
            />
          </div>
        </div>

        {isNew && (
          <div className="mb-4">
            <label className="field-label" htmlFor="user-password">
              {t('users.firstPassword')}
            </label>
            <input
              id="user-password"
              type="password"
              className="field-input"
              value={password}
              disabled={isSaving}
              onChange={(event) => {
                setPassword(event.target.value);
              }}
            />
            <p className="mt-1 text-xs text-ink-muted">
              At least eight characters, with a digit and a small letter. Tell it to him;
              nobody can read it back afterwards.
            </p>
          </div>
        )}

        <fieldset className="mb-4">
          <legend className="field-label">{t('field.whatHeMayDo')}</legend>
          <div className="grid gap-2 sm:grid-cols-2">
            {Object.values(RoleNames).map((role: RoleName) => (
              <label
                key={role}
                className="flex items-center gap-2 rounded-control border border-line px-3 py-2 text-sm"
              >
                <input
                  type="checkbox"
                  checked={roles.includes(role)}
                  disabled={isSaving}
                  onChange={() => {
                    toggle(role);
                  }}
                />
                <span className="text-ink">{labelForRole(role)}</span>
              </label>
            ))}
          </div>
          <p className="mt-1 text-xs text-ink-muted">
            {t('users.severalRoles')}
          </p>
        </fieldset>

        {!isNew && (
          <label className="mb-4 flex items-center gap-2 rounded-control bg-canvas px-3 py-2 text-sm">
            <input
              type="checkbox"
              checked={isActive}
              disabled={isSaving}
              onChange={(event) => {
                setIsActive(event.target.checked);
              }}
            />
            <span className="text-ink">{t('users.stillWorksHere')}</span>
          </label>
        )}

        {error !== null && (
          <p
            role="alert"
            className="mb-4 rounded-control border border-s-4 border-bad/30 border-s-bad bg-bad-soft px-4 py-3 text-sm font-medium text-bad"
          >
            {error}
          </p>
        )}

        <button type="submit" className="btn-primary" disabled={isSaving || !ready}>
          {isSaving ? 'Saving…' : isNew ? t('users.addHim') : t('users.saveChanges')}
        </button>

        {!isNew && (
          <p className="mt-2 text-xs text-ink-muted">
            {t('users.leaverNote')}
          </p>
        )}
      </form>
    </Modal>
  );
}

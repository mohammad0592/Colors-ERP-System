import { useState, type ReactElement } from 'react';
import { Modal } from '../../components/ui/Modal';
import { ApiError } from '../../lib/apiClient';
import { usersApi, type UserDto } from './api';

/**
 * Setting somebody's password (specification section 3).
 *
 * The administrator does this face to face. There is no self-service reset: the factory
 * has no email, so the flow that normally carries a reset link does not exist.
 *
 * Doing it also frees an account that wrong passwords had locked, because a forgotten
 * password is the usual reason it is locked in the first place.
 */
export function ResetPasswordDialog({
  user,
  onClose,
  onSaved,
}: {
  user: UserDto;
  onClose: () => void;
  onSaved: () => void;
}): ReactElement {
  const [password, setPassword] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [isSaving, setIsSaving] = useState(false);

  async function save(): Promise<void> {
    setError(null);
    setIsSaving(true);
    try {
      await usersApi.resetPassword(user.id, password);
      onSaved();
      onClose();
    } catch (caught) {
      setError(
        caught instanceof ApiError ? caught.message : 'Something went wrong. Try again.',
      );
    } finally {
      setIsSaving(false);
    }
  }

  return (
    <Modal title={`New password for ${user.fullName}`} onClose={onClose}>
      <form
        onSubmit={(event) => {
          event.preventDefault();
          void save();
        }}
        noValidate
      >
        <div className="mb-4">
          <label className="field-label" htmlFor="new-password">
            New password
          </label>
          <input
            id="new-password"
            type="password"
            className="field-input"
            value={password}
            disabled={isSaving}
            onChange={(event) => {
              setPassword(event.target.value);
            }}
          />
          <p className="mt-1 text-xs text-ink-muted">
            At least eight characters, with a digit and a small letter.
          </p>
        </div>

        {error !== null && (
          <p
            role="alert"
            className="mb-4 rounded-control border border-l-4 border-bad/30 border-l-bad bg-bad-soft px-4 py-3 text-sm font-medium text-bad"
          >
            {error}
          </p>
        )}

        <button type="submit" className="btn-primary" disabled={isSaving || password === ''}>
          {isSaving ? 'Saving…' : 'Set it'}
        </button>
        <p className="mt-2 text-xs text-ink-muted">
          Tell him the password now — nobody can read it back afterwards, not even an
          administrator. Any session he still has open will end.
        </p>
      </form>
    </Modal>
  );
}

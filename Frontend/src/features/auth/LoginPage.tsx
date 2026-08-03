import { useState, type ReactElement } from 'react';
import { useLocation, useNavigate } from 'react-router-dom';
import { useAuth } from '../../hooks/useAuth';
import { ApiError } from '../../lib/apiClient';
import './LoginPage.css';

/** Where the worker was heading before being sent to sign in. */
interface LoginLocationState {
  from?: string;
}

/**
 * The sign-in screen.
 *
 * Built for a tablet held on the factory floor: large fields, large button, one
 * message at a time, and no small text.
 */
export function LoginPage(): ReactElement {
  const [employeeNumber, setEmployeeNumber] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  const { signIn } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();

  async function submit(): Promise<void> {
    setError(null);
    setIsSubmitting(true);

    try {
      await signIn(employeeNumber.trim().toUpperCase(), password);

      // Back to the page they wanted before being sent here.
      const state = location.state as LoginLocationState | null;
      await navigate(state?.from ?? '/', { replace: true });
    } catch (caught) {
      setError(messageFor(caught));
      setPassword('');
    } finally {
      setIsSubmitting(false);
    }
  }

  const canSubmit = employeeNumber.trim() !== '' && password !== '' && !isSubmitting;

  return (
    <div className="login-page">
      <form
        className="login-card"
        onSubmit={(event) => {
          event.preventDefault();
          void submit();
        }}
        noValidate
      >
        <header className="login-header">
          <h1>Colors ERP</h1>
          <p>Styrofoam Factory</p>
        </header>

        <label className="field" htmlFor="employeeNumber">
          <span className="field-label">Employee number</span>
          <input
            id="employeeNumber"
            name="employeeNumber"
            type="text"
            inputMode="text"
            autoComplete="username"
            autoCapitalize="characters"
            spellCheck="false"
            placeholder="EMP0006"
            value={employeeNumber}
            onChange={(event) => {
              setEmployeeNumber(event.target.value);
            }}
            disabled={isSubmitting}
            autoFocus
            required
          />
        </label>

        <label className="field" htmlFor="password">
          <span className="field-label">Password</span>
          <input
            id="password"
            name="password"
            type="password"
            autoComplete="current-password"
            value={password}
            onChange={(event) => {
              setPassword(event.target.value);
            }}
            disabled={isSubmitting}
            required
          />
        </label>

        {/* role="alert" so a screen reader announces it. */}
        {error !== null && (
          <p className="login-error" role="alert">
            {error}
          </p>
        )}

        <button className="login-button" type="submit" disabled={!canSubmit}>
          {isSubmitting ? 'Signing in…' : 'Sign in'}
        </button>
      </form>
    </div>
  );
}

/**
 * Turns the backend's error code into something a worker can act on.
 * Branching on the code, not the text, so the wording can change freely.
 */
function messageFor(error: unknown): string {
  if (!(error instanceof ApiError)) {
    return 'Something went wrong. Try again.';
  }

  switch (error.code) {
    case 'InvalidCredentials':
      return 'Employee number or password is wrong.';
    case 'AccountLocked':
      return 'Too many wrong tries. Wait five minutes, then try again.';
    case 'AccountInactive':
      return 'This account is switched off. Ask the administrator.';
    case 'NetworkError':
      return 'Cannot reach the server. Check the network, then try again.';
    default:
      return error.message;
  }
}

import { useState, type ReactElement } from 'react';
import { useLocation, useNavigate } from 'react-router-dom';
import { useAuth } from '../../hooks/useAuth';
import { ApiError } from '../../lib/apiClient';

/** Where the worker was heading before being sent to sign in. */
interface LoginLocationState {
  from?: string;
}

/**
 * The sign-in screen.
 *
 * Follows the Figma design — centred card on a dark patterned background — but signs
 * in with an **employee number**, not an email. That is how the factory identifies
 * people on its paper forms, and many workers have no company email.
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
    <div className="relative grid min-h-dvh place-items-center overflow-hidden bg-sidebar p-6">
      {/* Faint grid, as in the design. Purely decorative. */}
      <div
        aria-hidden="true"
        className="absolute inset-0 opacity-[0.07]"
        style={{
          backgroundImage:
            'linear-gradient(#fff 1px, transparent 1px), linear-gradient(90deg, #fff 1px, transparent 1px)',
          backgroundSize: '48px 48px',
        }}
      />

      <form
        onSubmit={(event) => {
          event.preventDefault();
          void submit();
        }}
        noValidate
        className="relative w-full max-w-md rounded-2xl bg-surface p-8 shadow-raised sm:p-10"
      >
        <div className="mb-8 text-center">
          <img
            src="/logo-full.png"
            alt="Colors — Company for Paper and Plastic Industries"
            width={1151}
            height={656}
            className="mx-auto mb-6 h-auto w-full max-w-[260px]"
          />
          {/* The logo already carries the company name, so the heading says what
              the system is rather than repeating "Colors". */}
          <h1 className="text-xl font-bold text-ink">
            Production &amp; Inventory System
          </h1>
          <p className="mt-1 text-sm text-ink-muted">Styrofoam Factory</p>
        </div>

        <div className="mb-5">
          <label className="field-label" htmlFor="employeeNumber">
            Employee number
          </label>
          <input
            id="employeeNumber"
            name="employeeNumber"
            type="text"
            className="field-input"
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
        </div>

        <div className="mb-5">
          <label className="field-label" htmlFor="password">
            Password
          </label>
          <input
            id="password"
            name="password"
            type="password"
            className="field-input"
            autoComplete="current-password"
            value={password}
            onChange={(event) => {
              setPassword(event.target.value);
            }}
            disabled={isSubmitting}
            required
          />
        </div>

        {/* role="alert" so a screen reader announces it. */}
        {error !== null && (
          <p
            role="alert"
            className="mb-5 rounded-control border border-s-4 border-bad/30 border-s-bad bg-bad-soft px-4 py-3 text-sm font-medium text-bad"
          >
            {error}
          </p>
        )}

        <button type="submit" className="btn-primary" disabled={!canSubmit}>
          {isSubmitting ? 'Signing in…' : 'Sign in'}
        </button>

        <p className="mt-6 text-center text-xs text-ink-muted">
          Ask an administrator if you cannot sign in.
        </p>
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

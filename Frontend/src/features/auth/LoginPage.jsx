import { useState } from 'react';
import { useLocation, useNavigate } from 'react-router-dom';
import { useAuth } from '../../hooks/useAuth';
import './LoginPage.css';

/**
 * The sign-in screen.
 *
 * Built for a tablet held on the factory floor: large fields, large button, one
 * message at a time, and no small text.
 */
export function LoginPage() {
  const [employeeNumber, setEmployeeNumber] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  const { signIn } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();

  async function handleSubmit(event) {
    event.preventDefault();
    setError(null);
    setIsSubmitting(true);

    try {
      await signIn(employeeNumber.trim().toUpperCase(), password);
      // Back to the page they wanted before being sent here.
      navigate(location.state?.from ?? '/', { replace: true });
    } catch (apiError) {
      setError(messageFor(apiError));
      setPassword('');
    } finally {
      setIsSubmitting(false);
    }
  }

  const canSubmit = employeeNumber.trim() !== '' && password !== '' && !isSubmitting;

  return (
    <div className="login-page">
      <form className="login-card" onSubmit={handleSubmit} noValidate>
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
            onChange={(event) => setEmployeeNumber(event.target.value)}
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
            onChange={(event) => setPassword(event.target.value)}
            disabled={isSubmitting}
            required
          />
        </label>

        {/* role="alert" so a screen reader announces it, and aria-live for changes. */}
        {error && (
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
function messageFor(error) {
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
      return error.message ?? 'Something went wrong. Try again.';
  }
}

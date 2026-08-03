import { useAuth } from '../../hooks/useAuth';
import './HomePage.css';

/**
 * Placeholder home screen.
 *
 * Proves the whole chain works — sign in, token, protected call, roles, sign out.
 * The real screen for each worker comes with the modules in the build order
 * (specification section 17).
 */
export function HomePage() {
  const { user, signOut } = useAuth();

  return (
    <div className="home-page">
      <header className="home-header">
        <div>
          <h1>Colors ERP</h1>
          <p className="home-subtitle">Styrofoam Factory</p>
        </div>
        <button className="sign-out" type="button" onClick={signOut}>
          Sign out
        </button>
      </header>

      <main className="home-main">
        <section className="card">
          <h2>Signed in</h2>
          <dl className="details">
            <dt>Name</dt>
            <dd>{user.fullName}</dd>

            <dt>Employee number</dt>
            <dd>{user.employeeNumber}</dd>

            <dt>Roles</dt>
            <dd>
              <ul className="roles">
                {user.roles.map((role) => (
                  <li key={role}>{role}</li>
                ))}
              </ul>
            </dd>
          </dl>
        </section>

        <p className="next-up">
          Next: master data — production lines, shifts, units, materials, colours and
          plate sizes.
        </p>
      </main>
    </div>
  );
}

import type { ReactElement } from 'react';
import { Link } from 'react-router-dom';
import { PageHeader } from '../../components/ui/PageHeader';
import { Icon } from '../../components/ui/Icon';
import { useAuth } from '../../hooks/useAuth';

/**
 * The dashboard.
 *
 * The layout follows the Figma design — status pill, statistic cards, quick actions.
 * The numbers are deliberately absent until the modules that produce them exist:
 * an invented figure on a factory screen is worse than an empty one, because
 * somebody will act on it.
 */
export function DashboardPage(): ReactElement {
  const { user } = useAuth();

  const today = new Date().toLocaleDateString('en-GB', {
    weekday: 'long',
    day: 'numeric',
    month: 'long',
    year: 'numeric',
  });

  return (
    <>
      <PageHeader
        title="Operations Dashboard"
        subtitle={today}
        badge={
          <span className="inline-flex items-center gap-2 rounded-full bg-ok-soft px-3 py-1.5 text-sm font-semibold text-ok">
            <span className="size-2 rounded-full bg-ok" />
            System running
          </span>
        }
      />

      <section className="card mb-6 p-6">
        <h2 className="text-lg font-semibold text-ink">Phase 1 is complete</h2>
        <p className="mt-2 max-w-2xl text-ink-soft">
          Sign in, roles and sessions are working. The screens in the menu are being built
          in order — master data first, then the extruder, then thermoforming, pallets and
          the recycler.
        </p>

        <div className="mt-5 grid gap-3 sm:grid-cols-3">
          <Fact label="Signed in as" value={user?.fullName ?? ''} />
          <Fact label="Employee number" value={user?.employeeNumber ?? ''} />
          <Fact label="Roles" value={String(user?.roles.length ?? 0)} />
        </div>
      </section>

      <section>
        <h2 className="mb-3 text-sm font-semibold tracking-wider text-ink-muted uppercase">
          Quick actions
        </h2>

        <div className="grid grid-cols-2 gap-3 sm:grid-cols-3 lg:grid-cols-6">
          <QuickAction to="/inventory" icon="inventory" label="Inventory" />
          <QuickAction to="/inventory/receive" icon="receive" label="Receive" />
          <QuickAction to="/production/rolls" icon="roll" label="Rolls" />
          <QuickAction to="/production/thermo" icon="thermo" label="Thermo" />
          <QuickAction to="/production/pallets" icon="pallet" label="Pallets" />
          <QuickAction to="/production/recycler" icon="recycler" label="Recycler" />
        </div>
      </section>
    </>
  );
}

function Fact({ label, value }: { label: string; value: string }): ReactElement {
  return (
    <div className="rounded-control bg-canvas p-4">
      <p className="text-xs font-medium text-ink-muted">{label}</p>
      <p className="mt-1 truncate font-semibold text-ink">{value}</p>
    </div>
  );
}

function QuickAction({
  to,
  icon,
  label,
}: {
  to: string;
  icon: string;
  label: string;
}): ReactElement {
  return (
    <Link
      to={to}
      className="card flex flex-col items-center justify-center gap-2 p-5 transition-colors hover:border-brand-200 hover:bg-brand-50"
    >
      <span className="grid size-11 place-items-center rounded-control bg-brand-50 text-brand-600">
        <Icon name={icon} />
      </span>
      <span className="text-center text-sm font-medium text-ink-soft">{label}</span>
    </Link>
  );
}

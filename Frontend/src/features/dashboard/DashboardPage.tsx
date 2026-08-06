import { useQuery } from '@tanstack/react-query';
import type { ReactElement } from 'react';
import { Link } from 'react-router-dom';
import { navigation, type NavItem } from '../../components/layout/navigation';
import { PageHeader } from '../../components/ui/PageHeader';
import { Icon } from '../../components/ui/Icon';
import { useAuth } from '../../hooks/useAuth';
import { formatDate } from '../shifts/shiftFormat';
import { dashboardApi, type DashboardAlertDto } from './api';

/** The shortcuts offered, in the order a shift actually runs. */
const shortcutPaths = [
  '/inventory',
  '/inventory/receive',
  '/production/rolls',
  '/production/thermo',
  '/production/pallets',
  '/production/recycler',
];

/**
 * Where each kind of waiting work is done, so a count is something the reader can act
 * on rather than a number to worry about.
 */
const alertGoesTo: Record<string, string> = {
  'material-low': '/inventory',
  'ticket-open': '/inventory/issue',
  'roll-in-thermo': '/production/thermo',
  'roll-needs-test': '/production/roll-tests',
  'run-needs-count': '/production/thermo-tests',
  'bag-loose': '/production/pallets',
  'pallet-open': '/production/pallets',
};

/**
 * The dashboard (specification section 13).
 *
 * Two questions and nothing else: <b>what is running</b>, and <b>what is waiting for
 * somebody</b>. Every figure is read from records that already exist — the shift's
 * numbers come through the same service as the shift report, so this screen cannot tell
 * a different story about the same shift.
 *
 * Nothing that reads zero is shown. A dashboard with seven boxes, five of them empty, is
 * one people stop reading.
 */
export function DashboardPage(): ReactElement {
  const { user, hasRole } = useAuth();

  const dashboard = useQuery({
    queryKey: ['dashboard'],
    queryFn: () => dashboardApi.get(),
  });

  const today = new Date().toLocaleDateString('en-GB', {
    weekday: 'long',
    day: 'numeric',
    month: 'long',
    year: 'numeric',
  });

  // Built from the same list as the sidebar and filtered by the same rule, so a
  // shortcut can never offer a screen the menu has hidden.
  const allItems = navigation.flatMap((group) => group.items);
  const shortcuts: NavItem[] = shortcutPaths
    .map((path) => allItems.find((item) => item.path === path))
    .filter((item): item is NavItem => item !== undefined)
    .filter((item) => item.roles === undefined || hasRole(...item.roles));

  const shift = dashboard.data?.openShift ?? null;
  const summary = dashboard.data?.summary ?? null;
  const alerts = dashboard.data?.needsAttention ?? [];

  return (
    <>
      <PageHeader
        title="Operations Dashboard"
        subtitle={today}
        badge={
          shift === null ? (
            <span className="inline-flex items-center gap-2 rounded-full bg-canvas px-3 py-1.5 text-sm font-semibold text-ink-soft">
              <span className="size-2 rounded-full bg-ink-muted" />
              No shift open
            </span>
          ) : (
            <span className="inline-flex items-center gap-2 rounded-full bg-ok-soft px-3 py-1.5 text-sm font-semibold text-ok">
              <span className="size-2 rounded-full bg-ok" />
              Shift {shift.shiftName} is running
            </span>
          )
        }
      />

      {dashboard.isError && (
        <p className="mb-6 rounded-control border border-l-4 border-bad/30 border-l-bad bg-bad-soft px-4 py-3 text-sm font-medium text-bad">
          Could not load what is happening. The rest of the system still works.
        </p>
      )}

      {shift === null ? (
        <section className="card mb-6 p-6">
          <h2 className="text-lg font-semibold text-ink">No shift is open</h2>
          <p className="mt-2 max-w-2xl text-ink-soft">
            The factory is between shifts. A supervisor opens the next one from the Shifts
            screen, ticking the lines that will run.
          </p>
          <div className="mt-5 grid gap-3 sm:grid-cols-3">
            <Fact label="Signed in as" value={user?.fullName ?? ''} />
            <Fact label="Employee number" value={user?.employeeNumber ?? ''} />
            <Fact label="Roles" value={String(user?.roles.length ?? 0)} />
          </div>
        </section>
      ) : (
        <section className="card mb-6 p-6">
          <div className="mb-4 flex flex-wrap items-baseline justify-between gap-3">
            <h2 className="text-lg font-semibold text-ink">
              Shift {shift.shiftName} · {formatDate(shift.productionDate)}
            </h2>
            <p className="text-sm text-ink-muted">
              {shift.lineNames.join(' · ')}
              {shift.supervisorName !== null && ` · ${shift.supervisorName}`}
            </p>
          </div>

          {summary === null ? (
            <p className="text-ink-soft">Nothing has been recorded on it yet.</p>
          ) : (
            <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-5">
              <Fact label="Rolls made" value={String(summary.rollsProduced)} />
              <Fact label="Rolls formed" value={String(summary.rollsFormed)} />
              <Fact label="Bags" value={String(summary.bagCount)} />
              <Fact label="Pallets finished" value={String(summary.palletsCompleted)} />
              <Fact
                label="Lost in forming"
                value={
                  summary.lossPercentage === null
                    ? '—'
                    : `${String(summary.lossPercentage)}%`
                }
              />
            </div>
          )}
        </section>
      )}

      {alerts.length > 0 && (
        <section className="mb-6">
          <h2 className="mb-3 text-sm font-semibold tracking-wider text-ink-muted uppercase">
            Waiting for someone
          </h2>

          <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
            {alerts.map((alert) => (
              <Alert key={alert.kind} alert={alert} />
            ))}
          </div>
        </section>
      )}

      {shortcuts.length > 0 && (
        <section>
          <h2 className="mb-3 text-sm font-semibold tracking-wider text-ink-muted uppercase">
            Quick actions
          </h2>

          <div className="grid grid-cols-2 gap-3 sm:grid-cols-3 lg:grid-cols-6">
            {shortcuts.map((item) => (
              <QuickAction
                key={item.path}
                to={item.path}
                icon={item.icon}
                label={item.label}
              />
            ))}
          </div>
        </section>
      )}
    </>
  );
}

/**
 * One thing waiting for somebody, and the screen where it gets done.
 *
 * The ones that stop a shift closing are marked, because a supervisor at the end of his
 * shift needs to know which of these is merely untidy and which will refuse him.
 */
function Alert({ alert }: { alert: DashboardAlertDto }): ReactElement {
  const to = alertGoesTo[alert.kind];

  const body = (
    <>
      <div className="flex items-baseline justify-between gap-3">
        <p className="font-semibold text-ink first-letter:uppercase">
          {alert.count} {alert.count === 1 ? alert.label : alert.labelPlural}
        </p>
        {alert.blocksShiftClose && (
          <span className="shrink-0 rounded-full bg-warn-soft px-2 py-0.5 text-xs font-semibold text-warn">
            holds the shift open
          </span>
        )}
      </div>
      <p className="mt-1 text-sm text-ink-muted">{alert.detail}</p>
    </>
  );

  return to === undefined ? (
    <div className="card p-4">{body}</div>
  ) : (
    <Link
      to={to}
      className="card p-4 transition-colors hover:border-brand-200 hover:bg-brand-50"
    >
      {body}
    </Link>
  );
}

function Fact({ label, value }: { label: string; value: string }): ReactElement {
  return (
    <div className="rounded-control bg-canvas p-4">
      <p className="text-xs font-medium text-ink-muted">{label}</p>
      <p className="mt-1 truncate text-xl font-bold text-ink tabular-nums">{value}</p>
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

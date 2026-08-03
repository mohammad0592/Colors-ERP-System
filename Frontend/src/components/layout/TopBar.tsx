import { useState, type ReactElement } from 'react';
import { useAuth } from '../../hooks/useAuth';
import { labelForRole } from '../../lib/roles';
import { Icon } from '../ui/Icon';

interface TopBarProps {
  /** Where the worker is, shown as "Colors ERP / Inventory". */
  breadcrumb: string;
  onOpenMobileMenu: () => void;
}

export function TopBar({ breadcrumb, onOpenMobileMenu }: TopBarProps): ReactElement {
  const { user, signOut } = useAuth();
  const [isMenuOpen, setIsMenuOpen] = useState(false);

  const initials =
    user?.fullName
      .split(' ')
      .filter((part) => part.length > 0)
      .slice(0, 2)
      .map((part) => part[0])
      .join('')
      .toUpperCase() ?? '';

  return (
    <header className="sticky top-0 z-20 flex h-16 shrink-0 items-center gap-3 border-b border-line bg-surface px-4 lg:px-6">
      <button
        type="button"
        onClick={onOpenMobileMenu}
        aria-label="Open menu"
        className="grid size-touch shrink-0 place-items-center rounded-control text-ink-soft hover:bg-canvas lg:hidden"
      >
        <Icon name="menu" />
      </button>

      <nav aria-label="Breadcrumb" className="min-w-0 flex-1">
        <p className="truncate text-sm text-ink-muted">
          Colors ERP <span className="mx-1.5">/</span>
          <span className="font-semibold text-ink">{breadcrumb}</span>
        </p>
      </nav>

      <div className="relative">
        <button
          type="button"
          onClick={() => {
            setIsMenuOpen((open) => !open);
          }}
          aria-expanded={isMenuOpen}
          aria-haspopup="menu"
          className="flex min-h-touch items-center gap-3 rounded-control px-2 transition-colors hover:bg-canvas"
        >
          <span className="grid size-9 shrink-0 place-items-center rounded-full bg-brand-100 text-sm font-bold text-brand-700">
            {initials}
          </span>
          <span className="hidden text-left sm:block">
            <span className="block text-sm font-semibold text-ink">{user?.fullName}</span>
            <span className="block text-xs text-ink-muted">
              {user?.roles[0] !== undefined ? labelForRole(user.roles[0]) : ''}
            </span>
          </span>
        </button>

        {isMenuOpen && (
          <>
            <button
              type="button"
              aria-label="Close menu"
              className="fixed inset-0 z-10 cursor-default"
              onClick={() => {
                setIsMenuOpen(false);
              }}
            />
            <div
              role="menu"
              className="absolute right-0 z-20 mt-2 w-64 rounded-card border border-line bg-surface p-2 shadow-raised"
            >
              <div className="border-b border-line px-3 pb-3">
                <p className="text-sm font-semibold text-ink">{user?.fullName}</p>
                <p className="text-xs text-ink-muted">{user?.employeeNumber}</p>
                <ul className="mt-2 flex flex-wrap gap-1.5">
                  {user?.roles.map((role) => (
                    <li
                      key={role}
                      className="rounded-full bg-brand-50 px-2 py-0.5 text-xs font-medium text-brand-700"
                    >
                      {labelForRole(role)}
                    </li>
                  ))}
                </ul>
              </div>

              <button
                type="button"
                role="menuitem"
                onClick={() => {
                  void signOut();
                }}
                className="mt-2 flex min-h-touch w-full items-center gap-3 rounded-control px-3 text-sm font-medium text-ink-soft transition-colors hover:bg-canvas"
              >
                <Icon name="logout" className="size-4" />
                Sign out
              </button>
            </div>
          </>
        )}
      </div>
    </header>
  );
}

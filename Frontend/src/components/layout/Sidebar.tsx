import type { ReactElement } from 'react';
import { NavLink } from 'react-router-dom';
import { useAuth } from '../../hooks/useAuth';
import { useTranslation } from '../../hooks/useTranslation';
import { Icon } from '../ui/Icon';
import { navigation, type NavItem } from './navigation';

interface SidebarProps {
  isCollapsed: boolean;
  onToggleCollapsed: () => void;
  /** Open state on a small screen, where the sidebar slides over the page. */
  isOpenOnMobile: boolean;
  onCloseMobile: () => void;
}

export function Sidebar({
  isCollapsed,
  onToggleCollapsed,
  isOpenOnMobile,
  onCloseMobile,
}: SidebarProps): ReactElement {
  const { t } = useTranslation();
  const { hasRole } = useAuth();

  const canSee = (item: NavItem): boolean =>
    item.roles === undefined || hasRole(...item.roles);

  return (
    <>
      {/* Tapping outside closes the sidebar on a phone or a held tablet. */}
      {isOpenOnMobile && (
        <button
          type="button"
          aria-label={t('common.close')}
          onClick={onCloseMobile}
          className="fixed inset-0 z-30 bg-ink/50 lg:hidden"
        />
      )}

      <aside
        className={[
          'fixed inset-y-0 start-0 z-40 flex flex-col bg-sidebar transition-all duration-200',
          isCollapsed ? 'w-sidebar-narrow' : 'w-sidebar',
          isOpenOnMobile ? 'translate-x-0' : '-translate-x-full',
          'lg:translate-x-0',
        ].join(' ')}
      >
        {/* Company mark. The wordmark itself is dark grey, so only the leaf
            symbol is used here — on the dark sidebar the letters would vanish. */}
        <div className="flex h-16 shrink-0 items-center gap-3 px-4">
          <img
            src="/logo-mark.png"
            alt=""
            width={40}
            height={40}
            className="size-10 shrink-0 object-contain"
          />
          {!isCollapsed && (
            <div className="min-w-0">
              <p className="truncate font-bold text-white">{t('app.name')}</p>
              <p className="truncate text-xs text-sidebar-heading">{t('app.tagline')}</p>
            </div>
          )}
        </div>

        <nav className="flex-1 overflow-y-auto px-3 pb-4">
          {navigation.map((group) => {
            const visible = group.items.filter(canSee);
            if (visible.length === 0) {
              return null;
            }

            return (
              <div key={t(group.heading)} className="mb-5">
                {!isCollapsed && (
                  <p className="mb-2 px-3 text-[11px] font-semibold tracking-wider text-sidebar-heading uppercase">
                    {t(group.heading)}
                  </p>
                )}

                <ul className="space-y-1">
                  {visible.map((item) => (
                    <li key={item.path}>
                      <NavLink
                        to={item.path}
                        end={item.path === '/'}
                        onClick={onCloseMobile}
                        title={isCollapsed ? t(item.label) : undefined}
                        className={({ isActive }) =>
                          [
                            'flex min-h-touch items-center gap-3 rounded-control px-3 text-sm font-medium transition-colors',
                            isCollapsed ? 'justify-center' : '',
                            isActive
                              ? 'bg-brand-600 text-white'
                              : 'text-sidebar-text hover:bg-sidebar-hover hover:text-white',
                          ].join(' ')
                        }
                      >
                        <Icon name={item.icon} className="size-5 shrink-0" />
                        {!isCollapsed && <span className="truncate">{t(item.label)}</span>}
                      </NavLink>
                    </li>
                  ))}
                </ul>
              </div>
            );
          })}
        </nav>

        <button
          type="button"
          onClick={onToggleCollapsed}
          className="hidden min-h-touch items-center gap-3 px-6 text-sm text-sidebar-heading transition-colors hover:text-white lg:flex"
        >
          <Icon name={isCollapsed ? 'chevronRight' : 'chevronLeft'} className="size-4" />
          {!isCollapsed && <span>{t('nav.collapse')}</span>}
        </button>
      </aside>
    </>
  );
}

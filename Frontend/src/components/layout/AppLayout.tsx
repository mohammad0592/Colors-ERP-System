import { useEffect, useState, type ReactElement } from 'react';
import { Outlet, useLocation } from 'react-router-dom';
import { navigation } from './navigation';
import { Sidebar } from './Sidebar';
import { TopBar } from './TopBar';

const COLLAPSED_KEY = 'colors.sidebarCollapsed';

/** The frame every signed-in screen sits inside: sidebar, top bar, content. */
export function AppLayout(): ReactElement {
  const location = useLocation();

  // Remembered, because a supervisor at a desk and an operator on a tablet want
  // different widths, and neither should set it again every morning.
  const [isCollapsed, setIsCollapsed] = useState(
    () => localStorage.getItem(COLLAPSED_KEY) === 'true',
  );
  const [isOpenOnMobile, setIsOpenOnMobile] = useState(false);

  useEffect(() => {
    localStorage.setItem(COLLAPSED_KEY, String(isCollapsed));
  }, [isCollapsed]);

  const breadcrumb =
    navigation
      .flatMap((group) => group.items)
      .find((item) => item.path === location.pathname)?.label ?? 'Dashboard';

  return (
    <div className="min-h-dvh">
      <Sidebar
        isCollapsed={isCollapsed}
        onToggleCollapsed={() => {
          setIsCollapsed((collapsed) => !collapsed);
        }}
        isOpenOnMobile={isOpenOnMobile}
        onCloseMobile={() => {
          setIsOpenOnMobile(false);
        }}
      />

      <div
        className={[
          'flex min-h-dvh flex-col transition-all duration-200',
          isCollapsed ? 'lg:ps-sidebar-narrow' : 'lg:ps-sidebar',
        ].join(' ')}
      >
        <TopBar
          breadcrumb={breadcrumb}
          onOpenMobileMenu={() => {
            setIsOpenOnMobile(true);
          }}
        />

        <main className="flex-1 p-4 lg:p-6">
          <Outlet />
        </main>
      </div>
    </div>
  );
}

import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import type { ReactElement } from 'react';
import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom';
import { AppLayout } from './components/layout/AppLayout';
import { AuditPage } from './features/audit/AuditPage';
import { AuthProvider } from './features/auth/AuthProvider';
import { LoginPage } from './features/auth/LoginPage';
import { DashboardPage } from './features/dashboard/DashboardPage';
import { DispatchPage } from './features/dispatch/DispatchPage';
import { InventoryPage } from './features/inventory/InventoryPage';
import { ReceiveMaterialsPage } from './features/inventory/ReceiveMaterialsPage';
import { MaterialIssuePage } from './features/material-issue/MaterialIssuePage';
import { RollProductionPage } from './features/production/RollProductionPage';
import { RollTestsPage } from './features/production/RollTestsPage';
import { MasterDataPage } from './features/master-data/MasterDataPage';
import { PackagingPage } from './features/packaging/PackagingPage';
import { PalletsPage } from './features/pallets/PalletsPage';
import { RecipesPage } from './features/recipes/RecipesPage';
import { RecyclerPage } from './features/recycler/RecyclerPage';
import { ReportsPage } from './features/reports/ReportsPage';
import { ShiftsPage } from './features/shifts/ShiftsPage';
import { UsersPage } from './features/users/UsersPage';
import { TracePage } from './features/trace/TracePage';
import { ThermoProductionPage } from './features/thermo/ThermoProductionPage';
import { ThermoTestsPage } from './features/thermo/ThermoTestsPage';
import { LanguageProvider } from './lib/i18n/LanguageProvider';
import { rolesFor, type ScreenPath } from './routes/access';
import { ProtectedRoute } from './routes/ProtectedRoute';
import { plannedRoutes } from './routes/routes';

// One client for the whole app. Factory data changes when someone changes it, not
// behind the user's back — so no refetch on window focus, and a short staleness
// window keeps tab switches instant without hiding real edits for long.
const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      retry: 1,
      refetchOnWindowFocus: false,
      staleTime: 30_000,
    },
  },
});

/**
 * Every screen behind the sign-in, and the component that draws it.
 *
 * Only the pairing lives here. **Who may open each one is not written here** — it is
 * read from `routes/access.ts`, the same list the sidebar reads, so the menu and the
 * guard cannot drift apart. They used to, in six places, and the result was a role
 * being let onto a screen it had no link to.
 */
const screens: { path: ScreenPath; element: ReactElement }[] = [
  { path: '/', element: <DashboardPage /> },
  { path: '/inventory', element: <InventoryPage /> },
  { path: '/trace', element: <TracePage /> },
  { path: '/inventory/receive', element: <ReceiveMaterialsPage /> },
  { path: '/inventory/issue', element: <MaterialIssuePage /> },
  { path: '/production/rolls', element: <RollProductionPage /> },
  { path: '/production/roll-tests', element: <RollTestsPage /> },
  { path: '/production/thermo', element: <ThermoProductionPage /> },
  { path: '/production/thermo-tests', element: <ThermoTestsPage /> },
  { path: '/production/pallets', element: <PalletsPage /> },
  { path: '/production/packaging', element: <PackagingPage /> },
  { path: '/production/dispatch', element: <DispatchPage /> },
  { path: '/production/recycler', element: <RecyclerPage /> },
  { path: '/reports', element: <ReportsPage /> },
  { path: '/audit', element: <AuditPage /> },
  { path: '/recipes', element: <RecipesPage /> },
  { path: '/shifts', element: <ShiftsPage /> },
  { path: '/master-data', element: <MasterDataPage /> },
  { path: '/users', element: <UsersPage /> },
];

export default function App(): ReactElement {
  return (
    <QueryClientProvider client={queryClient}>
      <LanguageProvider>
        <BrowserRouter>
          <AuthProvider>
            <Routes>
              <Route path="/login" element={<LoginPage />} />

              {/* Everything below needs a signed-in worker. */}
              <Route element={<ProtectedRoute />}>
                <Route element={<AppLayout />}>
                  {screens.map(({ path, element }) => (
                    <Route key={path} element={<ProtectedRoute roles={rolesFor(path)} />}>
                      <Route path={path} element={element} />
                    </Route>
                  ))}

                  {plannedRoutes.map(({ path, element }) => (
                    <Route key={path} path={path} element={element} />
                  ))}
                </Route>
              </Route>

              <Route path="*" element={<Navigate to="/" replace />} />
            </Routes>
          </AuthProvider>
        </BrowserRouter>
      </LanguageProvider>
    </QueryClientProvider>
  );
}

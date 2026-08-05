import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import type { ReactElement } from 'react';
import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom';
import { AppLayout } from './components/layout/AppLayout';
import { AuthProvider } from './features/auth/AuthProvider';
import { LoginPage } from './features/auth/LoginPage';
import { DashboardPage } from './features/dashboard/DashboardPage';
import { MasterDataPage } from './features/master-data/MasterDataPage';
import { RecipesPage } from './features/recipes/RecipesPage';
import { ShiftsPage } from './features/shifts/ShiftsPage';
import { RoleNames } from './lib/roles';
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

export default function App(): ReactElement {
  return (
    <QueryClientProvider client={queryClient}>
      <BrowserRouter>
        <AuthProvider>
          <Routes>
            <Route path="/login" element={<LoginPage />} />

            {/* Everything below needs a signed-in worker. */}
            <Route element={<ProtectedRoute />}>
              <Route element={<AppLayout />}>
                <Route path="/" element={<DashboardPage />} />

                {/* Master data changes affect every screen, so only the
                    administrator gets in (specification section 3). */}
                <Route element={<ProtectedRoute roles={[RoleNames.Administrator]} />}>
                  <Route path="/master-data" element={<MasterDataPage />} />
                </Route>

                {/* Recipes are the supervisor's job too — he is the one who
                    adjusts the percentages (specification section 3). */}
                <Route
                  element={
                    <ProtectedRoute
                      roles={[RoleNames.Administrator, RoleNames.Supervisor]}
                    />
                  }
                >
                  <Route path="/recipes" element={<RecipesPage />} />
                  {/* Opening and closing shifts is the supervisor's job
                      (specification section 3). Reopening a closed one is the
                      administrator's, which the screen and the server both enforce. */}
                  <Route path="/shifts" element={<ShiftsPage />} />
                </Route>

                {plannedRoutes.map(({ path, element }) => (
                  <Route key={path} path={path} element={element} />
                ))}
              </Route>
            </Route>

            <Route path="*" element={<Navigate to="/" replace />} />
          </Routes>
        </AuthProvider>
      </BrowserRouter>
    </QueryClientProvider>
  );
}

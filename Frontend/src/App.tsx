import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import type { ReactElement } from 'react';
import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom';
import { AppLayout } from './components/layout/AppLayout';
import { AuthProvider } from './features/auth/AuthProvider';
import { LoginPage } from './features/auth/LoginPage';
import { DashboardPage } from './features/dashboard/DashboardPage';
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
import { ShiftsPage } from './features/shifts/ShiftsPage';
import { TracePage } from './features/trace/TracePage';
import { ThermoProductionPage } from './features/thermo/ThermoProductionPage';
import { ThermoTestsPage } from './features/thermo/ThermoTestsPage';
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

                {/* Anyone signed in may see the store — an operator about to start a
                    batch needs to know the material is there. Receiving belongs to
                    the inventory manager (specification section 3). */}
                <Route path="/inventory" element={<InventoryPage />} />

                {/* Where did this come from. Open to anyone signed in: it writes
                    nothing, and whoever is holding a label may need to know what is
                    behind it (specification section 13). */}
                <Route path="/trace" element={<TracePage />} />
                <Route
                  element={
                    <ProtectedRoute
                      roles={[RoleNames.Administrator, RoleNames.InventoryManager]}
                    />
                  }
                >
                  <Route path="/inventory/receive" element={<ReceiveMaterialsPage />} />
                </Route>

                {/* Reading tickets is open wider than issuing them: the supervisor
                    closing a shift needs to see what is still outstanding. */}
                <Route
                  element={
                    <ProtectedRoute
                      roles={[
                        RoleNames.Administrator,
                        RoleNames.InventoryManager,
                        RoleNames.Supervisor,
                      ]}
                    />
                  }
                >
                  <Route path="/inventory/issue" element={<MaterialIssuePage />} />
                </Route>

                {/* Line 1. Making rolls and measuring them are different jobs, so
                    they are different screens — even though one man holds both roles
                    today (specification section 3). Reading is open wider: the thermo
                    operator needs to see what is in stock. */}
                <Route
                  element={
                    <ProtectedRoute
                      roles={[
                        RoleNames.Administrator,
                        RoleNames.Supervisor,
                        RoleNames.ExtruderOperator,
                        RoleNames.ExtruderTestPerson,
                      ]}
                    />
                  }
                >
                  <Route path="/production/rolls" element={<RollProductionPage />} />
                  <Route path="/production/roll-tests" element={<RollTestsPage />} />
                </Route>

                {/* Line 2, split the same way: forming is one job, counting what came
                    out is another. Today one man holds both roles. */}
                <Route
                  element={
                    <ProtectedRoute
                      roles={[
                        RoleNames.Administrator,
                        RoleNames.Supervisor,
                        RoleNames.ThermoOperator,
                        RoleNames.ThermoTestPerson,
                      ]}
                    />
                  }
                >
                  <Route path="/production/thermo" element={<ThermoProductionPage />} />
                  <Route path="/production/thermo-tests" element={<ThermoTestsPage />} />
                </Route>

                {/* Packing. Reading is open wider than scanning: the supervisor closing
                    a shift needs to see what is still part-built, and he is also the one
                    who takes a wrongly scanned bag back off. */}
                <Route
                  element={
                    <ProtectedRoute
                      roles={[
                        RoleNames.Administrator,
                        RoleNames.Supervisor,
                        RoleNames.PackagingOperator,
                      ]}
                    />
                  }
                >
                  <Route path="/production/pallets" element={<PalletsPage />} />
                  <Route path="/production/packaging" element={<PackagingPage />} />
                </Route>

                {/* Line 3, the recycler (specification section 11). Reading is open to
                    the supervisor as well: the weighed scrap is one half of the free
                    check against what the thermo calculated. */}
                <Route
                  element={
                    <ProtectedRoute
                      roles={[
                        RoleNames.Administrator,
                        RoleNames.Supervisor,
                        RoleNames.RecyclerOperator,
                      ]}
                    />
                  }
                >
                  <Route path="/production/recycler" element={<RecyclerPage />} />
                </Route>

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

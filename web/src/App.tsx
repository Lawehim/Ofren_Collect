import { Navigate, Outlet, Route, Routes } from 'react-router-dom';
import { useAuth } from './auth/useAuth';
import { Sidebar } from './components/Sidebar';
import { LoginPage } from './pages/LoginPage';
import { DashboardPage } from './pages/DashboardPage';
import { PlansPage } from './pages/PlansPage';
import { CustomersPage } from './pages/CustomersPage';

function AppLayout() {
  return (
    <div className="app">
      <Sidebar />
      <main className="main">
        <Outlet />
      </main>
    </div>
  );
}

export function App() {
  const { auth } = useAuth();

  return (
    <Routes>
      <Route path="/login" element={auth ? <Navigate to="/" replace /> : <LoginPage />} />
      <Route element={auth ? <AppLayout /> : <Navigate to="/login" replace />}>
        <Route path="/" element={<DashboardPage />} />
        <Route path="/plans" element={<PlansPage />} />
        <Route path="/customers" element={<CustomersPage />} />
      </Route>
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  );
}

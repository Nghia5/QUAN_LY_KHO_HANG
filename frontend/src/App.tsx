import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom';
import { ConfigProvider, Spin } from 'antd';
import viVN from 'antd/locale/vi_VN';
import { AuthProvider, useAuth } from './contexts/AuthContext';
import AppLayout from './components/AppLayout';
import LoginPage from './pages/auth/LoginPage';
import DashboardPage from './pages/dashboard/DashboardPage';
import UsersPage from './pages/users/UsersPage';
import RolesPage from './pages/roles/RolesPage';
import ProductsPage from './pages/products/ProductsPage';
import SuppliersPage from './pages/suppliers/SuppliersPage';
import WarehousesPage from './pages/warehouses/WarehousesPage';
import InventoryPage from './pages/inventory/InventoryPage';
import GoodsReceiptsPage from './pages/goods-receipts/GoodsReceiptsPage';
import GoodsIssuesPage from './pages/goods-issues/GoodsIssuesPage';
import StockTransfersPage from './pages/stock-transfers/StockTransfersPage';
import StocktakesPage from './pages/stocktakes/StocktakesPage';
import StockAdjustmentsPage from './pages/stock-adjustments/StockAdjustmentsPage';
import StockMovementsPage from './pages/stock-movements/StockMovementsPage';
import BatchesPage from './pages/batches/BatchesPage';
import LocationsPage from './pages/locations/LocationsPage';
import N8nAlertsPage from './pages/n8n/N8nAlertsPage';

function ProtectedRoute({ children }: { children: React.ReactNode }) {
  const { isAuthenticated, loading } = useAuth();
  if (loading) return <div style={{ display: 'flex', justifyContent: 'center', alignItems: 'center', height: '100vh' }}><Spin size="large" /></div>;
  if (!isAuthenticated) return <Navigate to="/login" replace />;
  return <>{children}</>;
}

function AppRoutes() {
  const { isAuthenticated, loading } = useAuth();
  if (loading) return <div style={{ display: 'flex', justifyContent: 'center', alignItems: 'center', height: '100vh' }}><Spin size="large" /></div>;

  return (
    <Routes>
      <Route path="/login" element={isAuthenticated ? <Navigate to="/" replace /> : <LoginPage />} />
      <Route path="/" element={<ProtectedRoute><AppLayout /></ProtectedRoute>}>
        <Route index element={<DashboardPage />} />
        <Route path="users" element={<UsersPage />} />
        <Route path="roles" element={<RolesPage />} />
        <Route path="products" element={<ProductsPage />} />
        <Route path="suppliers" element={<SuppliersPage />} />
        <Route path="warehouses" element={<WarehousesPage />} />
        <Route path="locations" element={<LocationsPage />} />
        <Route path="inventory" element={<InventoryPage />} />
        <Route path="goods-receipts" element={<GoodsReceiptsPage />} />
        <Route path="goods-issues" element={<GoodsIssuesPage />} />
        <Route path="stock-transfers" element={<StockTransfersPage />} />
        <Route path="stocktakes" element={<StocktakesPage />} />
        <Route path="stock-adjustments" element={<StockAdjustmentsPage />} />
        <Route path="stock-movements" element={<StockMovementsPage />} />
        <Route path="batches" element={<BatchesPage />} />
        <Route path="n8n-alerts" element={<N8nAlertsPage />} />
      </Route>
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  );
}

export default function App() {
  return (
    <ConfigProvider locale={viVN} theme={{ token: { colorPrimary: '#1677ff', borderRadius: 6 } }}>
      <BrowserRouter>
        <AuthProvider>
          <AppRoutes />
        </AuthProvider>
      </BrowserRouter>
    </ConfigProvider>
  );
}

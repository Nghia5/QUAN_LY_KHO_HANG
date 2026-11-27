import { useState } from 'react';
import { Layout, Menu, Avatar, Dropdown, Typography, theme } from 'antd';
import {
  DashboardOutlined, ShopOutlined, AppstoreOutlined, InboxOutlined,
  SwapOutlined, AuditOutlined, BarChartOutlined, UserOutlined,
  TeamOutlined, SafetyOutlined, ImportOutlined, ExportOutlined,
  SyncOutlined, FileSearchOutlined, ToolOutlined, ExperimentOutlined,
  LogoutOutlined, SettingOutlined, MenuFoldOutlined, MenuUnfoldOutlined,
} from '@ant-design/icons';
import { Outlet, useNavigate, useLocation } from 'react-router-dom';
import { useAuth } from '../contexts/AuthContext';

const { Header, Sider, Content } = Layout;

const menuItems = [
  { key: '/', icon: <DashboardOutlined />, label: 'Dashboard' },
  {
    key: 'master', icon: <AppstoreOutlined />, label: 'Danh mục',
    children: [
      { key: '/products', icon: <InboxOutlined />, label: 'Sản phẩm' },
      { key: '/suppliers', icon: <TeamOutlined />, label: 'Nhà cung cấp' },
    ],
  },
  {
    key: 'warehouse', icon: <ShopOutlined />, label: 'Kho hàng',
    children: [
      { key: '/warehouses', icon: <ShopOutlined />, label: 'Danh sách kho' },
      { key: '/inventory', icon: <BarChartOutlined />, label: 'Tồn kho' },
    ],
  },
  {
    key: 'operations', icon: <SwapOutlined />, label: 'Nghiệp vụ kho',
    children: [
      { key: '/goods-receipts', icon: <ImportOutlined />, label: 'Nhập kho' },
      { key: '/goods-issues', icon: <ExportOutlined />, label: 'Xuất kho' },
      { key: '/stock-transfers', icon: <SyncOutlined />, label: 'Chuyển kho' },
      { key: '/stocktakes', icon: <FileSearchOutlined />, label: 'Kiểm kê' },
      { key: '/stock-adjustments', icon: <ToolOutlined />, label: 'Điều chỉnh' },
    ],
  },
  {
    key: 'reports', icon: <AuditOutlined />, label: 'Báo cáo',
    children: [
      { key: '/stock-movements', icon: <BarChartOutlined />, label: 'Lịch sử kho' },
      { key: '/batches', icon: <ExperimentOutlined />, label: 'Lô / Hạn dùng' },
    ],
  },
  {
    key: 'admin', icon: <SafetyOutlined />, label: 'Quản trị',
    children: [
      { key: '/users', icon: <UserOutlined />, label: 'Người dùng' },
      { key: '/roles', icon: <SafetyOutlined />, label: 'Vai trò' },
    ],
  },
];

export default function AppLayout() {
  const [collapsed, setCollapsed] = useState(false);
  const navigate = useNavigate();
  const location = useLocation();
  const { user, logout } = useAuth();
  const { token: themeToken } = theme.useToken();

  const userMenu = {
    items: [
      { key: 'profile', icon: <SettingOutlined />, label: 'Hồ sơ' },
      { type: 'divider' as const },
      { key: 'logout', icon: <LogoutOutlined />, label: 'Đăng xuất', danger: true },
    ],
    onClick: ({ key }: { key: string }) => {
      if (key === 'logout') { logout(); navigate('/login'); }
    },
  };

  return (
    <Layout style={{ minHeight: '100vh' }}>
      <Sider trigger={null} collapsible collapsed={collapsed} width={260}
        style={{ background: themeToken.colorBgContainer, borderRight: `1px solid ${themeToken.colorBorderSecondary}` }}>
        <div style={{ height: 64, display: 'flex', alignItems: 'center', justifyContent: 'center', borderBottom: `1px solid ${themeToken.colorBorderSecondary}` }}>
          <Typography.Title level={4} style={{ margin: 0, whiteSpace: 'nowrap' }}>
            {collapsed ? '🏭' : '🏭 WMS'}
          </Typography.Title>
        </div>
        <Menu
          mode="inline"
          selectedKeys={[location.pathname]}
          defaultOpenKeys={['master', 'warehouse', 'operations', 'reports', 'admin']}
          items={menuItems}
          onClick={({ key }) => navigate(key)}
          style={{ borderRight: 0 }}
        />
      </Sider>
      <Layout>
        <Header style={{ padding: '0 24px', background: themeToken.colorBgContainer, display: 'flex', alignItems: 'center', justifyContent: 'space-between', borderBottom: `1px solid ${themeToken.colorBorderSecondary}` }}>
          <div style={{ cursor: 'pointer', fontSize: 18 }} onClick={() => setCollapsed(!collapsed)}>
            {collapsed ? <MenuUnfoldOutlined /> : <MenuFoldOutlined />}
          </div>
          <Dropdown menu={userMenu} placement="bottomRight">
            <div style={{ cursor: 'pointer', display: 'flex', alignItems: 'center', gap: 8 }}>
              <Avatar icon={<UserOutlined />} src={user?.avatarUrl} />
              <span>{user?.fullName || user?.username}</span>
            </div>
          </Dropdown>
        </Header>
        <Content style={{ margin: 24, padding: 24, background: themeToken.colorBgContainer, borderRadius: themeToken.borderRadiusLG, minHeight: 280 }}>
          <Outlet />
        </Content>
      </Layout>
    </Layout>
  );
}

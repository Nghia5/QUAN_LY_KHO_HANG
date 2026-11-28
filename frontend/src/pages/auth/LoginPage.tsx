import { useState } from 'react';
import { Form, Input, Button, Card, Typography, App } from 'antd';
import { UserOutlined, LockOutlined } from '@ant-design/icons';
import { useNavigate } from 'react-router-dom';
import { authApi } from '../../api/services';
import { useAuth } from '../../contexts/AuthContext';

const { Title } = Typography;

export default function LoginPage() {
  const { message } = App.useApp();
  const [loading, setLoading] = useState(false);
  const navigate = useNavigate();
  const { login } = useAuth();

  const onFinish = async (values: { usernameOrEmail: string; password: string }) => {
    setLoading(true);
    try {
      const { data } = await authApi.login(values);
      await login(data.accessToken, data.refreshToken, data.user);
      message.success('Đăng nhập thành công!');
      navigate('/');
    } catch (err: any) {
      const body = err.response?.data;
      message.error(body?.detail || body?.message || 'Sai tài khoản hoặc mật khẩu');
    } finally {
      setLoading(false);
    }
  };

  return (
    <div style={{ display: 'flex', justifyContent: 'center', alignItems: 'center', minHeight: '100vh', background: '#f0f2f5' }}>
      <Card style={{ width: 400, boxShadow: '0 2px 8px rgba(0,0,0,0.15)' }}>
        <div style={{ textAlign: 'center', marginBottom: 24 }}>
          <Title level={3}>🏭 Hệ thống Quản lý Kho</Title>
          <Typography.Text type="secondary">Đăng nhập để tiếp tục</Typography.Text>
        </div>
        <Form onFinish={onFinish} size="large">
          <Form.Item name="usernameOrEmail" rules={[{ required: true, message: 'Nhập tên đăng nhập hoặc email' }]}>
            <Input prefix={<UserOutlined />} placeholder="Tên đăng nhập / Email" autoComplete="username" autoFocus />
          </Form.Item>
          <Form.Item name="password" rules={[{ required: true, message: 'Nhập mật khẩu' }]}>
            <Input.Password prefix={<LockOutlined />} placeholder="Mật khẩu" autoComplete="current-password" />
          </Form.Item>
          <Form.Item>
            <Button type="primary" htmlType="submit" loading={loading} block>Đăng nhập</Button>
          </Form.Item>
        </Form>
        <div style={{ textAlign: 'center', marginTop: 12 }}>
          <Typography.Text type="secondary" style={{ fontSize: 12 }}>
            💡 Admin: <b>admin</b> / <b>Admin@123</b> · Quản lý kho: <b>manager01</b>…<b>manager30</b> · Nhân viên: <b>user031</b>…<b>user500</b> (mật khẩu <b>123456</b>)
          </Typography.Text>
        </div>
      </Card>
    </div>
  );
}

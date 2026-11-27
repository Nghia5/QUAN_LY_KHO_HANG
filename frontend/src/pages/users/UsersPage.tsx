import { useEffect, useState } from 'react';
import { Table, Button, Space, Input, Tag, Modal, Form, Select, Popconfirm, App } from 'antd';
import { PlusOutlined, ReloadOutlined } from '@ant-design/icons';
import type { UserResponse, RoleSummary } from '../../types';
import { usersApi, rolesApi } from '../../api/services';

export default function UsersPage() {
  const { message } = App.useApp();
  const [data, setData] = useState<UserResponse[]>([]);
  const [total, setTotal] = useState(0);
  const [loading, setLoading] = useState(false);
  const [params, setParams] = useState({ page: 1, pageSize: 20, keyword: '' });
  const [modalOpen, setModalOpen] = useState(false);
  const [editingUser, setEditingUser] = useState<UserResponse | null>(null);
  const [roles, setRoles] = useState<RoleSummary[]>([]);
  const [form] = Form.useForm();

  const fetchData = async () => {
    setLoading(true);
    try {
      const { data: res } = await usersApi.list(params);
      setData(res.items); setTotal(res.totalCount);
    } catch { message.error('Lỗi tải danh sách'); }
    finally { setLoading(false); }
  };

  const fetchRoles = async () => {
    try { const { data } = await rolesApi.list(); setRoles(data.map((r: any) => ({ id: r.id, code: r.code, name: r.name }))); } catch {}
  };

  useEffect(() => { fetchData(); }, [params]);
  useEffect(() => { fetchRoles(); }, []);

  const handleSave = async (values: any) => {
    try {
      if (editingUser) {
        await usersApi.update(editingUser.id, values);
        message.success('Cập nhật thành công');
      } else {
        await usersApi.create(values);
        message.success('Tạo người dùng thành công');
      }
      setModalOpen(false); form.resetFields(); setEditingUser(null); fetchData();
    } catch (err: any) { message.error(err.response?.data?.message || 'Lỗi'); }
  };

  const columns = [
    { title: 'ID', dataIndex: 'id', width: 60 },
    { title: 'Username', dataIndex: 'username' },
    { title: 'Họ tên', dataIndex: 'fullName' },
    { title: 'Email', dataIndex: 'email' },
    { title: 'Vai trò', dataIndex: 'roles', render: (r: string[]) => r?.map(v => <Tag key={v}>{v}</Tag>) },
    { title: 'Trạng thái', dataIndex: 'isActive', render: (v: boolean) => <Tag color={v ? 'green' : 'red'}>{v ? 'Hoạt động' : 'Vô hiệu'}</Tag> },
    {
      title: 'Thao tác', render: (_: any, record: UserResponse) => (
        <Space>
          <Button size="small" onClick={() => { setEditingUser(record); form.setFieldsValue({ ...record, roleIds: [] }); setModalOpen(true); }}>Sửa</Button>
          <Popconfirm title="Xác nhận xóa?" onConfirm={async () => { await usersApi.delete(record.id); message.success('Đã xóa'); fetchData(); }}>
            <Button size="small" danger>Xóa</Button>
          </Popconfirm>
        </Space>
      ),
    },
  ];

  return (
    <div>
      <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: 16 }}>
        <Space>
          <Input.Search placeholder="Tìm kiếm..." allowClear onSearch={(v) => setParams(p => ({ ...p, keyword: v, page: 1 }))} style={{ width: 300 }} />
          <Button icon={<ReloadOutlined />} onClick={fetchData} />
        </Space>
        <Button type="primary" icon={<PlusOutlined />} onClick={() => { setEditingUser(null); form.resetFields(); setModalOpen(true); }}>Thêm người dùng</Button>
      </div>
      <Table rowKey="id" columns={columns} dataSource={data} loading={loading}
        pagination={{ current: params.page, pageSize: params.pageSize, total, showSizeChanger: true, showTotal: (t) => `Tổng ${t}` }}
        onChange={(p) => setParams(prev => ({ ...prev, page: p.current || 1, pageSize: p.pageSize || 20 }))} />

      <Modal title={editingUser ? 'Sửa người dùng' : 'Thêm người dùng'} open={modalOpen}
        onCancel={() => { setModalOpen(false); setEditingUser(null); }} onOk={() => form.submit()} okText="Lưu">
        <Form form={form} layout="vertical" onFinish={handleSave}>
          {!editingUser && <Form.Item name="username" label="Username" rules={[{ required: true }]}><Input /></Form.Item>}
          <Form.Item name="fullName" label="Họ tên" rules={[{ required: true }]}><Input /></Form.Item>
          <Form.Item name="email" label="Email" rules={[{ required: true, type: 'email' }]}><Input /></Form.Item>
          {!editingUser && <Form.Item name="password" label="Mật khẩu" rules={[{ required: true, min: 8 }]}><Input.Password /></Form.Item>}
          <Form.Item name="phoneNumber" label="SĐT"><Input /></Form.Item>
          <Form.Item name="roleIds" label="Vai trò" rules={[{ required: true }]}>
            <Select mode="multiple" placeholder="Chọn vai trò">{roles.map(r => <Select.Option key={r.id} value={r.id}>{r.name}</Select.Option>)}</Select>
          </Form.Item>
        </Form>
      </Modal>
    </div>
  );
}

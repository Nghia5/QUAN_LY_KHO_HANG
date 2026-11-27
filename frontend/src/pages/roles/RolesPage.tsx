import { useEffect, useState } from 'react';
import { Table, Button, Input, Tag, Modal, Form, Select, message } from 'antd';
import { PlusOutlined, ReloadOutlined } from '@ant-design/icons';
import type { RoleResponse, PermissionResponse } from '../../types';
import { rolesApi } from '../../api/services';

export default function RolesPage() {
  const [data, setData] = useState<RoleResponse[]>([]);
  const [loading, setLoading] = useState(false);
  const [modalOpen, setModalOpen] = useState(false);
  const [editing, setEditing] = useState<RoleResponse | null>(null);
  const [permissions, setPermissions] = useState<PermissionResponse[]>([]);
  const [form] = Form.useForm();

  const fetchData = async () => {
    setLoading(true);
    try { const { data } = await rolesApi.list(); setData(data); } catch { message.error('Lỗi'); }
    finally { setLoading(false); }
  };

  useEffect(() => { fetchData(); rolesApi.permissions().then(r => setPermissions(r.data)).catch(() => {}); }, []);

  const handleSave = async (values: any) => {
    try {
      if (editing) { await rolesApi.update(editing.id, { ...values, isActive: true }); }
      else { await rolesApi.create(values); }
      message.success('Thành công'); setModalOpen(false); form.resetFields(); setEditing(null); fetchData();
    } catch (err: any) { message.error(err.response?.data?.message || 'Lỗi'); }
  };

  const permGroups = [...new Set(permissions.map(p => p.group))];

  const columns = [
    { title: 'ID', dataIndex: 'id', width: 60 },
    { title: 'Mã', dataIndex: 'code' },
    { title: 'Tên', dataIndex: 'name' },
    { title: 'Mô tả', dataIndex: 'description' },
    { title: 'Quyền', dataIndex: 'permissions', render: (ps: PermissionResponse[]) => ps?.length || 0 },
    { title: 'Trạng thái', dataIndex: 'isActive', render: (v: boolean) => <Tag color={v ? 'green' : 'red'}>{v ? 'Hoạt động' : 'Vô hiệu'}</Tag> },
    {
      title: 'Thao tác', render: (_: any, r: RoleResponse) => (
        <Button size="small" onClick={() => { setEditing(r); form.setFieldsValue({ ...r, permissionIds: r.permissions.map(p => p.id) }); setModalOpen(true); }}>Sửa</Button>
      ),
    },
  ];

  return (
    <div>
      <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: 16 }}>
        <Button icon={<ReloadOutlined />} onClick={fetchData} />
        <Button type="primary" icon={<PlusOutlined />} onClick={() => { setEditing(null); form.resetFields(); setModalOpen(true); }}>Thêm vai trò</Button>
      </div>
      <Table rowKey="id" columns={columns} dataSource={data} loading={loading} pagination={false} />
      <Modal title={editing ? 'Sửa vai trò' : 'Thêm vai trò'} open={modalOpen} onCancel={() => { setModalOpen(false); setEditing(null); }} onOk={() => form.submit()} okText="Lưu" width={600}>
        <Form form={form} layout="vertical" onFinish={handleSave}>
          {!editing && <Form.Item name="code" label="Mã vai trò" rules={[{ required: true }]}><Input /></Form.Item>}
          <Form.Item name="name" label="Tên vai trò" rules={[{ required: true }]}><Input /></Form.Item>
          <Form.Item name="description" label="Mô tả"><Input.TextArea rows={2} /></Form.Item>
          <Form.Item name="permissionIds" label="Quyền hạn">
            <Select mode="multiple" placeholder="Chọn quyền" optionFilterProp="label"
              options={permGroups.map(g => ({ label: g, options: permissions.filter(p => p.group === g).map(p => ({ label: `${p.code} — ${p.description || ''}`, value: p.id })) }))} />
          </Form.Item>
        </Form>
      </Modal>
    </div>
  );
}

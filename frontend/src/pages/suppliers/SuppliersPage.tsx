import { useEffect, useState } from 'react';
import { Table, Button, Space, Input, Tag, Modal, Form, Switch, Popconfirm, App } from 'antd';
import { PlusOutlined, ReloadOutlined } from '@ant-design/icons';
import type { SupplierResponse } from '../../types';
import { suppliersApi } from '../../api/services';

export default function SuppliersPage() {
  const { message } = App.useApp();
  const [data, setData] = useState<SupplierResponse[]>([]);
  const [total, setTotal] = useState(0);
  const [loading, setLoading] = useState(false);
  const [params, setParams] = useState({ page: 1, pageSize: 20, keyword: '' });
  const [modalOpen, setModalOpen] = useState(false);
  const [editing, setEditing] = useState<SupplierResponse | null>(null);
  const [form] = Form.useForm();

  const fetchData = async () => {
    setLoading(true);
    try {
      const { data: res } = await suppliersApi.list(params);
      const rawList: SupplierResponse[] = Array.isArray(res) ? res : ((res as any)?.items || []);
      let filtered = rawList;
      if (params.keyword) {
        const kw = params.keyword.toLowerCase().trim();
        filtered = filtered.filter(s =>
          (s.code && s.code.toLowerCase().includes(kw)) ||
          (s.name && s.name.toLowerCase().includes(kw)) ||
          (s.phone && s.phone.includes(kw)) ||
          (s.email && s.email.toLowerCase().includes(kw))
        );
      }
      setData(filtered);
      setTotal(filtered.length);
    } catch {
      message.error('Không thể tải danh sách nhà cung cấp');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => { fetchData(); }, [params]);

  const handleSave = async (values: any) => {
    try {
      if (editing) { await suppliersApi.update(editing.id, { ...values, isActive: values.isActive ?? true }); }
      else { await suppliersApi.create(values); }
      message.success('Thành công'); setModalOpen(false); form.resetFields(); setEditing(null); fetchData();
    } catch (err: any) { message.error(err.response?.data?.message || 'Lỗi'); }
  };

  const columns = [
    {
      title: 'Mã',
      dataIndex: 'code',
      width: 100,
      sorter: (a: SupplierResponse, b: SupplierResponse) => (a.code || '').localeCompare(b.code || ''),
    },
    {
      title: 'Tên NCC',
      dataIndex: 'name',
      sorter: (a: SupplierResponse, b: SupplierResponse) => (a.name || '').localeCompare(b.name || ''),
    },
    { title: 'SĐT', dataIndex: 'phone' },
    { title: 'Email', dataIndex: 'email' },
    { title: 'Địa chỉ', dataIndex: 'address', ellipsis: true },
    {
      title: 'Trạng thái',
      dataIndex: 'isActive',
      sorter: (a: SupplierResponse, b: SupplierResponse) => Number(a.isActive) - Number(b.isActive),
      render: (v: boolean) => <Tag color={v ? 'green' : 'red'}>{v ? 'Hoạt động' : 'Ngừng'}</Tag>
    },
    {
      title: 'Thao tác', render: (_: any, r: SupplierResponse) => (
        <Space>
          <Button size="small" onClick={() => { setEditing(r); form.setFieldsValue(r); setModalOpen(true); }}>Sửa</Button>
          <Popconfirm title="Xóa?" onConfirm={async () => { await suppliersApi.delete(r.id); message.success('Đã xóa'); fetchData(); }}>
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
        <Button type="primary" icon={<PlusOutlined />} onClick={() => { setEditing(null); form.resetFields(); setModalOpen(true); }}>Thêm NCC</Button>
      </div>
      <Table rowKey="id" columns={columns} dataSource={data} loading={loading}
        pagination={{ current: params.page, pageSize: params.pageSize, total, showSizeChanger: true, showTotal: (t) => `Tổng ${t}` }}
        onChange={(p) => setParams(prev => ({ ...prev, page: p.current || 1, pageSize: p.pageSize || 20 }))} />
      <Modal title={editing ? 'Sửa NCC' : 'Thêm NCC'} open={modalOpen} onCancel={() => { setModalOpen(false); setEditing(null); }} onOk={() => form.submit()} okText="Lưu">
        <Form form={form} layout="vertical" onFinish={handleSave}>
          <Form.Item name="code" label="Mã NCC" rules={[{ required: true }]}><Input disabled={!!editing} /></Form.Item>
          <Form.Item name="name" label="Tên" rules={[{ required: true }]}><Input /></Form.Item>
          <Form.Item name="phone" label="SĐT"><Input /></Form.Item>
          <Form.Item name="email" label="Email"><Input /></Form.Item>
          <Form.Item name="address" label="Địa chỉ"><Input /></Form.Item>
          {editing && <Form.Item name="isActive" label="Hoạt động" valuePropName="checked"><Switch /></Form.Item>}
        </Form>
      </Modal>
    </div>
  );
}

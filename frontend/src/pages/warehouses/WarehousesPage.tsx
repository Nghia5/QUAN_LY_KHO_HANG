import { useEffect, useState } from 'react';
import { Table, Button, Space, Input, Tag, Modal, Form, message, Popconfirm } from 'antd';
import { PlusOutlined, ReloadOutlined } from '@ant-design/icons';
import type { WarehouseResponse } from '../../types';
import { warehousesApi } from '../../api/services';

export default function WarehousesPage() {
  const [data, setData] = useState<WarehouseResponse[]>([]);
  const [total, setTotal] = useState(0);
  const [loading, setLoading] = useState(false);
  const [params, setParams] = useState({ page: 1, pageSize: 20, keyword: '' });
  const [modalOpen, setModalOpen] = useState(false);
  const [editing, setEditing] = useState<WarehouseResponse | null>(null);
  const [form] = Form.useForm();

  const fetchData = async () => {
    setLoading(true);
    try { const { data: res } = await warehousesApi.list(params); setData(res.items); setTotal(res.totalCount); }
    catch { message.error('Lỗi'); } finally { setLoading(false); }
  };

  useEffect(() => { fetchData(); }, [params]);

  const handleSave = async (values: any) => {
    try {
      if (editing) {
        await warehousesApi.update(editing.id, values);
        message.success('Cập nhật kho thành công');
      } else {
        await warehousesApi.create(values);
        message.success('Tạo kho mới thành công');
      }
      setModalOpen(false);
      form.resetFields();
      setEditing(null);
      fetchData();
    } catch (err: any) {
      const msg = err.response?.data?.detail || err.response?.data?.message || err.message || 'Lỗi thao tác';
      message.error(msg);
    }
  };

  const columns = [
    {
      title: 'Mã kho',
      dataIndex: 'code',
      width: 110,
      sorter: (a: WarehouseResponse, b: WarehouseResponse) => (a.code || '').localeCompare(b.code || ''),
      render: (v: string) => <b>{v}</b>,
    },
    {
      title: 'Tên kho',
      dataIndex: 'name',
      sorter: (a: WarehouseResponse, b: WarehouseResponse) => (a.name || '').localeCompare(b.name || ''),
    },
    { title: 'Địa chỉ', dataIndex: 'address', ellipsis: true },
    { title: 'SĐT', dataIndex: 'phone', width: 130 },
    {
      title: 'Trạng thái',
      dataIndex: 'isActive',
      width: 120,
      sorter: (a: WarehouseResponse, b: WarehouseResponse) => Number(a.isActive) - Number(b.isActive),
      render: (v: boolean) => <Tag color={v ? 'green' : 'red'}>{v ? 'Hoạt động' : 'Ngừng'}</Tag>,
    },
    {
      title: 'Thao tác', render: (_: any, r: WarehouseResponse) => (
        <Space>
          <Button size="small" onClick={() => { setEditing(r); form.setFieldsValue(r); setModalOpen(true); }}>Sửa</Button>
          <Button size="small" onClick={async () => { r.isActive ? await warehousesApi.deactivate(r.id) : await warehousesApi.activate(r.id); message.success('OK'); fetchData(); }}>
            {r.isActive ? 'Vô hiệu' : 'Kích hoạt'}
          </Button>
          <Popconfirm title="Xóa?" onConfirm={async () => { await warehousesApi.delete(r.id); message.success('Đã xóa'); fetchData(); }}>
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
        <Button type="primary" icon={<PlusOutlined />} onClick={() => { setEditing(null); form.resetFields(); setModalOpen(true); }}>Thêm kho</Button>
      </div>
      <Table rowKey="id" columns={columns} dataSource={data} loading={loading}
        pagination={{ current: params.page, pageSize: params.pageSize, total, showSizeChanger: true, showTotal: (t) => `Tổng ${t}` }}
        onChange={(p) => setParams(prev => ({ ...prev, page: p.current || 1, pageSize: p.pageSize || 20 }))} />
      <Modal title={editing ? 'Sửa kho' : 'Thêm kho'} open={modalOpen} onCancel={() => { setModalOpen(false); setEditing(null); }} onOk={() => form.submit()} okText="Lưu">
        <Form form={form} layout="vertical" onFinish={handleSave}>
          {!editing && <Form.Item name="code" label="Mã kho" rules={[{ required: true }]}><Input /></Form.Item>}
          <Form.Item name="name" label="Tên kho" rules={[{ required: true }]}><Input /></Form.Item>
          <Form.Item name="address" label="Địa chỉ"><Input /></Form.Item>
          <Form.Item name="phone" label="SĐT"><Input /></Form.Item>
          <Form.Item name="description" label="Mô tả"><Input.TextArea rows={2} /></Form.Item>
        </Form>
      </Modal>
    </div>
  );
}

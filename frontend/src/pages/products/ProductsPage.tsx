import { useEffect, useState } from 'react';
import { Table, Button, Space, Input, Tag, Modal, Form, InputNumber, message, Popconfirm } from 'antd';
import { PlusOutlined, ReloadOutlined } from '@ant-design/icons';
import type { ProductResponse } from '../../types';
import { productsApi } from '../../api/services';

export default function ProductsPage() {
  const [data, setData] = useState<ProductResponse[]>([]);
  const [total, setTotal] = useState(0);
  const [loading, setLoading] = useState(false);
  const [params, setParams] = useState({ page: 1, pageSize: 20, keyword: '' });
  const [modalOpen, setModalOpen] = useState(false);
  const [editing, setEditing] = useState<ProductResponse | null>(null);
  const [form] = Form.useForm();

  const fetchData = async () => {
    setLoading(true);
    try { const { data: res } = await productsApi.list(params); setData(res.items); setTotal(res.totalCount); }
    catch { message.error('Lỗi tải danh sách'); }
    finally { setLoading(false); }
  };

  useEffect(() => { fetchData(); }, [params]);

  const handleSave = async (values: any) => {
    try {
      if (editing) { await productsApi.update(editing.id, values); }
      else { await productsApi.create(values); }
      message.success('Thành công'); setModalOpen(false); form.resetFields(); setEditing(null); fetchData();
    } catch (err: any) { message.error(err.response?.data?.message || 'Lỗi'); }
  };

  const columns = [
    { title: 'SKU', dataIndex: 'sku', width: 120 },
    { title: 'Tên sản phẩm', dataIndex: 'name' },
    { title: 'Danh mục', dataIndex: 'category' },
    { title: 'ĐVT', dataIndex: 'unitOfMeasure', width: 80 },
    { title: 'Giá bán', dataIndex: 'salePrice', render: (v: number) => v?.toLocaleString('vi-VN') + ' ₫' },
    { title: 'Trạng thái', dataIndex: 'isActive', render: (v: boolean) => <Tag color={v ? 'green' : 'red'}>{v ? 'Hoạt động' : 'Ngừng'}</Tag> },
    {
      title: 'Thao tác', render: (_: any, r: ProductResponse) => (
        <Space>
          <Button size="small" onClick={() => { setEditing(r); form.setFieldsValue(r); setModalOpen(true); }}>Sửa</Button>
          <Popconfirm title="Xác nhận xóa?" onConfirm={async () => { await productsApi.delete(r.id); message.success('Đã xóa'); fetchData(); }}>
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
          <Input.Search placeholder="Tìm kiếm SKU, tên..." allowClear onSearch={(v) => setParams(p => ({ ...p, keyword: v, page: 1 }))} style={{ width: 300 }} />
          <Button icon={<ReloadOutlined />} onClick={fetchData} />
        </Space>
        <Button type="primary" icon={<PlusOutlined />} onClick={() => { setEditing(null); form.resetFields(); setModalOpen(true); }}>Thêm sản phẩm</Button>
      </div>
      <Table rowKey="id" columns={columns} dataSource={data} loading={loading}
        pagination={{ current: params.page, pageSize: params.pageSize, total, showSizeChanger: true, showTotal: (t) => `Tổng ${t}` }}
        onChange={(p) => setParams(prev => ({ ...prev, page: p.current || 1, pageSize: p.pageSize || 20 }))} />
      <Modal title={editing ? 'Sửa sản phẩm' : 'Thêm sản phẩm'} open={modalOpen} onCancel={() => { setModalOpen(false); setEditing(null); }} onOk={() => form.submit()} okText="Lưu">
        <Form form={form} layout="vertical" onFinish={handleSave}>
          <Form.Item name="sku" label="SKU" rules={[{ required: true }]}><Input disabled={!!editing} /></Form.Item>
          <Form.Item name="name" label="Tên sản phẩm" rules={[{ required: true }]}><Input /></Form.Item>
          <Form.Item name="category" label="Danh mục" rules={[{ required: true }]}><Input /></Form.Item>
          <Form.Item name="unitOfMeasure" label="Đơn vị tính" rules={[{ required: true }]}><Input /></Form.Item>
          <Form.Item name="salePrice" label="Giá bán" rules={[{ required: true }]}><InputNumber style={{ width: '100%' }} min={0} /></Form.Item>
          <Form.Item name="description" label="Mô tả"><Input.TextArea rows={2} /></Form.Item>
        </Form>
      </Modal>
    </div>
  );
}

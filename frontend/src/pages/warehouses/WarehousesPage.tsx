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
    try {
      const { data: res } = await warehousesApi.list(params);
      setData(res.items);
      setTotal(res.totalCount);
    } catch {
      message.error('Không thể tải danh sách kho');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchData();
  }, [params]);

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

  const handleToggleActive = async (r: WarehouseResponse) => {
    try {
      if (r.isActive) {
        await warehousesApi.deactivate(r.id);
        message.success(`Đã ngừng hoạt động kho "${r.name}"`);
      } else {
        await warehousesApi.activate(r.id);
        message.success(`Đã kích hoạt kho "${r.name}"`);
      }
      fetchData();
    } catch (err: any) {
      const detail = err.response?.data?.detail || err.response?.data?.message || err.message || 'Lỗi thao tác';
      Modal.error({
        title: 'Không thể vô hiệu hóa kho',
        content: (
          <div>
            <p>
              Kho <b>{r.name} ({r.code})</b> hiện không thể ngừng hoạt động do quy tắc ràng buộc dữ liệu:
            </p>
            <div
              style={{
                background: '#fff2f0',
                border: '1px solid #ffccc7',
                padding: '10px 14px',
                borderRadius: 6,
                marginTop: 8,
                color: '#cf1322',
                fontSize: 13,
                lineHeight: 1.6,
              }}
            >
              {detail}
            </div>
            <p style={{ marginTop: 12, color: '#666', fontSize: 13 }}>
              * <b>Hướng dẫn xử lý:</b> Cần chuyển hết hàng tồn kho sang kho khác hoặc hoàn tất/hủy các đơn Nhập kho,
              Xuất kho, Chuyển kho và Kiểm kê đang mở trước khi vô hiệu hóa kho này.
            </p>
          </div>
        ),
        width: 540,
        okText: 'Đã hiểu',
      });
    }
  };

  const handleDelete = async (r: WarehouseResponse) => {
    try {
      await warehousesApi.delete(r.id);
      message.success(`Đã xóa kho "${r.name}"`);
      fetchData();
    } catch (err: any) {
      const detail = err.response?.data?.detail || err.response?.data?.message || err.message || 'Lỗi thao tác';
      Modal.error({
        title: 'Không thể xóa kho',
        content: (
          <div>
            <p>
              Không thể xóa kho <b>{r.name} ({r.code})</b>:
            </p>
            <div
              style={{
                background: '#fff2f0',
                border: '1px solid #ffccc7',
                padding: '10px 14px',
                borderRadius: 6,
                marginTop: 8,
                color: '#cf1322',
                fontSize: 13,
                lineHeight: 1.6,
              }}
            >
              {detail}
            </div>
            <p style={{ marginTop: 12, color: '#666', fontSize: 13 }}>
              * <b>Lưu ý:</b> Kho đã từng phát sinh chứng từ/tồn kho sẽ được bảo toàn lịch sử theo quy định và không thể
              xóa vĩnh viễn.
            </p>
          </div>
        ),
        width: 540,
        okText: 'Đã hiểu',
      });
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
      title: 'Thao tác',
      width: 220,
      render: (_: any, r: WarehouseResponse) => (
        <Space>
          <Button
            size="small"
            onClick={() => {
              setEditing(r);
              form.setFieldsValue(r);
              setModalOpen(true);
            }}
          >
            Sửa
          </Button>
          <Button size="small" onClick={() => handleToggleActive(r)}>
            {r.isActive ? 'Vô hiệu' : 'Kích hoạt'}
          </Button>
          <Popconfirm
            title={`Bạn có chắc chắn muốn xóa kho "${r.name}"?`}
            onConfirm={() => handleDelete(r)}
            okText="Xóa"
            cancelText="Hủy"
          >
            <Button size="small" danger>
              Xóa
            </Button>
          </Popconfirm>
        </Space>
      ),
    },
  ];

  return (
    <div>
      <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: 16 }}>
        <Space>
          <Input.Search
            placeholder="Tìm kiếm kho..."
            allowClear
            onSearch={(v) => setParams((p) => ({ ...p, keyword: v, page: 1 }))}
            style={{ width: 300 }}
          />
          <Button icon={<ReloadOutlined />} onClick={fetchData} />
        </Space>
        <Button
          type="primary"
          icon={<PlusOutlined />}
          onClick={() => {
            setEditing(null);
            form.resetFields();
            setModalOpen(true);
          }}
        >
          Thêm kho
        </Button>
      </div>
      <Table
        rowKey="id"
        columns={columns}
        dataSource={data}
        loading={loading}
        pagination={{
          current: params.page,
          pageSize: params.pageSize,
          total,
          showSizeChanger: true,
          showTotal: (t) => `Tổng ${t} kho`,
        }}
        onChange={(p) => setParams((prev) => ({ ...prev, page: p.current || 1, pageSize: p.pageSize || 20 }))}
      />
      <Modal
        title={editing ? 'Sửa thông tin kho' : 'Thêm kho mới'}
        open={modalOpen}
        onCancel={() => {
          setModalOpen(false);
          setEditing(null);
        }}
        onOk={() => form.submit()}
        okText="Lưu"
        cancelText="Hủy"
      >
        <Form form={form} layout="vertical" onFinish={handleSave}>
          {!editing && (
            <Form.Item name="code" label="Mã kho" rules={[{ required: true, message: 'Vui lòng nhập mã kho' }]}>
              <Input placeholder="Ví dụ: KH-HCM-03" />
            </Form.Item>
          )}
          <Form.Item name="name" label="Tên kho" rules={[{ required: true, message: 'Vui lòng nhập tên kho' }]}>
            <Input placeholder="Ví dụ: Kho Tổng Miền Nam" />
          </Form.Item>
          <Form.Item name="address" label="Địa chỉ">
            <Input placeholder="Số nhà, đường, quận/huyện, tỉnh/thành phố" />
          </Form.Item>
          <Form.Item name="phone" label="Số điện thoại">
            <Input placeholder="Số điện thoại liên hệ quản lý kho" />
          </Form.Item>
          <Form.Item name="description" label="Mô tả">
            <Input.TextArea rows={2} placeholder="Ghi chú về diện tích, mặt hàng bảo quản..." />
          </Form.Item>
        </Form>
      </Modal>
    </div>
  );
}

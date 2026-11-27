import { useEffect, useState } from 'react';
import {
  Table, Tag, Input, Space, Button, Select, message, Card, Alert,
  Modal, Form, InputNumber
} from 'antd';
import {
  ReloadOutlined, ThunderboltOutlined,
  ToolOutlined, SearchOutlined
} from '@ant-design/icons';
import type { InventoryResponse, WarehouseResponse } from '../../types';
import { inventoryApi, warehousesApi, stockAdjustmentsApi } from '../../api/services';
import axios from 'axios';

const stockStatusMap: Record<number, { text: string; color: string }> = {
  1: { text: 'Đủ hàng', color: 'green' },
  2: { text: 'Tồn thấp', color: 'orange' },
  3: { text: 'Hết hàng', color: 'red' },
};

export default function InventoryPage() {
  const [data, setData] = useState<InventoryResponse[]>([]);
  const [total, setTotal] = useState(0);
  const [loading, setLoading] = useState(false);
  const [warehouses, setWarehouses] = useState<WarehouseResponse[]>([]);
  const [params, setParams] = useState<Record<string, any>>({ page: 1, pageSize: 20 });

  // W-15 Checkbox selection
  const [selectedRowKeys, setSelectedRowKeys] = useState<React.Key[]>([]);

  // W-16 Bulk Adjustment modal
  const [bulkAdjustmentModalOpen, setBulkAdjustmentModalOpen] = useState(false);
  const [bulkForm] = Form.useForm();
  const [bulkSubmitting, setBulkSubmitting] = useState(false);

  const fetchData = async () => {
    setLoading(true);
    try {
      const { data: res } = await inventoryApi.list(params);
      setData(res.items);
      setTotal(res.totalCount);
    } catch {
      message.error('Không thể tải dữ liệu tồn kho');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    warehousesApi.list({ pageSize: 100 }).then(r => setWarehouses(r.data.items)).catch(() => {});
  }, []);

  useEffect(() => {
    fetchData();
  }, [params]);

  // Bulk adjustment submission (W-16)
  const handleBulkAdjustment = async (values: any) => {
    const selectedItems = data.filter(item => selectedRowKeys.includes(item.id));
    if (selectedItems.length === 0) return;

    // Nhóm theo từng kho
    const warehouseId = selectedItems[0].warehouseId;
    setBulkSubmitting(true);
    try {
      await stockAdjustmentsApi.create({
        warehouseId: warehouseId,
        reasonCode: values.reasonCode ?? 2,
        reason: values.reason || 'Điều chỉnh tồn kho hàng loạt theo danh sách chọn',
        note: values.note,
        lines: selectedItems.map(item => ({
          productId: item.productId,
          locationId: item.locationId,
          adjustmentQuantity: Number(values.adjustmentQuantity) || 0,
          note: values.reason || 'Điều chỉnh hàng loạt',
        })),
      });
      message.success(`Đã tạo phiếu điều chỉnh cho ${selectedItems.length} mặt hàng!`);
      setBulkAdjustmentModalOpen(false);
      setSelectedRowKeys([]);
      bulkForm.resetFields();
      fetchData();
    } catch (err: any) {
      message.error(err.response?.data?.message || 'Không thể tạo phiếu điều chỉnh hàng loạt');
    } finally {
      setBulkSubmitting(false);
    }
  };

  // N8N Alert Trigger cho các dòng đã chọn
  const handleSendN8nAlert = async () => {
    const webhookUrl = localStorage.getItem('wms_n8n_url') || 'http://localhost:5678/webhook/wms-alerts';
    const secretKey = localStorage.getItem('wms_n8n_secret') || '';
    const selectedItems = data.filter(item => selectedRowKeys.includes(item.id));

    if (selectedItems.length === 0) {
      message.warning('Vui lòng chọn ít nhất 1 dòng tồn kho!');
      return;
    }

    let sent = 0;
    for (const item of selectedItems) {
      const payload = {
        eventId: `EVT-INV-${Date.now()}-${item.id}`,
        eventType: item.quantity === 0 ? 'OUT_OF_STOCK' : 'LOW_STOCK',
        timestamp: new Date().toISOString(),
        data: {
          productSku: item.productSku,
          productName: item.productName,
          warehouseName: item.warehouseCode,
          locationCode: item.locationCode,
          quantity: item.quantity,
          availableQuantity: item.availableQuantity,
        }
      };

      try {
        await axios.post(webhookUrl, payload, {
          headers: {
            'Content-Type': 'application/json',
            'X-WMS-Secret': secretKey
          },
          timeout: 4000
        });
        sent++;
      } catch {}
    }
    message.success(`Đã kích hoạt gửi ${sent} cảnh báo sang N8N Webhook!`);
  };

  const columns = [
    { title: 'SKU', dataIndex: 'productSku', width: 130, render: (v: string) => <b>{v}</b> },
    { title: 'Sản phẩm', dataIndex: 'productName' },
    { title: 'ĐVT', dataIndex: 'unitOfMeasure', width: 80 },
    { title: 'Kho hàng', dataIndex: 'warehouseCode', width: 110 },
    { title: 'Vị trí', dataIndex: 'locationCode', width: 130 },
    {
      title: 'Tồn thực tế',
      dataIndex: 'quantity',
      width: 110,
      render: (v: number) => <b>{v?.toLocaleString()}</b>,
      sorter: true,
    },
    { title: 'Đang giữ', dataIndex: 'reservedQuantity', width: 90, render: (v: number) => v?.toLocaleString() },
    {
      title: 'Khả dụng',
      dataIndex: 'availableQuantity',
      width: 110,
      render: (v: number) => <span style={{ color: '#1677ff', fontWeight: 500 }}>{v?.toLocaleString()}</span>
    },
    {
      title: 'Trạng thái',
      dataIndex: 'stockStatus',
      width: 120,
      render: (v: number) => {
        const s = stockStatusMap[v];
        return <Tag color={s?.color}>{s?.text || v}</Tag>;
      }
    },
  ];

  return (
    <div>
      <Card
        title="📊 Báo cáo & Quản lý Tồn kho (Inventory Master)"
        extra={<Button icon={<ReloadOutlined />} onClick={fetchData}>Làm mới</Button>}
        style={{ marginBottom: 16 }}
      >
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: 12 }}>
          <Space wrap>
            <Input.Search
              placeholder="Tìm theo SKU, tên sản phẩm..."
              allowClear
              enterButton={<SearchOutlined />}
              onSearch={(v) => setParams(p => ({ ...p, keyword: v, page: 1 }))}
              style={{ width: 280 }}
            />
            <Select
              placeholder="Lọc theo kho"
              allowClear
              style={{ width: 220 }}
              onChange={(v) => setParams(p => ({ ...p, warehouseId: v, page: 1 }))}
              options={warehouses.map(w => ({ value: w.id, label: `${w.name} (${w.code})` }))}
            />
            <Select
              placeholder="Tình trạng tồn"
              allowClear
              style={{ width: 160 }}
              onChange={(v) => setParams(p => ({ ...p, stockStatus: v, page: 1 }))}
              options={[
                { value: 1, label: '🟢 Đủ hàng' },
                { value: 2, label: '🟠 Tồn thấp' },
                { value: 3, label: '🔴 Hết hàng' },
              ]}
            />
          </Space>

          {/* W-15 & W-16: Checkbox & Bulk Actions Toolbar */}
          {selectedRowKeys.length > 0 && (
            <Alert
              type="info"
              showIcon
              message={
                <Space>
                  <span>Đã chọn <b>{selectedRowKeys.length}</b> dòng:</span>
                  <Button
                    size="small"
                    type="primary"
                    icon={<ToolOutlined />}
                    onClick={() => {
                      bulkForm.resetFields();
                      setBulkAdjustmentModalOpen(true);
                    }}
                  >
                    Tạo phiếu điều chỉnh hàng loạt
                  </Button>
                  <Button
                    size="small"
                    icon={<ThunderboltOutlined />}
                    style={{ color: '#722ed1', borderColor: '#722ed1' }}
                    onClick={handleSendN8nAlert}
                  >
                    Bắn N8N Alert
                  </Button>
                  <Button size="small" type="link" onClick={() => setSelectedRowKeys([])}>
                    Bỏ chọn
                  </Button>
                </Space>
              }
              style={{ padding: '6px 16px' }}
            />
          )}
        </div>
      </Card>

      <Table
        rowKey="id"
        columns={columns}
        dataSource={data}
        loading={loading}
        rowSelection={{
          selectedRowKeys,
          onChange: (keys) => setSelectedRowKeys(keys),
        }}
        pagination={{
          current: params.page,
          pageSize: params.pageSize,
          total,
          showSizeChanger: true,
          showTotal: (t) => `Tổng cộng ${t} dòng tồn kho`,
        }}
        onChange={(p) => setParams(prev => ({ ...prev, page: p.current || 1, pageSize: p.pageSize || 20 }))}
      />

      {/* Modal Bulk Adjustment (W-16) */}
      <Modal
        title={`⚖️ Tạo Phiếu Điều Chỉnh Hàng Loạt (${selectedRowKeys.length} mặt hàng)`}
        open={bulkAdjustmentModalOpen}
        onCancel={() => setBulkAdjustmentModalOpen(false)}
        onOk={() => bulkForm.submit()}
        okText="Tạo phiếu điều chỉnh"
        cancelText="Hủy"
        confirmLoading={bulkSubmitting}
      >
        <Alert
          type="warning"
          showIcon
          message="Quy tắc điều chỉnh số lượng (W-16)"
          description="Hệ thống không cập nhật thẳng vào Database mà sẽ tạo Phiếu Điều Chỉnh Tồn Kho để đảm bảo tính bất biến của lịch sử StockMovement và AuditLog."
          style={{ marginBottom: 16 }}
        />
        <Form form={bulkForm} layout="vertical" onFinish={handleBulkAdjustment}>
          <Form.Item
            name="reasonCode"
            label="Phân loại lý do"
            initialValue={2}
            rules={[{ required: true }]}
          >
            <Select
              options={[
                { value: 0, label: 'Hư hỏng / Hỏng hóc' },
                { value: 1, label: 'Mất mát / Thất thoát' },
                { value: 2, label: 'Sửa lỗi kiểm kê định kỳ' },
                { value: 4, label: 'Khác' },
              ]}
            />
          </Form.Item>

          <Form.Item
            name="adjustmentQuantity"
            label="Số lượng bù trừ (+Tăng hoặc -Giảm)"
            rules={[{ required: true, message: 'Nhập số lượng điều chỉnh áp dụng chung' }]}
          >
            <InputNumber style={{ width: '100%' }} placeholder="VD: +5 hoặc -2" />
          </Form.Item>

          <Form.Item
            name="reason"
            label="Lý do chi tiết"
            rules={[{ required: true, message: 'Bắt buộc nhập lý do' }]}
          >
            <Input placeholder="VD: Điều chỉnh sau kiểm kê tháng 11..." />
          </Form.Item>

          <Form.Item name="note" label="Ghi chú thêm">
            <Input.TextArea rows={2} placeholder="Ghi chú bổ sung..." />
          </Form.Item>
        </Form>
      </Modal>
    </div>
  );
}

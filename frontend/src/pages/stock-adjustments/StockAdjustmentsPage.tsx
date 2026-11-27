import { useEffect, useState, useCallback } from 'react';
import {
  Table, Button, Space, Input, Select, Drawer, Descriptions, App,
  Divider, Tag, Modal, Form, InputNumber, Card
} from 'antd';
import {
  PlusOutlined, ReloadOutlined, EyeOutlined, DeleteOutlined
} from '@ant-design/icons';
import type {
  StockAdjustmentSummary, StockAdjustmentDetail, WarehouseResponse,
  ProductResponse, LocationResponse
} from '../../types';
import {
  stockAdjustmentsApi, warehousesApi, productsApi, locationsApi, extractItems
} from '../../api/services';
import { StatusTag, DocumentActions } from '../../components/DocumentActions';

const REASON_OPTIONS = [
  { value: 0, label: 'Hư hỏng / Hỏng hóc' },
  { value: 1, label: 'Mất mát / Thất thoát' },
  { value: 2, label: 'Sửa lỗi kiểm đếm' },
  { value: 3, label: 'Trả hàng / Nhận lại' },
  { value: 4, label: 'Khác' },
];

const reasonLabels: Record<number, string> = {
  0: 'Hư hỏng',
  1: 'Mất mát',
  2: 'Sửa lỗi',
  3: 'Trả hàng',
  4: 'Khác'
};

export default function StockAdjustmentsPage() {
  const { message, modal } = App.useApp();
  const [data, setData] = useState<StockAdjustmentSummary[]>([]);
  const [total, setTotal] = useState(0);
  const [loading, setLoading] = useState(false);
  const [warehouses, setWarehouses] = useState<WarehouseResponse[]>([]);
  const [products, setProducts] = useState<ProductResponse[]>([]);
  const [locations, setLocations] = useState<LocationResponse[]>([]);

  const [params, setParams] = useState<Record<string, any>>({ page: 1, pageSize: 20 });
  const [detail, setDetail] = useState<StockAdjustmentDetail | null>(null);
  const [drawerOpen, setDrawerOpen] = useState(false);

  // Modal tạo điều chỉnh
  const [createModalOpen, setCreateModalOpen] = useState(false);
  const [createForm] = Form.useForm();
  const [selectedWarehouseForCreate, setSelectedWarehouseForCreate] = useState<number | null>(null);

  const fetchData = useCallback(async () => {
    setLoading(true);
    try {
      const { data: res } = await stockAdjustmentsApi.list(params);
      setData(res.items);
      setTotal(res.totalCount);
    } catch {
      message.error('Không thể tải danh sách phiếu điều chỉnh');
    } finally {
      setLoading(false);
    }
  }, [params]);

  useEffect(() => {
    warehousesApi.list({ pageSize: 100 }).then(r => setWarehouses(extractItems(r.data))).catch(() => setWarehouses([]));
    productsApi.list({ pageSize: 100 }).then(r => setProducts(extractItems(r.data))).catch(() => setProducts([]));
  }, []);

  useEffect(() => {
    fetchData();
  }, [fetchData]);

  useEffect(() => {
    if (selectedWarehouseForCreate) {
      locationsApi.list(selectedWarehouseForCreate, { pageSize: 100 })
        .then(r => setLocations(extractItems(r.data)))
        .catch(() => setLocations([]));
    } else {
      setLocations([]);
    }
  }, [selectedWarehouseForCreate]);

  const showDetail = async (id: number) => {
    try {
      const { data } = await stockAdjustmentsApi.get(id);
      setDetail(data);
      setDrawerOpen(true);
    } catch {
      message.error('Lỗi khi tải chi tiết phiếu điều chỉnh');
    }
  };

  const handleAction = async (action: string, reason?: string) => {
    if (!detail) return;
    const act = action.toLowerCase();
    if (act === 'delete') {
      try {
        await stockAdjustmentsApi.delete(detail.id);
        message.success('Đã xóa phiếu điều chỉnh thành công');
        setDrawerOpen(false);
        fetchData();
      } catch (err: any) {
        message.error(err.response?.data?.detail || err.response?.data?.message || 'Không thể xóa phiếu');
      }
      return;
    }
    if (act === 'edit') {
      message.info('Để chỉnh sửa, bạn có thể xóa phiếu nháp này và tạo lại.');
      return;
    }
    const fn = (stockAdjustmentsApi as any)[act] || (stockAdjustmentsApi as any)[action];
    if (fn) {
      try {
        if (reason) await fn(detail.id, reason);
        else await fn(detail.id);
        message.success('Thao tác thành công');
        await showDetail(detail.id);
        fetchData();
      } catch (err: any) {
        const msg = err.response?.data?.detail || err.response?.data?.message || err.message || 'Thao tác không thành công';
        if (err.response?.status === 422) {
          modal.warning({
            title: 'Quy tắc nghiệp vụ WMS',
            content: (
              <div>
                <p>Hệ thống không thể thực hiện thao tác do ràng buộc nghiệp vụ kho:</p>
                <div
                  style={{
                    background: '#fffbe6',
                    border: '1px solid #ffe58f',
                    padding: '10px 14px',
                    borderRadius: 6,
                    color: '#d46b08',
                    fontSize: 13,
                    lineHeight: 1.6,
                    marginTop: 8,
                  }}
                >
                  {msg}
                </div>
              </div>
            ),
            width: 540,
            okText: 'Đã hiểu',
          });
        } else {
          message.error(msg);
        }
      }
    } else {
      message.warning(`Chưa hỗ trợ thao tác: ${action}`);
    }
  };

  const handleCreateAdjustment = async (values: any) => {
    try {
      const selectedReasonText = REASON_OPTIONS.find(o => o.value === values.reasonCode)?.label || 'Điều chỉnh tồn kho';
      await stockAdjustmentsApi.create({
        warehouseId: values.warehouseId,
        reasonCode: values.reasonCode,
        reason: values.reason || selectedReasonText,
        note: values.note,
        lines: (values.lines || []).map((l: any) => ({
          productId: l.productId,
          locationId: l.locationId,
          adjustmentQuantity: Number(l.quantity),
          note: l.reason || values.reason || selectedReasonText,
        })),
      });
      message.success('Tạo phiếu điều chỉnh tồn kho thành công!');
      setCreateModalOpen(false);
      createForm.resetFields();
      fetchData();
    } catch (err: any) {
      message.error(err.response?.data?.message || 'Không thể tạo phiếu điều chỉnh');
    }
  };

  const columns = [
    { title: 'Số phiếu', dataIndex: 'documentNo', width: 140, render: (v: string) => <b>{v}</b> },
    { title: 'Kho', dataIndex: 'warehouseCode', width: 110 },
    {
      title: 'Lý do',
      dataIndex: 'reasonCode',
      width: 140,
      render: (v: number) => <Tag color="blue">{reasonLabels[v] || v}</Tag>
    },
    {
      title: 'Tăng tồn',
      dataIndex: 'totalIncrease',
      width: 100,
      render: (v: number) => (v > 0 ? <Tag color="green">+{v}</Tag> : '0')
    },
    {
      title: 'Giảm tồn',
      dataIndex: 'totalDecrease',
      width: 100,
      render: (v: number) => (v > 0 ? <Tag color="red">-{v}</Tag> : '0')
    },
    { title: 'Trạng thái', dataIndex: 'status', width: 130, render: (v: any) => <StatusTag status={v} /> },
    {
      title: 'Thao tác',
      width: 90,
      fixed: 'right' as const,
      render: (_: any, r: StockAdjustmentSummary) => (
        <Button icon={<EyeOutlined />} size="small" type="primary" ghost onClick={() => showDetail(r.id)}>
          Xem
        </Button>
      ),
    },
  ];

  return (
    <div>
      <Card
        title="⚖️ Quản lý Điều chỉnh Tồn kho (Stock Adjustments)"
        extra={
          <Space>
            <Button icon={<ReloadOutlined />} onClick={fetchData} />
            <Button
              type="primary"
              icon={<PlusOutlined />}
              onClick={() => {
                createForm.resetFields();
                createForm.setFieldsValue({
                  reasonCode: 2,
                  lines: [{ quantity: 1 }]
                });
                setCreateModalOpen(true);
              }}
            >
              Tạo phiếu điều chỉnh mới
            </Button>
          </Space>
        }
        style={{ marginBottom: 16 }}
      >
        <Space wrap>
          <Input.Search
            placeholder="Tìm theo số phiếu, lý do..."
            allowClear
            onSearch={(v) => setParams(p => ({ ...p, keyword: v, page: 1 }))}
            style={{ width: 260 }}
          />
          <Select
            placeholder="Lọc theo kho"
            allowClear
            style={{ width: 200 }}
            onChange={(v) => setParams(p => ({ ...p, warehouseId: v, page: 1 }))}
            options={(warehouses || []).map(w => ({ value: w.id, label: w.name }))}
          />
        </Space>
      </Card>

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
          showTotal: (t) => `Tổng ${t} phiếu điều chỉnh`,
        }}
        onChange={(p) => setParams(prev => ({ ...prev, page: p.current || 1, pageSize: p.pageSize || 20 }))}
      />

      {/* Modal Tạo Phiếu Điều Chỉnh */}
      <Modal
        title="⚖️ Tạo Phiếu Điều Chỉnh Tồn Kho Mới"
        open={createModalOpen}
        onCancel={() => setCreateModalOpen(false)}
        onOk={() => createForm.submit()}
        okText="Tạo phiếu điều chỉnh"
        cancelText="Hủy"
        width={850}
      >
        <Form form={createForm} layout="vertical" onFinish={handleCreateAdjustment}>
          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 16 }}>
            <Form.Item
              name="warehouseId"
              label="Kho hàng cần điều chỉnh"
              rules={[{ required: true, message: 'Vui lòng chọn kho' }]}
            >
              <Select
                placeholder="Chọn kho hàng"
                options={(warehouses || []).map(w => ({ value: w.id, label: `${w.name} (${w.code})` }))}
                onChange={(val) => setSelectedWarehouseForCreate(val)}
              />
            </Form.Item>

            <Form.Item
              name="reasonCode"
              label="Phân loại lý do điều chỉnh"
              rules={[{ required: true, message: 'Chọn phân loại lý do' }]}
            >
              <Select options={REASON_OPTIONS} />
            </Form.Item>
          </div>

          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 16 }}>
            <Form.Item
              name="reason"
              label="Lý do chi tiết"
              rules={[{ required: true, message: 'Bắt buộc nhập lý do điều chỉnh' }]}
            >
              <Input placeholder="VD: Hàng bị vỡ trong lúc dỡ pallet, bù trừ sau kiểm kê..." />
            </Form.Item>
            <Form.Item name="note" label="Ghi chú">
              <Input placeholder="Ghi chú thêm..." />
            </Form.Item>
          </div>

          <Divider orientation="left">Danh sách mặt hàng điều chỉnh số lượng</Divider>

          <Form.List name="lines">
            {(fields, { add, remove }) => (
              <>
                {fields.map(({ key, name, ...restField }) => (
                  <div key={key} style={{ display: 'flex', gap: 12, alignItems: 'center', marginBottom: 12, background: '#fafafa', padding: 10, borderRadius: 6 }}>
                    <Form.Item
                      {...restField}
                      name={[name, 'productId']}
                      rules={[{ required: true, message: 'Chọn SP' }]}
                      style={{ flex: 4, margin: 0 }}
                    >
                      <Select
                        placeholder="Chọn sản phẩm"
                        showSearch
                        optionFilterProp="label"
                        options={(products || []).map(p => ({ value: p.id, label: `${p.sku} - ${p.name}` }))}
                      />
                    </Form.Item>

                    <Form.Item
                      {...restField}
                      name={[name, 'locationId']}
                      rules={[{ required: true, message: 'Chọn vị trí' }]}
                      style={{ flex: 4, margin: 0 }}
                    >
                      <Select
                        placeholder="Vị trí ô chứa"
                        options={(locations || []).map(l => ({ value: l.id, label: `${l.code} - ${l.name}` }))}
                        disabled={!selectedWarehouseForCreate}
                      />
                    </Form.Item>

                    <Form.Item
                      {...restField}
                      name={[name, 'quantity']}
                      rules={[{ required: true, message: 'SL thay đổi' }]}
                      style={{ flex: 3, margin: 0 }}
                    >
                      <InputNumber
                        placeholder="+Tăng hoặc -Giảm"
                        style={{ width: '100%' }}
                      />
                    </Form.Item>

                    <Button type="text" danger icon={<DeleteOutlined />} onClick={() => remove(name)} />
                  </div>
                ))}
                <Button type="dashed" onClick={() => add()} block icon={<PlusOutlined />}>
                  Thêm dòng điều chỉnh
                </Button>
              </>
            )}
          </Form.List>
        </Form>
      </Modal>

      {/* Drawer Chi tiết Phiếu Điều Chỉnh */}
      <Drawer
        title={`Chi tiết phiếu điều chỉnh: ${detail?.documentNo || ''}`}
        width={700}
        open={drawerOpen}
        onClose={() => setDrawerOpen(false)}
      >
        {detail && (
          <>
            <Descriptions bordered size="small" column={2}>
              <Descriptions.Item label="Số phiếu">{detail.documentNo}</Descriptions.Item>
              <Descriptions.Item label="Trạng thái"><StatusTag status={detail.status} /></Descriptions.Item>
              <Descriptions.Item label="Kho">{detail.warehouseCode}</Descriptions.Item>
              <Descriptions.Item label="Phân loại">{reasonLabels[detail.reasonCode] || detail.reasonCode}</Descriptions.Item>
              <Descriptions.Item label="Lý do" span={2}>{detail.reason}</Descriptions.Item>
              <Descriptions.Item label="Ghi chú" span={2}>{detail.note || '-'}</Descriptions.Item>
            </Descriptions>

            <Divider orientation="left">Chi tiết hàng điều chỉnh</Divider>
            <Table
              size="small"
              rowKey="id"
              dataSource={detail.lines}
              pagination={false}
              columns={[
                { title: 'Sản phẩm', render: (_: any, r: any) => `${r.productSku} - ${r.productName}` },
                { title: 'Vị trí', dataIndex: 'locationCode' },
                {
                  title: 'Số lượng thay đổi',
                  dataIndex: 'adjustmentQuantity',
                  render: (v: number) => {
                    if (v > 0) return <Tag color="green">+{v}</Tag>;
                    if (v < 0) return <Tag color="red">{v}</Tag>;
                    return '0';
                  }
                },
                { title: 'Ghi chú dòng', dataIndex: 'note' },
              ]}
            />

            <Divider orientation="left">Quy trình xử lý</Divider>
            <DocumentActions
              availableActions={detail.availableActions}
              onAction={handleAction}
            />
          </>
        )}
      </Drawer>
    </div>
  );
}

import { useEffect, useState, useCallback } from 'react';
import {
  Table, Button, Space, Input, Select, message, Drawer, Descriptions,
  Divider, Steps, Modal, Form, InputNumber, Card, Tag
} from 'antd';
import {
  PlusOutlined, ReloadOutlined, EyeOutlined, DeleteOutlined
} from '@ant-design/icons';
import type {
  StockTransferSummary, StockTransferDetail, WarehouseResponse,
  ProductResponse, LocationResponse
} from '../../types';
import {
  stockTransfersApi, warehousesApi, productsApi, locationsApi, extractItems
} from '../../api/services';
import { StatusTag, DocumentActions } from '../../components/DocumentActions';

export default function StockTransfersPage() {
  const [data, setData] = useState<StockTransferSummary[]>([]);
  const [total, setTotal] = useState(0);
  const [loading, setLoading] = useState(false);
  const [warehouses, setWarehouses] = useState<WarehouseResponse[]>([]);
  const [products, setProducts] = useState<ProductResponse[]>([]);
  const [sourceLocations, setSourceLocations] = useState<LocationResponse[]>([]);
  const [destLocations, setDestLocations] = useState<LocationResponse[]>([]);

  const [params, setParams] = useState<Record<string, any>>({ page: 1, pageSize: 20 });
  const [detail, setDetail] = useState<StockTransferDetail | null>(null);
  const [drawerOpen, setDrawerOpen] = useState(false);

  // Modal tạo chuyển kho
  const [createModalOpen, setCreateModalOpen] = useState(false);
  const [createForm] = Form.useForm();
  const [selectedSourceWh, setSelectedSourceWh] = useState<number | null>(null);
  const [selectedDestWh, setSelectedDestWh] = useState<number | null>(null);

  const fetchData = useCallback(async () => {
    setLoading(true);
    try {
      const { data: res } = await stockTransfersApi.list(params);
      setData(res.items);
      setTotal(res.totalCount);
    } catch {
      message.error('Không thể tải danh sách phiếu chuyển kho');
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

  // Load vị trí khi chọn kho nguồn / đích
  useEffect(() => {
    if (selectedSourceWh) {
      locationsApi.list(selectedSourceWh, { pageSize: 100 }).then(r => setSourceLocations(extractItems(r.data))).catch(() => setSourceLocations([]));
    } else {
      setSourceLocations([]);
    }
  }, [selectedSourceWh]);

  useEffect(() => {
    if (selectedDestWh) {
      locationsApi.list(selectedDestWh, { pageSize: 100 }).then(r => setDestLocations(extractItems(r.data))).catch(() => setDestLocations([]));
    } else {
      setDestLocations([]);
    }
  }, [selectedDestWh]);

  const showDetail = async (id: number) => {
    try {
      const { data } = await stockTransfersApi.get(id);
      setDetail(data);
      setDrawerOpen(true);
    } catch {
      message.error('Lỗi khi tải chi tiết phiếu chuyển kho');
    }
  };

  const handleAction = async (action: string, reason?: string) => {
    if (!detail) return;
    const fn = (stockTransfersApi as any)[action];
    if (fn) {
      try {
        if (reason) await fn(detail.id, reason);
        else await fn(detail.id);
        message.success('Thao tác thành công');
        await showDetail(detail.id);
        fetchData();
      } catch (err: any) {
        message.error(err.response?.data?.message || 'Thao tác không thành công');
      }
    }
  };

  const handleCreateTransfer = async (values: any) => {
    try {
      await stockTransfersApi.create({
        warehouseId: values.warehouseId,
        destinationWarehouseId: values.destinationWarehouseId,
        reason: values.reason,
        note: values.note,
        lines: (values.lines || []).map((l: any) => ({
          productId: l.productId,
          sourceLocationId: l.sourceLocationId,
          destinationLocationId: l.destinationLocationId,
          quantity: Number(l.quantity),
          note: l.note,
        })),
      });
      message.success('Tạo phiếu chuyển kho thành công!');
      setCreateModalOpen(false);
      createForm.resetFields();
      fetchData();
    } catch (err: any) {
      message.error(err.response?.data?.message || 'Không thể tạo phiếu chuyển kho');
    }
  };

  const getTransferStep = () => {
    if (!detail) return 0;
    if (detail.progress?.receivedAt) return 3;
    if (detail.progress?.shippedAt) return 2;
    if (detail.progress?.pickedAt) return 1;
    return 0;
  };

  const columns = [
    { title: 'Số phiếu', dataIndex: 'documentNo', width: 140, render: (v: string) => <b>{v}</b> },
    { title: 'Từ kho', dataIndex: 'sourceWarehouseCode', width: 110 },
    { title: 'Đến kho', dataIndex: 'destinationWarehouseCode', width: 110 },
    {
      title: 'Phân loại',
      dataIndex: 'isInternal',
      width: 100,
      render: (v: boolean) => <Tag color={v ? 'purple' : 'cyan'}>{v ? 'Nội bộ kho' : 'Liên kho'}</Tag>
    },
    { title: 'Dòng', dataIndex: 'lineCount', width: 70 },
    { title: 'Tổng SL', dataIndex: 'totalQuantity', width: 100, render: (v: number) => v?.toLocaleString() },
    { title: 'Trạng thái', dataIndex: 'status', width: 130, render: (v: any) => <StatusTag status={v} /> },
    {
      title: 'Thao tác',
      width: 90,
      fixed: 'right' as const,
      render: (_: any, r: StockTransferSummary) => (
        <Button icon={<EyeOutlined />} size="small" type="primary" ghost onClick={() => showDetail(r.id)}>
          Xem
        </Button>
      ),
    },
  ];

  return (
    <div>
      <Card
        title="🔄 Quản lý Chuyển Kho (Stock Transfers)"
        extra={
          <Space>
            <Button icon={<ReloadOutlined />} onClick={fetchData} />
            <Button
              type="primary"
              icon={<PlusOutlined />}
              onClick={() => {
                createForm.resetFields();
                createForm.setFieldsValue({
                  lines: [{ quantity: 1 }]
                });
                setCreateModalOpen(true);
              }}
            >
              Tạo phiếu chuyển mới
            </Button>
          </Space>
        }
        style={{ marginBottom: 16 }}
      >
        <Space wrap>
          <Input.Search
            placeholder="Tìm theo số phiếu..."
            allowClear
            onSearch={(v) => setParams(p => ({ ...p, keyword: v, page: 1 }))}
            style={{ width: 260 }}
          />
          <Select
            placeholder="Kho nguồn"
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
          showTotal: (t) => `Tổng ${t} phiếu chuyển`,
        }}
        onChange={(p) => setParams(prev => ({ ...prev, page: p.current || 1, pageSize: p.pageSize || 20 }))}
      />

      {/* Modal Tạo Phiếu Chuyển Kho */}
      <Modal
        title="🔄 Tạo Phiếu Chuyển Kho Mới"
        open={createModalOpen}
        onCancel={() => setCreateModalOpen(false)}
        onOk={() => createForm.submit()}
        okText="Tạo phiếu chuyển"
        cancelText="Hủy"
        width={850}
      >
        <Form form={createForm} layout="vertical" onFinish={handleCreateTransfer}>
          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 16 }}>
            <Form.Item
              name="warehouseId"
              label="Từ kho (Kho xuất đi)"
              rules={[{ required: true, message: 'Vui lòng chọn kho nguồn' }]}
            >
              <Select
                placeholder="Chọn kho nguồn"
                options={(warehouses || []).map(w => ({ value: w.id, label: `${w.name} (${w.code})` }))}
                onChange={(val) => setSelectedSourceWh(val)}
              />
            </Form.Item>

            <Form.Item
              name="destinationWarehouseId"
              label="Đến kho (Kho nhận đến)"
              rules={[{ required: true, message: 'Vui lòng chọn kho đích' }]}
            >
              <Select
                placeholder="Chọn kho nhận đến"
                options={(warehouses || []).map(w => ({ value: w.id, label: `${w.name} (${w.code})` }))}
                onChange={(val) => setSelectedDestWh(val)}
              />
            </Form.Item>
          </div>

          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 16 }}>
            <Form.Item name="reason" label="Lý do điều chuyển">
              <Input placeholder="VD: Cân đối tồn kho, Tiếp ứng chi nhánh..." />
            </Form.Item>
            <Form.Item name="note" label="Ghi chú">
              <Input placeholder="Ghi chú thêm..." />
            </Form.Item>
          </div>

          <Divider orientation="left">Danh sách hàng hóa điều chuyển</Divider>

          <Form.List name="lines">
            {(fields, { add, remove }) => (
              <>
                {fields.map(({ key, name, ...restField }) => (
                  <div key={key} style={{ display: 'flex', gap: 12, alignItems: 'center', marginBottom: 12, background: '#fafafa', padding: 10, borderRadius: 6 }}>
                    <Form.Item
                      {...restField}
                      name={[name, 'productId']}
                      rules={[{ required: true, message: 'Chọn SP' }]}
                      style={{ flex: 3, margin: 0 }}
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
                      name={[name, 'sourceLocationId']}
                      rules={[{ required: true, message: 'Vui lòng chọn vị trí đi' }]}
                      style={{ flex: 3, margin: 0 }}
                    >
                      <Select
                        placeholder="Vị trí lấy hàng"
                        options={(sourceLocations || []).map(l => ({ value: l.id, label: `${l.code} - ${l.name}` }))}
                        disabled={!selectedSourceWh}
                      />
                    </Form.Item>

                    <Form.Item
                      {...restField}
                      name={[name, 'destinationLocationId']}
                      rules={[{ required: true, message: 'Vui lòng chọn vị trí đến' }]}
                      style={{ flex: 3, margin: 0 }}
                    >
                      <Select
                        placeholder="Vị trí cất hàng"
                        options={(destLocations || []).map(l => ({ value: l.id, label: `${l.code} - ${l.name}` }))}
                        disabled={!selectedDestWh}
                      />
                    </Form.Item>

                    <Form.Item
                      {...restField}
                      name={[name, 'quantity']}
                      rules={[{ required: true, message: 'SL' }]}
                      style={{ flex: 2, margin: 0 }}
                    >
                      <InputNumber min={1} placeholder="Số lượng" style={{ width: '100%' }} />
                    </Form.Item>

                    <Button type="text" danger icon={<DeleteOutlined />} onClick={() => remove(name)} />
                  </div>
                ))}
                <Button type="dashed" onClick={() => add()} block icon={<PlusOutlined />}>
                  Thêm dòng chuyển kho
                </Button>
              </>
            )}
          </Form.List>
        </Form>
      </Modal>

      {/* Drawer Chi tiết Phiếu Chuyển Kho */}
      <Drawer
        title={`Chi tiết phiếu chuyển: ${detail?.documentNo || ''}`}
        width={750}
        open={drawerOpen}
        onClose={() => setDrawerOpen(false)}
      >
        {detail && (
          <>
            <Descriptions bordered size="small" column={2}>
              <Descriptions.Item label="Số phiếu">{detail.documentNo}</Descriptions.Item>
              <Descriptions.Item label="Trạng thái"><StatusTag status={detail.status} /></Descriptions.Item>
              <Descriptions.Item label="Từ kho">{detail.sourceWarehouseCode}</Descriptions.Item>
              <Descriptions.Item label="Đến kho">{detail.destinationWarehouseCode}</Descriptions.Item>
              <Descriptions.Item label="Lý do">{detail.reason || '-'}</Descriptions.Item>
              <Descriptions.Item label="Ghi chú">{detail.note || '-'}</Descriptions.Item>
            </Descriptions>

            <Divider orientation="left">Tiến trình vận chuyển</Divider>
            <Steps
              size="small"
              current={getTransferStep()}
              items={[
                { title: 'Tạo phiếu' },
                { title: 'Đã lấy hàng (Pick)' },
                { title: 'Đang giao (Ship)' },
                { title: 'Đã nhận (Receive)' },
              ]}
              style={{ marginBottom: 20 }}
            />

            <Divider orientation="left">Chi tiết hàng chuyển</Divider>
            <Table
              size="small"
              rowKey="id"
              dataSource={detail.lines}
              pagination={false}
              columns={[
                { title: 'Sản phẩm', render: (_: any, r: any) => `${r.productSku} - ${r.productName}` },
                { title: 'Vị trí đi', dataIndex: 'sourceLocationCode' },
                { title: 'Vị trí đến', dataIndex: 'destinationLocationCode' },
                { title: 'SL chuyển', dataIndex: 'quantity', render: (v: number) => v?.toLocaleString() },
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

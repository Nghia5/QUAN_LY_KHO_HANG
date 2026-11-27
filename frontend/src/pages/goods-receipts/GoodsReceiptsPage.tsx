import { useEffect, useState, useCallback } from 'react';
import {
  Table, Button, Space, Input, Select, message, Drawer, Descriptions,
  Divider, Modal, Form, InputNumber, Card, Tag
} from 'antd';
import {
  PlusOutlined, ReloadOutlined, EyeOutlined, DeleteOutlined
} from '@ant-design/icons';
import type {
  GoodsReceiptSummary, GoodsReceiptDetail, WarehouseResponse,
  SupplierResponse, ProductResponse, LocationResponse
} from '../../types';
import {
  goodsReceiptsApi, warehousesApi, suppliersApi, productsApi, locationsApi, extractItems
} from '../../api/services';
import { StatusTag, DocumentActions } from '../../components/DocumentActions';

export default function GoodsReceiptsPage() {
  const [data, setData] = useState<GoodsReceiptSummary[]>([]);
  const [total, setTotal] = useState(0);
  const [loading, setLoading] = useState(false);
  const [warehouses, setWarehouses] = useState<WarehouseResponse[]>([]);
  const [suppliers, setSuppliers] = useState<SupplierResponse[]>([]);
  const [products, setProducts] = useState<ProductResponse[]>([]);
  const [locations, setLocations] = useState<LocationResponse[]>([]);

  const [params, setParams] = useState<Record<string, any>>({ page: 1, pageSize: 20 });
  const [detail, setDetail] = useState<GoodsReceiptDetail | null>(null);
  const [drawerOpen, setDrawerOpen] = useState(false);

  // Modal tạo phiếu nhập
  const [createModalOpen, setCreateModalOpen] = useState(false);
  const [createForm] = Form.useForm();
  const [selectedWarehouseForCreate, setSelectedWarehouseForCreate] = useState<number | null>(null);

  const fetchData = useCallback(async () => {
    setLoading(true);
    try {
      const { data: res } = await goodsReceiptsApi.list(params);
      setData(res.items);
      setTotal(res.totalCount);
    } catch {
      message.error('Không thể tải danh sách phiếu nhập kho');
    } finally {
      setLoading(false);
    }
  }, [params]);

  useEffect(() => {
    warehousesApi.list({ pageSize: 100 }).then(r => setWarehouses(extractItems(r.data))).catch(() => setWarehouses([]));
    suppliersApi.list({ pageSize: 100 }).then(r => setSuppliers(extractItems(r.data))).catch(() => setSuppliers([]));
    productsApi.list({ pageSize: 100 }).then(r => setProducts(extractItems(r.data))).catch(() => setProducts([]));
  }, []);

  useEffect(() => {
    fetchData();
  }, [fetchData]);

  // Load locations khi chọn kho trong modal tạo mới
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
      const { data } = await goodsReceiptsApi.get(id);
      setDetail(data);
      setDrawerOpen(true);
    } catch {
      message.error('Lỗi khi tải chi tiết phiếu nhập');
    }
  };

  const handleAction = async (action: string, reason?: string) => {
    if (!detail) return;
    const fn = (goodsReceiptsApi as any)[action];
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

  const handleCreateReceipt = async (values: any) => {
    try {
      await goodsReceiptsApi.create({
        warehouseId: values.warehouseId,
        supplierId: values.supplierId,
        supplierName: suppliers.find(s => s.id === values.supplierId)?.name || values.supplierName,
        referenceNo: values.referenceNo,
        note: values.note,
        lines: (values.lines || []).map((l: any) => ({
          productId: l.productId,
          locationId: l.locationId,
          quantity: Number(l.quantity),
          unitCost: Number(l.unitCost) || 0,
          batchCode: l.batchCode,
          expiryDate: l.expiryDate,
          note: l.note,
        })),
      });
      message.success('Tạo phiếu nhập kho thành công!');
      setCreateModalOpen(false);
      createForm.resetFields();
      fetchData();
    } catch (err: any) {
      message.error(err.response?.data?.message || 'Không thể tạo phiếu nhập kho');
    }
  };

  const columns = [
    { title: 'Số phiếu', dataIndex: 'documentNo', width: 140, render: (v: string) => <b>{v}</b> },
    { title: 'Kho', dataIndex: 'warehouseCode', width: 110 },
    { title: 'Nhà cung cấp', dataIndex: 'supplierName', ellipsis: true },
    { title: 'Ngày nhập', dataIndex: 'receiptDate', width: 120, render: (v: string) => v ? new Date(v).toLocaleDateString('vi-VN') : '-' },
    { title: 'Dòng', dataIndex: 'lineCount', width: 70 },
    { title: 'Tổng SL', dataIndex: 'totalQuantity', width: 100, render: (v: number) => v?.toLocaleString() },
    { title: 'Trạng thái', dataIndex: 'status', width: 130, render: (v: any) => <StatusTag status={v} /> },
    {
      title: 'Thao tác',
      width: 90,
      fixed: 'right' as const,
      render: (_: any, r: GoodsReceiptSummary) => (
        <Button icon={<EyeOutlined />} size="small" type="primary" ghost onClick={() => showDetail(r.id)}>
          Xem
        </Button>
      ),
    },
  ];

  return (
    <div>
      <Card
        title="📥 Quản lý Phiếu Nhập Kho (Goods Receipts)"
        extra={
          <Space>
            <Button icon={<ReloadOutlined />} onClick={fetchData} />
            <Button
              type="primary"
              icon={<PlusOutlined />}
              onClick={() => {
                createForm.resetFields();
                createForm.setFieldsValue({
                  lines: [{ quantity: 1, unitCost: 0 }]
                });
                setCreateModalOpen(true);
              }}
            >
              Tạo phiếu nhập mới
            </Button>
          </Space>
        }
        style={{ marginBottom: 16 }}
      >
        <Space wrap>
          <Input.Search
            placeholder="Tìm theo số phiếu, ghi chú..."
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
          <Select
            placeholder="Lọc theo NCC"
            allowClear
            style={{ width: 220 }}
            onChange={(v) => setParams(p => ({ ...p, supplierId: v, page: 1 }))}
            options={(suppliers || []).map(s => ({ value: s.id, label: s.name }))}
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
          showTotal: (t) => `Tổng ${t} phiếu nhập`,
        }}
        onChange={(p) => setParams(prev => ({ ...prev, page: p.current || 1, pageSize: p.pageSize || 20 }))}
      />

      {/* Modal Tạo Phiếu Nhập Kho Mới */}
      <Modal
        title="📥 Tạo Phiếu Nhập Kho Mới"
        open={createModalOpen}
        onCancel={() => setCreateModalOpen(false)}
        onOk={() => createForm.submit()}
        okText="Tạo phiếu nhập"
        cancelText="Hủy"
        width={850}
      >
        <Form form={createForm} layout="vertical" onFinish={handleCreateReceipt}>
          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 16 }}>
            <Form.Item
              name="warehouseId"
              label="Kho nhận hàng"
              rules={[{ required: true, message: 'Vui lòng chọn kho' }]}
            >
              <Select
                placeholder="Chọn kho hàng nhận"
                options={(warehouses || []).map(w => ({ value: w.id, label: `${w.name} (${w.code})` }))}
                onChange={(val) => {
                  setSelectedWarehouseForCreate(val);
                }}
              />
            </Form.Item>

            <Form.Item
              name="supplierId"
              label="Nhà cung cấp"
              rules={[{ required: true, message: 'Vui lòng chọn nhà cung cấp' }]}
            >
              <Select
                placeholder="Chọn nhà cung cấp"
                options={(suppliers || []).map(s => ({ value: s.id, label: `${s.name} (${s.code})` }))}
              />
            </Form.Item>
          </div>

          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 16 }}>
            <Form.Item name="referenceNo" label="Mã tham chiếu / Số hóa đơn NCC">
              <Input placeholder="VD: PO-2026-001, HĐ-8892" />
            </Form.Item>
            <Form.Item name="note" label="Ghi chú nhập kho">
              <Input placeholder="Ghi chú đợt giao hàng..." />
            </Form.Item>
          </div>

          <Divider orientation="left">Danh sách hàng hóa nhập kho</Divider>

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
                      name={[name, 'locationId']}
                      rules={[{ required: true, message: 'Chọn vị trí' }]}
                      style={{ flex: 3, margin: 0 }}
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
                      rules={[{ required: true, message: 'SL' }]}
                      style={{ flex: 2, margin: 0 }}
                    >
                      <InputNumber min={1} placeholder="Số lượng" style={{ width: '100%' }} />
                    </Form.Item>

                    <Form.Item
                      {...restField}
                      name={[name, 'unitCost']}
                      style={{ flex: 2, margin: 0 }}
                    >
                      <InputNumber min={0} placeholder="Giá nhập (₫)" style={{ width: '100%' }} />
                    </Form.Item>

                    <Form.Item
                      {...restField}
                      name={[name, 'batchCode']}
                      style={{ flex: 2, margin: 0 }}
                    >
                      <Input placeholder="Số lô (nếu có)" />
                    </Form.Item>

                    <Button type="text" danger icon={<DeleteOutlined />} onClick={() => remove(name)} />
                  </div>
                ))}
                <Button type="dashed" onClick={() => add()} block icon={<PlusOutlined />}>
                  Thêm dòng sản phẩm
                </Button>
              </>
            )}
          </Form.List>
        </Form>
      </Modal>

      {/* Drawer Chi tiết Phiếu Nhập Kho */}
      <Drawer
        title={`Chi tiết phiếu: ${detail?.documentNo || ''}`}
        width={700}
        open={drawerOpen}
        onClose={() => setDrawerOpen(false)}
      >
        {detail && (
          <>
            <Descriptions bordered size="small" column={2}>
              <Descriptions.Item label="Số phiếu">{detail.documentNo}</Descriptions.Item>
              <Descriptions.Item label="Trạng thái"><StatusTag status={detail.status} /></Descriptions.Item>
              <Descriptions.Item label="Kho nhận">{detail.warehouseCode}</Descriptions.Item>
              <Descriptions.Item label="Nhà cung cấp">{detail.supplierName || '-'}</Descriptions.Item>
              <Descriptions.Item label="Ngày nhập">{detail.receiptDate ? new Date(detail.receiptDate).toLocaleString('vi-VN') : '-'}</Descriptions.Item>
              <Descriptions.Item label="Mã tham chiếu">{detail.referenceNo || '-'}</Descriptions.Item>
              <Descriptions.Item label="Ghi chú" span={2}>{detail.note || '-'}</Descriptions.Item>
            </Descriptions>

            <Divider orientation="left">Chi tiết hàng nhập</Divider>
            <Table
              size="small"
              rowKey="id"
              dataSource={detail.lines}
              pagination={false}
              columns={[
                { title: 'Sản phẩm', render: (_: any, r: any) => `${r.productSku} - ${r.productName}` },
                { title: 'Vị trí', dataIndex: 'locationCode' },
                { title: 'SL', dataIndex: 'quantity', render: (v: number) => v?.toLocaleString() },
                { title: 'Đơn giá', dataIndex: 'unitCost', render: (v: number) => v ? `${Number(v).toLocaleString()} ₫` : '-' },
                { title: 'Lô hàng', dataIndex: 'batchCode', render: (v: string) => v ? <Tag color="blue">{v}</Tag> : '-' },
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

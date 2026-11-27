import { useEffect, useState, useCallback } from 'react';
import {
  Table, Button, Space, Input, Select, Drawer, Descriptions,
  Divider, Modal, Form, InputNumber, Card, Tag, App
} from 'antd';
import {
  PlusOutlined, ReloadOutlined, EyeOutlined, DeleteOutlined,
  PrinterOutlined, DownloadOutlined
} from '@ant-design/icons';
import type {
  GoodsIssueSummary, GoodsIssueDetail, WarehouseResponse,
  ProductResponse, LocationResponse
} from '../../types';
import {
  goodsIssuesApi, warehousesApi, productsApi, locationsApi, extractItems
} from '../../api/services';
import { StatusTag, DocumentActions } from '../../components/DocumentActions';
import { DocumentAttachments } from '../../components/DocumentAttachments';
import { exportToCsv, printDocument } from '../../utils/exportReceipt';

export default function GoodsIssuesPage() {
  const { message, modal } = App.useApp();
  const [data, setData] = useState<GoodsIssueSummary[]>([]);
  const [total, setTotal] = useState(0);
  const [loading, setLoading] = useState(false);
  const [warehouses, setWarehouses] = useState<WarehouseResponse[]>([]);
  const [products, setProducts] = useState<ProductResponse[]>([]);
  const [locations, setLocations] = useState<LocationResponse[]>([]);

  const [params, setParams] = useState<Record<string, any>>({ page: 1, pageSize: 20 });
  const [detail, setDetail] = useState<GoodsIssueDetail | null>(null);
  const [drawerOpen, setDrawerOpen] = useState(false);

  // Modal tạo phiếu xuất
  const [createModalOpen, setCreateModalOpen] = useState(false);
  const [createForm] = Form.useForm();
  const [selectedWarehouseForCreate, setSelectedWarehouseForCreate] = useState<number | null>(null);

  const fetchData = useCallback(async () => {
    setLoading(true);
    try {
      const { data: res } = await goodsIssuesApi.list(params);
      setData(res.items);
      setTotal(res.totalCount);
    } catch {
      message.error('Không thể tải danh sách phiếu xuất kho');
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
      const { data } = await goodsIssuesApi.get(id);
      setDetail(data);
      setDrawerOpen(true);
    } catch {
      message.error('Lỗi khi tải chi tiết phiếu xuất');
    }
  };

  const handleAction = async (action: string, reason?: string) => {
    if (!detail) return;
    const act = action.toLowerCase();
    if (act === 'delete') {
      try {
        await goodsIssuesApi.delete(detail.id);
        message.success('Đã xóa phiếu xuất kho thành công');
        setDrawerOpen(false);
        fetchData();
      } catch (err: any) {
        message.error(err.response?.data?.detail || err.response?.data?.message || 'Không thể xóa phiếu');
      }
      return;
    }
    if (act === 'edit') {
      message.info('Để chỉnh sửa thông tin hàng hóa, bạn có thể xóa phiếu nháp này và tạo lại.');
      return;
    }
    const fn = (goodsIssuesApi as any)[act] || (goodsIssuesApi as any)[action];
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

  const handlePrint = () => {
    if (!detail) return;
    printDocument({
      documentNo: detail.documentNo,
      type: 'ISSUE',
      date: detail.issueDate || new Date().toISOString(),
      warehouseName: detail.warehouseCode || 'Kho xuất',
      partnerName: detail.customerName,
      referenceNo: detail.referenceNo,
      note: detail.note,
      lines: (detail.lines || []).map(l => ({
        sku: l.productSku,
        name: l.productName,
        location: l.locationCode,
        quantity: l.quantity,
        batchCode: l.batchCode,
      })),
    });
  };

  const handleExportCsv = () => {
    if (!detail) return;
    const headers = ['Mã SP', 'Tên sản phẩm', 'Vị trí lấy', 'Số lượng xuất', 'Lô hàng'];
    const rows = (detail.lines || []).map(l => [
      l.productSku,
      l.productName,
      l.locationCode,
      l.quantity,
      l.batchCode || '',
    ]);
    exportToCsv(`PhieuXuatKho_${detail.documentNo}`, headers, rows);
  };

  const handleCreateIssue = async (values: any) => {
    try {
      await goodsIssuesApi.create({
        warehouseId: values.warehouseId,
        customerName: values.customerName,
        referenceNo: values.referenceNo,
        note: values.note,
        lines: (values.lines || []).map((l: any) => ({
          productId: l.productId,
          locationId: l.locationId,
          quantity: Number(l.quantity),
          note: l.note,
        })),
      });
      message.success('Tạo phiếu xuất kho thành công!');
      setCreateModalOpen(false);
      createForm.resetFields();
      fetchData();
    } catch (err: any) {
      const detail = err.response?.data?.detail || err.response?.data?.message || err.message || 'Không thể tạo phiếu xuất kho';
      modal.error({
        title: 'Không thể tạo phiếu xuất kho (Quy tắc nghiệp vụ WMS)',
        content: (
          <div>
            <p>Hệ thống từ chối tạo phiếu xuất kho:</p>
            <div
              style={{
                background: '#fff2f0',
                border: '1px solid #ffccc7',
                padding: '10px 14px',
                borderRadius: 6,
                color: '#cf1322',
                fontSize: 13,
                lineHeight: 1.6,
                marginTop: 8,
              }}
            >
              {detail}
            </div>
            <p style={{ marginTop: 12, color: '#666', fontSize: 13 }}>
              * <b>Lưu ý quan trọng:</b> Hàng hóa chỉ được xuất từ vị trí ô chứa hàng cấp <b>Bin</b> (và phải còn đủ số
              lượng tồn khả dụng tại Bin đó).
            </p>
          </div>
        ),
        width: 560,
        okText: 'Đã hiểu',
      });
    }
  };

  const columns = [
    { title: 'Số phiếu', dataIndex: 'documentNo', width: 140, render: (v: string) => <b>{v}</b> },
    { title: 'Kho xuất', dataIndex: 'warehouseCode', width: 110 },
    { title: 'Khách hàng / Người nhận', dataIndex: 'customerName', ellipsis: true },
    { title: 'Ngày xuất', dataIndex: 'issueDate', width: 120, render: (v: string) => v ? new Date(v).toLocaleDateString('vi-VN') : '-' },
    { title: 'Dòng', dataIndex: 'lineCount', width: 70 },
    { title: 'Tổng SL', dataIndex: 'totalQuantity', width: 100, render: (v: number) => v?.toLocaleString() },
    { title: 'Trạng thái', dataIndex: 'status', width: 130, render: (v: any) => <StatusTag status={v} /> },
    {
      title: 'Thao tác',
      width: 90,
      fixed: 'right' as const,
      render: (_: any, r: GoodsIssueSummary) => (
        <Button icon={<EyeOutlined />} size="small" type="primary" ghost onClick={() => showDetail(r.id)}>
          Xem
        </Button>
      ),
    },
  ];

  return (
    <div>
      <Card
        title="📤 Quản lý Phiếu Xuất Kho (Goods Issues)"
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
              Tạo phiếu xuất mới
            </Button>
          </Space>
        }
        style={{ marginBottom: 16 }}
      >
        <Space wrap>
          <Input.Search
            placeholder="Tìm theo số phiếu, khách hàng..."
            allowClear
            onSearch={(v) => setParams(p => ({ ...p, keyword: v, page: 1 }))}
            style={{ width: 280 }}
          />
          <Select
            placeholder="Lọc theo kho"
            allowClear
            style={{ width: 220 }}
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
          showTotal: (t) => `Tổng ${t} phiếu xuất`,
        }}
        onChange={(p) => setParams(prev => ({ ...prev, page: p.current || 1, pageSize: p.pageSize || 20 }))}
      />

      {/* Modal Tạo Phiếu Xuất Kho */}
      <Modal
        title="📤 Tạo Phiếu Xuất Kho Mới"
        open={createModalOpen}
        onCancel={() => setCreateModalOpen(false)}
        onOk={() => createForm.submit()}
        okText="Tạo phiếu xuất"
        cancelText="Hủy"
        width={850}
      >
        <Form form={createForm} layout="vertical" onFinish={handleCreateIssue}>
          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 16 }}>
            <Form.Item
              name="warehouseId"
              label="Kho xuất hàng"
              rules={[{ required: true, message: 'Vui lòng chọn kho xuất' }]}
            >
              <Select
                placeholder="Chọn kho hàng xuất"
                options={(warehouses || []).map(w => ({ value: w.id, label: `${w.name} (${w.code})` }))}
                onChange={(val) => setSelectedWarehouseForCreate(val)}
              />
            </Form.Item>

            <Form.Item
              name="customerName"
              label="Khách hàng / Người nhận hàng"
              rules={[{ required: true, message: 'Nhập tên khách hàng/đối tác' }]}
            >
              <Input placeholder="Tên khách hàng hoặc bộ phận nhận..." />
            </Form.Item>
          </div>

          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 16 }}>
            <Form.Item name="referenceNo" label="Mã tham chiếu / Số đơn hàng xuất (SO)">
              <Input placeholder="VD: SO-2026-9901" />
            </Form.Item>
            <Form.Item name="note" label="Ghi chú xuất kho">
              <Input placeholder="Lý do xuất kho hoặc hướng dẫn giao nhận..." />
            </Form.Item>
          </div>

          <Divider orientation="left">Danh sách hàng hóa xuất kho</Divider>

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
                      rules={[{ required: true, message: 'Chọn vị trí xuất' }]}
                      style={{ flex: 4, margin: 0 }}
                    >
                      <Select
                        placeholder="Vị trí lấy hàng (Bin)"
                        options={(locations || []).map(l => ({
                          value: l.id,
                          label: l.type === 'Bin' ? `📦 [Bin] ${l.code} - ${l.name}` : `🚫 [${l.type}] ${l.code} (Cần chọn Bin)`,
                          disabled: l.type !== 'Bin',
                        }))}
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

                    <Button type="text" danger icon={<DeleteOutlined />} onClick={() => remove(name)} />
                  </div>
                ))}
                <Button type="dashed" onClick={() => add()} block icon={<PlusOutlined />}>
                  Thêm dòng xuất kho
                </Button>
              </>
            )}
          </Form.List>
        </Form>
      </Modal>

      {/* Drawer Chi tiết Phiếu Xuất Kho */}
      <Drawer
        title={`Chi tiết phiếu: ${detail?.documentNo || ''}`}
        width={720}
        open={drawerOpen}
        onClose={() => setDrawerOpen(false)}
        extra={
          <Space>
            <Button icon={<PrinterOutlined />} onClick={handlePrint}>In phiếu</Button>
            <Button icon={<DownloadOutlined />} onClick={handleExportCsv}>Xuất CSV</Button>
          </Space>
        }
      >
        {detail && (
          <>
            <Descriptions bordered size="small" column={2}>
              <Descriptions.Item label="Số phiếu">{detail.documentNo}</Descriptions.Item>
              <Descriptions.Item label="Trạng thái"><StatusTag status={detail.status} /></Descriptions.Item>
              <Descriptions.Item label="Kho xuất">{detail.warehouseCode}</Descriptions.Item>
              <Descriptions.Item label="Khách hàng">{detail.customerName || '-'}</Descriptions.Item>
              <Descriptions.Item label="Ngày xuất">{detail.issueDate ? new Date(detail.issueDate).toLocaleString('vi-VN') : '-'}</Descriptions.Item>
              <Descriptions.Item label="Mã tham chiếu">{detail.referenceNo || '-'}</Descriptions.Item>
              <Descriptions.Item label="Ghi chú" span={2}>{detail.note || '-'}</Descriptions.Item>
            </Descriptions>

            <Divider orientation="left">Chi tiết hàng xuất</Divider>
            <Table
              size="small"
              rowKey="id"
              dataSource={detail.lines}
              pagination={false}
              columns={[
                { title: 'Sản phẩm', render: (_: any, r: any) => `${r.productSku} - ${r.productName}` },
                { title: 'Vị trí lấy', dataIndex: 'locationCode' },
                { title: 'SL xuất', dataIndex: 'quantity', render: (v: number) => v?.toLocaleString() },
                { title: 'Lô hàng', dataIndex: 'batchCode', render: (v: string) => v ? <Tag color="blue">{v}</Tag> : '-' },
              ]}
            />

            <Divider orientation="left">Quy trình xử lý</Divider>
            <DocumentActions
              availableActions={detail.availableActions}
              onAction={handleAction}
            />

            <Divider orientation="left">Tệp đính kèm (Phiếu xuất ký nhận, chứng từ)</Divider>
            <DocumentAttachments
              documentType="Issue"
              documentId={detail.id}
              readonly={detail.status === 'Completed' || detail.status === 'Cancelled'}
            />
          </>
        )}
      </Drawer>
    </div>
  );
}

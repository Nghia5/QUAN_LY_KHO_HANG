import { useEffect, useState, useCallback } from 'react';
import {
  Table, Button, Space, Input, Select, Drawer, Descriptions, App,
  Divider, Statistic, Row, Col, Modal, Form, InputNumber, Card, Tag, Alert,
  Typography
} from 'antd';
import {
  PlusOutlined, ReloadOutlined, EyeOutlined, CheckSquareOutlined,
  EditOutlined
} from '@ant-design/icons';
import type {
  StocktakeSummary, StocktakeDetail, WarehouseResponse,
  ProductResponse, LocationResponse
} from '../../types';
import {
  stocktakesApi, warehousesApi, productsApi, locationsApi, extractItems
} from '../../api/services';
import { StatusTag, DocumentActions } from '../../components/DocumentActions';

export default function StocktakesPage() {
  const { message, modal } = App.useApp();
  const [data, setData] = useState<StocktakeSummary[]>([]);
  const [total, setTotal] = useState(0);
  const [loading, setLoading] = useState(false);
  const [warehouses, setWarehouses] = useState<WarehouseResponse[]>([]);
  const [products, setProducts] = useState<ProductResponse[]>([]);
  const [locations, setLocations] = useState<LocationResponse[]>([]);

  const [params, setParams] = useState<Record<string, any>>({ page: 1, pageSize: 20 });
  const [detail, setDetail] = useState<StocktakeDetail | null>(null);
  const [drawerOpen, setDrawerOpen] = useState(false);

  // Modal tạo đợt kiểm kê
  const [createModalOpen, setCreateModalOpen] = useState(false);
  const [createForm] = Form.useForm();

  // Modal ghi nhận số đếm kiểm kê (Count Item)
  const [countModalOpen, setCountModalOpen] = useState(false);
  const [countForm] = Form.useForm();
  const [selectedLineForCount, setSelectedLineForCount] = useState<any | null>(null);

  const fetchData = useCallback(async () => {
    setLoading(true);
    try {
      const { data: res } = await stocktakesApi.list(params);
      setData(res.items);
      setTotal(res.totalCount);
    } catch {
      message.error('Không thể tải danh sách đợt kiểm kê');
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

  const showDetail = async (id: number) => {
    try {
      const { data } = await stocktakesApi.get(id);
      setDetail(data);
      setDrawerOpen(true);
      if (data.warehouseId) {
        locationsApi.list(data.warehouseId, { pageSize: 100 })
          .then(r => setLocations(extractItems(r.data)))
          .catch(() => setLocations([]));
      }
    } catch {
      message.error('Lỗi khi tải chi tiết đợt kiểm kê');
    }
  };

  const handleAction = async (action: string, reason?: string) => {
    if (!detail) return;
    const act = action.toLowerCase();
    if (act === 'delete') {
      try {
        await stocktakesApi.delete(detail.id);
        message.success('Đã xóa đợt kiểm kê thành công');
        setDrawerOpen(false);
        fetchData();
      } catch (err: any) {
        message.error(err.response?.data?.detail || err.response?.data?.message || 'Không thể xóa phiếu');
      }
      return;
    }
    if (act === 'count') {
      setCountModalOpen(true);
      return;
    }
    if (act === 'edit') {
      message.info('Để chỉnh sửa phạm vi, bạn có thể xóa đợt kiểm kê nháp này và tạo lại.');
      return;
    }
    const fn = (stocktakesApi as any)[act] || (stocktakesApi as any)[action];
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

  const handleCreateStocktake = async (values: any) => {
    try {
      await stocktakesApi.create({
        warehouseId: values.warehouseId,
        note: values.note,
      });
      message.success('Khởi tạo đợt kiểm kê thành công!');
      setCreateModalOpen(false);
      createForm.resetFields();
      fetchData();
    } catch (err: any) {
      message.error(err.response?.data?.message || 'Không thể tạo đợt kiểm kê');
    }
  };

  const handleRecordCount = async (values: any) => {
    if (!detail) return;
    try {
      await stocktakesApi.counts(detail.id, [
        {
          productId: selectedLineForCount ? selectedLineForCount.productId : values.productId,
          locationId: selectedLineForCount ? selectedLineForCount.locationId : values.locationId,
          countedQuantity: Number(values.countedQuantity),
        }
      ]);
      message.success('Đã ghi nhận số lượng kiểm đếm!');
      setCountModalOpen(false);
      countForm.resetFields();
      setSelectedLineForCount(null);
      await showDetail(detail.id);
    } catch (err: any) {
      message.error(err.response?.data?.message || 'Không thể cập nhật số lượng kiểm đếm');
    }
  };

  const columns = [
    { title: 'Số phiếu', dataIndex: 'documentNo', width: 140, render: (v: string) => <b>{v}</b> },
    { title: 'Kho kiểm kê', dataIndex: 'warehouseCode', width: 120 },
    { title: 'Tổng dòng', dataIndex: 'lineCount', width: 90 },
    { title: 'Đã đếm', dataIndex: 'countedLines', width: 90, render: (v: number) => <Tag color="blue">{v}</Tag> },
    {
      title: 'Có chênh lệch',
      dataIndex: 'linesWithDifference',
      width: 120,
      render: (v: number) => <Tag color={v > 0 ? 'red' : 'green'}>{v} dòng</Tag>
    },
    { title: 'Trạng thái', dataIndex: 'status', width: 130, render: (v: any) => <StatusTag status={v} /> },
    {
      title: 'Thao tác',
      width: 90,
      fixed: 'right' as const,
      render: (_: any, r: StocktakeSummary) => (
        <Button icon={<EyeOutlined />} size="small" type="primary" ghost onClick={() => showDetail(r.id)}>
          Xem
        </Button>
      ),
    },
  ];

  return (
    <div>
      <Card
        title="📋 Quản lý Kiểm kê Kho (Stocktakes)"
        extra={
          <Space>
            <Button icon={<ReloadOutlined />} onClick={fetchData} />
            <Button
              type="primary"
              icon={<PlusOutlined />}
              onClick={() => {
                createForm.resetFields();
                setCreateModalOpen(true);
              }}
            >
              Tạo đợt kiểm kê mới
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
          showTotal: (t) => `Tổng ${t} đợt kiểm kê`,
        }}
        onChange={(p) => setParams(prev => ({ ...prev, page: p.current || 1, pageSize: p.pageSize || 20 }))}
      />

      {/* Modal Tạo Đợt Kiểm Kê */}
      <Modal
        title="📋 Tạo Đợt Kiểm Kê Mới"
        open={createModalOpen}
        onCancel={() => setCreateModalOpen(false)}
        onOk={() => createForm.submit()}
        okText="Bắt đầu kiểm kê"
        cancelText="Hủy"
      >
        <Alert
          type="info"
          showIcon
          message="Lưu ý kiểm kê"
          description="Khi bắt đầu kiểm kê, hệ thống sẽ tự động chụp lại tồn kho hiện tại (Snapshot). Đội ngũ kho sau đó đi kiểm đếm và ghi nhận số lượng thực tế."
          style={{ marginBottom: 16 }}
        />
        <Form form={createForm} layout="vertical" onFinish={handleCreateStocktake}>
          <Form.Item
            name="warehouseId"
            label="Kho hàng cần kiểm kê"
            rules={[{ required: true, message: 'Vui lòng chọn kho kiểm kê' }]}
          >
            <Select
              placeholder="Chọn kho kiểm kê"
              options={(warehouses || []).map(w => ({ value: w.id, label: `${w.name} (${w.code})` }))}
            />
          </Form.Item>
          <Form.Item name="note" label="Ghi chú đợt kiểm kê">
            <Input.TextArea rows={2} placeholder="VD: Kiểm kê định kỳ tháng 11, kiểm tra đột xuất..." />
          </Form.Item>
        </Form>
      </Modal>

      {/* Modal Nhập Số Đếm Thực Tế */}
      <Modal
        title="📝 Ghi nhận số lượng kiểm đếm thực tế"
        open={countModalOpen}
        onCancel={() => { setCountModalOpen(false); setSelectedLineForCount(null); }}
        onOk={() => countForm.submit()}
        okText="Cập nhật số đếm"
        cancelText="Hủy"
      >
        <Form form={countForm} layout="vertical" onFinish={handleRecordCount}>
          {selectedLineForCount && (
            <Alert
              message={`Sản phẩm: ${selectedLineForCount.productSku} - ${selectedLineForCount.productName}`}
              description={`Vị trí: ${selectedLineForCount.locationCode} | Tồn hệ thống lúc chụp: ${selectedLineForCount.snapshotQuantity}`}
              style={{ marginBottom: 16 }}
            />
          )}

          {!selectedLineForCount && (
            <>
              <Form.Item name="productId" label="Sản phẩm" rules={[{ required: true }]}>
                <Select
                  showSearch
                  placeholder="Chọn sản phẩm"
                  options={(products || []).map(p => ({ value: p.id, label: `${p.sku} - ${p.name}` }))}
                />
              </Form.Item>
              <Form.Item name="locationId" label="Vị trí kho" rules={[{ required: true }]}>
                <Select
                  placeholder="Chọn vị trí ô chứa"
                  options={(locations || []).map(l => ({ value: l.id, label: `${l.code} - ${l.name}` }))}
                />
              </Form.Item>
            </>
          )}

          <Form.Item
            name="countedQuantity"
            label="Số lượng thực tế đếm được"
            rules={[{ required: true, message: 'Vui lòng nhập số đếm' }]}
          >
            <InputNumber min={0} style={{ width: '100%' }} placeholder="Nhập số thực tế đếm được" />
          </Form.Item>
        </Form>
      </Modal>

      {/* Drawer Chi tiết Phiếu Kiểm Kê */}
      <Drawer
        title={`Chi tiết đợt kiểm kê: ${detail?.documentNo || ''}`}
        width={780}
        open={drawerOpen}
        onClose={() => setDrawerOpen(false)}
      >
        {detail && (
          <>
            <Descriptions bordered size="small" column={2}>
              <Descriptions.Item label="Số phiếu">{detail.documentNo}</Descriptions.Item>
              <Descriptions.Item label="Trạng thái"><StatusTag status={detail.status} /></Descriptions.Item>
              <Descriptions.Item label="Kho kiểm">{detail.warehouseCode}</Descriptions.Item>
              <Descriptions.Item label="Ghi chú">{detail.note || '-'}</Descriptions.Item>
            </Descriptions>

            <Row gutter={16} style={{ marginTop: 16, marginBottom: 16 }}>
              <Col span={8}>
                <Card size="small">
                  <Statistic title="Tổng mặt hàng" value={detail.lines?.length || 0} />
                </Card>
              </Col>
              <Col span={8}>
                <Card size="small">
                  <Statistic
                    title="Đã kiểm đếm"
                    value={detail.lines?.filter((l: any) => l.isCounted)?.length || 0}
                    valueStyle={{ color: '#1677ff' }}
                  />
                </Card>
              </Col>
              <Col span={8}>
                <Card size="small">
                  <Statistic
                    title="Số mặt hàng chênh lệch"
                    value={detail.lines?.filter((l: any) => (l.differenceQuantity || 0) !== 0)?.length || 0}
                    valueStyle={{ color: '#cf1322' }}
                  />
                </Card>
              </Col>
            </Row>

            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 12 }}>
              <Typography.Text strong>Chi tiết dòng kiểm kê:</Typography.Text>
              <Button
                size="small"
                type="primary"
                icon={<CheckSquareOutlined />}
                onClick={() => {
                  countForm.resetFields();
                  setSelectedLineForCount(null);
                  setCountModalOpen(true);
                }}
              >
                Nhập số đếm thủ công
              </Button>
            </div>

            <Table
              size="small"
              rowKey="id"
              dataSource={detail.lines}
              pagination={false}
              columns={[
                { title: 'Sản phẩm', render: (_: any, r: any) => `${r.productSku} - ${r.productName}` },
                { title: 'Vị trí', dataIndex: 'locationCode', width: 90 },
                { title: 'Tồn hệ thống', dataIndex: 'snapshotQuantity', width: 100, render: (v: number) => v?.toLocaleString() },
                {
                  title: 'Thực tế đếm',
                  dataIndex: 'countedQuantity',
                  width: 110,
                  render: (v: number, r: any) => r.isCounted ? <b>{v?.toLocaleString()}</b> : <Tag>Chưa đếm</Tag>
                },
                {
                  title: 'Chênh lệch',
                  dataIndex: 'differenceQuantity',
                  width: 100,
                  render: (v: number) => {
                    if (v === undefined || v === null) return '-';
                    if (v === 0) return <Tag color="green">Khớp (0)</Tag>;
                    return <Tag color={v > 0 ? 'blue' : 'red'}>{v > 0 ? `+${v}` : v}</Tag>;
                  }
                },
                {
                  title: 'Đếm',
                  width: 70,
                  render: (_: any, r: any) => (
                    <Button
                      size="small"
                      icon={<EditOutlined />}
                      onClick={() => {
                        setSelectedLineForCount(r);
                        countForm.setFieldsValue({ countedQuantity: r.countedQuantity ?? r.snapshotQuantity });
                        setCountModalOpen(true);
                      }}
                    />
                  )
                }
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

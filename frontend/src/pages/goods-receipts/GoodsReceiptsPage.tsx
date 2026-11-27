import { useEffect, useState, useCallback } from 'react';
import { Table, Button, Space, Input, Select, message, Drawer, Descriptions, Divider } from 'antd';
import { PlusOutlined, ReloadOutlined, EyeOutlined } from '@ant-design/icons';
import type { GoodsReceiptSummary, GoodsReceiptDetail, WarehouseResponse } from '../../types';
import { goodsReceiptsApi, warehousesApi } from '../../api/services';
import { StatusTag, DocumentActions } from '../../components/DocumentActions';
import { useNavigate } from 'react-router-dom';

export default function GoodsReceiptsPage() {
  const [data, setData] = useState<GoodsReceiptSummary[]>([]);
  const [total, setTotal] = useState(0);
  const [loading, setLoading] = useState(false);
  const [warehouses, setWarehouses] = useState<WarehouseResponse[]>([]);
  const [params, setParams] = useState<Record<string, any>>({ page: 1, pageSize: 20 });
  const [detail, setDetail] = useState<GoodsReceiptDetail | null>(null);
  const [drawerOpen, setDrawerOpen] = useState(false);
  const navigate = useNavigate();

  const fetchData = useCallback(async () => {
    setLoading(true);
    try { const { data: res } = await goodsReceiptsApi.list(params); setData(res.items); setTotal(res.totalCount); }
    catch { message.error('Lỗi'); } finally { setLoading(false); }
  }, [params]);

  useEffect(() => { warehousesApi.list({ pageSize: 100 }).then(r => setWarehouses(r.data.items)).catch(() => {}); }, []);
  useEffect(() => { fetchData(); }, [fetchData]);

  const showDetail = async (id: number) => {
    try { const { data } = await goodsReceiptsApi.get(id); setDetail(data); setDrawerOpen(true); }
    catch { message.error('Lỗi tải chi tiết'); }
  };

  const handleAction = async (action: string, reason?: string) => {
    if (!detail) return;
    const fn = (goodsReceiptsApi as any)[action];
    if (fn) { reason ? await fn(detail.id, reason) : await fn(detail.id); }
    await showDetail(detail.id); fetchData();
  };

  const columns = [
    { title: 'Số phiếu', dataIndex: 'documentNo', width: 140 },
    { title: 'Kho', dataIndex: 'warehouseCode', width: 100 },
    { title: 'NCC', dataIndex: 'supplierName', ellipsis: true },
    { title: 'Ngày nhập', dataIndex: 'receiptDate', width: 110 },
    { title: 'Dòng', dataIndex: 'lineCount', width: 60 },
    { title: 'Tổng SL', dataIndex: 'totalQuantity', render: (v: number) => v?.toLocaleString() },
    { title: 'Trạng thái', dataIndex: 'status', render: (v: any) => <StatusTag status={v} /> },
    {
      title: 'Thao tác', width: 80, render: (_: any, r: GoodsReceiptSummary) => (
        <Button icon={<EyeOutlined />} size="small" onClick={() => showDetail(r.id)} />
      ),
    },
  ];

  return (
    <div>
      <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: 16 }}>
        <Space>
          <Input.Search placeholder="Tìm kiếm..." allowClear onSearch={(v) => setParams(p => ({ ...p, keyword: v, page: 1 }))} style={{ width: 250 }} />
          <Select placeholder="Kho" allowClear style={{ width: 150 }} onChange={(v) => setParams(p => ({ ...p, warehouseId: v, page: 1 }))}>
            {warehouses.map(w => <Select.Option key={w.id} value={w.id}>{w.code}</Select.Option>)}
          </Select>
          <Select placeholder="Trạng thái" allowClear style={{ width: 130 }} onChange={(v) => setParams(p => ({ ...p, status: v, page: 1 }))}>
            {['Draft','Submitted','Approved','Completed','Rejected','Cancelled'].map(s => <Select.Option key={s} value={s}>{s}</Select.Option>)}
          </Select>
          <Button icon={<ReloadOutlined />} onClick={fetchData} />
        </Space>
        <Button type="primary" icon={<PlusOutlined />} onClick={() => navigate('/goods-receipts/new')}>Tạo phiếu nhập</Button>
      </div>
      <Table rowKey="id" columns={columns} dataSource={data} loading={loading}
        pagination={{ current: params.page, pageSize: params.pageSize, total, showSizeChanger: true, showTotal: (t) => `Tổng ${t}` }}
        onChange={(p) => setParams(prev => ({ ...prev, page: p.current || 1, pageSize: p.pageSize || 20 }))} />

      <Drawer title={`Phiếu nhập: ${detail?.documentNo || ''}`} width={720} open={drawerOpen} onClose={() => setDrawerOpen(false)}>
        {detail && (
          <>
            <Descriptions column={2} bordered size="small">
              <Descriptions.Item label="Số phiếu">{detail.documentNo}</Descriptions.Item>
              <Descriptions.Item label="Trạng thái"><StatusTag status={detail.status} /></Descriptions.Item>
              <Descriptions.Item label="Kho">{detail.warehouseCode} - {detail.warehouseName}</Descriptions.Item>
              <Descriptions.Item label="Ngày nhập">{detail.receiptDate}</Descriptions.Item>
              <Descriptions.Item label="NCC">{detail.supplierName || '—'}</Descriptions.Item>
              <Descriptions.Item label="Mã tham chiếu">{detail.referenceNo || '—'}</Descriptions.Item>
              <Descriptions.Item label="Ghi chú" span={2}>{detail.note || '—'}</Descriptions.Item>
            </Descriptions>
            <Divider>Chi tiết hàng hoá</Divider>
            <Table rowKey="id" dataSource={detail.lines} pagination={false} size="small" columns={[
              { title: 'SKU', dataIndex: 'productSku', width: 100 },
              { title: 'Tên', dataIndex: 'productName' },
              { title: 'Vị trí', dataIndex: 'locationCode', width: 100 },
              { title: 'SL', dataIndex: 'quantity' },
              { title: 'Đơn giá', dataIndex: 'unitCost', render: (v: number) => v?.toLocaleString() || '—' },
              { title: 'Lô', dataIndex: 'batchCode' },
            ]} />
            <Divider />
            <DocumentActions actions={detail.availableActions} onAction={handleAction} />
          </>
        )}
      </Drawer>
    </div>
  );
}

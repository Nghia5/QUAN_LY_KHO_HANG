import { useEffect, useState, useCallback } from 'react';
import { Table, Button, Space, Input, Select, message, Drawer, Descriptions, Divider, Steps } from 'antd';
import { ReloadOutlined, EyeOutlined } from '@ant-design/icons';
import type { StockTransferSummary, StockTransferDetail, WarehouseResponse } from '../../types';
import { stockTransfersApi, warehousesApi } from '../../api/services';
import { StatusTag, DocumentActions } from '../../components/DocumentActions';

export default function StockTransfersPage() {
  const [data, setData] = useState<StockTransferSummary[]>([]);
  const [total, setTotal] = useState(0);
  const [loading, setLoading] = useState(false);
  const [warehouses, setWarehouses] = useState<WarehouseResponse[]>([]);
  const [params, setParams] = useState<Record<string, any>>({ page: 1, pageSize: 20 });
  const [detail, setDetail] = useState<StockTransferDetail | null>(null);
  const [drawerOpen, setDrawerOpen] = useState(false);

  const fetchData = useCallback(async () => {
    setLoading(true);
    try { const { data: res } = await stockTransfersApi.list(params); setData(res.items); setTotal(res.totalCount); }
    catch { message.error('Lỗi'); } finally { setLoading(false); }
  }, [params]);

  useEffect(() => { warehousesApi.list({ pageSize: 100 }).then(r => setWarehouses(r.data.items)).catch(() => {}); }, []);
  useEffect(() => { fetchData(); }, [fetchData]);

  const showDetail = async (id: number) => {
    try { const { data } = await stockTransfersApi.get(id); setDetail(data); setDrawerOpen(true); } catch { message.error('Lỗi'); }
  };

  const handleAction = async (action: string, reason?: string) => {
    if (!detail) return;
    const fn = (stockTransfersApi as any)[action];
    if (fn) { reason ? await fn(detail.id, reason) : await fn(detail.id); }
    await showDetail(detail.id); fetchData();
  };

  const getTransferStep = () => {
    if (!detail) return 0;
    if (detail.progress.receivedAt) return 3;
    if (detail.progress.shippedAt) return 2;
    if (detail.progress.pickedAt) return 1;
    return 0;
  };

  const columns = [
    { title: 'Số phiếu', dataIndex: 'documentNo', width: 140 },
    { title: 'Từ kho', dataIndex: 'sourceWarehouseCode', width: 100 },
    { title: 'Đến kho', dataIndex: 'destinationWarehouseCode', width: 100 },
    { title: 'Nội bộ', dataIndex: 'isInternal', render: (v: boolean) => v ? 'Nội bộ' : 'Liên kho', width: 90 },
    { title: 'Dòng', dataIndex: 'lineCount', width: 60 },
    { title: 'Tổng SL', dataIndex: 'totalQuantity', render: (v: number) => v?.toLocaleString() },
    { title: 'Trạng thái', dataIndex: 'status', render: (v: any) => <StatusTag status={v} /> },
    { title: '', width: 80, render: (_: any, r: StockTransferSummary) => <Button icon={<EyeOutlined />} size="small" onClick={() => showDetail(r.id)} /> },
  ];

  return (
    <div>
      <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: 16 }}>
        <Space>
          <Input.Search placeholder="Tìm kiếm..." allowClear onSearch={(v) => setParams(p => ({ ...p, keyword: v, page: 1 }))} style={{ width: 250 }} />
          <Select placeholder="Kho" allowClear style={{ width: 150 }} onChange={(v) => setParams(p => ({ ...p, warehouseId: v, page: 1 }))}>
            {warehouses.map(w => <Select.Option key={w.id} value={w.id}>{w.code}</Select.Option>)}
          </Select>
          <Button icon={<ReloadOutlined />} onClick={fetchData} />
        </Space>
      </div>
      <Table rowKey="id" columns={columns} dataSource={data} loading={loading}
        pagination={{ current: params.page, pageSize: params.pageSize, total, showSizeChanger: true, showTotal: (t) => `Tổng ${t}` }}
        onChange={(p) => setParams(prev => ({ ...prev, page: p.current || 1, pageSize: p.pageSize || 20 }))} />

      <Drawer title={`Phiếu chuyển: ${detail?.documentNo || ''}`} width={720} open={drawerOpen} onClose={() => setDrawerOpen(false)}>
        {detail && (
          <>
            <Steps current={getTransferStep()} size="small" items={[
              { title: 'Lấy hàng', description: detail.progress.pickedAt?.slice(0, 10) },
              { title: 'Vận chuyển', description: detail.progress.shippedAt?.slice(0, 10) },
              { title: 'Nhận hàng', description: detail.progress.receivedAt?.slice(0, 10) },
            ]} style={{ marginBottom: 16 }} />
            <Descriptions column={2} bordered size="small">
              <Descriptions.Item label="Số phiếu">{detail.documentNo}</Descriptions.Item>
              <Descriptions.Item label="Trạng thái"><StatusTag status={detail.status} /></Descriptions.Item>
              <Descriptions.Item label="Từ kho">{detail.sourceWarehouseCode}</Descriptions.Item>
              <Descriptions.Item label="Đến kho">{detail.destinationWarehouseCode}</Descriptions.Item>
              <Descriptions.Item label="Lý do">{detail.reason || '—'}</Descriptions.Item>
              <Descriptions.Item label="Ghi chú">{detail.note || '—'}</Descriptions.Item>
            </Descriptions>
            <Divider>Chi tiết</Divider>
            <Table rowKey="id" dataSource={detail.lines} pagination={false} size="small" columns={[
              { title: 'SKU', dataIndex: 'productSku', width: 100 },
              { title: 'Tên', dataIndex: 'productName' },
              { title: 'Từ', dataIndex: 'fromLocationCode', width: 100 },
              { title: 'Đến', dataIndex: 'toLocationCode', width: 100 },
              { title: 'SL', dataIndex: 'quantity' },
              { title: 'Khả dụng', dataIndex: 'availableAtSource' },
            ]} />
            <Divider />
            <DocumentActions actions={detail.availableActions} onAction={handleAction} />
          </>
        )}
      </Drawer>
    </div>
  );
}

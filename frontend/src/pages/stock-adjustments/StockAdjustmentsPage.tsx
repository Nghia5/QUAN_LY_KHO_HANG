import { useEffect, useState, useCallback } from 'react';
import { Table, Button, Space, Input, Select, message, Drawer, Descriptions, Divider, Tag } from 'antd';
import { ReloadOutlined, EyeOutlined } from '@ant-design/icons';
import type { StockAdjustmentSummary, StockAdjustmentDetail, WarehouseResponse } from '../../types';
import { stockAdjustmentsApi, warehousesApi } from '../../api/services';
import { StatusTag, DocumentActions } from '../../components/DocumentActions';

const reasonLabels: Record<number, string> = { 0: 'Hư hỏng', 1: 'Mất cắp', 2: 'Sửa lỗi', 3: 'Trả hàng', 4: 'Khác' };

export default function StockAdjustmentsPage() {
  const [data, setData] = useState<StockAdjustmentSummary[]>([]);
  const [total, setTotal] = useState(0);
  const [loading, setLoading] = useState(false);
  const [warehouses, setWarehouses] = useState<WarehouseResponse[]>([]);
  const [params, setParams] = useState<Record<string, any>>({ page: 1, pageSize: 20 });
  const [detail, setDetail] = useState<StockAdjustmentDetail | null>(null);
  const [drawerOpen, setDrawerOpen] = useState(false);

  const fetchData = useCallback(async () => {
    setLoading(true);
    try { const { data: res } = await stockAdjustmentsApi.list(params); setData(res.items); setTotal(res.totalCount); }
    catch { message.error('Lỗi'); } finally { setLoading(false); }
  }, [params]);

  useEffect(() => { warehousesApi.list({ pageSize: 100 }).then(r => setWarehouses(r.data.items)).catch(() => {}); }, []);
  useEffect(() => { fetchData(); }, [fetchData]);

  const showDetail = async (id: number) => {
    try { const { data } = await stockAdjustmentsApi.get(id); setDetail(data); setDrawerOpen(true); } catch { message.error('Lỗi'); }
  };

  const handleAction = async (action: string, reason?: string) => {
    if (!detail) return;
    const fn = (stockAdjustmentsApi as any)[action];
    if (fn) { reason ? await fn(detail.id, reason) : await fn(detail.id); }
    await showDetail(detail.id); fetchData();
  };

  const columns = [
    { title: 'Số phiếu', dataIndex: 'documentNo', width: 140 },
    { title: 'Kho', dataIndex: 'warehouseCode', width: 100 },
    { title: 'Lý do', dataIndex: 'reasonCode', render: (v: number) => reasonLabels[v] || v },
    { title: 'Tăng', dataIndex: 'totalIncrease', render: (v: number) => v > 0 ? <Tag color="green">+{v}</Tag> : '0' },
    { title: 'Giảm', dataIndex: 'totalDecrease', render: (v: number) => v > 0 ? <Tag color="red">-{v}</Tag> : '0' },
    { title: 'Trạng thái', dataIndex: 'status', render: (v: any) => <StatusTag status={v} /> },
    { title: '', width: 80, render: (_: any, r: StockAdjustmentSummary) => <Button icon={<EyeOutlined />} size="small" onClick={() => showDetail(r.id)} /> },
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

      <Drawer title={`Điều chỉnh: ${detail?.documentNo || ''}`} width={720} open={drawerOpen} onClose={() => setDrawerOpen(false)}>
        {detail && (
          <>
            <Descriptions column={2} bordered size="small">
              <Descriptions.Item label="Số phiếu">{detail.documentNo}</Descriptions.Item>
              <Descriptions.Item label="Trạng thái"><StatusTag status={detail.status} /></Descriptions.Item>
              <Descriptions.Item label="Kho">{detail.warehouseCode}</Descriptions.Item>
              <Descriptions.Item label="Lý do">{detail.reason}</Descriptions.Item>
              <Descriptions.Item label="Cần duyệt">{detail.requiresApproval ? 'Có' : 'Không'}</Descriptions.Item>
              <Descriptions.Item label="Ghi chú">{detail.note || '—'}</Descriptions.Item>
            </Descriptions>
            <Divider>Chi tiết</Divider>
            <Table rowKey="id" dataSource={detail.lines} pagination={false} size="small" columns={[
              { title: 'SKU', dataIndex: 'productSku', width: 100 },
              { title: 'Tên', dataIndex: 'productName' },
              { title: 'Vị trí', dataIndex: 'locationCode', width: 100 },
              { title: 'Trước', dataIndex: 'currentQuantity', width: 80 },
              { title: 'Điều chỉnh', dataIndex: 'adjustmentQuantity', width: 90, render: (v: number) => v > 0 ? `+${v}` : v },
              { title: 'Sau', dataIndex: 'expectedAfterQuantity', width: 80 },
            ]} />
            <Divider />
            <DocumentActions actions={detail.availableActions} onAction={handleAction} />
          </>
        )}
      </Drawer>
    </div>
  );
}

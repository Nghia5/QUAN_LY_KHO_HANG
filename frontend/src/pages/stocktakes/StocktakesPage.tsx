import { useEffect, useState, useCallback } from 'react';
import { Table, Button, Space, Input, Select, message, Drawer, Descriptions, Divider, Statistic, Row, Col } from 'antd';
import { ReloadOutlined, EyeOutlined } from '@ant-design/icons';
import type { StocktakeSummary, StocktakeDetail, WarehouseResponse } from '../../types';
import { stocktakesApi, warehousesApi } from '../../api/services';
import { StatusTag, DocumentActions } from '../../components/DocumentActions';

export default function StocktakesPage() {
  const [data, setData] = useState<StocktakeSummary[]>([]);
  const [total, setTotal] = useState(0);
  const [loading, setLoading] = useState(false);
  const [warehouses, setWarehouses] = useState<WarehouseResponse[]>([]);
  const [params, setParams] = useState<Record<string, any>>({ page: 1, pageSize: 20 });
  const [detail, setDetail] = useState<StocktakeDetail | null>(null);
  const [drawerOpen, setDrawerOpen] = useState(false);

  const fetchData = useCallback(async () => {
    setLoading(true);
    try { const { data: res } = await stocktakesApi.list(params); setData(res.items); setTotal(res.totalCount); }
    catch { message.error('Lỗi'); } finally { setLoading(false); }
  }, [params]);

  useEffect(() => { warehousesApi.list({ pageSize: 100 }).then(r => setWarehouses(r.data.items)).catch(() => {}); }, []);
  useEffect(() => { fetchData(); }, [fetchData]);

  const showDetail = async (id: number) => {
    try { const { data } = await stocktakesApi.get(id); setDetail(data); setDrawerOpen(true); } catch { message.error('Lỗi'); }
  };

  const handleAction = async (action: string, reason?: string) => {
    if (!detail) return;
    const fn = (stocktakesApi as any)[action];
    if (fn) { reason ? await fn(detail.id, reason) : await fn(detail.id); }
    await showDetail(detail.id); fetchData();
  };

  const columns = [
    { title: 'Số phiếu', dataIndex: 'documentNo', width: 140 },
    { title: 'Kho', dataIndex: 'warehouseCode', width: 100 },
    { title: 'Dòng', dataIndex: 'lineCount', width: 60 },
    { title: 'Đã kiểm', dataIndex: 'countedLines', width: 80 },
    { title: 'Chênh lệch', dataIndex: 'linesWithDifference', width: 90 },
    { title: 'Trạng thái', dataIndex: 'status', render: (v: any) => <StatusTag status={v} /> },
    { title: '', width: 80, render: (_: any, r: StocktakeSummary) => <Button icon={<EyeOutlined />} size="small" onClick={() => showDetail(r.id)} /> },
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

      <Drawer title={`Kiểm kê: ${detail?.documentNo || ''}`} width={800} open={drawerOpen} onClose={() => setDrawerOpen(false)}>
        {detail && (
          <>
            <Descriptions column={2} bordered size="small">
              <Descriptions.Item label="Số phiếu">{detail.documentNo}</Descriptions.Item>
              <Descriptions.Item label="Trạng thái"><StatusTag status={detail.status} /></Descriptions.Item>
              <Descriptions.Item label="Kho">{detail.warehouseCode}</Descriptions.Item>
              <Descriptions.Item label="Ghi chú">{detail.note || '—'}</Descriptions.Item>
            </Descriptions>
            <Row gutter={16} style={{ margin: '16px 0' }}>
              <Col span={6}><Statistic title="Tổng dòng" value={detail.totals.lineCount} /></Col>
              <Col span={6}><Statistic title="Đã kiểm" value={detail.totals.countedLines} /></Col>
              <Col span={6}><Statistic title="Thừa" value={detail.totals.totalSurplus} valueStyle={{ color: '#3f8600' }} /></Col>
              <Col span={6}><Statistic title="Thiếu" value={detail.totals.totalShortage} valueStyle={{ color: '#cf1322' }} /></Col>
            </Row>
            <Divider>Chi tiết kiểm kê</Divider>
            <Table rowKey="id" dataSource={detail.lines} pagination={false} size="small" scroll={{ x: 900 }} columns={[
              { title: 'SKU', dataIndex: 'productSku', width: 100 },
              { title: 'Tên', dataIndex: 'productName' },
              { title: 'Vị trí', dataIndex: 'locationCode', width: 100 },
              { title: 'Hệ thống', dataIndex: 'systemQuantity', width: 90 },
              { title: 'Thực tế', dataIndex: 'countedQuantity', width: 90, render: (v: number) => v ?? '—' },
              { title: 'Chênh lệch', dataIndex: 'difference', width: 90, render: (v: number) => v != null ? (v > 0 ? `+${v}` : v) : '—' },
            ]} />
            <Divider />
            <DocumentActions actions={detail.availableActions} onAction={handleAction} />
          </>
        )}
      </Drawer>
    </div>
  );
}

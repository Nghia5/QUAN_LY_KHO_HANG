import { useEffect, useState } from 'react';
import { Table, Input, Select, Space, Button, Tag, Tabs, App } from 'antd';
import { ReloadOutlined, WarningOutlined } from '@ant-design/icons';
import type { BatchResponse, ExpiryAlertRow, WarehouseResponse } from '../../types';
import { batchesApi, warehousesApi, extractItems } from '../../api/services';

const expiryLabels: Record<number, { text: string; color: string }> = {
  1: { text: 'Không HSD', color: 'default' }, 2: { text: 'Còn hạn', color: 'green' },
  3: { text: 'Sắp hết hạn', color: 'orange' }, 4: { text: 'Hết hạn', color: 'red' },
};

export default function BatchesPage() {
  const { message } = App.useApp();
  const [data, setData] = useState<BatchResponse[]>([]);
  const [alerts, setAlerts] = useState<ExpiryAlertRow[]>([]);
  const [total, setTotal] = useState(0);
  const [alertTotal, setAlertTotal] = useState(0);
  const [loading, setLoading] = useState(false);
  const [warehouses, setWarehouses] = useState<WarehouseResponse[]>([]);
  const [params, setParams] = useState<Record<string, any>>({ page: 1, pageSize: 20 });
  const [alertParams, setAlertParams] = useState<Record<string, any>>({ page: 1, pageSize: 20 });

  const fetchData = async () => {
    setLoading(true);
    try { const { data: res } = await batchesApi.list(params); setData(res.items); setTotal(res.totalCount); }
    catch { message.error('Lỗi'); } finally { setLoading(false); }
  };

  const fetchAlerts = async () => {
    try { const { data: res } = await batchesApi.expiryAlerts(alertParams); setAlerts(res.items); setAlertTotal(res.totalCount); } catch {}
  };

  useEffect(() => { warehousesApi.list({ pageSize: 100 }).then(r => setWarehouses(extractItems(r.data))).catch(() => setWarehouses([])); }, []);
  useEffect(() => { fetchData(); }, [params]);
  useEffect(() => { fetchAlerts(); }, [alertParams]);

  const batchColumns = [
    { title: 'Mã lô', dataIndex: 'batchCode', width: 120 },
    { title: 'SKU', dataIndex: 'productSku', width: 100 },
    { title: 'Sản phẩm', dataIndex: 'productName' },
    { title: 'SL', dataIndex: 'quantity' },
    { title: 'Ngày SX', dataIndex: 'manufactureDate' },
    { title: 'HSD', dataIndex: 'expiryDate' },
    { title: 'Còn (ngày)', dataIndex: 'daysToExpiry', render: (v: number) => v ?? '—' },
    { title: 'Trạng thái', dataIndex: 'expiryStatus', render: (v: number) => { const e = expiryLabels[v]; return <Tag color={e?.color}>{e?.text}</Tag>; } },
  ];

  const alertColumns = [
    { title: 'Mã lô', dataIndex: 'batchCode', width: 120 },
    { title: 'SKU', dataIndex: 'productSku', width: 100 },
    { title: 'Sản phẩm', dataIndex: 'productName' },
    { title: 'Kho', dataIndex: 'warehouseCode', width: 80 },
    { title: 'Vị trí', dataIndex: 'locationCode', width: 100 },
    { title: 'SL', dataIndex: 'quantity' },
    { title: 'HSD', dataIndex: 'expiryDate' },
    { title: 'Còn (ngày)', dataIndex: 'daysToExpiry', render: (v: number) => <Tag color={v <= 0 ? 'red' : 'orange'}>{v} ngày</Tag> },
  ];

  return (
    <Tabs defaultActiveKey="batches" items={[
      {
        key: 'batches', label: 'Danh sách lô',
        children: (
          <div>
            <Space style={{ marginBottom: 16 }}>
              <Input.Search placeholder="Tìm kiếm..." allowClear onSearch={(v) => setParams(p => ({ ...p, keyword: v, page: 1 }))} style={{ width: 250 }} />
              <Button icon={<ReloadOutlined />} onClick={fetchData} />
            </Space>
            <Table rowKey="id" columns={batchColumns} dataSource={data} loading={loading}
              pagination={{ current: params.page, pageSize: params.pageSize, total, showSizeChanger: true, showTotal: (t) => `Tổng ${t}` }}
              onChange={(p) => setParams(prev => ({ ...prev, page: p.current || 1, pageSize: p.pageSize || 20 }))} />
          </div>
        ),
      },
      {
        key: 'alerts', label: <><WarningOutlined /> Cảnh báo hạn dùng</>,
        children: (
          <div>
            <Space style={{ marginBottom: 16 }}>
              <Select placeholder="Kho" allowClear style={{ width: 150 }} onChange={(v) => setAlertParams(p => ({ ...p, warehouseId: v, page: 1 }))}>
                {warehouses.map(w => <Select.Option key={w.id} value={w.id}>{w.code}</Select.Option>)}
              </Select>
              <Button icon={<ReloadOutlined />} onClick={fetchAlerts} />
            </Space>
            <Table rowKey={(r) => `${r.batchId}-${r.locationId}`} columns={alertColumns} dataSource={alerts}
              pagination={{ current: alertParams.page, pageSize: alertParams.pageSize, total: alertTotal, showSizeChanger: true }}
              onChange={(p) => setAlertParams(prev => ({ ...prev, page: p.current || 1, pageSize: p.pageSize || 20 }))} />
          </div>
        ),
      },
    ]} />
  );
}

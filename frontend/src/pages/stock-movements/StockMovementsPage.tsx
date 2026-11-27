import { useEffect, useState } from 'react';
import { Table, Input, Select, Space, Button, Tag, message } from 'antd';
import { ReloadOutlined } from '@ant-design/icons';
import type { StockMovementResponse, WarehouseResponse } from '../../types';
import { stockMovementsApi, warehousesApi, extractItems } from '../../api/services';

const movementTypeLabels: Record<number, { text: string; color: string }> = {
  1: { text: 'Nhập', color: 'green' }, 2: { text: 'Xuất', color: 'red' },
  3: { text: 'Chuyển ra', color: 'orange' }, 4: { text: 'Chuyển vào', color: 'blue' },
  5: { text: 'Điều chỉnh', color: 'purple' },
};

export default function StockMovementsPage() {
  const [data, setData] = useState<StockMovementResponse[]>([]);
  const [total, setTotal] = useState(0);
  const [loading, setLoading] = useState(false);
  const [warehouses, setWarehouses] = useState<WarehouseResponse[]>([]);
  const [params, setParams] = useState<Record<string, any>>({ page: 1, pageSize: 20 });

  const fetchData = async () => {
    setLoading(true);
    try { const { data: res } = await stockMovementsApi.list(params); setData(res.items); setTotal(res.totalCount); }
    catch { message.error('Lỗi'); } finally { setLoading(false); }
  };

  useEffect(() => { warehousesApi.list({ pageSize: 100 }).then(r => setWarehouses(extractItems(r.data))).catch(() => setWarehouses([])); }, []);
  useEffect(() => { fetchData(); }, [params]);

  const columns = [
    { title: 'ID', dataIndex: 'id', width: 60 },
    { title: 'SKU', dataIndex: 'productSku', width: 100 },
    { title: 'Sản phẩm', dataIndex: 'productName', ellipsis: true },
    { title: 'Kho', dataIndex: 'warehouseCode', width: 80 },
    { title: 'Vị trí', dataIndex: 'locationCode', width: 100 },
    { title: 'Loại', dataIndex: 'movementType', width: 100, render: (v: number) => { const m = movementTypeLabels[v]; return <Tag color={m?.color}>{m?.text || v}</Tag>; } },
    { title: 'SL', dataIndex: 'quantity', render: (v: number) => v?.toLocaleString() },
    { title: 'Trước', dataIndex: 'quantityBefore' },
    { title: 'Sau', dataIndex: 'quantityAfter' },
    { title: 'Tham chiếu', dataIndex: 'referenceNo' },
    { title: 'Thời gian', dataIndex: 'createdAt', width: 150, render: (v: string) => v?.slice(0, 19).replace('T', ' ') },
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
      <Table rowKey="id" columns={columns} dataSource={data} loading={loading} scroll={{ x: 1200 }}
        pagination={{ current: params.page, pageSize: params.pageSize, total, showSizeChanger: true, showTotal: (t) => `Tổng ${t}` }}
        onChange={(p) => setParams(prev => ({ ...prev, page: p.current || 1, pageSize: p.pageSize || 20 }))} />
    </div>
  );
}

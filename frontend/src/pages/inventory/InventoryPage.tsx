import { useEffect, useState } from 'react';
import { Table, Tag, Input, Space, Button, Select, message } from 'antd';
import { ReloadOutlined } from '@ant-design/icons';
import type { InventoryResponse, WarehouseResponse } from '../../types';
import { inventoryApi, warehousesApi } from '../../api/services';

const stockStatusMap: Record<number, { text: string; color: string }> = {
  1: { text: 'Đủ hàng', color: 'green' },
  2: { text: 'Tồn thấp', color: 'orange' },
  3: { text: 'Hết hàng', color: 'red' },
};

export default function InventoryPage() {
  const [data, setData] = useState<InventoryResponse[]>([]);
  const [total, setTotal] = useState(0);
  const [loading, setLoading] = useState(false);
  const [warehouses, setWarehouses] = useState<WarehouseResponse[]>([]);
  const [params, setParams] = useState<Record<string, any>>({ page: 1, pageSize: 20 });

  const fetchData = async () => {
    setLoading(true);
    try { const { data: res } = await inventoryApi.list(params); setData(res.items); setTotal(res.totalCount); }
    catch { message.error('Lỗi'); } finally { setLoading(false); }
  };

  useEffect(() => { warehousesApi.list({ pageSize: 100 }).then(r => setWarehouses(r.data.items)).catch(() => {}); }, []);
  useEffect(() => { fetchData(); }, [params]);

  const columns = [
    { title: 'SKU', dataIndex: 'productSku', width: 120 },
    { title: 'Sản phẩm', dataIndex: 'productName' },
    { title: 'ĐVT', dataIndex: 'unitOfMeasure', width: 70 },
    { title: 'Kho', dataIndex: 'warehouseCode', width: 100 },
    { title: 'Vị trí', dataIndex: 'locationCode', width: 120 },
    { title: 'Tồn kho', dataIndex: 'quantity', render: (v: number) => v?.toLocaleString(), sorter: true },
    { title: 'Đã đặt', dataIndex: 'reservedQuantity', render: (v: number) => v?.toLocaleString() },
    { title: 'Khả dụng', dataIndex: 'availableQuantity', render: (v: number) => v?.toLocaleString() },
    { title: 'Tối thiểu', dataIndex: 'minQuantity', render: (v: number) => v?.toLocaleString() },
    { title: 'Trạng thái', dataIndex: 'stockStatus', render: (v: number) => { const s = stockStatusMap[v]; return <Tag color={s?.color}>{s?.text}</Tag>; } },
  ];

  return (
    <div>
      <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: 16 }}>
        <Space>
          <Input.Search placeholder="Tìm kiếm..." allowClear onSearch={(v) => setParams(p => ({ ...p, keyword: v, page: 1 }))} style={{ width: 250 }} />
          <Select placeholder="Kho" allowClear style={{ width: 150 }} onChange={(v) => setParams(p => ({ ...p, warehouseId: v, page: 1 }))}>
            {warehouses.map(w => <Select.Option key={w.id} value={w.id}>{w.code} - {w.name}</Select.Option>)}
          </Select>
          <Select placeholder="Trạng thái" allowClear style={{ width: 130 }} onChange={(v) => setParams(p => ({ ...p, stockStatus: v, page: 1 }))}>
            <Select.Option value={1}>Đủ hàng</Select.Option>
            <Select.Option value={2}>Tồn thấp</Select.Option>
            <Select.Option value={3}>Hết hàng</Select.Option>
          </Select>
          <Button icon={<ReloadOutlined />} onClick={fetchData} />
        </Space>
      </div>
      <Table rowKey="id" columns={columns} dataSource={data} loading={loading}
        pagination={{ current: params.page, pageSize: params.pageSize, total, showSizeChanger: true, showTotal: (t) => `Tổng ${t}` }}
        onChange={(p) => setParams(prev => ({ ...prev, page: p.current || 1, pageSize: p.pageSize || 20 }))} />
    </div>
  );
}

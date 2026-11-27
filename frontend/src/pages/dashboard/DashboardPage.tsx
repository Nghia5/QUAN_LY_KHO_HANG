import { Card, Col, Row, Statistic } from 'antd';
import { ShopOutlined, InboxOutlined, SwapOutlined, WarningOutlined } from '@ant-design/icons';
import { useEffect, useState } from 'react';
import { inventoryApi, warehousesApi } from '../../api/services';

export default function DashboardPage() {
  const [stats, setStats] = useState({ warehouses: 0, products: 0, lowStock: 0, outOfStock: 0 });

  useEffect(() => {
    Promise.all([
      warehousesApi.list({ pageSize: 1 }),
      inventoryApi.list({ pageSize: 1 }),
      inventoryApi.list({ pageSize: 1, stockStatus: 2 }),
      inventoryApi.list({ pageSize: 1, stockStatus: 3 }),
    ]).then(([w, i, low, out]) => {
      setStats({
        warehouses: w.data.totalCount,
        products: i.data.totalCount,
        lowStock: low.data.totalCount,
        outOfStock: out.data.totalCount,
      });
    }).catch(() => {});
  }, []);

  return (
    <div>
      <h2>Dashboard</h2>
      <Row gutter={16}>
        <Col span={6}><Card><Statistic title="Kho hàng" value={stats.warehouses} prefix={<ShopOutlined />} /></Card></Col>
        <Col span={6}><Card><Statistic title="Dòng tồn kho" value={stats.products} prefix={<InboxOutlined />} /></Card></Col>
        <Col span={6}><Card><Statistic title="Tồn kho thấp" value={stats.lowStock} prefix={<WarningOutlined />} valueStyle={{ color: '#faad14' }} /></Card></Col>
        <Col span={6}><Card><Statistic title="Hết hàng" value={stats.outOfStock} prefix={<SwapOutlined />} valueStyle={{ color: '#ff4d4f' }} /></Card></Col>
      </Row>
    </div>
  );
}

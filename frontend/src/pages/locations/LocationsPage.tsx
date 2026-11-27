import { useEffect, useState } from 'react';
import {
  Card, Table, Select, Button, Space, Tag, Modal, Form, Input,
  message, Popconfirm, Tree, Row, Col, Alert
} from 'antd';
import {
  PlusOutlined, ReloadOutlined, DeleteOutlined
} from '@ant-design/icons';
import type { WarehouseResponse, LocationResponse, LocationTreeNode, LocationType } from '../../types';
import { warehousesApi, locationsApi } from '../../api/services';

const LOCATION_TYPES: { value: LocationType; label: string; color: string; level: number }[] = [
  { value: 'Zone', label: 'Zone (Khu vực)', color: 'blue', level: 1 },
  { value: 'Rack', label: 'Rack (Dãy kệ)', color: 'cyan', level: 2 },
  { value: 'Shelf', label: 'Shelf (Tầng kệ)', color: 'purple', level: 3 },
  { value: 'Bin', label: 'Bin (Ô chứa hàng)', color: 'green', level: 4 },
];

export default function LocationsPage() {
  const [warehouses, setWarehouses] = useState<WarehouseResponse[]>([]);
  const [selectedWarehouseId, setSelectedWarehouseId] = useState<number | null>(null);

  const [locations, setLocations] = useState<LocationResponse[]>([]);
  const [treeData, setTreeData] = useState<LocationTreeNode[]>([]);
  const [loading, setLoading] = useState(false);

  // Modal create
  const [modalOpen, setModalOpen] = useState(false);
  const [form] = Form.useForm();
  const [selectedParentType, setSelectedParentType] = useState<LocationType>('Zone');

  // Load danh sách kho
  useEffect(() => {
    const fetchWarehouses = async () => {
      try {
        const { data } = await warehousesApi.list({ page: 1, pageSize: 100 });
        setWarehouses(data.items);
        if (data.items.length > 0) {
          setSelectedWarehouseId(data.items[0].id);
        }
      } catch {
        message.error('Không thể tải danh sách kho');
      }
    };
    fetchWarehouses();
  }, []);

  // Load locations và tree khi kho thay đổi
  const fetchLocations = async () => {
    if (!selectedWarehouseId) return;
    setLoading(true);
    try {
      const [listRes, treeRes] = await Promise.all([
        locationsApi.list(selectedWarehouseId, { page: 1, pageSize: 200 }),
        locationsApi.tree(selectedWarehouseId),
      ]);
      setLocations(listRes.data.items);
      setTreeData(treeRes.data);
    } catch {
      message.error('Lỗi khi tải danh sách vị trí kho');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchLocations();
  }, [selectedWarehouseId]);

  const handleCreateLocation = async (values: any) => {
    if (!selectedWarehouseId) return;
    const typeObj = LOCATION_TYPES.find(t => t.value === values.type);
    const typeInt = typeObj ? typeObj.level : 1;
    try {
      await locationsApi.create(selectedWarehouseId, {
        segmentCode: values.segmentCode,
        name: values.name,
        type: typeInt,
        parentId: values.parentId || undefined,
        description: values.description,
      });
      message.success('Thêm vị trí kho thành công!');
      setModalOpen(false);
      form.resetFields();
      fetchLocations();
    } catch (err: any) {
      message.error(err.response?.data?.message || 'Không thể tạo vị trí kho');
    }
  };

  const handleDelete = async (id: number) => {
    if (!selectedWarehouseId) return;
    try {
      await locationsApi.delete(selectedWarehouseId, id);
      message.success('Đã xóa vị trí kho');
      fetchLocations();
    } catch (err: any) {
      message.error(err.response?.data?.message || 'Không thể xóa vị trí (có thể đang có hàng tồn)');
    }
  };

  // Convert tree data sang format của Ant Design Tree
  const formatAntTree = (nodes: LocationTreeNode[]): any[] => {
    return nodes.map(n => ({
      title: (
        <span>
          <b>{n.code}</b> - {n.name} <Tag color={LOCATION_TYPES.find(t => t.value === n.type)?.color}>{n.type}</Tag>
        </span>
      ),
      key: n.id,
      children: n.children && n.children.length > 0 ? formatAntTree(n.children) : undefined,
    }));
  };

  const getParentTypeNeeded = (currentType: LocationType): LocationType | null => {
    if (currentType === 'Rack') return 'Zone';
    if (currentType === 'Shelf') return 'Rack';
    if (currentType === 'Bin') return 'Shelf';
    return null;
  };

  const columns = [
    { title: 'Mã vị trí', dataIndex: 'code', render: (v: string) => <b>{v}</b> },
    { title: 'Tên vị trí', dataIndex: 'name' },
    {
      title: 'Cấp độ phân tầng',
      dataIndex: 'type',
      render: (type: LocationType) => {
        const item = LOCATION_TYPES.find(t => t.value === type);
        return <Tag color={item?.color}>{item?.label || type}</Tag>;
      }
    },
    { title: 'Mã đoạn (Segment)', dataIndex: 'segmentCode' },
    {
      title: 'Trạng thái',
      dataIndex: 'isActive',
      render: (v: boolean) => <Tag color={v ? 'green' : 'red'}>{v ? 'Sẵn sàng' : 'Khóa'}</Tag>
    },
    {
      title: 'Thao tác',
      render: (_: any, r: LocationResponse) => (
        <Space size="small">
          <Popconfirm
            title="Xác nhận xóa vị trí này?"
            okText="Xóa"
            cancelText="Hủy"
            onConfirm={() => handleDelete(r.id)}
          >
            <Button size="small" danger icon={<DeleteOutlined />} />
          </Popconfirm>
        </Space>
      )
    }
  ];

  return (
    <div>
      <Card
        title="📍 Cấu trúc Vị trí kho (Zone > Rack > Shelf > Bin)"
        extra={
          <Space>
            <Select
              style={{ width: 250 }}
              placeholder="Chọn kho hàng"
              value={selectedWarehouseId}
              onChange={(val) => setSelectedWarehouseId(val)}
              options={warehouses.map(w => ({ value: w.id, label: `🏭 ${w.name} (${w.code})` }))}
            />
            <Button icon={<ReloadOutlined />} onClick={fetchLocations} />
            <Button
              type="primary"
              icon={<PlusOutlined />}
              disabled={!selectedWarehouseId}
              onClick={() => {
                form.resetFields();
                form.setFieldsValue({ type: 'Zone' });
                setSelectedParentType('Zone');
                setModalOpen(true);
              }}
            >
              Thêm vị trí mới
            </Button>
          </Space>
        }
        style={{ marginBottom: 16 }}
      >
        <Alert
          type="info"
          showIcon
          message="Phân cấp vị trí kho chuẩn WMS:"
          description="Kho hàng > Zone (Khu vực: vd Khu mát, Khu pallet) > Rack (Dãy kệ hàng) > Shelf (Tầng kệ) > Bin (Ô chứa hàng cuối cùng). Mỗi tầng chỉ trỏ tới tầng cha trực tiếp phía trên."
        />
      </Card>

      <Row gutter={[16, 16]}>
        <Col span={8}>
          <Card title="🌳 Sơ đồ Cây vị trí kho" style={{ height: '100%', minHeight: 450 }}>
            {treeData.length > 0 ? (
              <Tree
                showLine
                defaultExpandAll
                treeData={formatAntTree(treeData)}
              />
            ) : (
              <div style={{ textAlign: 'center', color: '#999', marginTop: 40 }}>
                Chưa có cấu trúc vị trí nào trong kho này
              </div>
            )}
          </Card>
        </Col>

        <Col span={16}>
          <Card title="📋 Danh sách các vị trí">
            <Table
              rowKey="id"
              columns={columns}
              dataSource={locations}
              loading={loading}
              pagination={{ pageSize: 15 }}
            />
          </Card>
        </Col>
      </Row>

      {/* Modal tạo vị trí kho */}
      <Modal
        title="➕ Thêm vị trí kho mới"
        open={modalOpen}
        onCancel={() => setModalOpen(false)}
        onOk={() => form.submit()}
        okText="Tạo vị trí"
        cancelText="Hủy"
      >
        <Form form={form} layout="vertical" onFinish={handleCreateLocation}>
          <Form.Item
            name="type"
            label="Loại cấp bậc vị trí"
            rules={[{ required: true }]}
          >
            <Select
              options={LOCATION_TYPES.map(t => ({ value: t.value, label: t.label }))}
              onChange={(val: LocationType) => {
                setSelectedParentType(val);
                form.setFieldValue('parentId', undefined);
              }}
            />
          </Form.Item>

          {/* Nếu chọn Rack, Shelf, Bin thì chọn vị trí cha trực tiếp */}
          {getParentTypeNeeded(selectedParentType) && (
            <Form.Item
              name="parentId"
              label={`Vị trí cha (${LOCATION_TYPES.find(t => t.value === getParentTypeNeeded(selectedParentType))?.label})`}
              rules={[{ required: true, message: 'Bắt buộc chọn vị trí cấp trên' }]}
            >
              <Select
                placeholder="Chọn vị trí cha cấp trên"
                options={locations
                  .filter(l => l.type === getParentTypeNeeded(selectedParentType))
                  .map(l => ({ value: l.id, label: `${l.code} - ${l.name}` }))}
              />
            </Form.Item>
          )}

          <Form.Item
            name="segmentCode"
            label="Mã phân đoạn (Segment Code)"
            rules={[{ required: true, message: 'Vui lòng nhập mã phân đoạn' }]}
          >
            <Input placeholder="VD: A, 01, S1, B1" />
          </Form.Item>

          <Form.Item
            name="name"
            label="Tên hiển thị vị trí"
            rules={[{ required: true, message: 'Vui lòng nhập tên vị trí' }]}
          >
            <Input placeholder="VD: Khu vực thực phẩm, Kệ tầng 2..." />
          </Form.Item>

          <Form.Item name="description" label="Ghi chú thêm">
            <Input.TextArea rows={2} placeholder="Mô tả sức chứa, ghi chú đặc thù..." />
          </Form.Item>
        </Form>
      </Modal>
    </div>
  );
}

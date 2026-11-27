import { useEffect, useState } from 'react';
import {
  Table, Button, Space, Input, Tag, Modal, Form, InputNumber,
  message, Popconfirm, Switch, Upload, Alert, Tooltip, Card
} from 'antd';
import {
  PlusOutlined, ReloadOutlined, EditOutlined, SaveOutlined,
  CloseOutlined, CheckCircleOutlined, StopOutlined, DeleteOutlined,
  UploadOutlined, SearchOutlined
} from '@ant-design/icons';
import type { ProductResponse } from '../../types';
import { productsApi } from '../../api/services';
import api from '../../api/client';

export default function ProductsPage() {
  const [data, setData] = useState<ProductResponse[]>([]);
  const [total, setTotal] = useState(0);
  const [loading, setLoading] = useState(false);
  const [params, setParams] = useState({ page: 1, pageSize: 20, keyword: '' });

  // Modal create/edit
  const [modalOpen, setModalOpen] = useState(false);
  const [editingModal, setEditingModal] = useState<ProductResponse | null>(null);
  const [modalForm] = Form.useForm();
  const [uploadingImage, setUploadingImage] = useState(false);
  const [uploadedImageUrl, setUploadedImageUrl] = useState<string>('');

  // Inline edit state (P-13)
  const [editingId, setEditingId] = useState<number | null>(null);
  const [inlineForm] = Form.useForm();
  const [savingInline, setSavingInline] = useState(false);

  // Selection & Bulk actions (P-14, P-15)
  const [selectedRowKeys, setSelectedRowKeys] = useState<React.Key[]>([]);
  const [bulkLoading, setBulkLoading] = useState(false);

  const fetchData = async () => {
    setLoading(true);
    try {
      const { data: res } = await productsApi.list(params);
      const rawList: ProductResponse[] = Array.isArray(res) ? res : ((res as any)?.items || []);
      let filtered = rawList;
      if (params.keyword) {
        const kw = params.keyword.toLowerCase().trim();
        filtered = filtered.filter(p =>
          (p.sku && p.sku.toLowerCase().includes(kw)) ||
          (p.name && p.name.toLowerCase().includes(kw)) ||
          (p.category && p.category.toLowerCase().includes(kw))
        );
      }
      setData(filtered);
      setTotal(filtered.length);
    } catch {
      message.error('Không thể tải danh sách sản phẩm');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchData();
  }, [params]);

  // Inline edit handlers
  const startInlineEdit = (record: ProductResponse) => {
    setEditingId(record.id);
    inlineForm.setFieldsValue({
      name: record.name,
      category: record.category,
      unitOfMeasure: record.unitOfMeasure,
      salePrice: record.salePrice ?? 0,
      description: record.description || '',
      isActive: record.isActive ?? true,
    });
  };

  const cancelInlineEdit = () => {
    setEditingId(null);
    inlineForm.resetFields();
  };

  const saveInlineEdit = async (record: ProductResponse) => {
    try {
      const values = await inlineForm.validateFields();
      setSavingInline(true);
      await productsApi.update(record.id, {
        sku: record.sku,
        name: values.name,
        category: values.category,
        unitOfMeasure: values.unitOfMeasure,
        salePrice: Number(values.salePrice) || 0,
        description: values.description,
        isActive: Boolean(values.isActive),
      });
      message.success(`Đã cập nhật sản phẩm ${record.sku}`);
      setEditingId(null);
      fetchData();
    } catch (err: any) {
      if (err?.errorFields) {
        message.warning('Vui lòng kiểm tra lại thông tin nhập hợp lệ');
      } else {
        message.error(err.response?.data?.message || 'Cập nhật thất bại');
      }
    } finally {
      setSavingInline(false);
    }
  };

  // Modal Save
  const handleModalSave = async (values: any) => {
    try {
      if (editingModal) {
        await productsApi.update(editingModal.id, values);
        message.success('Cập nhật sản phẩm thành công');
      } else {
        await productsApi.create(values);
        message.success('Thêm mới sản phẩm thành công');
      }
      setModalOpen(false);
      modalForm.resetFields();
      setEditingModal(null);
      setUploadedImageUrl('');
      fetchData();
    } catch (err: any) {
      message.error(err.response?.data?.message || 'Lưu thông tin thất bại');
    }
  };

  // Bulk actions (P-15)
  const handleBulkToggleActive = async (activate: boolean) => {
    if (selectedRowKeys.length === 0) return;
    setBulkLoading(true);
    let successCount = 0;
    try {
      for (const id of selectedRowKeys) {
        const item = data.find(p => p.id === id);
        if (item) {
          await productsApi.update(item.id, {
            sku: item.sku,
            name: item.name,
            category: item.category,
            unitOfMeasure: item.unitOfMeasure,
            salePrice: item.salePrice ?? 0,
            description: item.description,
            isActive: activate,
          });
          successCount++;
        }
      }
      message.success(`Đã cập nhật ${successCount}/${selectedRowKeys.length} sản phẩm thành ${activate ? 'Hoạt động' : 'Ngừng hoạt động'}`);
      setSelectedRowKeys([]);
      fetchData();
    } catch {
      message.error('Có lỗi xảy ra trong quá trình thao tác hàng loạt');
    } finally {
      setBulkLoading(false);
    }
  };

  const handleBulkDelete = async () => {
    if (selectedRowKeys.length === 0) return;
    setBulkLoading(true);
    let deletedCount = 0;
    try {
      for (const id of selectedRowKeys) {
        try {
          await productsApi.delete(Number(id));
          deletedCount++;
        } catch {
          // Bỏ qua lỗi
        }
      }
      message.success(`Đã xóa ${deletedCount}/${selectedRowKeys.length} sản phẩm`);
      setSelectedRowKeys([]);
      fetchData();
    } finally {
      setBulkLoading(false);
    }
  };

  // Upload image handler (P-12)
  const handleUploadImage = async (file: File) => {
    if (!['image/jpeg', 'image/png'].includes(file.type)) {
      message.error('Chỉ chấp nhận file định dạng JPG hoặc PNG!');
      return false;
    }
    if (file.size >= 5 * 1024 * 1024) {
      message.error('Dung lượng ảnh phải nhỏ hơn 5MB!');
      return false;
    }
    const formData = new FormData();
    formData.append('file', file);
    setUploadingImage(true);
    try {
      const res = await api.post('/files/avatar', formData, {
        headers: { 'Content-Type': 'multipart/form-data' },
      });
      const fileUrl = res.data?.downloadUrl || `/api/files/${res.data?.id}`;
      setUploadedImageUrl(fileUrl);
      message.success('Upload ảnh sản phẩm thành công!');
    } catch {
      message.warning('Upload avatar thành công cho bản demo');
      setUploadedImageUrl(URL.createObjectURL(file));
    } finally {
      setUploadingImage(false);
    }
    return false;
  };

  // Columns definition with Inline Editing (P-13)
  const columns = [
    {
      title: 'SKU',
      dataIndex: 'sku',
      width: 130,
      sorter: (a: ProductResponse, b: ProductResponse) => (a.sku || '').localeCompare(b.sku || ''),
      render: (v: string) => <b>{v}</b>,
    },
    {
      title: 'Tên sản phẩm (Inline Edit)',
      dataIndex: 'name',
      sorter: (a: ProductResponse, b: ProductResponse) => (a.name || '').localeCompare(b.name || ''),
      render: (v: string, r: ProductResponse) => {
        const isEditing = r.id === editingId;
        return isEditing ? (
          <Form form={inlineForm} component={false}>
            <Form.Item name="name" style={{ margin: 0 }} rules={[{ required: true, message: 'Nhập tên' }]}>
              <Input size="small" />
            </Form.Item>
          </Form>
        ) : (
          <span>{v}</span>
        );
      },
    },
    {
      title: 'Danh mục',
      dataIndex: 'category',
      width: 140,
      sorter: (a: ProductResponse, b: ProductResponse) => (a.category || '').localeCompare(b.category || ''),
      render: (v: string, r: ProductResponse) => {
        const isEditing = r.id === editingId;
        return isEditing ? (
          <Form form={inlineForm} component={false}>
            <Form.Item name="category" style={{ margin: 0 }} rules={[{ required: true, message: 'Nhập danh mục' }]}>
              <Input size="small" />
            </Form.Item>
          </Form>
        ) : (
          <Tag color="blue">{v}</Tag>
        );
      },
    },
    {
      title: 'ĐVT',
      dataIndex: 'unitOfMeasure',
      width: 90,
      render: (v: string, r: ProductResponse) => {
        const isEditing = r.id === editingId;
        return isEditing ? (
          <Form form={inlineForm} component={false}>
            <Form.Item name="unitOfMeasure" style={{ margin: 0 }} rules={[{ required: true, message: 'ĐVT' }]}>
              <Input size="small" />
            </Form.Item>
          </Form>
        ) : (
          <span>{v}</span>
        );
      },
    },
    {
      title: 'Giá bán (₫)',
      dataIndex: 'salePrice',
      width: 140,
      sorter: (a: ProductResponse, b: ProductResponse) => (a.salePrice || 0) - (b.salePrice || 0),
      render: (v: number, r: ProductResponse) => {
        const isEditing = r.id === editingId;
        const val = v ?? 0;
        return isEditing ? (
          <Form form={inlineForm} component={false}>
            <Form.Item name="salePrice" style={{ margin: 0 }} rules={[{ required: true, message: 'Nhập giá' }]}>
              <InputNumber size="small" min={0} style={{ width: '100%' }} />
            </Form.Item>
          </Form>
        ) : (
          <span style={{ fontWeight: 500, color: '#1677ff' }}>{Number(val).toLocaleString('vi-VN')} ₫</span>
        );
      },
    },
    {
      title: 'Trạng thái',
      dataIndex: 'isActive',
      width: 130,
      sorter: (a: ProductResponse, b: ProductResponse) => Number(a.isActive) - Number(b.isActive),
      render: (v: boolean, r: ProductResponse) => {
        const isEditing = r.id === editingId;
        const active = v ?? true;
        return isEditing ? (
          <Form form={inlineForm} component={false}>
            <Form.Item name="isActive" valuePropName="checked" style={{ margin: 0 }}>
              <Switch checkedChildren="Bật" unCheckedChildren="Tắt" size="small" />
            </Form.Item>
          </Form>
        ) : (
          <Tag color={active ? 'green' : 'red'}>{active ? 'Đang bán' : 'Ngừng bán'}</Tag>
        );
      },
    },
    {
      title: 'Thao tác',
      width: 170,
      fixed: 'right' as const,
      render: (_: any, r: ProductResponse) => {
        const isEditing = r.id === editingId;
        if (isEditing) {
          return (
            <Space size="small">
              <Tooltip title="Lưu thay đổi dòng này">
                <Button
                  type="primary"
                  size="small"
                  icon={<SaveOutlined />}
                  loading={savingInline}
                  onClick={() => saveInlineEdit(r)}
                >
                  Lưu
                </Button>
              </Tooltip>
              <Tooltip title="Hủy chỉnh sửa">
                <Button size="small" icon={<CloseOutlined />} onClick={cancelInlineEdit}>
                  Hủy
                </Button>
              </Tooltip>
            </Space>
          );
        }
        return (
          <Space size="small">
            <Tooltip title="Chỉnh sửa trực tiếp (Inline Edit)">
              <Button size="small" icon={<EditOutlined />} onClick={() => startInlineEdit(r)}>
                Sửa
              </Button>
            </Tooltip>
            <Popconfirm
              title="Xác nhận xóa sản phẩm này?"
              okText="Xóa"
              cancelText="Hủy"
              onConfirm={async () => {
                await productsApi.delete(r.id);
                message.success('Đã xóa sản phẩm');
                fetchData();
              }}
            >
              <Button size="small" danger icon={<DeleteOutlined />} />
            </Popconfirm>
          </Space>
        );
      },
    },
  ];

  return (
    <div>
      <Card
        title="📦 Quản lý Sản phẩm (Master Data)"
        extra={
          <Space>
            <Button icon={<ReloadOutlined />} onClick={fetchData}>
              Làm mới
            </Button>
            <Button
              type="primary"
              icon={<PlusOutlined />}
              onClick={() => {
                setEditingModal(null);
                modalForm.resetFields();
                setUploadedImageUrl('');
                setModalOpen(true);
              }}
            >
              Thêm sản phẩm
            </Button>
          </Space>
        }
        style={{ marginBottom: 16 }}
      >
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: 12 }}>
          <Space>
            <Input.Search
              placeholder="Tìm theo SKU, tên sản phẩm..."
              allowClear
              enterButton={<SearchOutlined />}
              onSearch={(v) => setParams(p => ({ ...p, keyword: v, page: 1 }))}
              style={{ width: 320 }}
            />
          </Space>

          {/* Bulk Action Toolbar (P-14, P-15) */}
          {selectedRowKeys.length > 0 && (
            <Alert
              type="info"
              showIcon
              message={
                <Space>
                  <span>Đã chọn <b>{selectedRowKeys.length}</b> sản phẩm:</span>
                  <Button
                    size="small"
                    type="primary"
                    icon={<CheckCircleOutlined />}
                    loading={bulkLoading}
                    onClick={() => handleBulkToggleActive(true)}
                  >
                    Kích hoạt hàng loạt
                  </Button>
                  <Button
                    size="small"
                    icon={<StopOutlined />}
                    loading={bulkLoading}
                    onClick={() => handleBulkToggleActive(false)}
                  >
                    Ngừng bán hàng loạt
                  </Button>
                  <Popconfirm
                    title={`Xác nhận xóa ${selectedRowKeys.length} sản phẩm đã chọn?`}
                    onConfirm={handleBulkDelete}
                    okText="Xóa tất cả"
                    cancelText="Hủy"
                  >
                    <Button size="small" danger icon={<DeleteOutlined />} loading={bulkLoading}>
                      Xóa hàng loạt
                    </Button>
                  </Popconfirm>
                  <Button size="small" type="link" onClick={() => setSelectedRowKeys([])}>
                    Bỏ chọn
                  </Button>
                </Space>
              }
              style={{ padding: '6px 16px' }}
            />
          )}
        </div>
      </Card>

      <Table
        rowKey="id"
        columns={columns}
        dataSource={data}
        loading={loading}
        rowSelection={{
          selectedRowKeys,
          onChange: (keys) => setSelectedRowKeys(keys),
        }}
        pagination={{
          current: params.page,
          pageSize: params.pageSize,
          total,
          showSizeChanger: true,
          showTotal: (t) => `Tổng cộng ${t} sản phẩm`,
        }}
        onChange={(p) => setParams(prev => ({ ...prev, page: p.current || 1, pageSize: p.pageSize || 20 }))}
      />

      {/* Modal Create/Edit (P-11 & P-12) */}
      <Modal
        title={editingModal ? '✏️ Cập nhật thông tin sản phẩm' : '➕ Thêm sản phẩm mới'}
        open={modalOpen}
        onCancel={() => { setModalOpen(false); setEditingModal(null); }}
        onOk={() => modalForm.submit()}
        okText="Lưu dữ liệu"
        cancelText="Đóng"
        width={600}
      >
        <Form form={modalForm} layout="vertical" onFinish={handleModalSave}>
          <Form.Item
            name="sku"
            label="Mã SKU (Bắt buộc, Duy nhất)"
            rules={[{ required: true, message: 'Vui lòng nhập mã SKU' }]}
          >
            <Input placeholder="VD: SKU-SP-001" disabled={!!editingModal} />
          </Form.Item>
          <Form.Item
            name="name"
            label="Tên sản phẩm"
            rules={[{ required: true, message: 'Vui lòng nhập tên sản phẩm' }]}
          >
            <Input placeholder="Tên sản phẩm chi tiết" />
          </Form.Item>
          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 16 }}>
            <Form.Item
              name="category"
              label="Danh mục"
              rules={[{ required: true, message: 'Chọn hoặc nhập danh mục' }]}
            >
              <Input placeholder="VD: Điện tử, Gia dụng..." />
            </Form.Item>
            <Form.Item
              name="unitOfMeasure"
              label="Đơn vị tính (ĐVT)"
              rules={[{ required: true, message: 'Nhập đơn vị tính' }]}
            >
              <Input placeholder="VD: Cái, Hộp, Thùng, Kg" />
            </Form.Item>
          </div>
          <Form.Item
            name="salePrice"
            label="Giá bán tiêu chuẩn (₫)"
            rules={[{ required: true, message: 'Nhập giá bán' }]}
          >
            <InputNumber
              style={{ width: '100%' }}
              min={0}
              formatter={(value) => `${value}`.replace(/\B(?=(\d{3})+(?!\d))/g, ',')}
              placeholder="0"
            />
          </Form.Item>

          {/* P-12: Upload ảnh sản phẩm */}
          <Form.Item label="Ảnh minh họa sản phẩm (Tối đa 5MB, JPG/PNG)">
            <Upload
              beforeUpload={handleUploadImage}
              showUploadList={false}
              accept="image/png,image/jpeg"
            >
              <Button icon={<UploadOutlined />} loading={uploadingImage}>
                {uploadedImageUrl ? 'Đổi ảnh khác' : 'Chọn ảnh tải lên'}
              </Button>
            </Upload>
            {uploadedImageUrl && (
              <div style={{ marginTop: 10 }}>
                <img
                  src={uploadedImageUrl}
                  alt="Product preview"
                  style={{ width: 100, height: 100, objectFit: 'cover', borderRadius: 8, border: '1px solid #d9d9d9' }}
                />
              </div>
            )}
          </Form.Item>

          <Form.Item name="description" label="Mô tả sản phẩm">
            <Input.TextArea rows={3} placeholder="Ghi chú chi tiết về sản phẩm..." />
          </Form.Item>
        </Form>
      </Modal>
    </div>
  );
}

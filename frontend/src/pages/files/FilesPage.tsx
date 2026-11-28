import { useState, useEffect, useCallback, useMemo } from 'react';
import {
  Card, Table, Button, Input, Select, Space, Tag, Popconfirm,
  Statistic, Row, Col, App, Upload, Modal, Form
} from 'antd';
import {
  UploadOutlined, DownloadOutlined, DeleteOutlined, ReloadOutlined,
  FileSearchOutlined, InboxOutlined, FileTextOutlined
} from '@ant-design/icons';
import { filesApi } from '../../api/services';
import type { FileAttachment } from '../../types';
import { formatFileSize, getFileIcon } from '../../components/DocumentAttachments';

const DOCUMENT_TYPE_LABELS: Record<string, { label: string; color: string }> = {
  Receipt: { label: 'Phiếu Nhập kho', color: 'blue' },
  Issue: { label: 'Phiếu Xuất kho', color: 'orange' },
  Transfer: { label: 'Phiếu Chuyển kho', color: 'purple' },
  Stocktake: { label: 'Phiếu Kiểm kê', color: 'cyan' },
  Adjustment: { label: 'Phiếu Điều chỉnh', color: 'gold' },
};

export default function FilesPage() {
  const { message } = App.useApp();
  const [data, setData] = useState<FileAttachment[]>([]);
  const [loading, setLoading] = useState(false);
  const [keyword, setKeyword] = useState('');
  const [typeFilter, setTypeFilter] = useState<string>('ALL');

  // Modal tải lên tệp mới
  const [uploadModalOpen, setUploadModalOpen] = useState(false);
  const [uploadForm] = Form.useForm();
  const [submittingUpload, setSubmittingUpload] = useState(false);
  const [selectedFile, setSelectedFile] = useState<File | null>(null);

  const fetchData = useCallback(async () => {
    setLoading(true);
    try {
      const { data: res } = await filesApi.listAll();
      setData(Array.isArray(res) ? res : []);
    } catch {
      message.error('Không thể tải danh sách tệp tin');
    } finally {
      setLoading(false);
    }
  }, [message]);

  useEffect(() => {
    fetchData();
  }, [fetchData]);

  const handleDownload = async (file: FileAttachment) => {
    try {
      await filesApi.downloadFile(file.id, file.fileName);
      message.success(`Đang tải về: ${file.fileName}`);
    } catch {
      message.error('Không thể tải tệp tin');
    }
  };

  const handleDelete = async (id: string) => {
    try {
      await filesApi.delete(id);
      message.success('Đã xóa tệp tin thành công');
      fetchData();
    } catch {
      message.error('Không thể xóa tệp tin');
    }
  };

  const handleManualUpload = async (values: any) => {
    if (!selectedFile) {
      message.warning('Vui lòng chọn tệp tin cần tải lên');
      return;
    }

    setSubmittingUpload(true);
    try {
      if (values.uploadType === 'avatar') {
        await filesApi.uploadAvatar(selectedFile);
      } else {
        await filesApi.uploadDocument(
          selectedFile,
          values.documentType,
          Number(values.documentId),
          values.description
        );
      }
      message.success('Tải lên tệp thành công!');
      setUploadModalOpen(false);
      uploadForm.resetFields();
      setSelectedFile(null);
      fetchData();
    } catch (err: any) {
      message.error(err.response?.data?.message || err.message || 'Không thể tải lên tệp');
    } finally {
      setSubmittingUpload(false);
    }
  };

  const filteredData = useMemo(() => {
    return data.filter(item => {
      const matchKeyword = !keyword ||
        (item.fileName && item.fileName.toLowerCase().includes(keyword.toLowerCase())) ||
        (item.description && item.description.toLowerCase().includes(keyword.toLowerCase()));
      const matchType = typeFilter === 'ALL' || item.documentType === typeFilter;
      return matchKeyword && matchType;
    });
  }, [data, keyword, typeFilter]);

  const totalBytes = useMemo(() => {
    return data.reduce((sum, item) => sum + (Number(item.size) || 0), 0);
  }, [data]);

  const columns = [
    {
      title: 'Tên tệp tin',
      dataIndex: 'fileName',
      render: (v: string) => (
        <Space>
          {getFileIcon(v)}
          <b>{v}</b>
        </Space>
      ),
    },
    {
      title: 'Chứng từ liên kết',
      dataIndex: 'documentType',
      width: 180,
      render: (v: string, r: FileAttachment) => {
        if (!v) return <Tag>Ảnh đại diện / Khác</Tag>;
        const meta = DOCUMENT_TYPE_LABELS[v];
        return (
          <Space direction="vertical" size={2}>
            <Tag color={meta?.color || 'blue'}>{meta?.label || v}</Tag>
            {r.documentId && <span style={{ fontSize: 12, color: '#666' }}>ID Phiếu: #{r.documentId}</span>}
          </Space>
        );
      },
    },
    {
      title: 'Kích thước',
      dataIndex: 'size',
      width: 120,
      render: (v: number) => formatFileSize(v),
    },
    {
      title: 'Ngày tải lên',
      dataIndex: 'uploadedAt',
      width: 170,
      render: (v: string) => v ? new Date(v).toLocaleString('vi-VN') : '-',
    },
    {
      title: 'Mô tả / Ghi chú',
      dataIndex: 'description',
      ellipsis: true,
      render: (v: string) => v || '-',
    },
    {
      title: 'Thao tác',
      width: 150,
      fixed: 'right' as const,
      render: (_: any, r: FileAttachment) => (
        <Space>
          <Button
            type="primary"
            ghost
            size="small"
            icon={<DownloadOutlined />}
            onClick={() => handleDownload(r)}
          >
            Tải về
          </Button>
          <Popconfirm
            title="Xác nhận xóa tệp tin này?"
            onConfirm={() => handleDelete(r.id)}
            okText="Xóa"
            cancelText="Hủy"
            okButtonProps={{ danger: true }}
          >
            <Button type="text" danger size="small" icon={<DeleteOutlined />} />
          </Popconfirm>
        </Space>
      ),
    },
  ];

  return (
    <div>
      <Row gutter={[16, 16]} style={{ marginBottom: 16 }}>
        <Col xs={24} sm={12} md={8}>
          <Card>
            <Statistic
              title="Tổng số tệp tin đã lưu trữ"
              value={data.length}
              suffix="tệp"
              prefix={<FileTextOutlined style={{ color: '#1677ff' }} />}
            />
          </Card>
        </Col>
        <Col xs={24} sm={12} md={8}>
          <Card>
            <Statistic
              title="Tổng dung lượng lưu trữ"
              value={formatFileSize(totalBytes)}
              prefix={<InboxOutlined style={{ color: '#52c41a' }} />}
            />
          </Card>
        </Col>
      </Row>

      <Card
        title="📁 Quản lý Tệp tin & Chứng từ lưu trữ (File Management)"
        extra={
          <Space>
            <Button icon={<ReloadOutlined />} onClick={fetchData}>Làm mới</Button>
            <Button
              type="primary"
              icon={<UploadOutlined />}
              onClick={() => {
                uploadForm.resetFields();
                setSelectedFile(null);
                setUploadModalOpen(true);
              }}
            >
              Tải tệp lên
            </Button>
          </Space>
        }
        style={{ marginBottom: 16 }}
      >
        <Space wrap>
          <Input.Search
            placeholder="Tìm theo tên tệp hoặc ghi chú..."
            allowClear
            style={{ width: 280 }}
            onSearch={setKeyword}
            onChange={e => !e.target.value && setKeyword('')}
          />
          <Select
            value={typeFilter}
            onChange={setTypeFilter}
            style={{ width: 200 }}
            options={[
              { value: 'ALL', label: 'Tất cả loại chứng từ' },
              { value: 'Receipt', label: 'Phiếu Nhập kho' },
              { value: 'Issue', label: 'Phiếu Xuất kho' },
              { value: 'Transfer', label: 'Phiếu Chuyển kho' },
              { value: 'Stocktake', label: 'Phiếu Kiểm kê' },
              { value: 'Adjustment', label: 'Phiếu Điều chỉnh' },
            ]}
          />
        </Space>
      </Card>

      <Table
        rowKey="id"
        columns={columns}
        dataSource={filteredData}
        loading={loading}
        pagination={{ pageSize: 15, showTotal: t => `Tổng ${t} tệp tin` }}
      />

      {/* Modal Tải Lên Tệp Mới */}
      <Modal
        title="📤 Tải lên Tệp tin mới"
        open={uploadModalOpen}
        onCancel={() => setUploadModalOpen(false)}
        onOk={() => uploadForm.submit()}
        confirmLoading={submittingUpload}
        okText="Tải lên"
        cancelText="Hủy"
      >
        <Form
          form={uploadForm}
          layout="vertical"
          initialValues={{ uploadType: 'document', documentType: 'Receipt' }}
          onFinish={handleManualUpload}
        >
          <Form.Item
            name="uploadType"
            label="Loại tệp tải lên"
            rules={[{ required: true }]}
          >
            <Select
              options={[
                { value: 'document', label: 'Chứng từ kho (Hóa đơn / Biên bản / Excel)' },
                { value: 'avatar', label: 'Ảnh đại diện / Ảnh cá nhân' },
              ]}
            />
          </Form.Item>

          <Form.Item noStyle shouldUpdate={(prev, curr) => prev.uploadType !== curr.uploadType}>
            {({ getFieldValue }) =>
              getFieldValue('uploadType') === 'document' ? (
                <>
                  <Form.Item
                    name="documentType"
                    label="Loại chứng từ đính kèm"
                    rules={[{ required: true }]}
                  >
                    <Select
                      options={[
                        { value: 'Receipt', label: 'Phiếu Nhập kho (Goods Receipt)' },
                        { value: 'Issue', label: 'Phiếu Xuất kho (Goods Issue)' },
                        { value: 'Transfer', label: 'Phiếu Chuyển kho (Stock Transfer)' },
                        { value: 'Stocktake', label: 'Phiếu Kiểm kê (Stocktake)' },
                        { value: 'Adjustment', label: 'Phiếu Điều chỉnh (Stock Adjustment)' },
                      ]}
                    />
                  </Form.Item>

                  <Form.Item
                    name="documentId"
                    label="Mã ID Phiếu"
                    rules={[{ required: true, message: 'Vui lòng nhập ID phiếu' }]}
                    extra="Ví dụ: Nhập 5 nếu đính kèm vào phiếu số 5"
                  >
                    <Input type="number" placeholder="Nhập ID phiếu cần đính kèm..." />
                  </Form.Item>
                </>
              ) : null
            }
          </Form.Item>

          <Form.Item name="description" label="Mô tả / Ghi chú tệp">
            <Input placeholder="Ví dụ: Hóa đơn đỏ VAT số 8896, Biên bản nghiệm thu..." />
          </Form.Item>

          <Form.Item label="Chọn tệp tin" required>
            <Upload
              beforeUpload={(file) => {
                setSelectedFile(file);
                return false;
              }}
              maxCount={1}
              onRemove={() => setSelectedFile(null)}
            >
              <Button icon={<FileSearchOutlined />}>Chọn tệp từ máy tính</Button>
            </Upload>
            {selectedFile && (
              <div style={{ marginTop: 8, fontSize: 13, color: '#1677ff' }}>
                Đã chọn: <b>{selectedFile.name}</b> ({formatFileSize(selectedFile.size)})
              </div>
            )}
          </Form.Item>
        </Form>
      </Modal>
    </div>
  );
}

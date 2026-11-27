import { useState, useEffect, useCallback } from 'react';
import {
  Card, Button, Upload, List, Typography, Space, Popconfirm,
  Tag, Empty, App, Spin
} from 'antd';
import {
  UploadOutlined, DownloadOutlined, DeleteOutlined,
  FilePdfOutlined, FileExcelOutlined, FileWordOutlined,
  FileImageOutlined, FileOutlined, PaperClipOutlined
} from '@ant-design/icons';
import { filesApi } from '../api/services';
import type { FileAttachment } from '../types';

interface Props {
  documentType: 'Receipt' | 'Issue' | 'Transfer' | 'Stocktake' | 'Adjustment';
  documentId: number;
  readonly?: boolean;
}

export function formatFileSize(bytes: number): string {
  if (!bytes || bytes === 0) return '0 B';
  const k = 1024;
  const sizes = ['B', 'KB', 'MB', 'GB'];
  const i = Math.floor(Math.log(bytes) / Math.log(k));
  return `${parseFloat((bytes / Math.pow(k, i)).toFixed(1))} ${sizes[i]}`;
}

export function getFileIcon(filename: string) {
  const ext = (filename.split('.').pop() || '').toLowerCase();
  if (ext === 'pdf') return <FilePdfOutlined style={{ color: '#ff4d4f', fontSize: 20 }} />;
  if (['xls', 'xlsx', 'csv'].includes(ext)) return <FileExcelOutlined style={{ color: '#52c41a', fontSize: 20 }} />;
  if (['doc', 'docx'].includes(ext)) return <FileWordOutlined style={{ color: '#1677ff', fontSize: 20 }} />;
  if (['jpg', 'jpeg', 'png', 'webp', 'gif'].includes(ext)) return <FileImageOutlined style={{ color: '#fa8c16', fontSize: 20 }} />;
  return <FileOutlined style={{ color: '#8c8c8c', fontSize: 20 }} />;
}

export function DocumentAttachments({ documentType, documentId, readonly = false }: Props) {
  const { message } = App.useApp();
  const [files, setFiles] = useState<FileAttachment[]>([]);
  const [loading, setLoading] = useState(false);
  const [uploading, setUploading] = useState(false);

  const fetchFiles = useCallback(async () => {
    if (!documentId) return;
    setLoading(true);
    try {
      const { data } = await filesApi.listForDocument(documentType, documentId);
      setFiles(Array.isArray(data) ? data : []);
    } catch {
      setFiles([]);
    } finally {
      setLoading(false);
    }
  }, [documentType, documentId]);

  useEffect(() => {
    fetchFiles();
  }, [fetchFiles]);

  const handleUpload = async (file: File) => {
    if (file.size > 20 * 1024 * 1024) {
      message.error('Kích thước tệp vượt quá giới hạn cho phép (tối đa 20 MB)');
      return false;
    }

    setUploading(true);
    try {
      await filesApi.uploadDocument(file, documentType, documentId);
      message.success(`Đã tải lên tệp: ${file.name}`);
      await fetchFiles();
    } catch (err: any) {
      message.error(err.response?.data?.message || err.message || 'Không thể tải lên tệp đính kèm');
    } finally {
      setUploading(false);
    }
    return false; // prevent default antd upload request
  };

  const handleDownload = async (file: FileAttachment) => {
    try {
      await filesApi.downloadFile(file.id, file.fileName);
      message.success(`Bắt đầu tải về: ${file.fileName}`);
    } catch {
      message.error('Không thể tải tệp tin');
    }
  };

  const handleDelete = async (fileId: string) => {
    try {
      await filesApi.delete(fileId);
      message.success('Đã xóa tệp đính kèm');
      await fetchFiles();
    } catch {
      message.error('Không thể xóa tệp');
    }
  };

  return (
    <Card
      size="small"
      title={
        <Space>
          <PaperClipOutlined />
          <span>Tệp đính kèm & Chứng từ gốc ({files.length})</span>
        </Space>
      }
      extra={
        !readonly && (
          <Upload
            beforeUpload={handleUpload}
            showUploadList={false}
            accept=".pdf,.xlsx,.xls,.docx,.doc,.png,.jpg,.jpeg"
          >
            <Button
              type="primary"
              ghost
              size="small"
              icon={<UploadOutlined />}
              loading={uploading}
            >
              Đính kèm tệp
            </Button>
          </Upload>
        )
      }
      style={{ marginTop: 16 }}
    >
      {loading ? (
        <div style={{ textAlign: 'center', padding: '16px 0' }}><Spin size="small" /></div>
      ) : files.length === 0 ? (
        <Empty
          image={Empty.PRESENTED_IMAGE_SIMPLE}
          description="Chưa có hóa đơn hoặc chứng từ đính kèm"
          style={{ margin: '8px 0' }}
        />
      ) : (
        <List
          size="small"
          dataSource={files}
          renderItem={item => (
            <List.Item
              actions={[
                <Button
                  key="download"
                  type="link"
                  size="small"
                  icon={<DownloadOutlined />}
                  onClick={() => handleDownload(item)}
                >
                  Tải về
                </Button>,
                !readonly && (
                  <Popconfirm
                    key="delete"
                    title="Xác nhận xóa tệp đính kèm này?"
                    onConfirm={() => handleDelete(item.id)}
                    okText="Xóa"
                    cancelText="Hủy"
                    okButtonProps={{ danger: true }}
                  >
                    <Button type="link" danger size="small" icon={<DeleteOutlined />}>
                      Xóa
                    </Button>
                  </Popconfirm>
                ),
              ]}
            >
              <List.Item.Meta
                avatar={getFileIcon(item.fileName)}
                title={
                  <Space>
                    <Typography.Text strong ellipsis style={{ maxWidth: 300 }}>
                      {item.fileName}
                    </Typography.Text>
                    <Tag>{formatFileSize(item.size)}</Tag>
                  </Space>
                }
                description={
                  <span style={{ fontSize: 12, color: '#8c8c8c' }}>
                    Tải lên lúc: {new Date(item.uploadedAt).toLocaleString('vi-VN')}
                    {item.description ? ` • ${item.description}` : ''}
                  </span>
                }
              />
            </List.Item>
          )}
        />
      )}
    </Card>
  );
}

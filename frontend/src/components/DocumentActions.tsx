import { Button, Space, Popconfirm, Input, Tag } from 'antd';
import { useState } from 'react';
import {
  SendOutlined,
  CheckOutlined,
  CloseOutlined,
  CheckCircleOutlined,
  UndoOutlined,
  StopOutlined,
  DeleteOutlined,
  EditOutlined,
  InboxOutlined,
  CarOutlined,
  CameraOutlined,
  FormOutlined,
  SwapOutlined,
} from '@ant-design/icons';
import type { DocumentAction, DocumentStatus } from '../types';

const statusColors: Record<string, string> = {
  Draft: 'default',
  PendingApproval: 'processing',
  Submitted: 'processing',
  Approved: 'blue',
  Rejected: 'error',
  Completed: 'success',
  Cancelled: 'default',
  InProgress: 'warning',
  Adjusted: 'cyan',
  Picked: 'gold',
  InTransit: 'orange',
  Received: 'geekblue',
};

const statusLabels: Record<string, string> = {
  Draft: 'Nháp',
  PendingApproval: 'Chờ duyệt',
  Submitted: 'Chờ duyệt',
  Approved: 'Đã duyệt',
  Rejected: 'Từ chối',
  Completed: 'Hoàn thành',
  Cancelled: 'Đã huỷ',
  InProgress: 'Đang kiểm kê',
  Adjusted: 'Đã cân bằng',
  Picked: 'Đã lấy hàng',
  InTransit: 'Đang vận chuyển',
  Received: 'Đã nhận hàng',
};

export function StatusTag({ status }: { status: DocumentStatus | string }) {
  const color = statusColors[status] || 'default';
  const label = statusLabels[status] || status;
  return <Tag color={color}>{label}</Tag>;
}

interface ActionMeta {
  label: string;
  type?: 'primary' | 'default' | 'dashed' | 'link' | 'text';
  danger?: boolean;
  needsReason?: boolean;
  icon?: React.ReactNode;
  style?: React.CSSProperties;
  confirmTitle: string;
}

const ACTION_CONFIGS: Record<string, ActionMeta> = {
  submit: {
    label: 'Gửi duyệt',
    type: 'primary',
    icon: <SendOutlined />,
    confirmTitle: 'Xác nhận gửi duyệt phiếu này?',
  },
  approve: {
    label: 'Phê duyệt',
    type: 'primary',
    icon: <CheckOutlined />,
    style: { backgroundColor: '#52c41a', borderColor: '#52c41a' },
    confirmTitle: 'Xác nhận phê duyệt phiếu này?',
  },
  reject: {
    label: 'Từ chối',
    danger: true,
    icon: <CloseOutlined />,
    needsReason: true,
    confirmTitle: 'Lý do từ chối phiếu:',
  },
  complete: {
    label: 'Hoàn tất',
    type: 'primary',
    icon: <CheckCircleOutlined />,
    style: { backgroundColor: '#722ed1', borderColor: '#722ed1' },
    confirmTitle: 'Xác nhận hoàn tất phiếu? Tồn kho thực tế sẽ được cập nhật chính thức.',
  },
  reopen: {
    label: 'Mở lại (Nháp)',
    type: 'default',
    icon: <UndoOutlined />,
    confirmTitle: 'Mở lại phiếu về trạng thái Nháp để tiếp tục chỉnh sửa?',
  },
  cancel: {
    label: 'Hủy phiếu',
    danger: true,
    icon: <StopOutlined />,
    needsReason: true,
    confirmTitle: 'Lý do hủy phiếu:',
  },
  delete: {
    label: 'Xóa phiếu',
    danger: true,
    icon: <DeleteOutlined />,
    confirmTitle: 'Bạn có chắc chắn muốn xóa phiếu nháp này? Thao tác không thể hoàn tác.',
  },
  edit: {
    label: 'Chỉnh sửa',
    type: 'default',
    icon: <EditOutlined />,
    confirmTitle: 'Chỉnh sửa phiếu này?',
  },
  pick: {
    label: 'Lấy hàng (Pick)',
    type: 'primary',
    icon: <InboxOutlined />,
    confirmTitle: 'Xác nhận đã lấy đủ hàng để chuẩn bị chuyển?',
  },
  ship: {
    label: 'Xuất chuyển (Ship)',
    type: 'primary',
    icon: <CarOutlined />,
    style: { backgroundColor: '#fa8c16', borderColor: '#fa8c16' },
    confirmTitle: 'Xác nhận xuất kho và chuyển hàng đi?',
  },
  receive: {
    label: 'Nhận hàng (Receive)',
    type: 'primary',
    icon: <InboxOutlined />,
    style: { backgroundColor: '#13c2c2', borderColor: '#13c2c2' },
    confirmTitle: 'Xác nhận kho đích đã nhận được hàng và nhập kho?',
  },
  snapshot: {
    label: 'Chốt số tồn (Snapshot)',
    type: 'primary',
    icon: <CameraOutlined />,
    confirmTitle: 'Chốt số tồn sổ sách tại thời điểm này để tiến hành đếm kiểm kê?',
  },
  count: {
    label: 'Nhập số đếm',
    type: 'primary',
    icon: <FormOutlined />,
    confirmTitle: 'Mở biểu mẫu nhập số lượng kiểm đếm?',
  },
  adjust: {
    label: 'Cân bằng tồn (Adjust)',
    type: 'primary',
    icon: <SwapOutlined />,
    style: { backgroundColor: '#faad14', borderColor: '#faad14' },
    confirmTitle: 'Xác nhận tự động tạo điều chỉnh cân bằng số tồn thực tế và số sách?',
  },
};

interface Props {
  actions?: (DocumentAction | string)[];
  availableActions?: (DocumentAction | string)[];
  onAction: (action: string, reason?: string) => Promise<void>;
}

export function DocumentActions({ actions, availableActions, onAction }: Props) {
  const [reason, setReason] = useState('');
  const [acting, setActing] = useState('');

  const rawList = actions || availableActions || [];

  if (!rawList.length) {
    return (
      <span style={{ color: '#8c8c8c', fontSize: 13, fontStyle: 'italic' }}>
        Không có thao tác nào khả dụng cho trạng thái hiện tại.
      </span>
    );
  }

  const items = rawList.map(item => {
    const rawAction = typeof item === 'string' ? item : item.action;
    const actionKey = (rawAction || '').toLowerCase();
    const config = ACTION_CONFIGS[actionKey];

    const label = (typeof item === 'object' && item.label)
      ? item.label
      : config?.label || rawAction;

    return {
      actionKey,
      rawAction,
      label,
      type: config?.type || 'default',
      danger: config?.danger || false,
      needsReason: config?.needsReason || false,
      icon: config?.icon,
      style: config?.style,
      confirmTitle: config?.confirmTitle || `Xác nhận ${label}?`,
    };
  });

  const handle = async (action: string, needsReason: boolean) => {
    setActing(action);
    try {
      await onAction(action, needsReason ? reason : undefined);
      setReason('');
    } catch {
      // Error is caught and reported by onAction
    } finally {
      setActing('');
    }
  };

  return (
    <Space wrap size="middle">
      {items.map(item =>
        item.needsReason ? (
          <Popconfirm
            key={item.actionKey}
            title={item.confirmTitle}
            description={
              <div style={{ marginTop: 8, minWidth: 260 }}>
                <Input.TextArea
                  placeholder="Vui lòng nhập lý do (bắt buộc)..."
                  value={reason}
                  onChange={e => setReason(e.target.value)}
                  rows={2}
                  autoFocus
                />
              </div>
            }
            onConfirm={() => handle(item.actionKey, true)}
            okText="Xác nhận"
            cancelText="Đóng"
            okButtonProps={{ disabled: !reason.trim(), danger: true }}
          >
            <Button
              danger={item.danger}
              type={item.type}
              icon={item.icon}
              style={item.style}
              loading={acting === item.actionKey}
            >
              {item.label}
            </Button>
          </Popconfirm>
        ) : (
          <Popconfirm
            key={item.actionKey}
            title={item.confirmTitle}
            onConfirm={() => handle(item.actionKey, false)}
            okText="Xác nhận"
            cancelText="Hủy"
            okButtonProps={{ danger: item.danger }}
          >
            <Button
              type={item.type}
              danger={item.danger}
              icon={item.icon}
              style={item.style}
              loading={acting === item.actionKey}
            >
              {item.label}
            </Button>
          </Popconfirm>
        )
      )}
    </Space>
  );
}

import { Button, Space, Popconfirm, Input, message, Tag } from 'antd';
import { useState } from 'react';
import type { DocumentAction, DocumentStatus } from '../types';

const statusColors: Record<string, string> = {
  Draft: 'default', Submitted: 'processing', Approved: 'blue',
  Rejected: 'error', Completed: 'success', Cancelled: 'default',
};
const statusLabels: Record<string, string> = {
  Draft: 'Nháp', Submitted: 'Chờ duyệt', Approved: 'Đã duyệt',
  Rejected: 'Từ chối', Completed: 'Hoàn thành', Cancelled: 'Đã huỷ',
};

export function StatusTag({ status }: { status: DocumentStatus }) {
  return <Tag color={statusColors[status]}>{statusLabels[status] || status}</Tag>;
}

interface Props {
  actions: DocumentAction[];
  onAction: (action: string, reason?: string) => Promise<void>;
}

export function DocumentActions({ actions, onAction }: Props) {
  const [reason, setReason] = useState('');
  const [acting, setActing] = useState('');

  const handle = async (action: string, needsReason: boolean) => {
    setActing(action);
    try { await onAction(action, needsReason ? reason : undefined); message.success('Thao tác thành công'); setReason(''); }
    catch (err: any) { message.error(err.response?.data?.message || 'Lỗi'); }
    finally { setActing(''); }
  };

  const needsReason = (a: string) => ['reject', 'cancel'].includes(a);

  return (
    <Space wrap>
      {actions.map(a => needsReason(a.action) ? (
        <Popconfirm key={a.action} title={`Lý do ${a.label}?`} description={<Input.TextArea value={reason} onChange={e => setReason(e.target.value)} rows={2} />}
          onConfirm={() => handle(a.action, true)} okText="Xác nhận" disabled={!reason}>
          <Button danger loading={acting === a.action}>{a.label}</Button>
        </Popconfirm>
      ) : (
        <Popconfirm key={a.action} title={`Xác nhận ${a.label}?`} onConfirm={() => handle(a.action, false)} okText="Xác nhận">
          <Button type={a.action === 'approve' || a.action === 'complete' ? 'primary' : 'default'} loading={acting === a.action}>{a.label}</Button>
        </Popconfirm>
      ))}
    </Space>
  );
}

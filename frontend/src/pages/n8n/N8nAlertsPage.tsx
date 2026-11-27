import { useState, useEffect } from 'react';
import {
  Card, Form, Input, Button, Switch, Space, Table, Tag, Typography,
  Divider, Tabs, Alert, Row, Col, Statistic, App
} from 'antd';
import {
  ThunderboltOutlined, SendOutlined, SettingOutlined, HistoryOutlined,
  CopyOutlined, CheckCircleOutlined, CodeOutlined, ExperimentOutlined
} from '@ant-design/icons';
import axios from 'axios';
import { inventoryApi, batchesApi } from '../../api/services';

interface WebhookLog {
  id: string;
  timestamp: string;
  eventType: string;
  url: string;
  payload: any;
  status: 'SUCCESS' | 'FAILED' | 'TEST';
  responseCode?: number;
  message: string;
}

const DEFAULT_N8N_WORKFLOW_JSON = {
  name: "WMS Kho Hàng - N8N Alert Automation",
  nodes: [
    {
      parameters: {
        httpMethod: "POST",
        path: "wms-alerts",
        options: {}
      },
      name: "Webhook WMS",
      type: "n8n-nodes-base.webhook",
      typeVersion: 1,
      position: [250, 300]
    },
    {
      parameters: {
        dataType: "string",
        value1: "={{ $json.body.eventType }}",
        rules: {
          rules: [
            { value2: "LOW_STOCK", output: 0 },
            { value2: "OUT_OF_STOCK", output: 1 },
            { value2: "NEAR_EXPIRY", output: 2 },
            { value2: "EXPIRED", output: 3 },
            { value2: "PENDING_APPROVAL", output: 4 }
          ]
        }
      },
      name: "Phân loại Sự kiện",
      type: "n8n-nodes-base.switch",
      typeVersion: 1,
      position: [500, 300]
    },
    {
      parameters: {
        chatId: "@wms_alerts_channel",
        text: "=🚨 [WMS CẢNH BÁO KHO] - {{ $json.body.eventType }}\n📦 Chi tiết: {{ $json.body.message }}\n⏰ Thời gian: {{ $json.body.timestamp }}"
      },
      name: "Gửi Telegram / Discord Bot",
      type: "n8n-nodes-base.telegram",
      typeVersion: 1,
      position: [750, 300]
    }
  ],
  connections: {
    "Webhook WMS": {
      main: [[{ node: "Phân loại Sự kiện", type: "main", index: 0 }]]
    },
    "Phân loại Sự kiện": {
      main: [
        [{ node: "Gửi Telegram / Discord Bot", type: "main", index: 0 }],
        [{ node: "Gửi Telegram / Discord Bot", type: "main", index: 0 }],
        [{ node: "Gửi Telegram / Discord Bot", type: "main", index: 0 }],
        [{ node: "Gửi Telegram / Discord Bot", type: "main", index: 0 }],
        [{ node: "Gửi Telegram / Discord Bot", type: "main", index: 0 }]
      ]
    }
  }
};

export default function N8nAlertsPage() {
  const { message, modal } = App.useApp();
  const [form] = Form.useForm();
  const [testing, setTesting] = useState(false);
  const [scanning, setScanning] = useState(false);
  const [logs, setLogs] = useState<WebhookLog[]>([]);
  const [isSandbox, setIsSandbox] = useState<boolean>(() => {
    return localStorage.getItem('wms_n8n_sandbox') === 'true';
  });

  // Load saved webhook config
  useEffect(() => {
    const savedUrl = localStorage.getItem('wms_n8n_url') || 'http://localhost:5678/webhook/wms-alerts';
    const savedSecret = localStorage.getItem('wms_n8n_secret') || 'wms-secret-key-2026';
    const savedLogs = localStorage.getItem('wms_n8n_logs');

    form.setFieldsValue({
      webhookUrl: savedUrl,
      secretKey: savedSecret,
      alertLowStock: true,
      alertOutOfStock: true,
      alertNearExpiry: true,
      alertExpired: true,
      alertPendingApproval: true,
    });

    if (savedLogs) {
      try {
        setLogs(JSON.parse(savedLogs));
      } catch {}
    }
  }, [form]);

  const saveConfig = (values: any) => {
    localStorage.setItem('wms_n8n_url', values.webhookUrl);
    localStorage.setItem('wms_n8n_secret', values.secretKey || '');
    message.success('Đã lưu cấu hình kết nối N8N!');
  };

  const addLog = (log: WebhookLog) => {
    setLogs(prev => {
      const updated = [log, ...prev].slice(0, 50);
      localStorage.setItem('wms_n8n_logs', JSON.stringify(updated));
      return updated;
    });
  };

  // Test Webhook
  const handleTestWebhook = async () => {
    const values = form.getFieldsValue();
    if (!values.webhookUrl && !isSandbox) {
      message.error('Vui lòng nhập Webhook URL');
      return;
    }
    setTesting(true);
    const testPayload = {
      eventId: `EVT-${Date.now()}`,
      eventType: 'LOW_STOCK',
      timestamp: new Date().toISOString(),
      source: 'WMS.System.Frontend',
      idempotencyKey: `IDEMP-${Date.now()}`,
      data: {
        productSku: 'SKU-TEST-001',
        productName: 'Sản phẩm thử nghiệm kết nối N8N',
        warehouse: 'Kho Ha Noi',
        currentQuantity: 3,
        minStock: 10,
        message: 'Tồn kho đang ở mức thấp dưới cảnh báo tối thiểu'
      }
    };

    if (isSandbox) {
      await new Promise(r => setTimeout(r, 400));
      addLog({
        id: `LOG-${Date.now()}`,
        timestamp: new Date().toLocaleString('vi-VN'),
        eventType: 'TEST_CONNECTION',
        url: values.webhookUrl ? `${values.webhookUrl} [Sandbox Mock]` : 'Sandbox Mode',
        payload: testPayload,
        status: 'SUCCESS',
        responseCode: 200,
        message: 'Mô phỏng gửi Webhook thành công (HTTP 200 OK - Mock Sandbox)'
      });
      message.success('Mô phỏng gửi N8N Webhook thành công (HTTP 200)! Payload đã được lưu vào lịch sử bên dưới.');
      setTesting(false);
      return;
    }

    try {
      const res = await axios.post(values.webhookUrl, testPayload, {
        headers: {
          'Content-Type': 'application/json',
          'X-WMS-Secret': values.secretKey || '',
        },
        timeout: 5000,
      });

      addLog({
        id: `LOG-${Date.now()}`,
        timestamp: new Date().toLocaleString('vi-VN'),
        eventType: 'TEST_CONNECTION',
        url: values.webhookUrl,
        payload: testPayload,
        status: 'SUCCESS',
        responseCode: res.status,
        message: `Webhook phản hồi thành công (HTTP ${res.status})`
      });
      message.success(`Kiểm tra N8N Webhook thành công! HTTP ${res.status}`);
    } catch (err: any) {
      const status = err.response?.status;
      addLog({
        id: `LOG-${Date.now()}`,
        timestamp: new Date().toLocaleString('vi-VN'),
        eventType: 'TEST_CONNECTION',
        url: values.webhookUrl,
        payload: testPayload,
        status: 'FAILED',
        responseCode: status,
        message: err.message || 'Không thể kết nối tới N8N Webhook'
      });

      modal.warning({
        title: 'Chưa phát hiện máy chủ N8N đang chạy',
        width: 600,
        content: (
          <div>
            <p>Trình duyệt không thể kết nối tới <code>{values.webhookUrl}</code> (mã lỗi: <b>{err.message || 'ERR_CONNECTION_REFUSED'}</b>).</p>
            <div style={{ background: '#fffbe6', border: '1px solid #ffe58f', padding: 12, borderRadius: 6, fontSize: 13, lineHeight: 1.6 }}>
              <p style={{ margin: 0, fontWeight: 600, color: '#d48806' }}>💡 3 cách giải quyết nhanh:</p>
              <ul style={{ paddingLeft: 20, margin: '6px 0 0 0' }}>
                <li><b>Cách 1 (Khuyên dùng khi demo/test):</b> Bật cần gạt <b>"Chế độ mô phỏng (Sandbox / Mock)"</b> bên cạnh để kiểm thử toàn bộ dữ liệu webhook ngay lập tức mà không cần cài N8N.</li>
                <li><b>Cách 2 (Dùng Webhook online miễn phí):</b> Mở trang <a href="https://webhook.site" target="_blank" rel="noreferrer">https://webhook.site</a>, copy đường link của bạn và dán vào ô Webhook URL để xem request thật gửi qua Internet.</li>
                <li><b>Cách 3 (Khởi động N8N cục bộ):</b> Mở terminal và chạy lệnh: <code>npx n8n</code> hoặc <code>docker run -it --rm -p 5678:5678 docker.n8n.io/n8nio/n8n</code>.</li>
              </ul>
            </div>
          </div>
        ),
        okText: 'Đã hiểu',
      });
    } finally {
      setTesting(false);
    }
  };

  // Trigger Real Alert Scan
  const handleScanAndTrigger = async () => {
    const values = form.getFieldsValue();
    if (!values.webhookUrl && !isSandbox) {
      message.error('Chưa có cấu hình Webhook URL!');
      return;
    }
    setScanning(true);
    try {
      // 1. Quét tồn kho
      const { data: invRes } = await inventoryApi.list({ page: 1, pageSize: 100 });
      const lowStockItems = invRes.items.filter(i => i.quantity <= 10);

      // 2. Quét lô hàng sắp hết hạn
      const { data: batchRes } = await batchesApi.expiryAlerts({ page: 1, pageSize: 50, daysBeforeExpiry: 30 });

      let sentCount = 0;

      // Gửi sự kiện tồn kho thấp
      for (const item of lowStockItems) {
        const payload = {
          eventId: `EVT-${Date.now()}-${item.id}`,
          eventType: item.quantity === 0 ? 'OUT_OF_STOCK' : 'LOW_STOCK',
          timestamp: new Date().toISOString(),
          idempotencyKey: `IDEMP-INV-${item.id}-${new Date().toISOString().slice(0, 10)}`,
          data: {
            productId: item.productId,
            productName: item.productName,
            warehouseId: item.warehouseId,
            warehouseName: item.warehouseCode,
            locationCode: item.locationCode,
            availableQuantity: item.availableQuantity,
            quantity: item.quantity,
          }
        };

        if (isSandbox) {
          addLog({
            id: `LOG-${Date.now()}-${item.id}`,
            timestamp: new Date().toLocaleString('vi-VN'),
            eventType: payload.eventType,
            url: values.webhookUrl ? `${values.webhookUrl} [Sandbox Mock]` : 'Sandbox Mode',
            payload,
            status: 'SUCCESS',
            responseCode: 200,
            message: `[Mô phỏng] Cảnh báo tồn kho ${item.productName} (${item.quantity === 0 ? 'Hết hàng' : 'Tồn thấp'})`
          });
          sentCount++;
        } else {
          try {
            await axios.post(values.webhookUrl, payload, {
              headers: {
                'Content-Type': 'application/json',
                'X-WMS-Secret': values.secretKey || ''
              },
              timeout: 5000,
            });
            sentCount++;
          } catch {}
        }
      }

      // Gửi sự kiện lô sắp hết hạn
      for (const batch of (batchRes.items || [])) {
        const payload = {
          eventId: `EVT-${Date.now()}-${batch.batchId}`,
          eventType: 'NEAR_EXPIRY',
          timestamp: new Date().toISOString(),
          idempotencyKey: `IDEMP-BATCH-${batch.batchId}-${new Date().toISOString().slice(0, 10)}`,
          data: {
            batchId: batch.batchId,
            batchCode: batch.batchCode,
            productName: batch.productName,
            daysUntilExpiry: batch.daysToExpiry,
            expiryDate: batch.expiryDate,
            quantity: batch.quantity,
          }
        };

        if (isSandbox) {
          addLog({
            id: `LOG-${Date.now()}-${batch.batchId}`,
            timestamp: new Date().toLocaleString('vi-VN'),
            eventType: 'NEAR_EXPIRY',
            url: values.webhookUrl ? `${values.webhookUrl} [Sandbox Mock]` : 'Sandbox Mode',
            payload,
            status: 'SUCCESS',
            responseCode: 200,
            message: `[Mô phỏng] Cảnh báo lô ${batch.batchCode} sắp hết hạn trong ${batch.daysToExpiry} ngày`
          });
          sentCount++;
        } else {
          try {
            await axios.post(values.webhookUrl, payload, {
              headers: {
                'Content-Type': 'application/json',
                'X-WMS-Secret': values.secretKey || ''
              },
              timeout: 5000,
            });
            sentCount++;
          } catch {}
        }
      }

      addLog({
        id: `LOG-${Date.now()}`,
        timestamp: new Date().toLocaleString('vi-VN'),
        eventType: 'SCAN_AND_TRIGGER',
        url: isSandbox ? 'Sandbox Mock Mode' : values.webhookUrl,
        payload: { lowStockCount: lowStockItems.length, expiringBatchesCount: batchRes.items?.length || 0 },
        status: 'SUCCESS',
        message: `${isSandbox ? '[Mô phỏng] ' : ''}Đã quét và phát hiện ${lowStockItems.length} sản phẩm sắp hết hàng, ${batchRes.items?.length || 0} lô sắp hết hạn. Đã gửi ${sentCount} sự kiện webhook.`
      });

      message.success(`Hoàn thành quét! Đã ${isSandbox ? 'mô phỏng ' : ''}gửi ${sentCount} sự kiện cảnh báo sang N8N`);
    } catch (err: any) {
      message.error('Lỗi khi quét và kích hoạt cảnh báo: ' + err.message);
    } finally {
      setScanning(false);
    }
  };

  const copyWorkflowJson = () => {
    navigator.clipboard.writeText(JSON.stringify(DEFAULT_N8N_WORKFLOW_JSON, null, 2));
    message.success('Đã sao chép N8N Workflow JSON vào clipboard!');
  };

  const columns = [
    { title: 'Thời gian', dataIndex: 'timestamp', width: 170 },
    {
      title: 'Loại sự kiện',
      dataIndex: 'eventType',
      width: 160,
      render: (v: string) => {
        let color = 'blue';
        if (v === 'LOW_STOCK') color = 'orange';
        if (v === 'OUT_OF_STOCK') color = 'red';
        if (v === 'NEAR_EXPIRY') color = 'gold';
        if (v === 'EXPIRED') color = 'magenta';
        return <Tag color={color}>{v}</Tag>;
      }
    },
    {
      title: 'Trạng thái',
      dataIndex: 'status',
      width: 120,
      render: (s: string, r: WebhookLog) => (
        <Tag color={s === 'SUCCESS' ? 'green' : 'error'}>
          {s} {r.responseCode ? `(${r.responseCode})` : ''}
        </Tag>
      )
    },
    { title: 'Nội dung thông báo', dataIndex: 'message' },
    {
      title: 'Chi tiết Payload',
      width: 120,
      render: (_: any, r: WebhookLog) => (
        <Button
          size="small"
          type="link"
          onClick={() => {
            modal.info({
              title: `Payload ${r.eventType}`,
              width: 600,
              content: (
                <pre style={{ background: '#f5f5f5', padding: 12, borderRadius: 6, maxHeight: 350, overflow: 'auto' }}>
                  {JSON.stringify(r.payload, null, 2)}
                </pre>
              )
            });
          }}
        >
          Xem Payload
        </Button>
      )
    }
  ];

  return (
    <div>
      <div style={{ marginBottom: 20, display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <div>
          <Typography.Title level={3} style={{ margin: 0 }}>
            ⚡ Tích hợp Tự động hóa N8N (W-17 & W-18)
          </Typography.Title>
          <Typography.Text type="secondary">
            Cấu hình Webhook & Hệ thống cảnh báo kho tự động: Hết hàng, Sắp hết hạn, Duyệt chứng từ
          </Typography.Text>
        </div>
        <Space>
          <Space align="center" style={{ background: isSandbox ? '#f9f0ff' : '#f5f5f5', padding: '5px 12px', borderRadius: 6, border: `1px solid ${isSandbox ? '#d3adf7' : '#d9d9d9'}` }}>
            <ExperimentOutlined style={{ color: isSandbox ? '#722ed1' : '#8c8c8c', fontSize: 16 }} />
            <span style={{ fontSize: 13, fontWeight: 500, color: isSandbox ? '#531dab' : '#595959' }}>
              Chế độ mô phỏng (Sandbox / Mock):
            </span>
            <Switch
              checked={isSandbox}
              onChange={(val) => {
                setIsSandbox(val);
                localStorage.setItem('wms_n8n_sandbox', String(val));
                message.info(val ? 'Đã bật chế độ mô phỏng Sandbox (không yêu cầu cài đặt N8N)' : 'Đã chuyển sang chế độ gửi Webhook HTTP thực tế');
              }}
            />
          </Space>
          <Button
            type="primary"
            icon={<ThunderboltOutlined />}
            loading={scanning}
            onClick={handleScanAndTrigger}
            style={{ backgroundColor: '#722ed1', borderColor: '#722ed1' }}
          >
            Quét kho & Bắn cảnh báo ngay
          </Button>
        </Space>
      </div>

      <Row gutter={[16, 16]} style={{ marginBottom: 20 }}>
        <Col span={8}>
          <Card>
            <Statistic
              title="Sự kiện hỗ trợ"
              value={5}
              suffix="sự kiện"
              valueStyle={{ color: '#1677ff' }}
              prefix={<ThunderboltOutlined />}
            />
          </Card>
        </Col>
        <Col span={8}>
          <Card>
            <Statistic
              title="Tổng số lượt gửi Webhook"
              value={logs.length}
              valueStyle={{ color: '#52c41a' }}
              prefix={<SendOutlined />}
            />
          </Card>
        </Col>
        <Col span={8}>
          <Card>
            <Statistic
              title="Trạng thái tích hợp"
              value="Sẵn sàng"
              valueStyle={{ color: '#722ed1' }}
              prefix={<CheckCircleOutlined />}
            />
          </Card>
        </Col>
      </Row>

      <Tabs
        defaultActiveKey="config"
        items={[
          {
            key: 'config',
            label: (
              <span>
                <SettingOutlined /> Cấu hình Webhook
              </span>
            ),
            children: (
              <Card title="Cài đặt kết nối N8N Webhook">
                <Alert
                  type="info"
                  showIcon
                  message="Cách hoạt động"
                  description="Khi có sự kiện kho xảy ra (Hết hàng, Lô hàng sắp hết hạn, hoặc phiếu chờ duyệt), hệ thống WMS sẽ gửi HTTP POST kèm JSON payload và chữ ký xác thực tới URL Webhook của N8N để kích hoạt thông báo Telegram/Discord/Email."
                  style={{ marginBottom: 20 }}
                />

                <Form form={form} layout="vertical" onFinish={saveConfig}>
                  <Form.Item
                    name="webhookUrl"
                    label="URL Webhook tiếp nhận trên N8N"
                    rules={[{ required: true, message: 'Vui lòng nhập URL Webhook N8N' }]}
                    extra="Ví dụ: http://localhost:5678/webhook/wms-alerts hoặc Webhook Test URL trong N8N"
                  >
                    <Input placeholder="http://localhost:5678/webhook/wms-alerts" />
                  </Form.Item>

                  <Form.Item
                    name="secretKey"
                    label="X-WMS-Secret (Mã bí mật xác thực webhook)"
                    extra="N8N có thể kiểm tra Header X-WMS-Secret này để chống giả mạo request"
                  >
                    <Input.Password placeholder="Nhập khóa bí mật dùng chung" />
                  </Form.Item>

                  <Divider orientation="left">Loại sự kiện tự động kích hoạt cảnh báo</Divider>
                  <Row gutter={[16, 8]}>
                    <Col span={12}>
                      <Form.Item name="alertLowStock" label="LOW_STOCK (Tồn kho dưới ngưỡng tối thiểu)" valuePropName="checked">
                        <Switch />
                      </Form.Item>
                    </Col>
                    <Col span={12}>
                      <Form.Item name="alertOutOfStock" label="OUT_OF_STOCK (Hết sạch tồn kho)" valuePropName="checked">
                        <Switch />
                      </Form.Item>
                    </Col>
                    <Col span={12}>
                      <Form.Item name="alertNearExpiry" label="NEAR_EXPIRY (Lô hàng sắp hết hạn dưới 30 ngày)" valuePropName="checked">
                        <Switch />
                      </Form.Item>
                    </Col>
                    <Col span={12}>
                      <Form.Item name="alertExpired" label="EXPIRED (Lô hàng đã quá hạn dùng)" valuePropName="checked">
                        <Switch />
                      </Form.Item>
                    </Col>
                    <Col span={12}>
                      <Form.Item name="alertPendingApproval" label="PENDING_APPROVAL (Chứng từ chuyển/nhập/xuất chờ duyệt)" valuePropName="checked">
                        <Switch />
                      </Form.Item>
                    </Col>
                  </Row>

                  <Space style={{ marginTop: 16 }}>
                    <Button type="primary" htmlType="submit">
                      Lưu cấu hình
                    </Button>
                    <Button
                      icon={<SendOutlined />}
                      loading={testing}
                      onClick={handleTestWebhook}
                    >
                      Bắn thử nghiệm Test Payload
                    </Button>
                  </Space>
                </Form>
              </Card>
            )
          },
          {
            key: 'logs',
            label: (
              <span>
                <HistoryOutlined /> Lịch sử gửi Webhook ({logs.length})
              </span>
            ),
            children: (
              <Card
                title="Nhật ký các sự kiện Webhook đã gửi"
                extra={
                  <Space>
                    <Button
                      size="small"
                      danger
                      onClick={() => {
                        setLogs([]);
                        localStorage.removeItem('wms_n8n_logs');
                        message.success('Đã xóa nhật ký');
                      }}
                    >
                      Xóa lịch sử
                    </Button>
                  </Space>
                }
              >
                <Table
                  rowKey="id"
                  columns={columns}
                  dataSource={logs}
                  pagination={{ pageSize: 10 }}
                />
              </Card>
            )
          },
          {
            key: 'template',
            label: (
              <span>
                <CodeOutlined /> Mẫu Workflow N8N (JSON)
              </span>
            ),
            children: (
              <Card
                title="Mẫu Workflow N8N để Import"
                extra={
                  <Button icon={<CopyOutlined />} type="primary" onClick={copyWorkflowJson}>
                    Sao chép JSON Workflow
                  </Button>
                }
              >
                <Alert
                  type="success"
                  message="Hướng dẫn sử dụng mẫu Workflow:"
                  description="1. Bấm 'Sao chép JSON Workflow'. 2. Mở trình duyệt tại N8N (http://localhost:5678). 3. Nhấn Ctrl+V để dán trực tiếp vào bảng vẽ N8N. 4. Điền Token Bot Telegram / Discord của bạn để nhận thông báo cảnh báo kho tức thì!"
                  style={{ marginBottom: 16 }}
                />
                <pre style={{ background: '#282c34', color: '#abb2bf', padding: 16, borderRadius: 8, maxHeight: 400, overflow: 'auto' }}>
                  {JSON.stringify(DEFAULT_N8N_WORKFLOW_JSON, null, 2)}
                </pre>
              </Card>
            )
          }
        ]}
      />
    </div>
  );
}

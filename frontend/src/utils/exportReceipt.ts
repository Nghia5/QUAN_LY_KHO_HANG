/**
 * Utility functions for exporting and printing warehouse documents
 */

export function exportToCsv(
  filename: string,
  headers: string[],
  rows: (string | number | undefined | null)[][]
) {
  const sanitize = (val: string | number | undefined | null) => {
    if (val === undefined || val === null) return '""';
    const str = String(val).replace(/"/g, '""');
    return `"${str}"`;
  };

  const csvContent =
    '\uFEFF' + // UTF-8 BOM for Microsoft Excel compatibility
    headers.map(sanitize).join(',') +
    '\n' +
    rows.map(row => row.map(sanitize).join(',')).join('\n');

  const blob = new Blob([csvContent], { type: 'text/csv;charset=utf-8;' });
  const url = URL.createObjectURL(blob);
  const link = document.createElement('a');
  link.href = url;
  link.setAttribute('download', filename.endsWith('.csv') ? filename : `${filename}.csv`);
  document.body.appendChild(link);
  link.click();
  link.parentNode?.removeChild(link);
  URL.revokeObjectURL(url);
}

export interface PrintableDocument {
  documentNo: string;
  type: 'RECEIPT' | 'ISSUE' | 'TRANSFER';
  title?: string;
  date: string;
  warehouseName: string;
  partnerTitle?: string;
  partnerName?: string;
  referenceNo?: string;
  note?: string;
  lines: Array<{
    sku: string;
    name: string;
    unit?: string;
    location: string;
    quantity: number;
    unitCost?: number;
    batchCode?: string;
  }>;
}

export function printDocument(doc: PrintableDocument) {
  const isReceipt = doc.type === 'RECEIPT';
  const typeTitle = doc.title || (isReceipt ? 'PHIẾU NHẬP KHO' : doc.type === 'ISSUE' ? 'PHIẾU XUẤT KHO' : 'PHIẾU ĐIỀU CHUYỂN KHO');
  const templateCode = isReceipt ? 'Mẫu số 01 - VT' : 'Mẫu số 02 - VT';
  const partnerLabel = doc.partnerTitle || (isReceipt ? 'Nhà cung cấp / Đơn vị giao:' : 'Khách hàng / Đơn vị nhận:');

  const totalQty = doc.lines.reduce((sum, l) => sum + (Number(l.quantity) || 0), 0);
  const totalAmount = doc.lines.reduce((sum, l) => sum + ((Number(l.quantity) || 0) * (Number(l.unitCost) || 0)), 0);

  const printWindow = window.open('', '_blank', 'width=900,height=750');
  if (!printWindow) {
    alert('Trình duyệt đã chặn cửa sổ in (pop-up). Vui lòng cho phép mở pop-up để in phiếu.');
    return;
  }

  const html = `
    <!DOCTYPE html>
    <html lang="vi">
    <head>
      <meta charset="utf-8">
      <title>${typeTitle} - ${doc.documentNo}</title>
      <style>
        body {
          font-family: 'Times New Roman', Times, serif;
          margin: 20px;
          color: #000;
          font-size: 14px;
        }
        .header {
          display: flex;
          justify-content: space-between;
          margin-bottom: 20px;
        }
        .header .company {
          font-weight: bold;
          text-transform: uppercase;
        }
        .header .template {
          text-align: right;
          font-size: 12px;
          font-style: italic;
        }
        .title {
          text-align: center;
          margin: 15px 0 5px 0;
        }
        .title h1 {
          margin: 0;
          font-size: 22px;
          text-transform: uppercase;
          letter-spacing: 1px;
        }
        .title .doc-no {
          font-size: 14px;
          font-weight: bold;
          margin-top: 5px;
        }
        .meta-info {
          margin: 15px 0;
          line-height: 1.8;
        }
        table {
          width: 100%;
          border-collapse: collapse;
          margin: 15px 0;
        }
        th, td {
          border: 1px solid #000;
          padding: 6px 8px;
          text-align: left;
        }
        th {
          background-color: #f2f2f2;
          text-align: center;
          font-weight: bold;
        }
        .text-center { text-align: center; }
        .text-right { text-align: right; }
        .signatures {
          display: flex;
          justify-content: space-between;
          margin-top: 30px;
          text-align: center;
        }
        .sign-block {
          width: 22%;
        }
        .sign-block .title-sign {
          font-weight: bold;
          margin-bottom: 4px;
        }
        .sign-block .note-sign {
          font-size: 12px;
          font-style: italic;
        }
        @media print {
          @page { margin: 15mm; size: A4 portrait; }
          body { margin: 0; }
        }
      </style>
    </head>
    <body>
      <div class="header">
        <div>
          <div class="company">HỆ THỐNG QUẢN LÝ KHO HÀNG (WMS)</div>
          <div>Kho lưu trữ: <b>${doc.warehouseName}</b></div>
        </div>
        <div class="template">
          <b>${templateCode}</b><br/>
          (Ban hành theo TT số 200/2014/TT-BTC)
        </div>
      </div>

      <div class="title">
        <h1>${typeTitle}</h1>
        <div class="doc-no">Số: ${doc.documentNo}</div>
        <div style="font-style: italic; margin-top: 3px;">Ngày lập: ${doc.date}</div>
      </div>

      <div class="meta-info">
        <div>- ${partnerLabel} <b>${doc.partnerName || 'Nội bộ'}</b></div>
        <div>- Nhập / Xuất tại kho: <b>${doc.warehouseName}</b></div>
        ${doc.referenceNo ? `<div>- Mã tham chiếu / Hóa đơn: <b>${doc.referenceNo}</b></div>` : ''}
        ${doc.note ? `<div>- Lý do / Ghi chú: ${doc.note}</div>` : ''}
      </div>

      <table>
        <thead>
          <tr>
            <th style="width: 35px;">STT</th>
            <th style="width: 100px;">Mã SKU</th>
            <th>Tên sản phẩm, quy cách</th>
            <th style="width: 60px;">ĐVT</th>
            <th style="width: 90px;">Vị trí (Bin)</th>
            <th style="width: 80px;">Số lô</th>
            <th style="width: 70px;">Số lượng</th>
            ${totalAmount > 0 ? `
              <th style="width: 90px;">Đơn giá (₫)</th>
              <th style="width: 100px;">Thành tiền (₫)</th>
            ` : ''}
          </tr>
        </thead>
        <tbody>
          ${doc.lines.map((line, idx) => {
            const lineAmount = (line.quantity || 0) * (line.unitCost || 0);
            return `
              <tr>
                <td class="text-center">${idx + 1}</td>
                <td><b>${line.sku}</b></td>
                <td>${line.name}</td>
                <td class="text-center">${line.unit || 'Cái'}</td>
                <td class="text-center">${line.location}</td>
                <td class="text-center">${line.batchCode || '-'}</td>
                <td class="text-right"><b>${line.quantity.toLocaleString('vi-VN')}</b></td>
                ${totalAmount > 0 ? `
                  <td class="text-right">${line.unitCost ? line.unitCost.toLocaleString('vi-VN') : '-'}</td>
                  <td class="text-right">${lineAmount > 0 ? lineAmount.toLocaleString('vi-VN') : '-'}</td>
                ` : ''}
              </tr>
            `;
          }).join('')}
          <tr style="font-weight: bold; background-color: #fafafa;">
            <td colspan="6" class="text-center">TỔNG CỘNG</td>
            <td class="text-right">${totalQty.toLocaleString('vi-VN')}</td>
            ${totalAmount > 0 ? `
              <td></td>
              <td class="text-right">${totalAmount.toLocaleString('vi-VN')} ₫</td>
            ` : ''}
          </tr>
        </tbody>
      </table>

      <div class="signatures">
        <div class="sign-block">
          <div class="title-sign">Người lập phiếu</div>
          <div class="note-sign">(Ký, họ tên)</div>
          <div style="height: 60px;"></div>
        </div>
        <div class="sign-block">
          <div class="title-sign">Người giao hàng</div>
          <div class="note-sign">(Ký, họ tên)</div>
          <div style="height: 60px;"></div>
        </div>
        <div class="sign-block">
          <div class="title-sign">Thủ kho</div>
          <div class="note-sign">(Ký, họ tên)</div>
          <div style="height: 60px;"></div>
        </div>
        <div class="sign-block">
          <div class="title-sign">Kế toán trưởng</div>
          <div class="note-sign">(Ký, họ tên)</div>
          <div style="height: 60px;"></div>
        </div>
      </div>

      <script>
        window.onload = function() {
          setTimeout(function() {
            window.print();
          }, 300);
        };
      </script>
    </body>
    </html>
  `;

  printWindow.document.open();
  printWindow.document.write(html);
  printWindow.document.close();
}

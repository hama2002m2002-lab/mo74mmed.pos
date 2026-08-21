import os
import subprocess
from datetime import datetime
from reportlab.lib.pagesizes import letter, A4
from reportlab.lib import colors
from reportlab.platypus import SimpleDocTemplate, Paragraph, Spacer, Table, TableStyle, Image as RLImage
from reportlab.lib.styles import getSampleStyleSheet, ParagraphStyle
from config import PDF_DIR, APP_NAME, format_currency

class InvoicePrinter:
    @staticmethod
    def generate_order_a4_pdf(order: dict, items: list) -> str:
        """Generates an official A4 Delivery Invoice PDF for Rep/Store Order."""
        order_num = order.get("OrderNumber", "ORD-001")
        safe_num = order_num.replace("/", "_").replace("\\", "_")
        pdf_path = os.path.join(PDF_DIR, f"Order_{safe_num}.pdf")

        doc = SimpleDocTemplate(pdf_path, pagesize=A4, rightMargin=30, leftMargin=30, topMargin=30, bottomMargin=30)
        styles = getSampleStyleSheet()

        title_style = ParagraphStyle(
            name='TitleStyle',
            fontName='Helvetica-Bold',
            fontSize=18,
            leading=22,
            alignment=1, # Center
            textColor=colors.HexColor('#0F172A')
        )

        meta_style = ParagraphStyle(
            name='MetaStyle',
            fontName='Helvetica',
            fontSize=11,
            leading=15,
            alignment=2 # Right
        )

        elements = []

        # 1. Header
        elements.append(Paragraph(f"<b>{APP_NAME} - وصل تسليم وتجهيز طلبية</b>", title_style))
        elements.append(Spacer(1, 15))

        # 2. Meta Info Grid
        meta_data = [
            [
                f"رقم الطلبية: {order_num}",
                f"التاريخ: {order.get('CreatedAt', datetime.now().strftime('%Y-%m-%d %H:%M'))[:16]}"
            ],
            [
                f"المحل / العميل: {order.get('StoreName') or order.get('CustomerName', '---')}",
                f"المندوب المسؤول: {order.get('RepName', '---')} ({order.get('RepPhone', '')})"
            ],
            [
                f"العنوان: {order.get('StoreAddress', '---')}",
                f"الحالة: {order.get('Status', 'Pending')}"
            ]
        ]
        meta_table = Table(meta_data, colWidths=[270, 270])
        meta_table.setStyle(TableStyle([
            ('BACKGROUND', (0, 0), (-1, -1), colors.HexColor('#F8FAFC')),
            ('PADDING', (0, 0), (-1, -1), 8),
            ('BOX', (0, 0), (-1, -1), 1, colors.HexColor('#CBD5E1')),
            ('INNERGRID', (0, 0), (-1, -1), 0.5, colors.HexColor('#E2E8F0')),
            ('ALIGN', (0, 0), (-1, -1), 'RIGHT'),
            ('FONTNAME', (0, 0), (-1, -1), 'Helvetica'),
            ('FONTSIZE', (0, 0), (-1, -1), 10),
        ]))
        elements.append(meta_table)
        elements.append(Spacer(1, 20))

        # 3. Items Table
        table_data = [["#", "المادة / الصنف", "سعر الوحدة", "الكمية", "الإجمالي"]]
        for i, item in enumerate(items, 1):
            unit_price = float(item.get("UnitPrice", 0))
            qty = float(item.get("Quantity", 1))
            total_price = float(item.get("TotalPrice", unit_price * qty))
            table_data.append([
                str(i),
                item.get("ProductName", ""),
                f"{unit_price:,.0f} د.ع",
                f"{qty:,.0f}",
                f"{total_price:,.0f} د.ع"
            ])

        total_amount = float(order.get("TotalAmount", 0))
        table_data.append(["", "", "", "المجموع الكلي:", f"{total_amount:,.0f} د.ع"])

        items_table = Table(table_data, colWidths=[30, 240, 90, 60, 120])
        items_table.setStyle(TableStyle([
            ('BACKGROUND', (0, 0), (-1, 0), colors.HexColor('#0F172A')),
            ('TEXTCOLOR', (0, 0), (-1, 0), colors.white),
            ('ALIGN', (0, 0), (-1, -1), 'CENTER'),
            ('FONTNAME', (0, 0), (-1, 0), 'Helvetica-Bold'),
            ('FONTSIZE', (0, 0), (-1, 0), 10),
            ('BOTTOMPADDING', (0, 0), (-1, 0), 8),
            ('BACKGROUND', (0, 1), (-1, -2), colors.white),
            ('GRID', (0, 0), (-1, -2), 0.5, colors.HexColor('#E2E8F0')),
            ('ROWBACKGROUNDS', (0, 1), (-1, -2), [colors.white, colors.HexColor('#F8FAFC')]),
            ('FONTNAME', (0, -1), (-1, -1), 'Helvetica-Bold'),
            ('BACKGROUND', (0, -1), (-1, -1), colors.HexColor('#E2E8F0')),
            ('TEXTCOLOR', (0, -1), (-1, -1), colors.HexColor('#0F172A')),
            ('FONTSIZE', (0, -1), (-1, -1), 11),
        ]))
        elements.append(items_table)
        elements.append(Spacer(1, 30))

        # 4. Signatures
        sig_data = [
            ["توقيع المستلم (المحل): ........................", "توقيع وتأكيد المندوب / المحاسب: ........................"]
        ]
        sig_table = Table(sig_data, colWidths=[270, 270])
        sig_table.setStyle(TableStyle([
            ('ALIGN', (0, 0), (-1, -1), 'CENTER'),
            ('FONTNAME', (0, 0), (-1, -1), 'Helvetica-Bold'),
            ('FONTSIZE', (0, 0), (-1, -1), 10),
            ('TEXTCOLOR', (0, 0), (-1, -1), colors.HexColor('#475569'))
        ]))
        elements.append(sig_table)

        doc.build(elements)
        return pdf_path

    @staticmethod
    def open_pdf(pdf_path: str):
        if os.path.exists(pdf_path):
            os.startfile(pdf_path)

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using HamoPos.Data;
using HamoPos.Models;

namespace HamoPos.Services;

public class RepWebPortalService
{
    private static readonly Lazy<RepWebPortalService> _instance = new(() => new RepWebPortalService());
    public static RepWebPortalService Instance => _instance.Value;

    private TcpListener? _tcpListener;
    private CancellationTokenSource? _cts;
    private bool _isRunning = false;
    public int Port { get; private set; } = 5000;

    public string PortalUrl => $"http://{NetworkConfigService.GetLocalIpAddress()}:{Port}/";

    public event Action? OrderReceived;

    public void Start(int port = 5000)
    {
        if (_isRunning) return;

        Port = port;
        try
        {
            _cts = new CancellationTokenSource();
            _tcpListener = new TcpListener(IPAddress.Any, Port);
            _tcpListener.Start();
            _isRunning = true;

            Task.Run(() => ListenLoopAsync(_cts.Token));
            System.Diagnostics.Debug.WriteLine($"Rep Web Portal (TcpListener) started at {PortalUrl}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to start Rep Web Portal: {ex.Message}");
        }
    }

    public void Stop()
    {
        if (!_isRunning) return;
        _isRunning = false;
        try
        {
            _cts?.Cancel();
            _tcpListener?.Stop();
        }
        catch { }
    }

    private async Task ListenLoopAsync(CancellationToken token)
    {
        while (_isRunning && !token.IsCancellationRequested)
        {
            try
            {
                if (_tcpListener == null) break;
                var client = await _tcpListener.AcceptTcpClientAsync(token);
                _ = Task.Run(() => HandleClientAsync(client));
            }
            catch
            {
                if (!_isRunning) break;
            }
        }
    }

    private async Task HandleClientAsync(TcpClient client)
    {
        using (client)
        using (var networkStream = client.GetStream())
        {
            try
            {
                var buffer = new byte[8192];
                int bytesRead = await networkStream.ReadAsync(buffer, 0, buffer.Length);
                if (bytesRead == 0) return;

                string rawRequest = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                var lines = rawRequest.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
                if (lines.Length == 0) return;

                var requestLine = lines[0].Split(' ');
                if (requestLine.Length < 2) return;

                string method = requestLine[0].ToUpperInvariant();
                string fullUrl = requestLine[1];
                string path = fullUrl.Contains('?') ? fullUrl.Substring(0, fullUrl.IndexOf('?')) : fullUrl;
                string queryString = fullUrl.Contains('?') ? fullUrl.Substring(fullUrl.IndexOf('?') + 1) : "";

                // CORS Preflight
                if (method == "OPTIONS")
                {
                    await SendResponseAsync(networkStream, 200, "text/plain", Array.Empty<byte>());
                    return;
                }

                if (path == "/" || path.EndsWith("/index.html", StringComparison.OrdinalIgnoreCase))
                {
                    string html = GetEmbeddedMobileAppHtml();
                    byte[] body = Encoding.UTF8.GetBytes(html);
                    await SendResponseAsync(networkStream, 200, "text/html; charset=utf-8", body);
                }
                else if (path.Equals("/api/products", StringComparison.OrdinalIgnoreCase) && method == "GET")
                {
                    byte[] body = await GetProductsJsonAsync();
                    await SendResponseAsync(networkStream, 200, "application/json; charset=utf-8", body);
                }
                else if (path.Equals("/api/orders", StringComparison.OrdinalIgnoreCase) && method == "GET")
                {
                    string repName = ExtractQueryParam(queryString, "rep");
                    byte[] body = await GetOrdersJsonAsync(repName);
                    await SendResponseAsync(networkStream, 200, "application/json; charset=utf-8", body);
                }
                else if (path.Equals("/api/orders", StringComparison.OrdinalIgnoreCase) && method == "POST")
                {
                    // Extract JSON body
                    int bodyIndex = rawRequest.IndexOf("\r\n\r\n");
                    string reqBody = "";
                    if (bodyIndex != -1)
                    {
                        reqBody = rawRequest.Substring(bodyIndex + 4);
                    }

                    byte[] body = await SaveOrderFromJsonAsync(reqBody);
                    await SendResponseAsync(networkStream, 200, "application/json; charset=utf-8", body);
                }
                else
                {
                    byte[] body = Encoding.UTF8.GetBytes("Not Found");
                    await SendResponseAsync(networkStream, 404, "text/plain", body);
                }
            }
            catch (Exception ex)
            {
                try
                {
                    byte[] body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { error = ex.Message }));
                    await SendResponseAsync(networkStream, 500, "application/json; charset=utf-8", body);
                }
                catch { }
            }
        }
    }

    private static string ExtractQueryParam(string queryString, string key)
    {
        if (string.IsNullOrWhiteSpace(queryString)) return "";
        var pairs = queryString.Split('&');
        foreach (var p in pairs)
        {
            var parts = p.Split('=');
            if (parts.Length == 2 && parts[0].Equals(key, StringComparison.OrdinalIgnoreCase))
            {
                return Uri.UnescapeDataString(parts[1]);
            }
        }
        return "";
    }

    private static async Task SendResponseAsync(NetworkStream stream, int statusCode, string contentType, byte[] body)
    {
        string statusText = statusCode switch
        {
            200 => "OK",
            400 => "Bad Request",
            404 => "Not Found",
            500 => "Internal Server Error",
            _ => "OK"
        };

        var header = new StringBuilder();
        header.Append($"HTTP/1.1 {statusCode} {statusText}\r\n");
        header.Append($"Content-Type: {contentType}\r\n");
        header.Append($"Content-Length: {body.Length}\r\n");
        header.Append("Access-Control-Allow-Origin: *\r\n");
        header.Append("Access-Control-Allow-Methods: GET, POST, OPTIONS\r\n");
        header.Append("Access-Control-Allow-Headers: Content-Type\r\n");
        header.Append("Connection: close\r\n");
        header.Append("\r\n");

        byte[] headerBytes = Encoding.UTF8.GetBytes(header.ToString());
        await stream.WriteAsync(headerBytes, 0, headerBytes.Length);
        if (body.Length > 0)
        {
            await stream.WriteAsync(body, 0, body.Length);
        }
        await stream.FlushAsync();
    }

    private async Task<byte[]> GetProductsJsonAsync()
    {
        using var db = new AppDbContext();
        var products = await db.Products
            .Where(p => p.IsActive)
            .OrderBy(p => p.Name)
            .Select(p => new
            {
                id = p.Id,
                name = p.Name,
                barcode = p.Barcode,
                price = p.Price,
                wholesalePrice = p.WholesalePrice,
                cartonPrice = p.CartonSellingPrice,
                stock = p.StockQuantity,
                unit = p.Unit,
                itemsPerCarton = p.ItemsPerCarton
            })
            .ToListAsync();

        string json = JsonSerializer.Serialize(products);
        return Encoding.UTF8.GetBytes(json);
    }

    private async Task<byte[]> GetOrdersJsonAsync(string repName)
    {
        using var db = new AppDbContext();
        var query = db.SupplierOrders.Include(o => o.Items).AsQueryable();

        if (!string.IsNullOrWhiteSpace(repName))
        {
            query = query.Where(o => o.RepresentativeName == repName);
        }

        var orders = await query
            .OrderByDescending(o => o.OrderDate)
            .Take(30)
            .Select(o => new
            {
                id = o.Id,
                orderNumber = o.OrderNumber,
                orderDate = o.OrderDate.ToString("yyyy/MM/dd HH:mm"),
                marketName = o.MarketName,
                marketPhone = o.MarketPhone,
                marketAddress = o.MarketAddress,
                repName = o.RepresentativeName,
                status = o.Status.ToString(),
                totalAmount = o.TotalAmount,
                notes = o.Notes,
                itemsCount = o.Items.Count
            })
            .ToListAsync();

        string json = JsonSerializer.Serialize(orders);
        return Encoding.UTF8.GetBytes(json);
    }

    public class CreateOrderDto
    {
        public string MarketName { get; set; } = "";
        public string? MarketPhone { get; set; }
        public string? MarketAddress { get; set; }
        public string RepresentativeName { get; set; } = "";
        public string? Notes { get; set; }
        public List<CreateOrderItemDto> Items { get; set; } = new();
    }

    public class CreateOrderItemDto
    {
        public Guid? ProductId { get; set; }
        public string ProductName { get; set; } = "";
        public string Barcode { get; set; } = "";
        public decimal Quantity { get; set; }
        public string UnitType { get; set; } = "Retail";
        public decimal UnitPrice { get; set; }
    }

    private async Task<byte[]> SaveOrderFromJsonAsync(string body)
    {
        var dto = JsonSerializer.Deserialize<CreateOrderDto>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (dto == null || string.IsNullOrWhiteSpace(dto.MarketName) || !dto.Items.Any())
        {
            return Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { error = "بيانات الطلبية غير مكتملة" }));
        }

        using var db = new AppDbContext();
        string orderNum = $"ORD-{DateTime.Now:yyyyMMdd}-{new Random().Next(100, 999)}";

        var order = new SupplierOrder
        {
            OrderNumber = orderNum,
            OrderDate = DateTime.Now,
            MarketName = dto.MarketName.Trim(),
            MarketPhone = dto.MarketPhone?.Trim(),
            MarketAddress = dto.MarketAddress?.Trim(),
            RepresentativeName = string.IsNullOrWhiteSpace(dto.RepresentativeName) ? "مندوب الموبايل" : dto.RepresentativeName.Trim(),
            SupplierName = "مندوب خارجي",
            Status = OrderStatus.Pending,
            Notes = dto.Notes?.Trim(),
            TotalAmount = dto.Items.Sum(i => i.Quantity * i.UnitPrice),
            Items = dto.Items.Select(i => new SupplierOrderItem
            {
                ProductId = i.ProductId,
                ProductName = i.ProductName,
                Barcode = i.Barcode,
                Quantity = i.Quantity,
                UnitType = i.UnitType,
                UnitPrice = i.UnitPrice
            }).ToList()
        };

        db.SupplierOrders.Add(order);
        await db.SaveChangesAsync();

        OrderReceived?.Invoke();

        return Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { success = true, orderNumber = orderNum }));
    }

    private string GetEmbeddedMobileAppHtml()
    {
        return @"<!DOCTYPE html>
<html lang=""ar"" dir=""rtl"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0, maximum-scale=1.0, user-scalable=no"">
    <title>بوابة المندوب - 7amo POS</title>
    <style>
        :root {
            --primary: #0284c7;
            --primary-hover: #0369a1;
            --success: #10b981;
            --bg-dark: #0f172a;
            --bg-card: #1e293b;
            --text-main: #f8fafc;
            --text-sec: #94a3b8;
            --border: #334155;
        }
        * { box-sizing: border-box; margin: 0; padding: 0; font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; }
        body { background: var(--bg-dark); color: var(--text-main); padding-bottom: 80px; -webkit-tap-highlight-color: transparent; }
        .header { background: var(--bg-card); padding: 16px; border-bottom: 1px solid var(--border); position: sticky; top: 0; z-index: 100; display: flex; justify-content: space-between; align-items: center; }
        .brand { font-size: 18px; font-weight: 900; color: #38bdf8; display: flex; align-items: center; gap: 8px; }
        .rep-badge { font-size: 12px; background: #0c2548; color: #38bdf8; padding: 4px 10px; border-radius: 20px; font-weight: bold; border: 1px solid #1e3a8a; }
        
        .container { padding: 14px; max-width: 600px; margin: auto; }
        
        .tabs { display: flex; gap: 8px; margin-bottom: 14px; background: var(--bg-card); padding: 4px; border-radius: 12px; }
        .tab-btn { flex: 1; padding: 10px 0; border: none; background: transparent; color: var(--text-sec); font-size: 13px; font-weight: bold; border-radius: 8px; cursor: pointer; transition: 0.2s; }
        .tab-btn.active { background: var(--primary); color: white; }

        .card { background: var(--bg-card); border-radius: 14px; padding: 14px; margin-bottom: 12px; border: 1px solid var(--border); }
        .card-title { font-size: 14px; font-weight: bold; color: #38bdf8; margin-bottom: 10px; display: flex; align-items: center; gap: 6px; }

        input, select, textarea { width: 100%; background: #0b132b; border: 1px solid var(--border); color: white; padding: 11px 12px; border-radius: 10px; font-size: 14px; margin-bottom: 10px; outline: none; }
        input:focus, select:focus, textarea:focus { border-color: var(--primary); }

        .search-bar { position: sticky; top: 68px; z-index: 90; margin-bottom: 12px; }
        
        .product-item { display: flex; justify-content: space-between; align-items: center; padding: 12px; border-bottom: 1px solid #334155; }
        .product-item:last-child { border-bottom: none; }
        .product-name { font-size: 14px; font-weight: bold; margin-bottom: 4px; }
        .product-stock { font-size: 11px; color: var(--success); }
        .product-price { font-size: 13px; color: #fbbf24; font-weight: bold; }
        
        .btn-add { background: var(--primary); color: white; border: none; padding: 8px 14px; border-radius: 8px; font-weight: bold; font-size: 12px; cursor: pointer; }
        .btn-submit { width: 100%; background: var(--success); color: white; border: none; padding: 14px; border-radius: 12px; font-size: 15px; font-weight: 900; cursor: pointer; display: flex; justify-content: center; align-items: center; gap: 8px; }

        .cart-bar { position: fixed; bottom: 0; left: 0; right: 0; background: #0c1b33; border-top: 1px solid #1e3a8a; padding: 12px 16px; display: flex; justify-content: space-between; align-items: center; z-index: 100; max-width: 600px; margin: auto; }
        .badge { background: #ef4444; color: white; border-radius: 10px; padding: 2px 7px; font-size: 11px; font-weight: bold; }

        .order-card { border-right: 4px solid var(--primary); padding: 12px; margin-bottom: 10px; background: var(--bg-card); border-radius: 8px; border: 1px solid var(--border); }
        .order-status { font-size: 11px; font-weight: bold; padding: 3px 8px; border-radius: 6px; }
        .status-Pending { background: #854d0e; color: #fef08a; }
        .status-InPreparation { background: #1e40af; color: #93c5fd; }
        .status-Delivered { background: #065f46; color: #6ee7b7; }
    </style>
</head>
<body>

    <div class=""header"">
        <div class=""brand"">
            <span>📦</span> 7amo POS
        </div>
        <div class=""rep-badge"" id=""repBadge"" onclick=""changeRepName()"">👤 مندوب الماركتات</div>
    </div>

    <div class=""container"">
        <div class=""tabs"">
            <button class=""tab-btn active"" onclick=""switchTab('order')"">📝 إنشاء طلبية</button>
            <button class=""tab-btn"" onclick=""switchTab('catalog')"">📦 تصفح المخزن</button>
            <button class=""tab-btn"" onclick=""switchTab('myOrders')"">📋 طلبياتي</button>
        </div>

        <!-- TAB 1: ORDER CREATOR -->
        <div id=""tabOrder"">
            <div class=""card"">
                <div class=""card-title"">🏪 بيانات الماركت / العميل</div>
                <input type=""text"" id=""marketName"" placeholder=""اسم الماركت أو المحل *"" required>
                <input type=""tel"" id=""marketPhone"" placeholder=""رقم هاتف الماركت"">
                <input type=""text"" id=""marketAddress"" placeholder=""العنوان / المنطقة"">
                <textarea id=""orderNotes"" rows=""2"" placeholder=""ملاحظات التوصيل أو وقت الاستلام""></textarea>
            </div>

            <div class=""card"">
                <div class=""card-title"">🛒 المواد المضافة للطلبية (<span id=""cartCount"">0</span>)</div>
                <div id=""cartItemsList"">
                    <p style=""color: var(--text-sec); font-size: 12px; text-align: center; padding: 10px;"">لم تتم إضافة أي مادة بعد. اختر من قائمة المواد بالأسفل.</p>
                </div>
                <div style=""display: flex; justify-content: space-between; margin-top: 10px; font-size: 15px; font-weight: bold;"">
                    <span>الإجمالي التقديري:</span>
                    <span id=""cartGrandTotal"" style=""color: var(--success);"">0 د.ع</span>
                </div>
            </div>

            <button class=""btn-submit"" onclick=""submitOrder()"">🚀 إرسال الطلبية للمحل الآن</button>

            <!-- Quick Add Catalog Section -->
            <div class=""card"" style=""margin-top: 14px;"">
                <div class=""card-title"">🔍 أضف مواد من المخزن</div>
                <input type=""text"" id=""quickSearch"" placeholder=""ابحث عن مادة أو باركود..."" oninput=""filterProducts(this.value)"">
                <div id=""productsList""></div>
            </div>
        </div>

        <!-- TAB 2: CATALOG ONLY -->
        <div id=""tabCatalog"" style=""display: none;"">
            <div class=""card"">
                <div class=""card-title"">📦 المواد المتوفرة في المخزن</div>
                <input type=""text"" placeholder=""ابحث عن أي مادة..."" oninput=""filterProducts(this.value, 'catalogList')"">
                <div id=""catalogList""></div>
            </div>
        </div>

        <!-- TAB 3: REP'S ORDERS -->
        <div id=""tabMyOrders"" style=""display: none;"">
            <div class=""card"">
                <div class=""card-title"">📋 سجل الطلبيات التي أرسلتها</div>
                <button onclick=""loadMyOrders()"" style=""background: #1e3a8a; color: white; border: none; padding: 6px 12px; border-radius: 6px; font-size: 11px; font-weight: bold; margin-bottom: 10px; cursor: pointer;"">🔄 تحديث الحالة</button>
                <div id=""myOrdersList"">
                    <p style=""color: var(--text-sec); text-align: center; font-size: 12px;"">جارٍ جلب الطلبيات...</p>
                </div>
            </div>
        </div>
    </div>

    <script>
        let allProducts = [];
        let cart = [];
        let currentRep = localStorage.getItem('hamo_rep_name') || 'مندوب المبيعات';

        document.getElementById('repBadge').innerText = '👤 ' + currentRep;

        function changeRepName() {
            let name = prompt('أدخل اسمك كمندوب:', currentRep);
            if (name && name.trim()) {
                currentRep = name.trim();
                localStorage.setItem('hamo_rep_name', currentRep);
                document.getElementById('repBadge').innerText = '👤 ' + currentRep;
            }
        }

        async function init() {
            try {
                let res = await fetch('/api/products');
                allProducts = await res.json();
                renderProducts(allProducts, 'productsList', true);
                renderProducts(allProducts, 'catalogList', false);
            } catch (e) {
                console.error(e);
            }
        }

        function renderProducts(list, containerId, isOrderTab) {
            let container = document.getElementById(containerId);
            if (!container) return;
            if (!list.length) {
                container.innerHTML = '<p style=""color: var(--text-sec); font-size: 12px; text-align: center; padding: 10px;"">لا توجد مواد مطابقة</p>';
                return;
            }
            container.innerHTML = list.slice(0, 40).map(p => `
                <div class=""product-item"">
                    <div>
                        <div class=""product-name"">${p.name}</div>
                        <div class=""product-stock"">متوفر: ${p.stock} ${p.unit}</div>
                        <div class=""product-price"">${p.price.toLocaleString()} د.ع</div>
                    </div>
                    ${isOrderTab ? `<button class=""btn-add"" onclick=""addToCart('${p.id}')"">➕ إضافة</button>` : ''}
                </div>
            `).join('');
        }

        function filterProducts(query, targetId = 'productsList') {
            let q = query.trim().toLowerCase();
            let filtered = allProducts.filter(p => p.name.toLowerCase().includes(q) || (p.barcode && p.barcode.includes(q)));
            renderProducts(filtered, targetId, targetId === 'productsList');
        }

        function addToCart(productId) {
            let product = allProducts.find(p => p.id === productId);
            if (!product) return;

            let existing = cart.find(c => c.productId === productId);
            if (existing) {
                existing.quantity += 1;
            } else {
                cart.push({
                    productId: product.id,
                    productName: product.name,
                    barcode: product.barcode,
                    quantity: 1,
                    unitType: 'Retail',
                    unitPrice: product.price
                });
            }
            renderCart();
        }

        function renderCart() {
            let list = document.getElementById('cartItemsList');
            document.getElementById('cartCount').innerText = cart.length;
            if (!cart.length) {
                list.innerHTML = '<p style=""color: var(--text-sec); font-size: 12px; text-align: center; padding: 10px;"">لم تتم إضافة أي مادة بعد.</p>';
                document.getElementById('cartGrandTotal').innerText = '0 د.ع';
                return;
            }

            let total = 0;
            list.innerHTML = cart.map((item, idx) => {
                let itemTotal = item.quantity * item.unitPrice;
                total += itemTotal;
                return `
                    <div style=""display: flex; justify-content: space-between; align-items: center; padding: 8px 0; border-bottom: 1px solid #334155;"">
                        <div>
                            <div style=""font-weight: bold; font-size: 13px;"">${item.productName}</div>
                            <div style=""font-size: 11px; color: var(--text-sec);"">${item.unitPrice.toLocaleString()} د.ع × ${item.quantity} = ${itemTotal.toLocaleString()} د.ع</div>
                        </div>
                        <div style=""display: flex; align-items: center; gap: 6px;"">
                            <button onclick=""updateQty(${idx}, -1)"" style=""background: #334155; color: white; border: none; width: 26px; height: 26px; border-radius: 6px; font-weight: bold;"">-</button>
                            <span style=""font-weight: bold; font-size: 13px;"">${item.quantity}</span>
                            <button onclick=""updateQty(${idx}, 1)"" style=""background: #334155; color: white; border: none; width: 26px; height: 26px; border-radius: 6px; font-weight: bold;"">+</button>
                            <button onclick=""removeItem(${idx})"" style=""background: transparent; border: none; font-size: 14px; cursor: pointer; margin-right: 4px;"">🗑️</button>
                        </div>
                    </div>
                `;
            }).join('');

            document.getElementById('cartGrandTotal').innerText = total.toLocaleString() + ' د.ع';
        }

        function updateQty(idx, change) {
            cart[idx].quantity += change;
            if (cart[idx].quantity <= 0) cart.splice(idx, 1);
            renderCart();
        }

        function removeItem(idx) {
            cart.splice(idx, 1);
            renderCart();
        }

        async function submitOrder() {
            let market = document.getElementById('marketName').value.trim();
            if (!market) {
                alert('يرجى كتابة اسم الماركت أو المحل!');
                return;
            }
            if (!cart.length) {
                alert('يرجى إضافة مادة واحدة على الأقل في الطلبية!');
                return;
            }

            let payload = {
                marketName: market,
                marketPhone: document.getElementById('marketPhone').value.trim(),
                marketAddress: document.getElementById('marketAddress').value.trim(),
                representativeName: currentRep,
                notes: document.getElementById('orderNotes').value.trim(),
                items: cart
            };

            try {
                let res = await fetch('/api/orders', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify(payload)
                });
                let data = await res.json();
                if (data.success) {
                    alert('✔ تم إرسال الطلبية بنجاح برقم: ' + data.orderNumber);
                    cart = [];
                    renderCart();
                    document.getElementById('marketName').value = '';
                    document.getElementById('marketPhone').value = '';
                    document.getElementById('marketAddress').value = '';
                    document.getElementById('orderNotes').value = '';
                    switchTab('myOrders');
                } else {
                    alert('خطأ: ' + (data.error || 'فشل الإرسال'));
                }
            } catch (e) {
                alert('تعذر الاتصال بسيرفر المحل. تأكد من الاتصال بنفس الواي فاي.');
            }
        }

        async function loadMyOrders() {
            let list = document.getElementById('myOrdersList');
            try {
                let res = await fetch('/api/orders?rep=' + encodeURIComponent(currentRep));
                let orders = await res.json();
                if (!orders.length) {
                    list.innerHTML = '<p style=""color: var(--text-sec); text-align: center; font-size: 12px; padding: 10px;"">لا توجد طلبيات مسجلة لك بعد.</p>';
                    return;
                }
                list.innerHTML = orders.map(o => `
                    <div class=""order-card"">
                        <div style=""display: flex; justify-content: space-between; align-items: center; margin-bottom: 6px;"">
                            <span style=""font-weight: bold; font-size: 13px;"">${o.marketName}</span>
                            <span class=""order-status status-${o.status}"">${o.status === 'Pending' ? '⏳ قيد الانتظار' : (o.status === 'InPreparation' ? '📦 جاري التجهيز' : '✔ تم التوصيل')}</span>
                        </div>
                        <div style=""font-size: 11px; color: var(--text-sec); display: flex; justify-content: space-between;"">
                            <span>رقم: ${o.orderNumber} (${o.itemsCount} مواد)</span>
                            <span style=""color: var(--success); font-weight: bold;"">${o.totalAmount.toLocaleString()} د.ع</span>
                        </div>
                    </div>
                `).join('');
            } catch (e) {
                list.innerHTML = '<p style=""color: #ef4444; font-size: 12px; text-align: center;"">فشل جلب الطلبيات.</p>';
            }
        }

        function switchTab(tab) {
            document.querySelectorAll('.tab-btn').forEach(b => b.classList.remove('active'));
            document.getElementById('tabOrder').style.display = tab === 'order' ? 'block' : 'none';
            document.getElementById('tabCatalog').style.display = tab === 'catalog' ? 'block' : 'none';
            document.getElementById('tabMyOrders').style.display = tab === 'myOrders' ? 'block' : 'none';

            if (tab === 'order') event.target.classList.add('active');
            if (tab === 'catalog') {
                event.target.classList.add('active');
            }
            if (tab === 'myOrders') {
                event.target.classList.add('active');
                loadMyOrders();
            }
        }

        init();
    </script>
</body>
</html>";
    }
}

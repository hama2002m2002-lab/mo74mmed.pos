// ========================================================
// 7amo.pos Next-Gen App State & Quixotic Core Logic
// ========================================================

const state = {
  activeTab: 'dashboard',
  theme: localStorage.getItem('pos_theme') || 'light',
  language: 'ar',
  invoiceTabs: [
    { id: 1, title: 'فاتورة 1', items: [], discount: 0, paid: 0, paymentMethod: 'Cash' }
  ],
  selectedInvoiceTabId: 1,
  products: [],
  suppliers: [],
  weeklyChart: null,
  paymentChart: null
};

// C# Native Bridge Interface
async function callBackend(action, payload = {}) {
  return new Promise((resolve) => {
    if (window.chrome && window.chrome.webview) {
      const callbackId = 'cb_' + Date.now() + '_' + Math.random().toString(36).substr(2, 5);
      
      const handler = (event) => {
        try {
          const data = typeof event.data === 'string' ? JSON.parse(event.data) : event.data;
          if (data && data._callbackId === callbackId) {
            window.chrome.webview.removeEventListener('message', handler);
            resolve(data.result);
          }
        } catch (e) {
          resolve({ success: false, error: e.message });
        }
      };

      window.chrome.webview.addEventListener('message', handler);
      window.chrome.webview.postMessage({ action, payload: JSON.stringify(payload), _callbackId: callbackId });
    } else {
      console.log(`[C# Bridge Call] Action: ${action}`, payload);
      resolve({ success: true, message: "Browser Mode" });
    }
  });
}

// ========================================================
// INITIALIZATION
// ========================================================
document.addEventListener('DOMContentLoaded', async () => {
  applyTheme(state.theme);
  lucide.createIcons();
  startClock();
  setupGlobalKeyboardShortcuts();
  
  await loadProducts();
  await loadSuppliersList();
  renderInvoiceTabs();
  renderCashierCart();
  await loadDashboard();
  await loadRepOrders();
  
  setInterval(loadRepOrders, 4000); // 4s auto poll
});

function startClock() {
  const update = () => {
    const clock = document.getElementById('liveClock');
    if (clock) clock.innerText = new Date().toLocaleTimeString('ar-IQ');
  };
  update();
  setInterval(update, 1000);
}

function setupGlobalKeyboardShortcuts() {
  window.addEventListener('keydown', (e) => {
    if (e.key === 'F12') {
      e.preventDefault();
      submitCashierSale();
    } else if (e.key === 'F1') {
      e.preventDefault();
      switchTab('dashboard');
    } else if (e.key === 'F2') {
      e.preventDefault();
      switchTab('cashier');
    } else if (e.key === 'F3') {
      e.preventDefault();
      switchTab('addProduct');
    }
  });
}

// ========================================================
// TAB NAVIGATION (TOP PILL BAR & FLOATING RAIL DOCK)
// ========================================================
function switchTab(tabId) {
  state.activeTab = tabId;

  // 1. Hide all views
  document.querySelectorAll('.tab-content').forEach(el => el.classList.add('hidden'));

  // 2. Reset Top Navigation Pills
  document.querySelectorAll('.top-nav-btn').forEach(el => {
    el.classList.remove('bg-emerald-600', 'text-white', 'shadow-sm');
    el.classList.add('text-slate-500');
  });

  // 3. Reset Floating Rail Dock
  document.querySelectorAll('.rail-btn').forEach(el => {
    el.classList.remove('bg-emerald-600', 'text-white', 'shadow-md');
    el.classList.add('text-slate-400');
  });

  // 4. Activate Selected View
  const tabEl = document.getElementById(`tab-${tabId}`);
  if (tabEl) tabEl.classList.remove('hidden');

  const topBtn = document.getElementById(`top-nav-${tabId}`);
  if (topBtn) {
    topBtn.classList.add('bg-emerald-600', 'text-white', 'shadow-sm');
    topBtn.classList.remove('text-slate-500');
  }

  const railBtn = document.getElementById(`rail-${tabId}`);
  if (railBtn) {
    railBtn.classList.add('bg-emerald-600', 'text-white', 'shadow-md');
    railBtn.classList.remove('text-slate-400');
  }

  // Focus Barcode on Cashier
  if (tabId === 'cashier') {
    document.getElementById('cashierBarcodeInput')?.focus();
  }

  // Refresh View Data
  if (tabId === 'dashboard') loadDashboard();
  if (tabId === 'inventory') loadInventory();
  if (tabId === 'repOrders') loadRepOrders();
  if (tabId === 'suppliers') loadSuppliers();
  if (tabId === 'users') loadUsers();

  lucide.createIcons();
}

// ========================================================
// THEME SWITCHER (DAY / LIGHT <-> NIGHT / DARK)
// ========================================================
function toggleTheme() {
  state.theme = state.theme === 'dark' ? 'light' : 'dark';
  localStorage.setItem('pos_theme', state.theme);
  applyTheme(state.theme);
  if (state.activeTab === 'dashboard') {
    loadDashboard();
  }
}

function applyTheme(theme) {
  const icon = document.getElementById('themeIcon');
  if (theme === 'dark') {
    document.body.classList.add('dark-theme');
    if (icon) icon.innerText = '☀️';
  } else {
    document.body.classList.remove('dark-theme');
    if (icon) icon.innerText = '🌙';
  }
}

// ========================================================
// DASHBOARD LOGIC (QUIXOTIC STATS & CHARTS)
// ========================================================
async function loadDashboard() {
  const res = await callBackend('get_dashboard_data');
  if (!res || !res.success) return;

  // 1. KPI Numbers
  document.getElementById('kpiTodayRevenue').innerText = Number(res.todayRevenue || 0).toLocaleString() + ' د.ع';
  document.getElementById('kpiTodayInvoices').innerText = Number(res.todayInvoices || 0).toLocaleString() + ' فاتورة';
  document.getElementById('kpiMonthlyRevenue').innerText = Number(res.monthlyRevenue || 0).toLocaleString() + ' د.ع';
  document.getElementById('kpiLowStockCount').innerText = Number(res.lowStockCount || 0).toLocaleString();
  
  const dailyAvg = Math.round((res.monthlyRevenue || 0) / 30);
  const avgEl = document.getElementById('kpiDailyAvg');
  if (avgEl) avgEl.innerText = Number(dailyAvg).toLocaleString() + ' د.ع';

  // 2. Quixotic Striped & Highlighted Weekly Chart
  renderQuixoticWeeklyChart(res.weeklyTrend || []);

  // 3. Quixotic Donut Payment Chart
  renderQuixoticPaymentChart(res.payments || { cash: 0, card: 0, debt: 0 });

  // 4. Recent Sales History Table
  renderRecentSalesTable(res.recentSales || []);
}

function renderQuixoticWeeklyChart(data) {
  const ctx = document.getElementById('weeklyChart');
  if (!ctx) return;

  if (state.weeklyChart) state.weeklyChart.destroy();

  const isDark = state.theme === 'dark';
  const labels = data.map(d => d.dayName);
  const values = data.map(d => d.revenue);
  const maxVal = Math.max(...values, 1);

  // Peak bar gets solid dark emerald green (#065F46 / #059669), other bars get soft striped/translucent green
  const barColors = values.map(v => (v === maxVal && v > 0) ? '#059669' : (isDark ? 'rgba(16, 185, 129, 0.35)' : 'rgba(5, 150, 105, 0.38)'));

  state.weeklyChart = new Chart(ctx, {
    type: 'bar',
    data: {
      labels: labels,
      datasets: [{
        data: values,
        backgroundColor: barColors,
        borderRadius: 20,
        borderSkipped: false,
        barPercentage: 0.55
      }]
    },
    options: {
      responsive: true,
      maintainAspectRatio: false,
      plugins: { legend: { display: false } },
      scales: {
        y: { 
          grid: { color: isDark ? 'rgba(255,255,255,0.04)' : 'rgba(0,0,0,0.04)' }, 
          ticks: { color: isDark ? '#94A3B8' : '#6B7280', font: { family: 'Plus Jakarta Sans', size: 10 } } 
        },
        x: { 
          grid: { display: false }, 
          ticks: { color: isDark ? '#94A3B8' : '#6B7280', font: { family: 'Cairo', weight: 'bold', size: 11 } } 
        }
      }
    }
  });
}

function renderQuixoticPaymentChart(payments) {
  const ctx = document.getElementById('paymentChart');
  if (!ctx) return;

  if (state.paymentChart) state.paymentChart.destroy();

  state.paymentChart = new Chart(ctx, {
    type: 'doughnut',
    data: {
      labels: ['نقداً', 'بطاقة', 'آجل'],
      datasets: [{
        data: [payments.cash || 0, payments.card || 0, payments.debt || 0],
        backgroundColor: ['#059669', '#3B82F6', '#F59E0B'],
        borderWidth: 0
      }]
    },
    options: {
      responsive: true,
      maintainAspectRatio: false,
      cutout: '76%',
      plugins: { legend: { display: false } }
    }
  });
}

function renderRecentSalesTable(sales) {
  const tbody = document.getElementById('dashRecentSalesTbody');
  if (!tbody) return;

  if (sales.length === 0) {
    tbody.innerHTML = '<tr><td colspan="5" class="py-8 text-center text-slate-400">لا توجد مبيعات مسجلة حتى الآن</td></tr>';
    return;
  }

  tbody.innerHTML = '';
  sales.forEach(s => {
    const tr = document.createElement('tr');
    tr.className = 'hover:bg-slate-50 dark:hover:bg-slate-800/40';
    tr.innerHTML = `
      <td class="py-3.5 font-bold font-mono text-emerald-600 dark:text-emerald-400">${s.InvoiceNumber}</td>
      <td class="py-3.5 text-slate-500 dark:text-slate-400">${s.createdAt}</td>
      <td class="py-3.5"><span class="px-2.5 py-1 rounded-full text-[11px] font-bold ${s.PaymentMethod === 'Cash' ? 'bg-emerald-100 text-emerald-700 dark:bg-emerald-950/60 dark:text-emerald-400' : 'bg-blue-100 text-blue-700 dark:bg-blue-950/60 dark:text-blue-400'}">${s.PaymentMethod === 'Cash' ? '💵 نقداً' : s.PaymentMethod}</span></td>
      <td class="py-3.5"><span class="text-emerald-600 font-bold">● مكتمل</span></td>
      <td class="py-3.5 text-left font-black text-slate-900 dark:text-white">${Number(s.TotalAmount).toLocaleString()} د.ع</td>
    `;
    tbody.appendChild(tr);
  });
}

// ========================================================
// CASHIER / POS (MULTI-TABS & RAPID CHECKOUT)
// ========================================================
function getCurrentTab() {
  return state.invoiceTabs.find(t => t.id === state.selectedInvoiceTabId) || state.invoiceTabs[0];
}

function renderInvoiceTabs() {
  const container = document.getElementById('invoiceTabsContainer');
  if (!container) return;

  container.innerHTML = '';
  state.invoiceTabs.forEach(t => {
    const isSel = t.id === state.selectedInvoiceTabId;
    const tabEl = document.createElement('div');
    tabEl.className = `q-pill-btn flex items-center gap-2 px-4 py-2 cursor-pointer text-xs font-bold transition shadow-sm ${isSel ? 'bg-emerald-600 text-white shadow-md' : 'q-card text-slate-600 dark:text-slate-300 hover:border-emerald-500'}`;
    tabEl.onclick = () => selectInvoiceTab(t.id);
    tabEl.innerHTML = `
      <span>${t.title}</span>
      <span class="bg-white/20 px-1.5 py-0.2 rounded-full text-[10px]">${t.items.length}</span>
      ${state.invoiceTabs.length > 1 ? `<button onclick="event.stopPropagation(); closeInvoiceTab(${t.id})" class="text-rose-200 hover:text-white px-1 text-xs">✕</button>` : ''}
    `;
    container.appendChild(tabEl);
  });
}

function addNewInvoiceTab() {
  const newId = (state.invoiceTabs.length > 0 ? Math.max(...state.invoiceTabs.map(t => t.id)) : 0) + 1;
  state.invoiceTabs.push({
    id: newId,
    title: `فاتورة ${newId}`,
    items: [],
    discount: 0,
    paid: 0,
    paymentMethod: 'Cash'
  });
  selectInvoiceTab(newId);
}

function selectInvoiceTab(id) {
  state.selectedInvoiceTabId = id;
  renderInvoiceTabs();
  renderCashierCart();
  document.getElementById('cashierBarcodeInput')?.focus();
}

function closeInvoiceTab(id) {
  if (state.invoiceTabs.length <= 1) return;
  state.invoiceTabs = state.invoiceTabs.filter(t => t.id !== id);
  if (state.selectedInvoiceTabId === id) {
    state.selectedInvoiceTabId = state.invoiceTabs[0].id;
  }
  renderInvoiceTabs();
  renderCashierCart();
}

function handleBarcodeKeyDown(e) {
  if (e.key === 'Enter') {
    const input = document.getElementById('cashierBarcodeInput');
    const query = input.value.trim();
    if (!query) return;

    const matched = state.products.find(p => p.barcode === query || p.name.toLowerCase() === query.toLowerCase());
    if (matched) {
      addItemToCurrentCart(matched);
      input.value = '';
    } else {
      alert(`لم يتم العثور على مادة بالباركود: ${query}`);
    }
  }
}

function addItemToCurrentCart(product) {
  const currentTab = getCurrentTab();
  const existing = currentTab.items.find(i => i.id === product.id);

  if (existing) {
    existing.qty += 1;
  } else {
    currentTab.items.push({
      id: product.id,
      name: product.name,
      barcode: product.barcode,
      price: product.price,
      cost: product.cost,
      qty: 1
    });
  }

  renderInvoiceTabs();
  renderCashierCart();
}

function updateCartItemQty(id, delta) {
  const currentTab = getCurrentTab();
  const item = currentTab.items.find(i => i.id === id);
  if (item) {
    item.qty += delta;
    if (item.qty <= 0) {
      currentTab.items = currentTab.items.filter(i => i.id !== id);
    }
  }
  renderInvoiceTabs();
  renderCashierCart();
}

function removeCartItem(id) {
  const currentTab = getCurrentTab();
  currentTab.items = currentTab.items.filter(i => i.id !== id);
  renderInvoiceTabs();
  renderCashierCart();
}

function renderCashierCart() {
  const currentTab = getCurrentTab();
  const tbody = document.getElementById('cashierCartTbody');
  if (!tbody) return;

  if (currentTab.items.length === 0) {
    tbody.innerHTML = '<tr><td colspan="6" class="text-center py-16 text-slate-400">لا توجد مواد في هذه الفاتورة. امسح الباركود للبدء.</td></tr>';
    recalcCashierInvoice();
    return;
  }

  tbody.innerHTML = '';
  currentTab.items.forEach((item, index) => {
    const tr = document.createElement('tr');
    tr.className = 'hover:bg-slate-50 dark:hover:bg-slate-800/40';
    tr.innerHTML = `
      <td class="p-3.5 font-bold text-slate-400">${index + 1}</td>
      <td class="p-3.5 font-bold">${item.name}</td>
      <td class="p-3.5 font-bold text-emerald-600 dark:text-emerald-400">${Number(item.price).toLocaleString()} د.ع</td>
      <td class="p-3.5 text-center">
        <div class="inline-flex items-center gap-2 q-card px-2.5 py-1 rounded-xl shadow-sm">
          <button onclick="updateCartItemQty('${item.id}', -1)" class="w-6 h-6 rounded-lg bg-slate-100 dark:bg-slate-800 hover:bg-rose-100 text-rose-600 font-bold">-</button>
          <span class="font-bold px-2">${item.qty}</span>
          <button onclick="updateCartItemQty('${item.id}', 1)" class="w-6 h-6 rounded-lg bg-slate-100 dark:bg-slate-800 hover:bg-emerald-100 text-emerald-600 font-bold">+</button>
        </div>
      </td>
      <td class="p-3.5 font-black text-slate-900 dark:text-white">${Number(item.price * item.qty).toLocaleString()} د.ع</td>
      <td class="p-3.5 text-center">
        <button onclick="removeCartItem('${item.id}')" class="text-rose-500 hover:text-rose-600 font-bold p-1 text-xs">🗑</button>
      </td>
    `;
    tbody.appendChild(tr);
  });

  recalcCashierInvoice();
}

function recalcCashierInvoice() {
  const currentTab = getCurrentTab();
  const subtotal = currentTab.items.reduce((sum, i) => sum + (i.price * i.qty), 0);
  const discount = Number(document.getElementById('cashierDiscountInput')?.value || 0);
  const paid = Number(document.getElementById('cashierPaidInput')?.value || 0);

  const total = Math.max(0, subtotal - discount);
  const change = Math.max(0, paid - total);

  document.getElementById('cashierSubtotal').innerText = Number(subtotal).toLocaleString() + ' د.ع';
  document.getElementById('cashierTotalDisplay').innerText = Number(total).toLocaleString() + ' د.ع';
  document.getElementById('cashierChangeDisplay').innerText = Number(change).toLocaleString() + ' د.ع';
}

function setPaidAmount(amt) {
  const paidInput = document.getElementById('cashierPaidInput');
  if (paidInput) {
    paidInput.value = amt;
    recalcCashierInvoice();
  }
}

function setPaymentMethod(pm) {
  const currentTab = getCurrentTab();
  currentTab.paymentMethod = pm;
  ['Cash', 'Card', 'Debt'].forEach(m => {
    const btn = document.getElementById(`pm-${m}`);
    if (btn) {
      if (m === pm) {
        btn.className = 'flex-1 py-2 bg-emerald-600 text-white font-bold text-xs rounded-xl shadow-md';
      } else {
        btn.className = 'flex-1 py-2 q-card text-slate-600 dark:text-slate-300 font-bold text-xs rounded-xl';
      }
    }
  });
}

async function submitCashierSale() {
  const currentTab = getCurrentTab();
  if (currentTab.items.length === 0) {
    alert('الفاتورة فارغة!');
    return;
  }

  const discount = Number(document.getElementById('cashierDiscountInput')?.value || 0);
  const payload = {
    paymentMethod: currentTab.paymentMethod || 'Cash',
    discount: discount,
    items: currentTab.items
  };

  const res = await callBackend('complete_sale', payload);
  if (res && res.success) {
    alert(`✔ تم إتمام وحفظ الوصل بنجاح!\nرقم الفاتورة: ${res.invoiceNumber}\nالمبلغ المطلوب: ${Number(res.total).toLocaleString()} د.ع`);
    currentTab.items = [];
    document.getElementById('cashierDiscountInput').value = 0;
    document.getElementById('cashierPaidInput').value = 0;
    renderInvoiceTabs();
    renderCashierCart();
    loadDashboard();
  }
}

// ========================================================
// ADD PRODUCT & REPS LOGIC
// ========================================================
async function loadProducts() {
  const res = await callBackend('get_pos_products');
  if (res && res.success) {
    state.products = res.products || [];
  }
}

async function loadSuppliersList() {
  const res = await callBackend('get_suppliers');
  if (res && res.success) {
    state.suppliers = res.suppliers || [];
    const supSelect = document.getElementById('ap-supplier');
    if (supSelect) {
      supSelect.innerHTML = '<option value="">بدون مندوب (مباشر)</option>';
      state.suppliers.forEach(s => {
        supSelect.innerHTML += `<option value="${s.name}">${s.name} (${s.company || 'شركة'})</option>`;
      });
    }
  }
}

function generateRandomBarcode() {
  const barcodeInput = document.getElementById('ap-barcode');
  if (barcodeInput) {
    barcodeInput.value = '628' + Math.floor(100000000 + Math.random() * 900000000);
  }
}

function recalcAddProduct() {
  const itemsPerCarton = Math.max(1, Number(document.getElementById('ap-itemsPerCarton')?.value || 1));
  const cartonsCount = Math.max(0, Number(document.getElementById('ap-cartonsCount')?.value || 0));
  const cost = Number(document.getElementById('ap-cost')?.value || 0);
  const price = Number(document.getElementById('ap-price')?.value || 0);
  const cartonPurchase = Number(document.getElementById('ap-cartonPurchase')?.value || 0);
  const cartonSelling = Number(document.getElementById('ap-cartonSelling')?.value || 0);

  const totalPieces = itemsPerCarton * cartonsCount;
  const retailProfit = Math.max(0, price - cost);
  const cartonProfit = Math.max(0, cartonSelling - cartonPurchase);

  document.getElementById('ap-totalStockDisplay').innerText = `${totalPieces} قطعة`;
  document.getElementById('ap-retailProfitDisplay').innerText = `${Number(retailProfit).toLocaleString()} د.ع`;
  document.getElementById('ap-cartonProfitDisplay').innerText = `${Number(cartonProfit).toLocaleString()} د.ع`;
}

function clearAddProductForm() {
  document.getElementById('ap-id').value = '';
  document.getElementById('ap-name').value = '';
  document.getElementById('ap-barcode').value = '';
  document.getElementById('ap-category').value = '';
  document.getElementById('ap-itemsPerCarton').value = '12';
  document.getElementById('ap-cartonsCount').value = '5';
  document.getElementById('ap-cost').value = '1000';
  document.getElementById('ap-price').value = '1250';
  document.getElementById('ap-cartonPurchase').value = '12000';
  document.getElementById('ap-cartonSelling').value = '15000';
  document.getElementById('addProductFormTitle').innerText = 'إضافة مادة جديدة للمخزن';
  recalcAddProduct();
}

async function saveProductFull() {
  const name = document.getElementById('ap-name')?.value.trim();
  if (!name) {
    alert('يرجى كتابة اسم المادة!');
    return;
  }

  const itemsPerCarton = Math.max(1, Number(document.getElementById('ap-itemsPerCarton')?.value || 1));
  const cartonsCount = Math.max(0, Number(document.getElementById('ap-cartonsCount')?.value || 0));
  const totalStock = itemsPerCarton * cartonsCount;

  const payload = {
    id: document.getElementById('ap-id')?.value || undefined,
    name: name,
    barcode: document.getElementById('ap-barcode')?.value.trim() || undefined,
    category: document.getElementById('ap-category')?.value.trim() || 'عام',
    cost: Number(document.getElementById('ap-cost')?.value || 0),
    price: Number(document.getElementById('ap-price')?.value || 0),
    stockQuantity: totalStock,
    piecesPerCarton: itemsPerCarton,
    minStockAlert: Number(document.getElementById('ap-minStockAlert')?.value || 5)
  };

  const res = await callBackend('save_product', payload);
  if (res && res.success) {
    alert('✔ تم حفظ المادة بنجاح في قاعدة البيانات!');
    clearAddProductForm();
    await loadProducts();
    switchTab('inventory');
  }
}

// ========================================================
// REP ORDERS & INVENTORY LOADERS
// ========================================================
async function loadRepOrders() {
  const res = await callBackend('get_supplier_orders');
  if (!res || !res.success) return;

  const orders = res.orders || [];
  const pendingCount = orders.filter(o => o.status === 'Pending').length;

  const topBadge = document.getElementById('repTopBadge');
  const bellBadge = document.getElementById('repBellBadge');
  const railBadge = document.getElementById('repRailBadge');

  if (pendingCount > 0) {
    if (topBadge) { topBadge.innerText = pendingCount; topBadge.classList.remove('hidden'); }
    if (bellBadge) { bellBadge.innerText = pendingCount; bellBadge.classList.remove('hidden'); }
    if (railBadge) { railBadge.classList.remove('hidden'); }
  } else {
    if (topBadge) topBadge.classList.add('hidden');
    if (bellBadge) bellBadge.classList.add('hidden');
    if (railBadge) railBadge.classList.add('hidden');
  }

  const tbody = document.getElementById('repOrdersTableBody');
  if (!tbody) return;

  tbody.innerHTML = '';
  orders.forEach(o => {
    const tr = document.createElement('tr');
    tr.className = 'hover:bg-slate-50 dark:hover:bg-slate-800/40';
    tr.innerHTML = `
      <td class="p-4 font-mono font-bold text-emerald-600 dark:text-emerald-400">${o.orderNumber}</td>
      <td class="p-4 font-bold">${o.marketName || '--'}</td>
      <td class="p-4 text-slate-500">${o.representativeName || '--'}</td>
      <td class="p-4 font-bold text-amber-500">${o.itemsCount} مواد</td>
      <td class="p-4 font-black text-slate-900 dark:text-white">${Number(o.totalAmount).toLocaleString()} د.ع</td>
      <td class="p-4"><span class="px-2.5 py-1 rounded-full text-[10px] font-bold ${o.status === 'Pending' ? 'bg-amber-100 text-amber-700 dark:bg-amber-950/60 dark:text-amber-400' : 'bg-emerald-100 text-emerald-700 dark:bg-emerald-950/60 dark:text-emerald-400'}">${o.status === 'Pending' ? 'جديد قيد الانتظار' : o.status}</span></td>
      <td class="p-4 text-center">
        <button onclick="updateOrderStatus('${o.id}', 'Delivered')" class="q-pill-btn px-4 py-1.5 bg-emerald-600 hover:bg-emerald-500 text-white font-bold text-xs shadow-sm">تسليم</button>
      </td>
    `;
    tbody.appendChild(tr);
  });
}

async function updateOrderStatus(id, status) {
  await callBackend('update_order_status', { id, status });
  loadRepOrders();
}

async function loadInventory() {
  const res = await callBackend('get_inventory');
  if (!res || !res.success) return;

  const tbody = document.getElementById('inventoryTableBody');
  if (!tbody) return;

  tbody.innerHTML = '';
  (res.products || []).forEach(p => {
    const tr = document.createElement('tr');
    tr.className = 'hover:bg-slate-50 dark:hover:bg-slate-800/40';
    tr.innerHTML = `
      <td class="p-4 font-bold">${p.name}</td>
      <td class="p-4 font-mono text-slate-400">${p.barcode || '--'}</td>
      <td class="p-4 text-slate-500">${p.category || 'عام'}</td>
      <td class="p-4 font-bold text-blue-600 dark:text-blue-400">${Number(p.cost).toLocaleString()} د.ع</td>
      <td class="p-4 font-bold text-emerald-600 dark:text-emerald-400">${Number(p.price).toLocaleString()} د.ع</td>
      <td class="p-4 font-black ${p.stockQuantity <= p.minStockAlert ? 'text-rose-500 font-black' : ''}">${p.stockQuantity}</td>
      <td class="p-4 text-center">
        <button onclick="editProductFromInventory('${p.id}')" class="q-pill-btn px-3 py-1 q-card hover:bg-slate-100 text-emerald-600 font-bold text-xs">✏ تعديل</button>
      </td>
    `;
    tbody.appendChild(tr);
  });
}

function editProductFromInventory(id) {
  const prod = state.products.find(p => p.id === id);
  if (prod) {
    switchTab('addProduct');
    document.getElementById('ap-id').value = prod.id;
    document.getElementById('ap-name').value = prod.name;
    document.getElementById('ap-barcode').value = prod.barcode || '';
    document.getElementById('ap-category').value = prod.category || 'عام';
    document.getElementById('ap-cost').value = prod.cost;
    document.getElementById('ap-price').value = prod.price;
    document.getElementById('ap-itemsPerCarton').value = prod.piecesPerCarton || 1;
    document.getElementById('ap-cartonsCount').value = Math.floor(prod.stockQuantity / (prod.piecesPerCarton || 1));
    document.getElementById('addProductFormTitle').innerText = 'تعديل بيانات المادة';
    recalcAddProduct();
  }
}

async function loadSuppliers() {
  const res = await callBackend('get_suppliers');
  if (!res || !res.success) return;

  const grid = document.getElementById('suppliersCardsGrid');
  if (!grid) return;

  grid.innerHTML = '';
  (res.suppliers || []).forEach(s => {
    const card = document.createElement('div');
    card.className = 'q-card p-6 flex flex-col justify-between shadow-sm';
    card.innerHTML = `
      <div>
        <div class="flex items-center justify-between mb-2">
          <h4 class="font-black text-lg">${s.name}</h4>
          <span class="text-xs bg-emerald-100 text-emerald-700 dark:bg-emerald-950/60 dark:text-emerald-400 px-3 py-0.5 rounded-full font-bold">مندوب</span>
        </div>
        <p class="text-xs text-slate-400 mb-1">الشركة: ${s.company || 'غير محدد'}</p>
        <p class="text-xs text-slate-400">الهاتف: ${s.phone || '--'}</p>
      </div>
      <div class="mt-4 pt-3 border-t border-slate-100 dark:border-slate-800 flex items-center justify-between">
        <span class="text-xs text-slate-400 font-bold">الرصيد المستحق:</span>
        <span class="text-base font-black text-amber-500">${Number(s.balance).toLocaleString()} د.ع</span>
      </div>
    `;
    grid.appendChild(card);
  });
}

async function loadUsers() {
  const res = await callBackend('get_users');
  if (!res || !res.success) return;

  const grid = document.getElementById('usersGrid');
  if (!grid) return;

  grid.innerHTML = '';
  (res.users || []).forEach(u => {
    const card = document.createElement('div');
    card.className = 'q-card p-6 shadow-sm';
    card.innerHTML = `
      <div class="flex items-center gap-3 mb-3">
        <div class="w-12 h-12 rounded-2xl bg-purple-100 dark:bg-purple-950/60 text-purple-600 flex items-center justify-center font-bold text-xl">👤</div>
        <div>
          <h4 class="font-black text-base">${u.fullName}</h4>
          <span class="text-xs text-slate-400 font-mono">@${u.username} (${u.role})</span>
        </div>
      </div>
      <div class="flex items-center justify-between text-xs pt-3 border-t border-slate-100 dark:border-slate-800">
        <span class="text-slate-400">الحالة:</span>
        <span class="font-bold ${u.isActive ? 'text-emerald-600' : 'text-rose-500'}">${u.isActive ? 'نشط ومفعل ✔' : 'معطل ✕'}</span>
      </div>
    `;
    grid.appendChild(card);
  });
}

function openRepPortalModal() {
  document.getElementById('repModal')?.classList.remove('hidden');
}

function closeRepPortalModal() {
  document.getElementById('repModal')?.classList.add('hidden');
}

function toggleLanguage() {
  state.language = state.language === 'ar' ? 'ku' : 'ar';
  document.getElementById('langBtnText').innerText = state.language === 'ar' ? 'العربية' : 'کوردی';
}

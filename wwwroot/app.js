// ========================================================
// SchoolHub Modern Dashboard & POS App Logic
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
  attendanceChart: null,
  performanceChart: null,
  activityChart: null
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
      console.log(`[C# Bridge] ${action}`, payload);
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
  setupGlobalKeyboardShortcuts();
  
  await loadProducts();
  renderInvoiceTabs();
  renderCashierCart();
  await loadDashboard();
  await loadRepOrders();
  
  setInterval(loadRepOrders, 4000);
});

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
    }
  });
}

// ========================================================
// TAB NAVIGATION
// ========================================================
function switchTab(tabId) {
  state.activeTab = tabId;

  // 1. Hide all tabs
  document.querySelectorAll('.tab-content').forEach(el => el.classList.add('hidden'));

  // 2. Reset sidebar items
  document.querySelectorAll('.sidebar-item').forEach(el => {
    el.classList.remove('sidebar-item-active');
  });

  // 3. Activate selected tab
  const tabEl = document.getElementById(`tab-${tabId}`);
  if (tabEl) tabEl.classList.remove('hidden');

  const sideBtn = document.getElementById(`sidebar-${tabId}`);
  if (sideBtn) sideBtn.classList.add('sidebar-item-active');

  if (tabId === 'cashier') {
    document.getElementById('cashierBarcodeInput')?.focus();
  }
  if (tabId === 'dashboard') loadDashboard();
  if (tabId === 'notice') loadRepOrders();

  lucide.createIcons();
}

// ========================================================
// THEME SWITCHER
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

function toggleLanguage() {
  state.language = state.language === 'ar' ? 'ku' : 'ar';
  document.getElementById('langBtnText').innerText = state.language === 'ar' ? 'العربية' : 'کوردی';
}

// ========================================================
// DASHBOARD CHARTS (EXACT REPLICA FROM IMAGE)
// ========================================================
async function loadDashboard() {
  const res = await callBackend('get_dashboard_data');
  if (res && res.success) {
    const totalInc = Number(res.monthlyRevenue || 1682500);
    const incEl = document.getElementById('kpiTotalIncome');
    if (incEl) incEl.innerText = '$' + totalInc.toLocaleString();
  }

  renderAttendanceChart();
  renderPerformanceChart();
  renderActivitySplineChart();
}

function renderAttendanceChart() {
  const ctx = document.getElementById('attendanceChart');
  if (!ctx) return;

  if (state.attendanceChart) state.attendanceChart.destroy();

  state.attendanceChart = new Chart(ctx, {
    type: 'doughnut',
    data: {
      labels: ['Present', 'Absent'],
      datasets: [{
        data: [80, 20],
        backgroundColor: ['#A78BFA', '#FBBF24'],
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

function renderPerformanceChart() {
  const ctx = document.getElementById('performanceChart');
  if (!ctx) return;

  if (state.performanceChart) state.performanceChart.destroy();

  const isDark = state.theme === 'dark';

  state.performanceChart = new Chart(ctx, {
    type: 'bar',
    data: {
      labels: ['Mon', 'Tue', 'Wed', 'Thu', 'Fri'],
      datasets: [
        {
          label: 'Class 10',
          data: [95, 60, 75, 90, 65],
          backgroundColor: '#38BDF8',
          borderRadius: 6,
          barPercentage: 0.7
        },
        {
          label: 'Class 11',
          data: [65, 80, 60, 75, 45],
          backgroundColor: '#FBBF24',
          borderRadius: 6,
          barPercentage: 0.7
        },
        {
          label: 'Class 12',
          data: [75, 65, 80, 70, 50],
          backgroundColor: '#A78BFA',
          borderRadius: 6,
          barPercentage: 0.7
        }
      ]
    },
    options: {
      responsive: true,
      maintainAspectRatio: false,
      plugins: { legend: { display: false } },
      scales: {
        y: { 
          min: 0,
          max: 100,
          ticks: { stepSize: 25, callback: (v) => v + '%', color: isDark ? '#94A3B8' : '#94A3B8', font: { size: 10 } },
          grid: { color: isDark ? 'rgba(255,255,255,0.04)' : 'rgba(0,0,0,0.04)' }
        },
        x: { 
          ticks: { color: isDark ? '#94A3B8' : '#64748B', font: { size: 11, weight: 'bold' } },
          grid: { display: false }
        }
      }
    }
  });
}

function renderActivitySplineChart() {
  const ctx = document.getElementById('activitySplineChart');
  if (!ctx) return;

  if (state.activityChart) state.activityChart.destroy();

  const isDark = state.theme === 'dark';

  state.activityChart = new Chart(ctx, {
    type: 'line',
    data: {
      labels: ['1', '5', '10', '15', '20', '25', '30'],
      datasets: [{
        data: [50, 115, 65, 120, 75, 118, 85],
        borderColor: '#FBBF24',
        backgroundColor: 'rgba(251, 191, 36, 0.12)',
        tension: 0.45,
        fill: true,
        pointBackgroundColor: '#FBBF24',
        pointRadius: 4,
        pointHoverRadius: 6
      }]
    },
    options: {
      responsive: true,
      maintainAspectRatio: false,
      plugins: { legend: { display: false } },
      scales: {
        y: { 
          min: 40,
          max: 160,
          ticks: { stepSize: 40, color: isDark ? '#94A3B8' : '#94A3B8', font: { size: 10 } },
          grid: { color: isDark ? 'rgba(255,255,255,0.04)' : 'rgba(0,0,0,0.04)' }
        },
        x: { 
          grid: { display: false },
          ticks: { display: false }
        }
      }
    }
  });
}

// ========================================================
// CASHIER / POS SYSTEM (RAPID CHECKOUT)
// ========================================================
async function loadProducts() {
  const res = await callBackend('get_pos_products');
  if (res && res.success) {
    state.products = res.products || [];
  }
}

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
    tabEl.className = `flex items-center gap-2 px-3.5 py-1.5 rounded-xl cursor-pointer text-xs font-bold transition ${isSel ? 'bg-sky-500 text-white shadow-sm' : 'bg-slate-100 dark:bg-slate-800 text-slate-600 dark:text-slate-300'}`;
    tabEl.onclick = () => selectInvoiceTab(t.id);
    tabEl.innerHTML = `
      <span>${t.title}</span>
      <span class="bg-white/20 px-1.5 py-0.2 rounded-full text-[10px]">${t.items.length}</span>
      ${state.invoiceTabs.length > 1 ? `<button onclick="event.stopPropagation(); closeInvoiceTab(${t.id})" class="text-rose-200 hover:text-white px-1">✕</button>` : ''}
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
      <td class="p-3 font-bold text-slate-400">${index + 1}</td>
      <td class="p-3 font-bold">${item.name}</td>
      <td class="p-3 font-bold text-sky-500">${Number(item.price).toLocaleString()} د.ع</td>
      <td class="p-3 text-center">
        <div class="inline-flex items-center gap-2 bg-slate-100 dark:bg-slate-800 px-2.5 py-1 rounded-xl">
          <button onclick="updateCartItemQty('${item.id}', -1)" class="w-5 h-5 rounded text-rose-500 font-bold">-</button>
          <span class="font-bold px-1">${item.qty}</span>
          <button onclick="updateCartItemQty('${item.id}', 1)" class="w-5 h-5 rounded text-emerald-500 font-bold">+</button>
        </div>
      </td>
      <td class="p-3 font-black text-slate-800 dark:text-white">${Number(item.price * item.qty).toLocaleString()} د.ع</td>
      <td class="p-3 text-center">
        <button onclick="removeCartItem('${item.id}')" class="text-rose-500 hover:text-rose-600 font-bold text-xs">🗑</button>
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
        btn.className = 'flex-1 py-1.5 bg-sky-500 text-white font-bold text-xs rounded-xl shadow-md';
      } else {
        btn.className = 'flex-1 py-1.5 bg-slate-100 dark:bg-slate-800 text-slate-500 font-bold text-xs rounded-xl';
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
    alert(`✔ تم حفظ وإتمام الفاتورة بنجاح!\nرقم الفاتورة: ${res.invoiceNumber}\nالمبلغ المطلوب: ${Number(res.total).toLocaleString()} د.ع`);
    currentTab.items = [];
    document.getElementById('cashierDiscountInput').value = 0;
    document.getElementById('cashierPaidInput').value = 0;
    renderInvoiceTabs();
    renderCashierCart();
    loadDashboard();
  }
}

// ========================================================
// REP CLOUD ORDERS
// ========================================================
async function loadRepOrders() {
  const res = await callBackend('get_supplier_orders');
  if (!res || !res.success) return;

  const orders = res.orders || [];
  const pendingCount = orders.filter(o => o.status === 'Pending').length;

  const sideBadge = document.getElementById('repBadgeSidebar');
  const bellBadge = document.getElementById('repBellBadge');

  if (pendingCount > 0) {
    if (sideBadge) { sideBadge.innerText = pendingCount; sideBadge.classList.remove('hidden'); }
    if (bellBadge) { bellBadge.classList.remove('hidden'); }
  } else {
    if (sideBadge) sideBadge.classList.add('hidden');
    if (bellBadge) bellBadge.classList.add('hidden');
  }

  const tbody = document.getElementById('repOrdersTableBody');
  if (!tbody) return;

  tbody.innerHTML = '';
  orders.forEach(o => {
    const tr = document.createElement('tr');
    tr.className = 'hover:bg-slate-50 dark:hover:bg-slate-800/40';
    tr.innerHTML = `
      <td class="p-3 font-mono font-bold text-sky-500">${o.orderNumber}</td>
      <td class="p-3 font-bold">${o.marketName || '--'}</td>
      <td class="p-3 text-slate-500">${o.representativeName || '--'}</td>
      <td class="p-3 font-bold text-amber-500">${o.itemsCount} مواد</td>
      <td class="p-3 font-black text-slate-800 dark:text-white">${Number(o.totalAmount).toLocaleString()} د.ع</td>
      <td class="p-3"><span class="px-2 py-0.5 rounded-full text-[10px] font-bold ${o.status === 'Pending' ? 'bg-amber-100 text-amber-700' : 'bg-emerald-100 text-emerald-700'}">${o.status === 'Pending' ? 'قيد الانتظار' : o.status}</span></td>
      <td class="p-3 text-center">
        <button onclick="updateOrderStatus('${o.id}', 'Delivered')" class="px-3 py-1 bg-sky-500 hover:bg-sky-600 text-white font-bold text-xs rounded-xl shadow-sm">تسليم</button>
      </td>
    `;
    tbody.appendChild(tr);
  });
}

async function updateOrderStatus(id, status) {
  await callBackend('update_order_status', { id, status });
  loadRepOrders();
}

function openRepPortalModal() {
  document.getElementById('repModal')?.classList.remove('hidden');
}

function closeRepPortalModal() {
  document.getElementById('repModal')?.classList.add('hidden');
}

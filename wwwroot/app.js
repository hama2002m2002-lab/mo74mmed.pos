// ========================================================
// 7amo.pos Next-Gen App State & Core Logic
// ========================================================

const state = {
  activeTab: 'cashier',
  theme: 'dark',
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

// C# Native Bridge Call
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
      console.log(`[C# Bridge Mock] ${action}`, payload);
      resolve({ success: true, message: "Browser Mock" });
    }
  });
}

// ========================================================
// INITIALIZATION
// ========================================================
document.addEventListener('DOMContentLoaded', async () => {
  lucide.createIcons();
  startClock();
  setupGlobalKeyboardShortcuts();
  await loadProducts();
  await loadSuppliersList();
  renderInvoiceTabs();
  renderCashierCart();
  await loadDashboard();
  await loadRepOrders();
  setInterval(loadRepOrders, 4000); // 4s polling
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
      switchTab('cashier');
    } else if (e.key === 'F2') {
      e.preventDefault();
      switchTab('addProduct');
    }
  });
}

// ========================================================
// TAB NAVIGATION
// ========================================================
function switchTab(tabId) {
  state.activeTab = tabId;

  document.querySelectorAll('.tab-content').forEach(el => el.classList.add('hidden'));
  document.querySelectorAll('.nav-item').forEach(el => {
    el.classList.remove('bg-slate-800/90', 'text-blue-400', 'border', 'border-blue-500/30');
    el.classList.add('text-slate-300');
  });

  const tabEl = document.getElementById(`tab-${tabId}`);
  if (tabEl) tabEl.classList.remove('hidden');

  const navEl = document.getElementById(`nav-${tabId}`);
  if (navEl) {
    navEl.classList.add('bg-slate-800/90', 'text-blue-400', 'border', 'border-blue-500/30');
    navEl.classList.remove('text-slate-300');
  }

  if (tabId === 'cashier') {
    document.getElementById('cashierBarcodeInput')?.focus();
  }
  if (tabId === 'dashboard') loadDashboard();
  if (tabId === 'inventory') loadInventory();
  if (tabId === 'repOrders') loadRepOrders();
  if (tabId === 'suppliers') loadSuppliers();
  if (tabId === 'users') loadUsers();

  lucide.createIcons();
}

// ========================================================
// CASHIER / SALE LOGIC (MULTI-TABS & RAPID CHECKOUT)
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
    tabEl.className = `flex items-center gap-1.5 px-3 py-1.5 rounded-xl cursor-pointer text-xs font-bold transition border ${isSel ? 'bg-slate-800 text-blue-400 border-blue-500/40 shadow-lg' : 'bg-slate-900/80 text-slate-400 border-slate-800 hover:text-white'}`;
    tabEl.onclick = () => selectInvoiceTab(t.id);
    tabEl.innerHTML = `
      <span>${t.title}</span>
      <span class="bg-blue-600/30 text-blue-300 px-1.5 py-0.2 rounded-md text-[10px]">${t.items.length}</span>
      ${state.invoiceTabs.length > 1 ? `<button onclick="event.stopPropagation(); closeInvoiceTab(${t.id})" class="text-rose-400 hover:text-rose-300 px-1 text-xs">✕</button>` : ''}
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
    tbody.innerHTML = '<tr><td colspan="6" class="text-center py-12 text-slate-500">لا توجد مواد في هذه الفاتورة. امسح الباركود للبدء.</td></tr>';
    recalcCashierInvoice();
    return;
  }

  tbody.innerHTML = '';
  currentTab.items.forEach((item, index) => {
    const tr = document.createElement('tr');
    tr.className = 'hover:bg-slate-800/50';
    tr.innerHTML = `
      <td class="p-3 font-bold text-slate-400">${index + 1}</td>
      <td class="p-3 font-bold text-white">${item.name}</td>
      <td class="p-3 font-bold text-blue-400">${Number(item.price).toLocaleString()} د.ع</td>
      <td class="p-3 text-center">
        <div class="inline-flex items-center gap-1.5 bg-slate-900 px-2 py-1 rounded-lg border border-slate-800">
          <button onclick="updateCartItemQty('${item.id}', -1)" class="w-5 h-5 bg-slate-800 hover:bg-slate-700 rounded text-rose-400 font-bold">-</button>
          <span class="font-bold text-white px-2">${item.qty}</span>
          <button onclick="updateCartItemQty('${item.id}', 1)" class="w-5 h-5 bg-slate-800 hover:bg-slate-700 rounded text-emerald-400 font-bold">+</button>
        </div>
      </td>
      <td class="p-3 font-black text-emerald-400">${Number(item.price * item.qty).toLocaleString()} د.ع</td>
      <td class="p-3 text-center">
        <button onclick="removeCartItem('${item.id}')" class="text-rose-400 hover:text-rose-300 p-1 text-xs">🗑</button>
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
        btn.className = 'flex-1 py-1.5 bg-emerald-600 text-white font-bold text-xs rounded-xl border border-emerald-500 shadow-md';
      } else {
        btn.className = 'flex-1 py-1.5 bg-slate-800 text-slate-300 font-bold text-xs rounded-xl border border-slate-700';
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
    alert(`✔ تم حفظ وإتمام الفاتورة بنجاح!\nرقم الفاتورة: ${res.invoiceNumber}\nالإجمالي: ${Number(res.total).toLocaleString()} د.ع`);
    currentTab.items = [];
    document.getElementById('cashierDiscountInput').value = 0;
    document.getElementById('cashierPaidInput').value = 0;
    renderInvoiceTabs();
    renderCashierCart();
    loadDashboard();
  }
}

// ========================================================
// ADD / EDIT PRODUCT FULL FORM
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
// DASHBOARD & CHARTS
// ========================================================
async function loadDashboard() {
  const res = await callBackend('get_dashboard_data');
  if (!res || !res.success) return;

  document.getElementById('kpiTodayRevenue').innerText = Number(res.todayRevenue || 0).toLocaleString();
  document.getElementById('kpiTodayInvoices').innerText = Number(res.todayInvoices || 0).toLocaleString();
  document.getElementById('kpiMonthlyRevenue').innerText = Number(res.monthlyRevenue || 0).toLocaleString();
  document.getElementById('kpiLowStock').innerText = Number(res.lowStockCount || 0).toLocaleString();

  renderWeeklyChart(res.weeklyTrend || []);
  renderPaymentChart(res.payments || { cash: 0, card: 0, debt: 0 });
}

function renderWeeklyChart(data) {
  const ctx = document.getElementById('weeklyChart');
  if (!ctx) return;

  if (state.weeklyChart) state.weeklyChart.destroy();
  state.weeklyChart = new Chart(ctx, {
    type: 'bar',
    data: {
      labels: data.map(d => d.dayName),
      datasets: [{
        data: data.map(d => d.revenue),
        backgroundColor: '#3B82F6',
        borderRadius: 6
      }]
    },
    options: {
      responsive: true,
      maintainAspectRatio: false,
      plugins: { legend: { display: false } },
      scales: {
        y: { ticks: { color: '#94A3B8' }, grid: { color: 'rgba(255,255,255,0.05)' } },
        x: { ticks: { color: '#94A3B8' }, grid: { display: false } }
      }
    }
  });
}

function renderPaymentChart(payments) {
  const ctx = document.getElementById('paymentChart');
  if (!ctx) return;

  if (state.paymentChart) state.paymentChart.destroy();
  state.paymentChart = new Chart(ctx, {
    type: 'doughnut',
    data: {
      labels: ['نقداً', 'بطاقة', 'آجل'],
      datasets: [{
        data: [payments.cash || 0, payments.card || 0, payments.debt || 0],
        backgroundColor: ['#10B981', '#3B82F6', '#F59E0B'],
        borderWidth: 0
      }]
    },
    options: {
      responsive: true,
      maintainAspectRatio: false,
      cutout: '70%',
      plugins: { legend: { display: false } }
    }
  });

  const legend = document.getElementById('paymentLegend');
  if (legend) {
    legend.innerHTML = `
      <span class="text-emerald-400">💵 نقداً: ${Number(payments.cash || 0).toLocaleString()}</span>
      <span class="text-blue-400">💳 بطاقة: ${Number(payments.card || 0).toLocaleString()}</span>
      <span class="text-amber-400">📝 آجل: ${Number(payments.debt || 0).toLocaleString()}</span>
    `;
  }
}

// ========================================================
// REP ORDERS & INVENTORY
// ========================================================
async function loadRepOrders() {
  const res = await callBackend('get_supplier_orders');
  if (!res || !res.success) return;

  const orders = res.orders || [];
  const pendingCount = orders.filter(o => o.status === 'Pending').length;

  const badge = document.getElementById('repBadge');
  const sideBadge = document.getElementById('repSidebarBadge');
  if (badge && sideBadge) {
    if (pendingCount > 0) {
      badge.innerText = pendingCount;
      badge.classList.remove('hidden');
      sideBadge.innerText = pendingCount;
      sideBadge.classList.remove('hidden');
    } else {
      badge.classList.add('hidden');
      sideBadge.classList.add('hidden');
    }
  }

  const tbody = document.getElementById('repOrdersTableBody');
  if (!tbody) return;

  tbody.innerHTML = '';
  orders.forEach(o => {
    const tr = document.createElement('tr');
    tr.className = 'hover:bg-slate-800/40';
    tr.innerHTML = `
      <td class="p-3 font-mono font-bold text-blue-400">${o.orderNumber}</td>
      <td class="p-3 font-bold text-white">${o.marketName || '--'}</td>
      <td class="p-3 text-cyan-400">${o.representativeName || '--'}</td>
      <td class="p-3 text-amber-400 font-bold">${o.itemsCount} مواد</td>
      <td class="p-3 font-black text-emerald-400">${Number(o.totalAmount).toLocaleString()} د.ع</td>
      <td class="p-3"><span class="px-2 py-0.5 rounded text-[10px] font-bold ${o.status === 'Pending' ? 'bg-amber-500/20 text-amber-400' : 'bg-emerald-500/20 text-emerald-400'}">${o.status === 'Pending' ? 'جديد قيد الانتظار' : o.status}</span></td>
      <td class="p-3 text-center">
        <button onclick="updateOrderStatus('${o.id}', 'Delivered')" class="bg-emerald-600/20 hover:bg-emerald-600/40 text-emerald-400 border border-emerald-500/30 px-2 py-1 rounded text-xs font-bold">تسليم</button>
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
    tr.className = 'hover:bg-slate-800/40';
    tr.innerHTML = `
      <td class="p-3 font-bold text-white">${p.name}</td>
      <td class="p-3 font-mono text-slate-400">${p.barcode || '--'}</td>
      <td class="p-3 text-slate-400">${p.category || 'عام'}</td>
      <td class="p-3 font-bold text-blue-400">${Number(p.cost).toLocaleString()} د.ع</td>
      <td class="p-3 font-bold text-emerald-400">${Number(p.price).toLocaleString()} د.ع</td>
      <td class="p-3 font-black ${p.stockQuantity <= p.minStockAlert ? 'text-rose-400 animate-pulse' : 'text-slate-200'}">${p.stockQuantity}</td>
      <td class="p-3 text-center">
        <button onclick="editProductFromInventory('${p.id}')" class="text-blue-400 hover:text-blue-300 px-1 font-bold">✏ تعديل</button>
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

// ========================================================
// THEME & REPS
// ========================================================
function toggleTheme() {
  state.theme = state.theme === 'dark' ? 'light' : 'dark';
  document.getElementById('themeIcon').innerText = state.theme === 'dark' ? '🌙' : '☀️';
  document.getElementById('themeLabel').innerText = state.theme === 'dark' ? 'الوضع الليلي' : 'الوضع النهاري';
  
  if (state.theme === 'light') {
    document.body.classList.remove('bg-[#090D16]', 'text-slate-100');
    document.body.classList.add('bg-[#F8FAFC]', 'text-slate-900');
  } else {
    document.body.classList.add('bg-[#090D16]', 'text-slate-100');
    document.body.classList.remove('bg-[#F8FAFC]', 'text-slate-900');
  }
}

function toggleLanguage() {
  state.language = state.language === 'ar' ? 'ku' : 'ar';
  document.getElementById('langBtnText').innerText = state.language === 'ar' ? 'العربية' : 'کوردی';
}

function openRepPortalModal() {
  document.getElementById('repModal')?.classList.remove('hidden');
}

function closeRepPortalModal() {
  document.getElementById('repModal')?.classList.add('hidden');
}

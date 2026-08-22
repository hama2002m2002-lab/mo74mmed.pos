// ========================================================
// 7amo Market - Modern Supermarket & POS Core Logic
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
  await loadSuppliersList();
  await loadCategoriesList();
  renderInvoiceTabs();
  renderCashierCart();
  await loadDashboard();
  await loadRepOrders();
  recalcAddProduct();
  
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
    } else if (e.key === 'F3') {
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
  if (tabId === 'addProduct') {
    loadCategoriesList();
    recalcAddProduct();
  }
  if (tabId === 'dashboard') loadDashboard();
  if (tabId === 'inventory') loadInventory();
  if (tabId === 'repOrders') loadRepOrders();
  if (tabId === 'suppliers') loadSuppliers();
  if (tabId === 'users') loadUsers();

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
// DASHBOARD CHARTS
// ========================================================
async function loadDashboard() {
  const res = await callBackend('get_dashboard_data');
  if (res && res.success) {
    document.getElementById('kpiTodayRevenue').innerText = Number(res.todayRevenue || 0).toLocaleString() + ' د.ع';
    document.getElementById('kpiTodayInvoices').innerText = Number(res.todayInvoices || 0).toLocaleString();
    document.getElementById('kpiMonthlyRevenue').innerText = Number(res.monthlyRevenue || 0).toLocaleString() + ' د.ع';
    document.getElementById('kpiLowStockCount').innerText = Number(res.lowStockCount || 0).toLocaleString();
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
      labels: ['نقداً', 'آجل'],
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
      labels: ['السبت', 'الأحد', 'الاثنين', 'الثلاثاء', 'الأربعاء'],
      datasets: [
        {
          label: 'غذائية',
          data: [95, 60, 75, 90, 65],
          backgroundColor: '#38BDF8',
          borderRadius: 6,
          barPercentage: 0.7
        },
        {
          label: 'منظفات',
          data: [65, 80, 60, 75, 45],
          backgroundColor: '#FBBF24',
          borderRadius: 6,
          barPercentage: 0.7
        },
        {
          label: 'مشروبات',
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

  state.activityChart = new Chart(ctx, {
    type: 'line',
    data: {
      labels: ['8ص', '11ص', '2ظ', '5ع', '8م', '11م'],
      datasets: [{
        data: [45, 95, 60, 110, 140, 85],
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
          min: 20,
          max: 160,
          ticks: { stepSize: 40, color: '#94A3B8', font: { size: 10 } },
          grid: { color: 'rgba(255,255,255,0.04)' }
        },
        x: { 
          grid: { display: false },
          ticks: { color: '#94A3B8', font: { size: 10 } }
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
    tabEl.className = `flex items-center gap-2 px-3.5 py-1.5 rounded-xl cursor-pointer text-xs font-bold transition ${isSel ? 'bg-emerald-600 text-white shadow-sm' : 'bg-slate-100 dark:bg-slate-800 text-slate-600 dark:text-slate-300'}`;
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
      <td class="p-3 font-bold text-emerald-600 dark:text-emerald-400">${Number(item.price).toLocaleString()} د.ع</td>
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
        btn.className = 'flex-1 py-1.5 bg-emerald-600 text-white font-bold text-xs rounded-xl shadow-md';
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
// ADD / EDIT PRODUCT FULL FORM (DETAILED MARKET LOGIC)
// ========================================================
function generateUniqueMarketBarcode() {
  const prefix = "200245";
  let uniqueBarcode = "";
  let attempts = 0;

  do {
    const randomPart = Math.floor(100000 + Math.random() * 900000); // 6 digits
    uniqueBarcode = `${prefix}${randomPart}`;
    attempts++;
  } while (state.products.some(p => p.barcode === uniqueBarcode) && attempts < 500);

  const barcodeInput = document.getElementById('ap-barcode');
  if (barcodeInput) {
    barcodeInput.value = uniqueBarcode;
  }

  // Auto focus on Product Name as requested
  setTimeout(() => {
    document.getElementById('ap-name')?.focus();
  }, 50);
}

function handleBarcodeEnter(e) {
  if (e.key === 'Enter') {
    e.preventDefault();
    document.getElementById('ap-name')?.focus();
  }
}

async function loadCategoriesList() {
  const res = await callBackend('get_categories');
  const catSelect = document.getElementById('ap-categorySelect');
  if (!catSelect) return;

  const currentVal = catSelect.value || "عام";
  catSelect.innerHTML = '';
  
  const cats = (res && res.success && res.categories && res.categories.length > 0) 
    ? res.categories 
    : ["عام", "مواد غذائية", "معلبات", "منظفات", "مشروبات وعصائر", "ألبان وأجبان", "حلويات وبسكويت"];

  cats.forEach(c => {
    catSelect.innerHTML += `<option value="${c}">${c}</option>`;
  });

  if (cats.includes(currentVal)) {
    catSelect.value = currentVal;
  }
}

async function promptAddCategory() {
  const newCat = prompt("أدخل اسم التصنيف الجديد:");
  if (newCat && newCat.trim()) {
    const res = await callBackend('add_category', { name: newCat.trim() });
    await loadCategoriesList();
    const catSelect = document.getElementById('ap-categorySelect');
    if (catSelect) catSelect.value = newCat.trim();
  }
}

async function deleteCurrentCategory() {
  const catSelect = document.getElementById('ap-categorySelect');
  if (!catSelect) return;
  const selectedCat = catSelect.value;
  if (!selectedCat || selectedCat === "عام") {
    alert("لا يمكن حذف التصنيف العام الأساسي!");
    return;
  }

  if (confirm(`هل أنت متأكد من حذف التصنيف: "${selectedCat}"؟`)) {
    await callBackend('delete_category', { name: selectedCat });
    await loadCategoriesList();
  }
}

function recalcAddProduct() {
  const itemsPerCarton = Math.max(1, Number(document.getElementById('ap-itemsPerCarton')?.value || 1));
  const cartonsCount = Math.max(0, Number(document.getElementById('ap-cartonsCount')?.value || 0));
  const cartonPurchase = Number(document.getElementById('ap-cartonPurchase')?.value || 0);

  // Total stock in pieces
  const totalPieces = itemsPerCarton * cartonsCount;
  const totalStockEl = document.getElementById('ap-totalStock');
  if (totalStockEl) totalStockEl.value = `${totalPieces} قطعة`;

  // Calculated Piece Cost from Carton (Readonly)
  const pieceCostFromCarton = itemsPerCarton > 0 ? (cartonPurchase / itemsPerCarton) : 0;
  const pieceCostDisplayEl = document.getElementById('ap-pieceCostFromCarton');
  if (pieceCostDisplayEl) pieceCostDisplayEl.value = `${Math.round(pieceCostFromCarton).toLocaleString()} د.ع`;

  const costInput = document.getElementById('ap-cost');
  if (costInput && (!costInput.value || Number(costInput.value) === 0)) {
    costInput.value = Math.round(pieceCostFromCarton);
  }

  const cost = Number(costInput?.value || Math.round(pieceCostFromCarton));
  const price = Number(document.getElementById('ap-price')?.value || 0); // بيع مفرد
  const wholesale = Number(document.getElementById('ap-wholesalePrice')?.value || 0); // بيع جملة
  const cartonSelling = Number(document.getElementById('ap-cartonSelling')?.value || 0); // بيع كرتون

  // 1. Retail calculations
  const retailPieceProfit = price - cost;
  const retailCartonTotal = price * itemsPerCarton;
  const retailCartonProfit = (price - cost) * itemsPerCarton;

  const rppEl = document.getElementById('ap-retailPieceProfit');
  if (rppEl) rppEl.innerText = `${retailPieceProfit >= 0 ? '+' : ''}${Math.round(retailPieceProfit).toLocaleString()} د.ع`;
  const rctEl = document.getElementById('ap-retailCartonTotal');
  if (rctEl) rctEl.innerText = `${Math.round(retailCartonTotal).toLocaleString()} د.ع`;
  const rcpEl = document.getElementById('ap-retailCartonProfit');
  if (rcpEl) rcpEl.innerText = `${retailCartonProfit >= 0 ? '+' : ''}${Math.round(retailCartonProfit).toLocaleString()} د.ع`;

  // 2. Wholesale calculations
  const wholesalePieceProfit = wholesale - cost;
  const wholesaleCartonProfit = (wholesale - cost) * itemsPerCarton;

  const wppEl = document.getElementById('ap-wholesalePieceProfit');
  if (wppEl) wppEl.innerText = `${wholesalePieceProfit >= 0 ? '+' : ''}${Math.round(wholesalePieceProfit).toLocaleString()} د.ع`;
  const wcpEl = document.getElementById('ap-wholesaleCartonProfit');
  if (wcpEl) wcpEl.innerText = `${wholesaleCartonProfit >= 0 ? '+' : ''}${Math.round(wholesaleCartonProfit).toLocaleString()} د.ع`;

  // 3. Carton calculations
  const cartonDirectProfit = cartonSelling - cartonPurchase;
  const cdpEl = document.getElementById('ap-cartonDirectProfit');
  if (cdpEl) cdpEl.innerText = `${cartonDirectProfit >= 0 ? '+' : ''}${Math.round(cartonDirectProfit).toLocaleString()} د.ع`;
}

function clearAddProductForm() {
  document.getElementById('ap-id').value = '';
  document.getElementById('ap-barcode').value = '';
  document.getElementById('ap-name').value = '';
  document.getElementById('ap-cartonsCount').value = '5';
  document.getElementById('ap-itemsPerCarton').value = '12';
  document.getElementById('ap-cartonPurchase').value = '12000';
  document.getElementById('ap-cost').value = '1000';
  document.getElementById('ap-price').value = '1250';
  document.getElementById('ap-wholesalePrice').value = '1150';
  document.getElementById('ap-cartonSelling').value = '14000';
  document.getElementById('ap-minStockAlert').value = '6';
  document.getElementById('addProductFormTitle').innerText = 'إضافة وتعديل مادة جديدة بالمخزن';
  recalcAddProduct();
}

async function saveProductFull() {
  const name = document.getElementById('ap-name')?.value.trim();
  if (!name) {
    alert('يرجى كتابة اسم المادة أولاً!');
    document.getElementById('ap-name')?.focus();
    return;
  }

  const barcode = document.getElementById('ap-barcode')?.value.trim();
  const itemsPerCarton = Math.max(1, Number(document.getElementById('ap-itemsPerCarton')?.value || 1));
  const cartonsCount = Math.max(0, Number(document.getElementById('ap-cartonsCount')?.value || 0));
  const totalStock = itemsPerCarton * cartonsCount;

  const payload = {
    id: document.getElementById('ap-id')?.value || undefined,
    name: name,
    barcode: barcode || undefined,
    category: document.getElementById('ap-categorySelect')?.value || 'عام',
    cost: Number(document.getElementById('ap-cost')?.value || 0),
    price: Number(document.getElementById('ap-price')?.value || 0),
    wholesalePrice: Number(document.getElementById('ap-wholesalePrice')?.value || 0),
    stockQuantity: totalStock,
    piecesPerCarton: itemsPerCarton,
    minStockAlert: Number(document.getElementById('ap-minStockAlert')?.value || 5)
  };

  const res = await callBackend('save_product', payload);
  if (res && res.success) {
    alert('✔ تم حفظ المادة وتفاصيل الكرتون والأسعار بنجاح في قاعدة البيانات!');
    clearAddProductForm();
    await loadProducts();
    switchTab('inventory');
  }
}

// ========================================================
// INVENTORY, SUPPLIERS & USERS
// ========================================================
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
      <td class="p-3.5 font-bold">${p.name}</td>
      <td class="p-3.5 font-mono text-slate-400">${p.barcode || '--'}</td>
      <td class="p-3.5 text-slate-500">${p.category || 'عام'}</td>
      <td class="p-3.5 font-bold text-blue-500">${Number(p.cost).toLocaleString()} د.ع</td>
      <td class="p-3.5 font-bold text-emerald-500">${Number(p.price).toLocaleString()} د.ع</td>
      <td class="p-3.5 font-black ${p.stockQuantity <= p.minStockAlert ? 'text-rose-500 font-black' : ''}">${p.stockQuantity}</td>
      <td class="p-3.5 text-center">
        <button onclick="editProductFromInventory('${p.id}')" class="px-2.5 py-1 bg-sky-100 dark:bg-sky-950/60 text-sky-600 font-bold text-xs rounded-lg">✏ تعديل</button>
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
    card.className = 'sh-card p-5 flex flex-col justify-between';
    card.innerHTML = `
      <div>
        <div class="flex items-center justify-between mb-2">
          <h4 class="font-black text-base">${s.name}</h4>
          <span class="text-[10px] bg-sky-100 text-sky-700 dark:bg-sky-950/60 dark:text-sky-400 px-2.5 py-0.5 rounded-full font-bold">مندوب</span>
        </div>
        <p class="text-xs text-slate-400 mb-1">الشركة: ${s.company || 'غير محدد'}</p>
        <p class="text-xs text-slate-400">الهاتف: ${s.phone || '--'}</p>
      </div>
      <div class="mt-3 pt-3 border-t border-slate-100 dark:border-slate-800 flex items-center justify-between">
        <span class="text-xs text-slate-400 font-bold">الرصيد المستحق:</span>
        <span class="text-sm font-black text-amber-500">${Number(s.balance).toLocaleString()} د.ع</span>
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
    card.className = 'sh-card p-5';
    card.innerHTML = `
      <div class="flex items-center gap-3 mb-2">
        <div class="w-10 h-10 rounded-2xl bg-sky-100 dark:bg-sky-950/60 text-sky-600 flex items-center justify-center font-bold text-lg">👤</div>
        <div>
          <h4 class="font-black text-sm">${u.fullName}</h4>
          <span class="text-[10px] text-slate-400 font-mono">@${u.username} (${u.role})</span>
        </div>
      </div>
      <div class="flex items-center justify-between text-xs pt-2 border-t border-slate-100 dark:border-slate-800">
        <span class="text-slate-400">الحالة:</span>
        <span class="font-bold ${u.isActive ? 'text-emerald-600' : 'text-rose-500'}">${u.isActive ? 'نشط ومفعل ✔' : 'معطل ✕'}</span>
      </div>
    `;
    grid.appendChild(card);
  });
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

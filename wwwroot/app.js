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
  await loadRepOrders(false); // Initial load without sound alert

  // Setup C# Push Event Listener
  if (window.chrome && window.chrome.webview) {
    window.chrome.webview.addEventListener('message', (event) => {
      try {
        const data = typeof event.data === 'string' ? JSON.parse(event.data) : event.data;
        if (data && data._event === 'new_order_received') {
          loadRepOrders(true);
        }
      } catch (e) {}
    });
  }

  // Periodic automatic sync for incoming rep orders every 6 seconds
  setInterval(() => {
    loadRepOrders(true);
  }, 6000);

  recalcAddProduct();
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
// I18N LOCALIZATION DICTIONARY (ARABIC & KURDISH)
// ========================================================
const i18n = {
  ar: {
    ap_back: "العودة للرئيسية",
    ap_title: "إضافة وتعديل مادة جديدة بالمخزن",
    ap_subtitle: "انقر مرتين على حقل الباركود للتوليد التلقائي (200245) بدون تكرار",
    ap_clear: "مسح الحقول",
    ap_save: "حفظ المادة",
    ap_sec1_title: "1. بيانات المادة والباركود والتعبئة",
    ap_sec1_tag: "نقر مزدوج للباركود ⚡",
    ap_lbl_barcode: "1. حقل الباركود (انقر مرتين للتوليد 200245) *",
    ap_btn_gen: "توليد",
    ap_lbl_name: "2. اسم المادة *",
    ap_name_ph: "اسم المادة",
    ap_lbl_cat: "3. تصنيف المادة",
    ap_lbl_sup: "4. اسم المندوب / الشركة الموردة",
    ap_lbl_cartons: "عدد الكراتين",
    ap_lbl_pieces: "المواد بالكرتون",
    ap_lbl_total: "مجموع كل المواد",
    ap_lbl_alert: "تنبيه النواقص عند وصول الرصيد إلى (قطع):",
    ap_sec2_title: "2. التكلفة وأسعار البيع والأرباح المحسوبة",
    ap_lbl_carton_purchase: "سعر شراء الكرتون (د.ع)",
    ap_lbl_piece_cost_calc: "تكلفة القطعة من الكرتون (للقراءة فقط)",
    ap_lbl_cost: "تكلفة القطعة المعتمدة (د.ع) *",
    ap_lbl_retail_price: "سعر بيع المفرد للقطعة (د.ع) *",
    ap_r_profit: "ربح القطعة:",
    ap_r_total: "بيع الكرتون بالمفرد:",
    ap_r_c_profit: "ربح الكرتون بالمفرد:",
    ap_lbl_wholesale_price: "سعر بيع الجملة للقطعة (د.ع)",
    ap_w_profit: "ربح قطعة الجملة:",
    ap_w_c_profit: "ربح الكرتون بالجملة:",
    ap_lbl_carton_sell: "سعر بيع الكرتون كاملاً (د.ع)",
    ap_c_profit: "ربح بيع الكرتون كاملاً:"
  },
  ku: {
    ap_back: "گەڕانەوە بۆ سەرەکی",
    ap_title: "زیادکردن و دەستکاریکردنی کاڵا لە کۆگا",
    ap_subtitle: "دووجار کلیک لەسەر خانەی بارکۆد بکە بۆ دروستکردنی بارکۆد (200245) بەبێ دووبارەبوونەوە",
    ap_clear: "سڕینەوەی خانەکان",
    ap_save: "پاشەکەوتکردنی کاڵا",
    ap_sec1_title: "١. زانیاری کاڵا، بارکۆد و پاکێج",
    ap_sec1_tag: "دووجار کلیک بۆ بارکۆد ⚡",
    ap_lbl_barcode: "١. خانەی بارکۆد (دووجار کلیک بکە بۆ ٢٠٠٢٤٥) *",
    ap_btn_gen: "دروستکردن",
    ap_lbl_name: "٢. ناوی کاڵا *",
    ap_name_ph: "ناوی کاڵا",
    ap_lbl_cat: "٣. پۆلێنی کاڵا (جۆر)",
    ap_lbl_sup: "٤. ناوی مەندوب / کۆمپانیا",
    ap_lbl_cartons: "ژمارەی کارتۆن",
    ap_lbl_pieces: "دانە لە کارتۆندا",
    ap_lbl_total: "کۆی گشتی هەموو کاڵاکان",
    ap_lbl_alert: "ئاگادارکردنەوە لە کەمی کاڵا لە (دانە):",
    ap_sec2_title: "٢. تێچوون، نرخەکانی فرۆشتن و قازانجەکان",
    ap_lbl_carton_purchase: "نرخی کڕینی کارتۆن (د.ع)",
    ap_lbl_piece_cost_calc: "تێچووی دانە لە کارتۆندا (تەنها خوێندنەوە)",
    ap_lbl_cost: "تێچووی پەسەندکراوی دانە (د.ع) *",
    ap_lbl_retail_price: "نرخی فرۆشتنی تاک بۆ دانە (د.ع) *",
    ap_r_profit: "قازانجی دانە:",
    ap_r_total: "فرۆشتنی کارتۆن بە تاک:",
    ap_r_c_profit: "قازانجی کارتۆن بە تاک:",
    ap_lbl_wholesale_price: "نرخی فرۆشتنی کۆ بۆ دانە (د.ع)",
    ap_w_profit: "قازانجی دانە بە کۆ:",
    ap_w_c_profit: "قازانجی کارتۆن بە کۆ:",
    ap_lbl_carton_sell: "نرخی فرۆشتنی تەواوی کارتۆن (د.ع)",
    ap_c_profit: "قازانجی فرۆشتنی کارتۆن:"
  }
};

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

  // 3. Auto-hide sidebar when opening Add Product or Cashier view as requested
  const sidebar = document.getElementById('appSidebar');
  if (sidebar) {
    if (tabId === 'addProduct' || tabId === 'cashier') {
      sidebar.classList.add('hidden');
    } else {
      sidebar.classList.remove('hidden');
    }
  }

  // 4. Activate selected tab
  const tabEl = document.getElementById(`tab-${tabId}`);
  if (tabEl) tabEl.classList.remove('hidden');

  const sideBtn = document.getElementById(`sidebar-${tabId}`);
  if (sideBtn) sideBtn.classList.add('sidebar-item-active');

  if (tabId === 'cashier') {
    loadProducts();
    setTimeout(() => {
      document.getElementById('cashierBarcodeInput')?.focus();
    }, 50);
  }
  if (tabId === 'addProduct') {
    loadCategoriesList();
    recalcAddProduct();
    document.getElementById('ap-barcode')?.focus();
  }
  if (tabId === 'dashboard') loadDashboard();
  if (tabId === 'inventory') loadInventory();
  if (tabId === 'repOrders') loadRepOrders();
  if (tabId === 'suppliers') loadSuppliers();
  if (tabId === 'users') loadUsers();

  lucide.createIcons();
}

// ========================================================
// THEME & LANGUAGE SWITCHER
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
  applyLanguage(state.language);
}

function applyLanguage(lang) {
  const dict = i18n[lang] || i18n.ar;
  document.querySelectorAll('[data-i18n]').forEach(el => {
    const key = el.getAttribute('data-i18n');
    if (dict[key]) {
      el.innerText = dict[key];
    }
  });

  const nameInput = document.getElementById('ap-name');
  if (nameInput) {
    nameInput.placeholder = dict.ap_name_ph || "اسم المادة";
  }
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
async function loadProducts(showAlert = false) {
  const res = await callBackend('get_pos_products');
  if (res && res.success) {
    state.products = res.products || [];
    if (showAlert) {
      alert(`✔ تم تحديث قائمة المواد من المخزن بنجاح! (${state.products.length} مادة جاهزة للبيع)`);
    }
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

// Ensure invoice tabs are strictly numbered sequentially (فاتورة 1, فاتورة 2...)
function reindexInvoiceTabs() {
  state.invoiceTabs.forEach((tab, index) => {
    tab.title = `فاتورة ${index + 1}`;
  });
}

function getCurrentTab() {
  return state.invoiceTabs.find(t => t.id === state.selectedInvoiceTabId) || state.invoiceTabs[0];
}

function renderInvoiceTabs() {
  const container = document.getElementById('invoiceTabsContainer');
  if (!container) return;

  reindexInvoiceTabs();
  container.innerHTML = '';
  state.invoiceTabs.forEach(t => {
    const isSel = t.id === state.selectedInvoiceTabId;
    const tabEl = document.createElement('div');
    tabEl.className = `flex items-center gap-2 px-3.5 py-1.5 rounded-xl cursor-pointer text-xs font-bold transition ${isSel ? 'bg-emerald-600 text-white shadow-sm' : 'bg-slate-100 dark:bg-slate-800 text-slate-600 dark:text-slate-300'}`;
    tabEl.onclick = () => selectInvoiceTab(t.id);
    tabEl.innerHTML = `
      <span>${t.title}</span>
      <span class="bg-white/20 px-1.5 py-0.2 rounded-full text-[10px]">${t.items.length}</span>
      ${state.invoiceTabs.length > 1 ? `<button onclick="event.stopPropagation(); closeInvoiceTab('${t.id}')" class="text-rose-200 hover:text-white px-1 font-black" title="إغلاق الفاتورة">✕</button>` : ''}
    `;
    container.appendChild(tabEl);
  });
}

function addNewInvoiceTab() {
  const newId = 'inv_' + Date.now() + '_' + Math.floor(Math.random() * 1000);
  state.invoiceTabs.push({
    id: newId,
    title: `فاتورة ${state.invoiceTabs.length + 1}`,
    items: [],
    discount: 0,
    paid: 0,
    paymentMethod: 'Cash'
  });
  reindexInvoiceTabs();
  selectInvoiceTab(newId);
}

function selectInvoiceTab(id) {
  state.selectedInvoiceTabId = id;
  renderInvoiceTabs();
  renderCashierCart();
  setTimeout(() => {
    document.getElementById('cashierBarcodeInput')?.focus();
  }, 50);
}

function closeInvoiceTab(id) {
  if (state.invoiceTabs.length <= 1) return;
  const index = state.invoiceTabs.findIndex(t => t.id === id || String(t.id) === String(id));
  state.invoiceTabs = state.invoiceTabs.filter(t => t.id !== id && String(t.id) !== String(id));
  reindexInvoiceTabs();
  if (state.selectedInvoiceTabId === id || String(state.selectedInvoiceTabId) === String(id)) {
    const nextTab = state.invoiceTabs[Math.max(0, index - 1)] || state.invoiceTabs[0];
    state.selectedInvoiceTabId = nextTab.id;
  }
  renderInvoiceTabs();
  renderCashierCart();
  setTimeout(() => {
    document.getElementById('cashierBarcodeInput')?.focus();
  }, 50);
}

// Flash Barcode Input RED on Not Found Error
function flashBarcodeError() {
  const input = document.getElementById('cashierBarcodeInput');
  if (!input) return;
  input.classList.remove('border-slate-200', 'dark:border-slate-800');
  input.classList.add('border-rose-500', 'bg-rose-50', 'dark:bg-rose-950/60', 'text-rose-500', 'ring-2', 'ring-rose-500');
  input.select();
  setTimeout(() => {
    input.classList.remove('border-rose-500', 'bg-rose-50', 'dark:bg-rose-950/60', 'text-rose-500', 'ring-2', 'ring-rose-500');
    input.classList.add('border-slate-200', 'dark:border-slate-800');
  }, 1800);
}

async function handleBarcodeKeyDown(e) {
  if (e.key === 'Enter') {
    e.preventDefault();
    const input = document.getElementById('cashierBarcodeInput');
    const rawQuery = input ? input.value : '';
    const query = rawQuery.replace(/[\r\n]/g, '').trim();
    if (!query) return;

    hideCashierSearchResults();

    // 1. Check local state.products (exact barcode or name)
    let matched = state.products.find(p => 
      (p.barcode && String(p.barcode).trim().toLowerCase() === query.toLowerCase()) ||
      (p.name && String(p.name).trim().toLowerCase() === query.toLowerCase())
    );

    // 2. Check local state.products (partial match or contains)
    if (!matched) {
      matched = state.products.find(p => 
        (p.barcode && String(p.barcode).includes(query)) ||
        (p.name && String(p.name).toLowerCase().includes(query.toLowerCase()))
      );
    }

    // 3. Fallback: Search directly in database via backend RPC
    if (!matched) {
      const res = await callBackend('find_product', { query });
      if (res && res.success && res.found && res.product) {
        matched = res.product;
        if (!state.products.some(p => p.id === matched.id)) {
          state.products.push(matched);
        }
      }
    }

    if (matched) {
      addItemToCurrentCart(matched);
      if (input) input.value = '';
      setTimeout(() => input?.focus(), 50);
    } else {
      flashBarcodeError();
    }
  }
}

function handleCashierSearchInput() {
  const input = document.getElementById('cashierBarcodeInput');
  const query = input ? input.value.trim().toLowerCase() : '';
  const resultsContainer = document.getElementById('cashierSearchResults');
  if (!resultsContainer) return;

  if (!query || query.length < 1) {
    resultsContainer.classList.add('hidden');
    resultsContainer.innerHTML = '';
    return;
  }

  const matches = state.products.filter(p => 
    (p.barcode && p.barcode.toLowerCase().includes(query)) ||
    (p.name && p.name.toLowerCase().includes(query))
  ).slice(0, 8);

  if (matches.length === 0) {
    resultsContainer.classList.add('hidden');
    return;
  }

  resultsContainer.innerHTML = '';
  matches.forEach(p => {
    const item = document.createElement('div');
    item.className = 'p-2.5 hover:bg-emerald-50 dark:hover:bg-slate-800 cursor-pointer flex items-center justify-between border-b border-slate-100 dark:border-slate-800 transition';
    item.onclick = () => {
      addItemToCurrentCart(p);
      input.value = '';
      resultsContainer.classList.add('hidden');
      input.focus();
    };
    item.innerHTML = `
      <div class="flex items-center gap-2">
        <span class="text-base">🏷</span>
        <div>
          <div class="font-bold text-xs text-slate-800 dark:text-white">${p.name}</div>
          <div class="text-[10px] font-mono text-slate-400">باركود: ${p.barcode || '--'}</div>
        </div>
      </div>
      <div class="text-right">
        <div class="font-black text-xs text-emerald-600 dark:text-emerald-400">${Number(p.price).toLocaleString()} د.ع</div>
        <div class="text-[10px] text-slate-400 font-bold">الرصيد: ${p.stockQuantity || 0} قطعة</div>
      </div>
    `;
    resultsContainer.appendChild(item);
  });
  resultsContainer.classList.remove('hidden');
}

function hideCashierSearchResults() {
  const rc = document.getElementById('cashierSearchResults');
  if (rc) rc.classList.add('hidden');
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
  const cartonPurchase = Number(document.getElementById('ap-cartonPurchase')?.value || 0);
  const cartonSelling = Number(document.getElementById('ap-cartonSelling')?.value || 0);
  const supplierName = document.getElementById('ap-supplier')?.value || '';

  const payload = {
    id: document.getElementById('ap-id')?.value || undefined,
    name: name,
    barcode: barcode || undefined,
    category: document.getElementById('ap-categorySelect')?.value || 'عام',
    supplierName: supplierName,
    cost: Number(document.getElementById('ap-cost')?.value || 0),
    price: Number(document.getElementById('ap-price')?.value || 0),
    wholesalePrice: Number(document.getElementById('ap-wholesalePrice')?.value || 0),
    cartonPurchasePrice: cartonPurchase,
    cartonSellingPrice: cartonSelling,
    stockQuantity: totalStock,
    cartonsCount: cartonsCount,
    piecesPerCarton: itemsPerCarton,
    minStockAlert: Number(document.getElementById('ap-minStockAlert')?.value || 5)
  };

  const res = await callBackend('save_product', payload);
  if (res && res.success) {
    alert('✔ تم حفظ المادة وتفاصيل الكرتون والأسعار بنجاح في قاعدة البيانات!');
    clearAddProductForm();
    await loadProducts();
    await loadInventory();
    switchTab('inventory');
  }
}

// ========================================================
// INVENTORY & WAREHOUSE MANAGEMENT (FULL DETAILS)
// ========================================================
let inventoryData = [];

async function loadInventory() {
  const res = await callBackend('get_inventory');
  if (!res || !res.success) return;

  inventoryData = res.products || [];

  // 1. Update KPI Summary Cards
  const totalProdsEl = document.getElementById('invTotalProducts');
  if (totalProdsEl) totalProdsEl.innerText = `${inventoryData.length} مادة`;

  const totalCostEl = document.getElementById('invTotalCostValue');
  if (totalCostEl) totalCostEl.innerText = `${Number(res.totalCostValue || 0).toLocaleString()} د.ع`;

  const totalSellEl = document.getElementById('invTotalSellingValue');
  if (totalSellEl) totalSellEl.innerText = `${Number(res.totalSellingValue || 0).toLocaleString()} د.ع`;

  const totalProfitEl = document.getElementById('invTotalProfitValue');
  if (totalProfitEl) totalProfitEl.innerText = `+${Number(res.expectedProfit || 0).toLocaleString()} د.ع`;

  // 2. Update Categories Filter Dropdown
  const catFilter = document.getElementById('invCategoryFilter');
  if (catFilter) {
    const cats = [...new Set(inventoryData.map(p => p.category).filter(Boolean))];
    const currentVal = catFilter.value;
    catFilter.innerHTML = '<option value="">جميع التصنيفات</option>';
    cats.forEach(c => {
      catFilter.innerHTML += `<option value="${c}">${c}</option>`;
    });
    if (cats.includes(currentVal)) catFilter.value = currentVal;
  }

  filterInventoryTable();
}

function filterInventoryTable() {
  const query = (document.getElementById('invSearchInput')?.value || '').toLowerCase().trim();
  const selCat = document.getElementById('invCategoryFilter')?.value || '';
  const lowStockOnly = document.getElementById('invLowStockOnly')?.checked || false;

  const tbody = document.getElementById('inventoryTableBody');
  if (!tbody) return;

  const filtered = inventoryData.filter(p => {
    const pName = (p.name || p.Name || '').toLowerCase();
    const pBar = (p.barcode || p.Barcode || '').toLowerCase();
    const pCat = (p.category || p.Category || '').toLowerCase();
    const pSup = (p.supplierName || p.SupplierName || '').toLowerCase();

    const matchesSearch = !query || 
      pName.includes(query) || 
      pBar.includes(query) ||
      pCat.includes(query) ||
      pSup.includes(query);
    
    const matchesCat = !selCat || (p.category === selCat || p.Category === selCat);
    const matchesLowStock = !lowStockOnly || (p.stockQuantity <= (p.minStockAlert || 5));

    return matchesSearch && matchesCat && matchesLowStock;
  });

  if (filtered.length === 0) {
    tbody.innerHTML = `<tr><td colspan="10" class="text-center py-12 text-slate-400 font-bold">لا توجد مواد مطابقة للبحث أو الفلترة</td></tr>`;
    return;
  }

  tbody.innerHTML = '';
  filtered.forEach((p, idx) => {
    const isLow = p.stockQuantity <= (p.minStockAlert || 5);
    const isOutOfStock = p.stockQuantity <= 0;
    const rProfit = (p.price || 0) - (p.cost || 0);
    const wProfit = (p.wholesalePrice || 0) - (p.cost || 0);
    const cProfit = (p.cartonSellingPrice || 0) - (p.cartonPurchasePrice || 0);
    const displayName = p.name || p.Name || 'مادة بدون اسم';
    const displayBarcode = p.barcode || p.Barcode || 'بدون باركود';

    const tr = document.createElement('tr');
    tr.className = 'hover:bg-slate-50 dark:hover:bg-slate-800/40 transition cursor-pointer';
    tr.onclick = (e) => {
      if (e.target.tagName !== 'BUTTON' && !e.target.closest('button')) {
        openProductDetailModal(p.id || p.Id);
      }
    };

    tr.innerHTML = `
      <td class="p-2.5 text-center text-slate-400 font-bold">${idx + 1}</td>
      <td class="p-2.5">
        <div class="font-black text-slate-800 dark:text-white text-xs">${displayName}</div>
        <div class="text-[10px] font-mono text-sky-500 font-bold flex items-center gap-1">
          <span>🏷</span><span>${displayBarcode}</span>
        </div>
      </td>
      <td class="p-2.5">
        <span class="inline-block px-2 py-0.5 rounded-md bg-slate-100 dark:bg-slate-800 text-[10px] font-bold text-slate-600 dark:text-slate-300 mb-0.5">${p.category || 'عام'}</span>
        ${p.supplierName ? `<div class="text-[10px] text-slate-400 font-semibold">🏢 ${p.supplierName}</div>` : ''}
      </td>
      <td class="p-2.5 text-center">
        <div class="font-bold text-slate-700 dark:text-slate-200 text-xs">${p.cartonsCount || 0} كرتون</div>
        <div class="text-[10px] text-slate-400 font-semibold">(${p.piecesPerCarton || 1} قطعة/كرتون)</div>
      </td>
      <td class="p-2.5 text-center">
        <div class="font-black text-xs ${isOutOfStock ? 'text-rose-500' : isLow ? 'text-amber-500' : 'text-slate-800 dark:text-white'}">
          ${p.stockQuantity} قطعة
        </div>
      </td>
      <td class="p-2.5 text-center">
        <div class="font-black text-blue-600 dark:text-blue-400 text-xs">${Number(p.cost).toLocaleString()} د.ع</div>
        ${p.cartonPurchasePrice > 0 ? `<div class="text-[10px] text-slate-400 font-semibold">شراء كرتون: ${Number(p.cartonPurchasePrice).toLocaleString()} د.ع</div>` : ''}
      </td>
      <td class="p-2.5 text-center space-y-0.5">
        <div class="text-xs font-black text-emerald-600 dark:text-emerald-400">مفرد: ${Number(p.price).toLocaleString()} د.ع</div>
        <div class="text-[10px] font-bold text-sky-600 dark:text-sky-400">جملة: ${Number(p.wholesalePrice || 0).toLocaleString()} د.ع</div>
        ${p.cartonSellingPrice > 0 ? `<div class="text-[10px] font-bold text-purple-600 dark:text-purple-400">كرتون: ${Number(p.cartonSellingPrice).toLocaleString()} د.ع</div>` : ''}
      </td>
      <td class="p-2.5 text-center space-y-0.5">
        <div class="text-[10px] font-black text-emerald-600 dark:text-emerald-400">ربح مفرد: +${Number(rProfit).toLocaleString()} د.ع</div>
        <div class="text-[10px] font-bold text-sky-600 dark:text-sky-400">ربح جملة: +${Number(wProfit).toLocaleString()} د.ع</div>
        ${cProfit > 0 ? `<div class="text-[10px] font-bold text-purple-600 dark:text-purple-400">ربح كرتون: +${Number(cProfit).toLocaleString()} د.ع</div>` : ''}
      </td>
      <td class="p-2.5 text-center">
        ${isOutOfStock 
          ? `<span class="px-2 py-0.5 rounded-full text-[10px] font-black bg-rose-100 text-rose-700 dark:bg-rose-950/60 dark:text-rose-400">نفذ الرصيد</span>` 
          : isLow 
          ? `<span class="px-2 py-0.5 rounded-full text-[10px] font-black bg-amber-100 text-amber-700 dark:bg-amber-950/60 dark:text-amber-400">نواقص (${p.stockQuantity})</span>` 
          : `<span class="px-2 py-0.5 rounded-full text-[10px] font-black bg-emerald-100 text-emerald-700 dark:bg-emerald-950/60 dark:text-emerald-400">متوفر ✔</span>`}
      </td>
      <td class="p-2.5 text-center">
        <div class="flex items-center justify-center gap-1.5">
          <button onclick="openProductDetailModal('${p.id}')" class="p-1.5 bg-slate-100 hover:bg-slate-200 dark:bg-slate-800 dark:hover:bg-slate-700 text-slate-700 dark:text-slate-300 rounded-lg text-xs" title="عرض التفاصيل الكاملة">👁</button>
          <button onclick="editProductFromInventory('${p.id}')" class="p-1.5 bg-sky-100 hover:bg-sky-200 dark:bg-sky-950/60 text-sky-600 dark:text-sky-400 rounded-lg text-xs font-bold" title="تعديل">✏</button>
          <button onclick="deleteProductFromInventory('${p.id}')" class="p-1.5 bg-rose-100 hover:bg-rose-200 dark:bg-rose-950/60 text-rose-600 dark:text-rose-400 rounded-lg text-xs font-bold" title="حذف">🗑</button>
        </div>
      </td>
    `;
    tbody.appendChild(tr);
  });
}

function openProductDetailModal(id) {
  const prod = inventoryData.find(p => p.id === id);
  if (!prod) return;

  document.getElementById('pdm-title').innerText = prod.name;
  
  const content = document.getElementById('pdm-content');
  if (content) {
    const rProfit = (prod.price || 0) - (prod.cost || 0);
    const rCartonProfit = rProfit * (prod.piecesPerCarton || 1);
    const wProfit = (prod.wholesalePrice || 0) - (prod.cost || 0);
    const cProfit = (prod.cartonSellingPrice || 0) - (prod.cartonPurchasePrice || 0);

    content.innerHTML = `
      <div class="grid grid-cols-2 gap-2 p-3 bg-slate-50 dark:bg-slate-900 rounded-2xl">
        <div><span class="text-slate-400 block text-[10px]">الباركود:</span><span class="font-mono font-bold text-sky-500">${prod.barcode || 'بدون باركود'}</span></div>
        <div><span class="text-slate-400 block text-[10px]">التصنيف:</span><span class="font-bold">${prod.category || 'عام'}</span></div>
        <div><span class="text-slate-400 block text-[10px]">المندوب / الشركة:</span><span class="font-bold">${prod.supplierName || 'بدون مندوب'}</span></div>
        <div><span class="text-slate-400 block text-[10px]">تاريخ التسجيل:</span><span class="font-bold text-slate-500">${prod.createdAt || '--'}</span></div>
      </div>

      <div class="grid grid-cols-3 gap-2 p-3 bg-sky-50/60 dark:bg-sky-950/30 rounded-2xl border border-sky-500/20 text-center">
        <div><span class="text-slate-400 block text-[10px]">عدد الكراتين:</span><span class="font-black text-sky-600">${prod.cartonsCount || 0} كرتون</span></div>
        <div><span class="text-slate-400 block text-[10px]">المواد بالكرتون:</span><span class="font-black text-sky-600">${prod.piecesPerCarton || 1} قطعة</span></div>
        <div><span class="text-slate-400 block text-[10px]">إجمالي الرصيد:</span><span class="font-black text-emerald-600 text-sm">${prod.stockQuantity} قطعة</span></div>
      </div>

      <div class="space-y-1.5 p-3 bg-emerald-50/60 dark:bg-emerald-950/30 rounded-2xl border border-emerald-500/20">
        <div class="flex justify-between font-bold"><span>تكلفة القطعة المعتمدة:</span><span class="text-blue-600 font-black">${Number(prod.cost).toLocaleString()} د.ع</span></div>
        <div class="flex justify-between font-bold"><span>سعر بيع المفرد:</span><span class="text-emerald-600 font-black">${Number(prod.price).toLocaleString()} د.ع</span></div>
        <div class="flex justify-between font-bold"><span>ربح القطعة المفردة:</span><span class="text-emerald-600 font-black">+${Number(rProfit).toLocaleString()} د.ع</span></div>
        <div class="flex justify-between font-bold"><span>ربح الكرتون كاملاً بالمفرد:</span><span class="text-emerald-600 font-black">+${Number(rCartonProfit).toLocaleString()} د.ع</span></div>
      </div>

      <div class="space-y-1.5 p-3 bg-purple-50/60 dark:bg-purple-950/30 rounded-2xl border border-purple-500/20">
        <div class="flex justify-between font-bold"><span>سعر شراء الكرتون:</span><span class="text-slate-700 dark:text-slate-200">${Number(prod.cartonPurchasePrice || 0).toLocaleString()} د.ع</span></div>
        <div class="flex justify-between font-bold"><span>سعر بيع الكرتون كاملاً:</span><span class="text-purple-600 font-black">${Number(prod.cartonSellingPrice || 0).toLocaleString()} د.ع</span></div>
        <div class="flex justify-between font-bold"><span>ربح بيع الكرتون:</span><span class="text-purple-600 font-black">+${Number(cProfit).toLocaleString()} د.ع</span></div>
      </div>
    `;
  }

  const editBtn = document.getElementById('pdm-editBtn');
  if (editBtn) {
    editBtn.onclick = () => {
      closeProductDetailModal();
      editProductFromInventory(prod.id);
    };
  }

  document.getElementById('productDetailModal')?.classList.remove('hidden');
}

function closeProductDetailModal() {
  document.getElementById('productDetailModal')?.classList.add('hidden');
}

async function deleteProductFromInventory(id) {
  const prod = inventoryData.find(p => p.id === id);
  const name = prod ? prod.name : 'هذه المادة';
  if (confirm(`هل أنت متأكد من حذف: "${name}" من المخزن؟`)) {
    const res = await callBackend('delete_product', { id });
    if (res && res.success) {
      await loadProducts();
      await loadInventory();
    }
  }
}

function editProductFromInventory(id) {
  const prod = inventoryData.find(p => p.id === id) || state.products.find(p => p.id === id);
  if (prod) {
    switchTab('addProduct');
    document.getElementById('ap-id').value = prod.id;
    document.getElementById('ap-name').value = prod.name;
    document.getElementById('ap-barcode').value = prod.barcode || '';
    document.getElementById('ap-categorySelect').value = prod.category || 'عام';
    document.getElementById('ap-supplier').value = prod.supplierName || '';
    document.getElementById('ap-cost').value = prod.cost;
    document.getElementById('ap-price').value = prod.price;
    document.getElementById('ap-wholesalePrice').value = prod.wholesalePrice || 0;
    document.getElementById('ap-cartonPurchase').value = prod.cartonPurchasePrice || 0;
    document.getElementById('ap-cartonSelling').value = prod.cartonSellingPrice || 0;
    document.getElementById('ap-itemsPerCarton').value = prod.piecesPerCarton || 1;
    document.getElementById('ap-cartonsCount').value = prod.cartonsCount || Math.floor((prod.stockQuantity || 0) / (prod.piecesPerCarton || 1));
    document.getElementById('ap-minStockAlert').value = prod.minStockAlert || 5;
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
// REP CLOUD ORDERS & LIVE NOTIFICATION SYSTEM
// ========================================================
let activeOrderModalData = null;
let knownPendingOrderIds = new Set();
let isFirstRepOrdersLoad = true;

// Synthesize pleasant two-tone notification sound (Web Audio API)
function playOrderNotificationSound() {
  try {
    const audioCtx = new (window.AudioContext || window.webkitAudioContext)();
    const now = audioCtx.currentTime;

    // Tone 1 (High bell chime)
    const osc1 = audioCtx.createOscillator();
    const gain1 = audioCtx.createGain();
    osc1.type = 'sine';
    osc1.frequency.setValueAtTime(587.33, now); // D5
    osc1.frequency.exponentialRampToValueAtTime(880.00, now + 0.15); // A5
    gain1.gain.setValueAtTime(0.35, now);
    gain1.gain.exponentialRampToValueAtTime(0.01, now + 0.5);
    osc1.connect(gain1);
    gain1.connect(audioCtx.destination);
    osc1.start(now);
    osc1.stop(now + 0.5);

    // Tone 2 (Sparkle chime)
    const osc2 = audioCtx.createOscillator();
    const gain2 = audioCtx.createGain();
    osc2.type = 'sine';
    osc2.frequency.setValueAtTime(1174.66, now + 0.18); // D6
    gain2.gain.setValueAtTime(0.4, now + 0.18);
    gain2.gain.exponentialRampToValueAtTime(0.001, now + 0.8);
    osc2.connect(gain2);
    gain2.connect(audioCtx.destination);
    osc2.start(now + 0.18);
    osc2.stop(now + 0.8);
  } catch (e) {
    console.log('[Audio] Notification sound skipped:', e);
  }
}

// Show floating interactive notification card
function showOrderNotificationToast(order) {
  const container = document.getElementById('orderNotificationToastContainer');
  if (!container) return;

  const toastId = 'toast_' + Date.now() + '_' + Math.random().toString(36).substr(2, 4);
  const toast = document.createElement('div');
  toast.id = toastId;
  toast.className = 'pointer-events-auto sh-card p-4 rounded-2xl shadow-2xl border-2 border-amber-500/80 bg-slate-900/95 text-white animate-in slide-in-from-top duration-300 flex items-start gap-3 backdrop-blur-md';
  
  toast.innerHTML = `
    <div class="w-10 h-10 rounded-2xl bg-amber-500 text-slate-950 flex items-center justify-center font-black text-xl flex-shrink-0 animate-bounce">
      🔔
    </div>
    <div class="flex-1 min-w-0">
      <div class="flex items-center justify-between gap-2 mb-1">
        <h4 class="font-black text-sm text-amber-400">وصلت طلبية جديدة الآن!</h4>
        <span class="text-[10px] font-mono text-slate-400">${order.orderNumber}</span>
      </div>
      <p class="text-xs font-bold text-slate-200 truncate">🏬 ${order.marketName || 'ماركت'}</p>
      <div class="flex items-center justify-between text-xs mt-1 text-slate-300">
        <span>👤 المندوب: <b class="text-sky-400">${order.representativeName || 'عام'}</b></span>
        <span class="font-black text-emerald-400">${Number(order.totalAmount).toLocaleString()} د.ع</span>
      </div>
      <div class="flex items-center gap-2 mt-3 pt-2 border-t border-slate-800">
        <button onclick="openRepOrderFromToast('${order.id}', '${toastId}')" class="flex-1 py-1.5 px-3 bg-amber-500 hover:bg-amber-400 text-slate-950 font-black text-xs rounded-xl shadow-md flex items-center justify-center gap-1">
          <span>🧾</span>
          <span>فتح الوصل والتجهيز</span>
        </button>
        <button onclick="dismissOrderToast('${toastId}')" class="py-1.5 px-2.5 bg-slate-800 hover:bg-slate-700 text-slate-300 font-bold text-xs rounded-xl">
          إغلاق
        </button>
      </div>
    </div>
  `;

  container.prepend(toast);

  // Auto remove after 14 seconds
  setTimeout(() => {
    dismissOrderToast(toastId);
  }, 14000);
}

function dismissOrderToast(toastId) {
  const el = document.getElementById(toastId);
  if (el) {
    el.classList.add('animate-out', 'fade-out', 'slide-out-to-top', 'duration-200');
    setTimeout(() => el.remove(), 200);
  }
}

function openRepOrderFromToast(orderId, toastId) {
  dismissOrderToast(toastId);
  switchTab('repOrders');
  openRepOrderInvoiceModal(orderId);
}

async function loadRepOrders(triggerAlerts = true) {
  const res = await callBackend('get_supplier_orders');
  if (!res || !res.success) return;

  const orders = res.orders || [];
  const pendingOrders = orders.filter(o => o.status === 'Pending');
  const pendingCount = pendingOrders.length;

  // Check for newly arrived pending orders
  if (triggerAlerts && !isFirstRepOrdersLoad) {
    pendingOrders.forEach(o => {
      if (!knownPendingOrderIds.has(o.id)) {
        playOrderNotificationSound();
        showOrderNotificationToast(o);
      }
    });
  }

  // Update known set
  knownPendingOrderIds = new Set(pendingOrders.map(o => o.id));
  isFirstRepOrdersLoad = false;

  const sideBadge = document.getElementById('repBadgeSidebar');
  const bellBadge = document.getElementById('repBellBadge');
  const notifBadge = document.getElementById('repNotificationCountBadge');

  if (pendingCount > 0) {
    if (sideBadge) { sideBadge.innerText = pendingCount; sideBadge.classList.remove('hidden'); }
    if (bellBadge) { bellBadge.classList.remove('hidden'); }
    if (notifBadge) { notifBadge.innerText = pendingCount; notifBadge.classList.remove('hidden'); }
  } else {
    if (sideBadge) sideBadge.classList.add('hidden');
    if (bellBadge) bellBadge.classList.add('hidden');
    if (notifBadge) notifBadge.classList.add('hidden');
  }

  const tbody = document.getElementById('repOrdersTableBody');
  if (!tbody) return;

  if (orders.length === 0) {
    tbody.innerHTML = `<tr><td colspan="8" class="text-center py-12 text-slate-400 font-bold">لا توجد طلبيات سحابية واردة حتى الآن</td></tr>`;
    return;
  }

  tbody.innerHTML = '';
  orders.forEach(o => {
    const tr = document.createElement('tr');
    tr.className = 'hover:bg-slate-50 dark:hover:bg-slate-800/40 transition';
    
    let badgeClass = 'bg-amber-100 text-amber-700 dark:bg-amber-950/60 dark:text-amber-400';
    let statusText = 'قيد الانتظار';
    if (o.status === 'InPreparation') {
      badgeClass = 'bg-sky-100 text-sky-700 dark:bg-sky-950/60 dark:text-sky-400';
      statusText = 'جاري التجهيز';
    } else if (o.status === 'Delivered') {
      badgeClass = 'bg-emerald-100 text-emerald-700 dark:bg-emerald-950/60 dark:text-emerald-400';
      statusText = 'تم التسليم ✔';
    } else if (o.status === 'Cancelled') {
      badgeClass = 'bg-rose-100 text-rose-700 dark:bg-rose-950/60 dark:text-rose-400';
      statusText = 'ملغية ✕';
    }

    tr.innerHTML = `
      <td class="p-3 font-mono font-bold text-sky-500">${o.orderNumber}</td>
      <td class="p-3 font-bold">
        <div>${o.marketName || 'ماركت'}</div>
        <div class="text-[10px] text-slate-400 font-normal">${o.marketPhone || ''} ${o.marketCity ? '• ' + o.marketCity : ''}</div>
      </td>
      <td class="p-3 font-bold text-slate-600 dark:text-slate-300">👤 ${o.representativeName || '--'}</td>
      <td class="p-3 text-center font-bold text-amber-500">${o.itemsCount} مواد</td>
      <td class="p-3 text-center font-black text-slate-800 dark:text-white">${Number(o.totalAmount).toLocaleString()} د.ع</td>
      <td class="p-3 text-center text-slate-400 text-[11px] font-mono">${o.date}</td>
      <td class="p-3 text-center"><span class="px-2.5 py-1 rounded-full text-[10px] font-black ${badgeClass}">${statusText}</span></td>
      <td class="p-3 text-center">
        <div class="flex items-center justify-center gap-1.5">
          <button onclick="openRepOrderInvoiceModal('${o.id}')" class="px-3 py-1.5 bg-amber-500 hover:bg-amber-600 text-white font-bold text-xs rounded-xl shadow-sm flex items-center gap-1">
            <span>🧾</span>
            <span>فتح الوصل وتعديل الأسعار</span>
          </button>
          <button onclick="updateOrderStatus('${o.id}', 'Delivered')" class="px-2.5 py-1.5 bg-emerald-600 hover:bg-emerald-500 text-white font-bold text-xs rounded-xl shadow-sm" title="تسليم سريع">
            ✔
          </button>
        </div>
      </td>
    `;
    tbody.appendChild(tr);
  });
}

// ========================================================
// CREATE REP ACCOUNT MODAL
// ========================================================
function openCreateRepAccountModal() {
  document.getElementById('newRep-name').value = '';
  document.getElementById('newRep-company').value = '';
  document.getElementById('newRep-phone').value = '';
  document.getElementById('newRep-balance').value = '0';
  document.getElementById('newRep-address').value = '';
  document.getElementById('createRepAccountModal')?.classList.remove('hidden');
}

function closeCreateRepAccountModal() {
  document.getElementById('createRepAccountModal')?.classList.add('hidden');
}

async function saveNewRepAccount() {
  const name = document.getElementById('newRep-name')?.value.trim();
  const company = document.getElementById('newRep-company')?.value.trim();
  if (!name || !company) {
    alert('يرجى كتابة اسم المندوب واسم الشركة أولاً!');
    return;
  }

  const payload = {
    name: name,
    company: company,
    phone: document.getElementById('newRep-phone')?.value.trim() || '',
    balance: Number(document.getElementById('newRep-balance')?.value || 0),
    address: document.getElementById('newRep-address')?.value.trim() || ''
  };

  const res = await callBackend('create_rep_account', payload);
  if (res && res.success) {
    alert(`✔ تم إنشاء حساب المندوب (${name}) بنجاح!`);
    closeCreateRepAccountModal();
    await loadSuppliersList();
    await loadSuppliers();
  }
}

// ========================================================
// INTERACTIVE ORDER INVOICE MODAL (PRICE & QTY EDITOR)
// ========================================================
async function openRepOrderInvoiceModal(orderId) {
  const res = await callBackend('get_rep_order_details', { id: orderId });
  if (!res || !res.success || !res.order) {
    alert('تعذر تحميل تفاصيل الوصل');
    return;
  }

  activeOrderModalData = res.order;
  const o = res.order;

  document.getElementById('oim-orderNumber').innerText = o.orderNumber;
  document.getElementById('oim-marketName').innerText = o.marketName || 'ماركت';
  document.getElementById('oim-marketContact').innerText = `${o.marketPhone || '--'} | ${o.marketAddress || 'العراق'}`;
  document.getElementById('oim-repName').innerText = o.representativeName || o.supplierName || 'مندوب عام';
  document.getElementById('oim-notes').value = o.notes || '';
  document.getElementById('oim-statusSelect').value = o.status || 'Pending';

  renderOrderModalItemsTable();
  recalcOrderModalInvoice();

  document.getElementById('orderInvoiceModal')?.classList.remove('hidden');
}

function closeOrderInvoiceModal() {
  document.getElementById('orderInvoiceModal')?.classList.add('hidden');
  activeOrderModalData = null;
}

function renderOrderModalItemsTable() {
  const tbody = document.getElementById('oim-itemsTableBody');
  if (!tbody || !activeOrderModalData) return;

  tbody.innerHTML = '';
  (activeOrderModalData.items || []).forEach((item, idx) => {
    const tr = document.createElement('tr');
    tr.id = `oim-row-${item.id}`;
    tr.className = 'hover:bg-slate-50 dark:hover:bg-slate-800/40';
    tr.innerHTML = `
      <td class="p-2.5">
        <div class="font-bold text-slate-800 dark:text-white text-xs">${item.productName}</div>
        <div class="text-[10px] text-slate-400 font-mono">${item.barcode || '--'}</div>
      </td>
      <td class="p-2.5 text-center">
        <input type="number" step="1" min="1" value="${item.quantity}" oninput="updateOrderModalItemValue('${item.id}', 'quantity', this.value)" class="w-20 bg-slate-50 dark:bg-slate-900 border border-slate-200 dark:border-slate-800 rounded-lg px-2 py-1 text-center font-bold text-sky-500">
      </td>
      <td class="p-2.5 text-center">
        <input type="number" step="250" min="0" value="${item.unitPrice}" oninput="updateOrderModalItemValue('${item.id}', 'unitPrice', this.value)" class="w-24 bg-slate-50 dark:bg-slate-900 border border-slate-200 dark:border-slate-800 rounded-lg px-2 py-1 text-center font-bold text-emerald-600">
      </td>
      <td class="p-2.5 text-center font-black text-slate-800 dark:text-white text-xs" id="oim-item-total-${item.id}">
        ${Number(item.quantity * item.unitPrice).toLocaleString()} د.ع
      </td>
      <td class="p-2.5 text-center">
        <button onclick="removeOrderModalItem('${item.id}')" class="text-rose-500 hover:text-rose-600 font-black text-xs">🗑</button>
      </td>
    `;
    tbody.appendChild(tr);
  });
}

function updateOrderModalItemValue(itemId, field, value) {
  if (!activeOrderModalData) return;
  const item = activeOrderModalData.items.find(i => i.id === itemId);
  if (item) {
    item[field] = Math.max(0, Number(value) || 0);
    const totalEl = document.getElementById(`oim-item-total-${itemId}`);
    if (totalEl) {
      totalEl.innerText = `${Number(item.quantity * item.unitPrice).toLocaleString()} د.ع`;
    }
    recalcOrderModalInvoice();
  }
}

function removeOrderModalItem(itemId) {
  if (!activeOrderModalData) return;
  activeOrderModalData.items = activeOrderModalData.items.filter(i => i.id !== itemId);
  renderOrderModalItemsTable();
  recalcOrderModalInvoice();
}

function recalcOrderModalInvoice() {
  if (!activeOrderModalData) return;

  const currentOrderTotal = activeOrderModalData.items.reduce((sum, i) => sum + (i.quantity * i.unitPrice), 0);
  const prevDebt = Number(activeOrderModalData.previousDebt || 0);
  const grandTotal = prevDebt + currentOrderTotal;

  document.getElementById('oim-previousDebt').innerText = `${Number(prevDebt).toLocaleString()} د.ع`;
  document.getElementById('oim-currentTotal').innerText = `${Number(currentOrderTotal).toLocaleString()} د.ع`;
  document.getElementById('oim-grandTotal').innerText = `${Number(grandTotal).toLocaleString()} د.ع`;
}

async function syncCloudNow() {
  const btn = document.getElementById('syncCloudBtn');
  if (btn) btn.innerHTML = '<span>⏳</span><span>جارٍ المزامنة مع السحابة...</span>';

  const res = await callBackend('sync_cloud_orders');
  if (res && res.success) {
    alert('✔ تمت المزامنة مع السحابة وتحديث كافة طلبيات المناديب بنجاح!');
    await loadRepOrders();
    await loadInventory();
    await loadDashboard();
  }
  if (btn) btn.innerHTML = '<span>🔄</span><span>مزامنة السحابة والطلبيات</span>';
}

async function acceptRepOrderDirectly(orderId) {
  if (confirm('هل أنت متأكد من قبول واعتماد هذه الطلبية وخصم موادها من المخزن فوراً؟')) {
    const res = await callBackend('accept_rep_order', { id: orderId, status: 'Delivered' });
    if (res && res.success) {
      alert('✔ تم قبول الطلبية، خصم المواد من المخزن، ومزامنة تطبيق المندوب بنجاح!');
      await loadRepOrders();
      await loadInventory();
      await loadDashboard();
    }
  }
}

async function saveEditedRepOrder() {
  if (!activeOrderModalData) return;

  const status = document.getElementById('oim-statusSelect')?.value || 'InPreparation';
  const payload = {
    id: activeOrderModalData.id,
    status: status,
    notes: document.getElementById('oim-notes')?.value || '',
    items: activeOrderModalData.items.map(i => ({
      id: i.id,
      quantity: i.quantity,
      unitPrice: i.unitPrice
    }))
  };

  // If status is Delivered, also deduct stock
  let res;
  if (status === 'Delivered') {
    res = await callBackend('accept_rep_order', payload);
  } else {
    res = await callBackend('save_rep_order_items', payload);
  }

  if (res && res.success) {
    alert('✔ تم حفظ التعديلات وتحديث الوصل بنجاح!');
    closeOrderInvoiceModal();
    await loadRepOrders();
    await loadInventory();
    await loadDashboard();
  }
}

function printRepOrderInvoice() {
  if (!activeOrderModalData) return;
  window.print();
}

async function updateOrderStatus(id, status) {
  if (status === 'Delivered') {
    await acceptRepOrderDirectly(id);
  } else {
    await callBackend('save_rep_order_items', { id, status });
    await loadRepOrders();
  }
}

function openRepPortalModal() {
  document.getElementById('repModal')?.classList.remove('hidden');
}

function closeRepPortalModal() {
  document.getElementById('repModal')?.classList.add('hidden');
}

// ========================================================
// CASHIER CONTINUOUS AUTO-FOCUS MANAGEMENT
// ========================================================
document.addEventListener('click', (e) => {
  if (state.activeTab !== 'cashier') return;
  const tag = e.target.tagName;
  const isInteractiveInput = tag === 'INPUT' || tag === 'SELECT' || tag === 'TEXTAREA' || tag === 'BUTTON';
  const inModal = e.target.closest('.fixed') || e.target.closest('#productDetailModal') || e.target.closest('#orderInvoiceModal') || e.target.closest('#createRepAccountModal') || e.target.closest('#repModal');
  if (!isInteractiveInput && !inModal) {
    document.getElementById('cashierBarcodeInput')?.focus();
  }
});


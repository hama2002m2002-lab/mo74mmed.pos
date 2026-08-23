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

  // 4. Auto-hide the global top navbar when in Cashier view to give 100% full screen
  const appTopHeader = document.getElementById('appTopHeader');
  if (appTopHeader) {
    if (tabId === 'cashier') {
      appTopHeader.classList.add('hidden');
    } else {
      appTopHeader.classList.remove('hidden');
    }
  }

  // 5. Activate selected tab
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
  if (tabId === 'customers') loadCustomers();
  if (tabId === 'stockAudit') loadStockAudit();
  if (tabId === 'damagedItems') loadDamagedItems();
  if (tabId === 'reports') loadReports();
  if (tabId === 'users') loadUsers();
  if (tabId === 'printing') loadPrintingSettings();
  if (tabId === 'settings') loadSettingsInfo();

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
  const posThemeText = document.getElementById('posThemeText');
  if (theme === 'dark') {
    document.body.classList.add('dark-theme');
    if (icon) icon.innerText = '☀️';
    if (posThemeText) posThemeText.innerText = 'شەو';
  } else {
    document.body.classList.remove('dark-theme');
    if (icon) icon.innerText = '🌙';
    if (posThemeText) posThemeText.innerText = 'ڕۆژ';
  }
}

function toggleLanguage() {
  state.language = state.language === 'ar' ? 'ku' : 'ar';
  setLanguage(state.language);
}

function setLanguage(lang) {
  state.language = lang;
  localStorage.setItem('pos_lang', lang);
  ['ar', 'ku', 'en'].forEach(l => {
    const btn = document.getElementById(`posLang${l.charAt(0).toUpperCase() + l.slice(1)}`);
    if (btn) {
      if (l === lang) {
        btn.className = 'px-2.5 py-1 rounded-xl bg-sky-500 text-white transition';
      } else {
        btn.className = 'px-2.5 py-1 rounded-xl text-slate-300 hover:text-white transition';
      }
    }
  });
  const langBtnText = document.getElementById('langBtnText');
  if (langBtnText) {
    langBtnText.innerText = lang === 'ar' ? 'العربية' : lang === 'ku' ? 'کوردی' : 'English';
  }
  applyLanguage(lang);
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
    const badge = document.getElementById('cashierTotalProductsBadge');
    if (badge) badge.innerText = state.products.length.toLocaleString();
    if (showAlert) {
      alert(`✔ تم تحديث قائمة المواد من المخزن بنجاح! (${state.products.length.toLocaleString()} مادة جاهزة للبيع)`);
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

// Ensure invoice tabs are strictly numbered sequentially (پەنجەرە 1, پەنجەرە 2...)
function reindexInvoiceTabs() {
  state.invoiceTabs.forEach((tab, index) => {
    tab.title = `پەنجەرە ${index + 1}`;
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
    tabEl.className = `flex items-center gap-1.5 px-3 py-1.5 rounded-xl cursor-pointer text-xs font-bold transition border ${isSel ? 'bg-gradient-to-r from-teal-600 to-emerald-600 text-white border-teal-400 shadow-md' : 'bg-slate-900/90 border-slate-800 text-slate-300 hover:text-white'}`;
    tabEl.onclick = () => selectInvoiceTab(t.id);
    tabEl.innerHTML = `
      <span>${t.title}</span>
      <span class="bg-black/30 px-1.5 py-0.2 rounded-full text-[10px] font-mono">${t.items.length}</span>
      ${state.invoiceTabs.length > 1 ? `<button onclick="event.stopPropagation(); closeInvoiceTab('${t.id}')" class="text-rose-300 hover:text-rose-100 px-1 font-black" title="داخستن">✕</button>` : ''}
    `;
    container.appendChild(tabEl);
  });
}

function addNewInvoiceTab() {
  const newId = 'inv_' + Date.now() + '_' + Math.floor(Math.random() * 1000);
  state.invoiceTabs.push({
    id: newId,
    title: `پەنجەرە ${state.invoiceTabs.length + 1}`,
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
  input.classList.add('border-rose-500', 'text-rose-400', 'ring-4', 'ring-rose-500/30');
  input.select();
  setTimeout(() => {
    input.classList.remove('border-rose-500', 'text-rose-400', 'ring-4', 'ring-rose-500/30');
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
    item.className = 'p-3 hover:bg-slate-800/80 cursor-pointer flex items-center justify-between transition border-b border-slate-800/60';
    item.onclick = () => {
      addItemToCurrentCart(p);
      input.value = '';
      resultsContainer.classList.add('hidden');
      input.focus();
    };
    item.innerHTML = `
      <div class="flex items-center gap-2.5">
        <span class="text-lg">🏷</span>
        <div>
          <div class="font-bold text-xs text-white">${p.name || p.Name}</div>
          <div class="text-[10px] font-mono text-sky-400">باركود: ${p.barcode || p.Barcode || '--'}</div>
        </div>
      </div>
      <div class="text-left">
        <div class="font-black text-xs text-emerald-400 font-mono">${Number(p.price || p.Price || 0).toLocaleString()} د.ع</div>
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
  const prodId = product.id || product.Id;
  const prodName = product.name || product.Name || 'مادة بدون اسم';
  const prodBarcode = product.barcode || product.Barcode || '--';
  const prodPrice = Number(product.price ?? product.Price ?? 0) || 0;
  const prodCost = Number(product.cost ?? product.Cost ?? 0) || 0;

  const existing = currentTab.items.find(i => i.id === prodId);

  if (existing) {
    existing.qty = (Number(existing.qty) || 1) + 1;
  } else {
    currentTab.items.push({
      id: prodId,
      name: prodName,
      barcode: prodBarcode,
      price: prodPrice,
      cost: prodCost,
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
    item.qty = (Number(item.qty) || 1) + delta;
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

function clearCurrentInvoice() {
  const currentTab = getCurrentTab();
  currentTab.items = [];
  const discountInput = document.getElementById('cashierDiscountInput');
  const paidInput = document.getElementById('cashierPaidInput');
  const taxInput = document.getElementById('cashierTaxInput');
  if (discountInput) discountInput.value = 0;
  if (paidInput) paidInput.value = 0;
  if (taxInput) taxInput.value = 0;

  renderInvoiceTabs();
  renderCashierCart();
  setTimeout(() => {
    document.getElementById('cashierBarcodeInput')?.focus();
  }, 50);
}

function renderCashierCart() {
  const currentTab = getCurrentTab();
  const emptyState = document.getElementById('cashierCartEmptyState');
  const tableWrapper = document.getElementById('cashierCartTableWrapper');
  const tbody = document.getElementById('cashierCartTbody');

  if (!tbody) return;

  if (currentTab.items.length === 0) {
    if (emptyState) emptyState.classList.remove('hidden');
    if (tableWrapper) tableWrapper.classList.add('hidden');
    recalcCashierInvoice();
    return;
  }

  if (emptyState) emptyState.classList.add('hidden');
  if (tableWrapper) tableWrapper.classList.remove('hidden');

  tbody.innerHTML = '';
  currentTab.items.forEach((item, index) => {
    const itemPrice = Number(item.price) || 0;
    const itemQty = Number(item.qty) || 1;
    const itemTotal = itemPrice * itemQty;

    const tr = document.createElement('tr');
    tr.className = 'hover:bg-slate-800/50 transition';
    tr.innerHTML = `
      <td class="p-3 text-center text-slate-400 font-bold font-mono">${index + 1}</td>
      <td class="p-3 font-bold text-white">${item.name}</td>
      <td class="p-3 font-mono text-sky-400 text-xs">${item.barcode || '--'}</td>
      <td class="p-3 text-center font-bold text-emerald-400 font-mono">${itemPrice.toLocaleString()} د.ع</td>
      <td class="p-3 text-center">
        <div class="inline-flex items-center gap-2 bg-[#0b1329] border border-slate-700 px-2 py-1 rounded-xl">
          <button onclick="updateCartItemQty('${item.id}', -1)" class="w-6 h-6 rounded-lg bg-rose-500/20 hover:bg-rose-500/40 text-rose-400 font-black text-sm flex items-center justify-center">-</button>
          <span class="font-black px-1 text-white font-mono">${itemQty}</span>
          <button onclick="updateCartItemQty('${item.id}', 1)" class="w-6 h-6 rounded-lg bg-emerald-500/20 hover:bg-emerald-500/40 text-emerald-400 font-black text-sm flex items-center justify-center">+</button>
        </div>
      </td>
      <td class="p-3 text-center font-black text-emerald-300 font-mono">${itemTotal.toLocaleString()} د.ع</td>
      <td class="p-3 text-center">
        <button onclick="removeCartItem('${item.id}')" class="p-1.5 hover:bg-rose-500/20 text-rose-400 rounded-lg font-bold text-xs" title="سڕینەوە">🗑</button>
      </td>
    `;
    tbody.appendChild(tr);
  });

  recalcCashierInvoice();
}

function recalcCashierInvoice() {
  const currentTab = getCurrentTab();
  const totalItems = currentTab.items.reduce((sum, i) => sum + (Number(i.qty) || 1), 0);
  const subtotal = currentTab.items.reduce((sum, i) => sum + ((Number(i.price) || 0) * (Number(i.qty) || 1)), 0);
  const discount = Math.max(0, Number(document.getElementById('cashierDiscountInput')?.value || 0));
  const tax = Math.max(0, Number(document.getElementById('cashierTaxInput')?.value || 0));
  const paid = Math.max(0, Number(document.getElementById('cashierPaidInput')?.value || 0));

  const total = Math.max(0, subtotal - discount + tax);
  const change = Math.max(0, paid - total);

  const subtotalEl = document.getElementById('cashierSubtotal');
  const totalEl = document.getElementById('cashierTotalDisplay');
  const changeEl = document.getElementById('cashierChangeDisplay');
  const countBadgeEl = document.getElementById('cashierItemCountBadge');

  if (subtotalEl) subtotalEl.innerText = `${subtotal.toLocaleString()} د.ع`;
  if (totalEl) totalEl.innerText = `${total.toLocaleString()} د.ع`;
  if (changeEl) changeEl.innerText = `${change.toLocaleString()} د.ع`;
  if (countBadgeEl) countBadgeEl.innerText = `${totalItems} کاڵا لە سەبەتەدا`;
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
  ['Cash', 'Debt', 'Card', 'Nfc'].forEach(m => {
    const btn = document.getElementById(`pm-${m}`);
    if (btn) {
      if (m === pm) {
        btn.className = 'py-2 px-3 bg-emerald-600 hover:bg-emerald-500 text-white font-bold text-xs rounded-xl shadow-md flex items-center justify-center gap-1.5 transition';
      } else {
        btn.className = 'py-2 px-3 bg-[#060c1c] hover:bg-slate-800 text-slate-300 border border-slate-700 font-bold text-xs rounded-xl flex items-center justify-center gap-1.5 transition';
      }
    }
  });
}

async function submitCashierSale(printReceipt = false) {
  const currentTab = getCurrentTab();
  if (currentTab.items.length === 0) {
    alert('سەبەتە بەتاڵە! تکایە پێش فرۆشتن کاڵا زیاد بکە.');
    return;
  }

  const discount = Math.max(0, Number(document.getElementById('cashierDiscountInput')?.value || 0));
  const payload = {
    paymentMethod: currentTab.paymentMethod || 'Cash',
    discount: discount,
    items: currentTab.items
  };

  const res = await callBackend('complete_sale', payload);
  if (res && res.success) {
    alert(`🎉 فرۆشتن بە سەرکەوتوویی تەواو بوو!\nژمارەی پسوولە: ${res.invoiceNumber}\nبڕی پارە: ${Number(res.total).toLocaleString()} د.ع`);
    
    if (printReceipt || isFastPrintEnabled) {
      window.print();
    }

    currentTab.items = [];
    document.getElementById('cashierDiscountInput').value = 0;
    document.getElementById('cashierPaidInput').value = 0;
    document.getElementById('cashierTaxInput').value = 0;
    
    renderInvoiceTabs();
    renderCashierCart();
    loadDashboard();
    loadInventory();
  } else {
    alert('هەڵەیەک ڕوویدا لە کاتی فرۆشتندا: ' + (res?.message || ''));
  }
}

// --------------------------------------------------------
// QUICK WAREHOUSE PRODUCTS MODAL (کۆگا F4)
// --------------------------------------------------------
function openQuickProductModal() {
  renderQuickProductsGrid(state.products);
  document.getElementById('quickProductModal')?.classList.remove('hidden');
  setTimeout(() => {
    document.getElementById('qpm-search')?.focus();
  }, 100);
}

function closeQuickProductModal() {
  document.getElementById('quickProductModal')?.classList.add('hidden');
  document.getElementById('cashierBarcodeInput')?.focus();
}

function renderQuickProductsGrid(list) {
  const grid = document.getElementById('qpm-grid');
  if (!grid) return;

  grid.innerHTML = '';
  list.slice(0, 60).forEach(p => {
    const card = document.createElement('div');
    card.className = 'p-3 bg-slate-900/90 hover:bg-slate-800 border border-slate-700/80 hover:border-emerald-500 rounded-xl cursor-pointer transition flex flex-col justify-between';
    card.onclick = () => {
      addItemToCurrentCart(p);
      closeQuickProductModal();
    };
    card.innerHTML = `
      <div>
        <div class="font-bold text-xs text-white line-clamp-1">${p.name || p.Name}</div>
        <div class="text-[10px] font-mono text-sky-400">${p.barcode || p.Barcode || '--'}</div>
      </div>
      <div class="flex items-center justify-between pt-2 mt-2 border-t border-slate-800 text-xs">
        <span class="font-black text-emerald-400 font-mono">${Number(p.price || p.Price || 0).toLocaleString()} د.ع</span>
        <span class="text-[10px] text-slate-400 font-bold">رصيد: ${p.stockQuantity || 0}</span>
      </div>
    `;
    grid.appendChild(card);
  });
}

function filterQuickProductModal() {
  const q = (document.getElementById('qpm-search')?.value || '').toLowerCase().trim();
  const filtered = state.products.filter(p => 
    (p.name && p.name.toLowerCase().includes(q)) || (p.barcode && p.barcode.includes(q))
  );
  renderQuickProductsGrid(filtered);
}

let isFastPrintEnabled = false;
function toggleFastPrint() {
  isFastPrintEnabled = !isFastPrintEnabled;
  const btn = document.getElementById('btnFastPrint');
  if (btn) {
    btn.className = isFastPrintEnabled 
      ? 'px-3.5 py-2 bg-teal-600 text-white font-bold text-xs rounded-xl shadow-md flex items-center gap-1.5 transition whitespace-nowrap'
      : 'px-3.5 py-2 bg-slate-900 border border-teal-500/40 text-teal-400 hover:bg-teal-950/40 rounded-xl text-xs font-bold flex items-center gap-1.5 transition whitespace-nowrap';
  }
}

let isSmartPrintEnabled = false;
function toggleSmartPrint() {
  isSmartPrintEnabled = !isSmartPrintEnabled;
  const btn = document.getElementById('btnSmartPrint');
  if (btn) {
    btn.className = isSmartPrintEnabled
      ? 'flex-1 py-1.5 bg-teal-600 text-white font-bold rounded-xl text-[11px] flex items-center justify-center gap-1 transition'
      : 'flex-1 py-1.5 bg-[#060c1c] border border-teal-500/40 text-teal-400 hover:bg-teal-950/40 rounded-xl text-[11px] font-bold flex items-center justify-center gap-1 transition';
  }
}

let isReturnMode = false;
function toggleReturnMode() {
  isReturnMode = !isReturnMode;
  const btn = document.getElementById('btnReturnMode');
  if (btn) {
    btn.className = isReturnMode
      ? 'px-3 py-2 bg-rose-600 text-white font-bold text-xs rounded-xl shadow-md flex items-center gap-1.5 transition whitespace-nowrap'
      : 'px-3 py-2 bg-slate-900 border border-slate-700 text-slate-300 hover:bg-slate-800 rounded-xl text-xs font-bold flex items-center gap-1.5 transition whitespace-nowrap';
  }
}

// Global Keyboard Shortcuts Listener
window.addEventListener('keydown', (e) => {
  if (state.activeTab !== 'cashier') return;

  if (e.key === 'F1') {
    e.preventDefault();
    addNewInvoiceTab();
  } else if (e.key === 'F3') {
    e.preventDefault();
    document.getElementById('cashierBarcodeInput')?.focus();
  } else if (e.key === 'F4') {
    e.preventDefault();
    openQuickProductModal();
  } else if (e.key === 'F8') {
    e.preventDefault();
    clearCurrentInvoice();
  } else if (e.key === 'F12') {
    e.preventDefault();
    submitCashierSale(true);
  }
});

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
let invFilteredData = [];

async function loadInventory() {
  const res = await callBackend('get_inventory');
  if (!res || !res.success) return;

  inventoryData = res.products || [];

  // 1. Update KPI Summary Cards
  const totalProdsEl = document.getElementById('invTotalProducts');
  if (totalProdsEl) totalProdsEl.innerText = `${inventoryData.length.toLocaleString()} مادة`;

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

  invFilteredData = inventoryData.filter(p => {
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

  renderInventoryTable();
}

function renderInventoryTable() {
  const tbody = document.getElementById('inventoryTableBody');
  const summaryEl = document.getElementById('invCountSummary');
  const showAllBtn = document.getElementById('invShowAllBtn');
  if (!tbody) return;

  if (invFilteredData.length === 0) {
    tbody.innerHTML = `<tr><td colspan="10" class="text-center py-12 text-slate-400 font-bold">لا توجد مواد مطابقة للبحث أو الفلترة</td></tr>`;
    if (summaryEl) summaryEl.innerText = 'يتم عرض 0 مادة';
    if (showAllBtn) showAllBtn.classList.add('hidden');
    return;
  }

  const limitVal = document.getElementById('invDisplayLimit')?.value || '1000';
  let limit = invFilteredData.length;
  if (limitVal !== 'all') {
    limit = parseInt(limitVal, 10) || 1000;
  }

  const displayItems = invFilteredData.slice(0, limit);

  let rowsHtml = '';
  for (let i = 0; i < displayItems.length; i++) {
    const p = displayItems[i];
    const isLow = p.stockQuantity <= (p.minStockAlert || 5);
    const isOutOfStock = p.stockQuantity <= 0;
    const rProfit = (p.price || 0) - (p.cost || 0);
    const wProfit = (p.wholesalePrice || 0) - (p.cost || 0);
    const cProfit = (p.cartonSellingPrice || 0) - (p.cartonPurchasePrice || 0);
    const displayName = p.name || p.Name || 'مادة بدون اسم';
    const displayBarcode = p.barcode || p.Barcode || 'بدون باركود';

    rowsHtml += `
      <tr class="hover:bg-slate-50 dark:hover:bg-slate-800/40 transition cursor-pointer" onclick="handleInventoryRowClick(event, '${p.id || p.Id}')">
        <td class="p-2.5 text-center text-slate-400 font-bold">${i + 1}</td>
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
            <button onclick="openProductDetailModal('${p.id || p.Id}')" class="p-1.5 bg-slate-100 hover:bg-slate-200 dark:bg-slate-800 dark:hover:bg-slate-700 text-slate-700 dark:text-slate-300 rounded-lg text-xs" title="عرض التفاصيل الكاملة">👁</button>
            <button onclick="editProductFromInventory('${p.id || p.Id}')" class="p-1.5 bg-sky-100 hover:bg-sky-200 dark:bg-sky-950/60 text-sky-600 dark:text-sky-400 rounded-lg text-xs font-bold" title="تعديل">✏</button>
            <button onclick="deleteProductFromInventory('${p.id || p.Id}')" class="p-1.5 bg-rose-100 hover:bg-rose-200 dark:bg-rose-950/60 text-rose-600 dark:text-rose-400 rounded-lg text-xs font-bold" title="حذف">🗑</button>
          </div>
        </td>
      </tr>
    `;
  }

  tbody.innerHTML = rowsHtml;

  if (summaryEl) {
    summaryEl.innerText = `يتم عرض ${displayItems.length.toLocaleString()} مادة من إجمالي ${invFilteredData.length.toLocaleString()} مادة`;
  }

  if (showAllBtn) {
    if (invFilteredData.length > displayItems.length) {
      showAllBtn.innerText = `👁 عرض كافة المواد (${invFilteredData.length.toLocaleString()} مادة) دفعة واحدة`;
      showAllBtn.classList.remove('hidden');
    } else {
      showAllBtn.classList.add('hidden');
    }
  }
}

function showAllInventoryItems() {
  const limitSelect = document.getElementById('invDisplayLimit');
  if (limitSelect) limitSelect.value = 'all';
  renderInventoryTable();
}

function handleInventoryRowClick(event, id) {
  if (event.target.tagName !== 'BUTTON' && !event.target.closest('button')) {
    openProductDetailModal(id);
  }
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

// ========================================================
// SETTINGS, SYSTEM UPDATES & EXCEL BULK IMPORTER
// ========================================================
let parsedExcelProductsList = [];

async function loadSettingsInfo() {
  const res = await callBackend('get_app_info');
  if (res && res.success) {
    const verEl = document.getElementById('settingsAppVersion');
    if (verEl) verEl.innerText = res.version ? `v${res.version}` : 'v2.5.0 Pro';

    const stEl = document.getElementById('settingsStoreId');
    if (stEl) stEl.innerText = res.storeId || 'MARKET-DEFAULT-01';
  }
}

async function checkForAppUpdates() {
  alert('🔄 جاري فحص خادم التحديثات السحابي الآن...\nإذا توفر إصدار أحدث، سيبدأ التحديث التلقائي فوراً.');
  await callBackend('check_for_updates');
}

// ========================================================
// 1. SMART ROBUST EXCEL & CSV BULK IMPORTER (FOOLPROOF)
// ========================================================
async function handleExcelFileUpload(event) {
  const file = event.target.files[0];
  if (!file) return;

  const fileName = file.name.toLowerCase();
  const reader = new FileReader();

  if (fileName.endsWith('.xlsx') || fileName.endsWith('.xls')) {
    reader.onload = (e) => {
      try {
        const data = new Uint8Array(e.target.result);
        if (typeof XLSX !== 'undefined') {
          const workbook = XLSX.read(data, { type: 'array' });
          const firstSheetName = workbook.SheetNames[0];
          const worksheet = workbook.Sheets[firstSheetName];
          // Get raw 2D array of all rows & columns
          const raw2D = XLSX.utils.sheet_to_json(worksheet, { header: 1, defval: '' });
          processExcel2DArray(raw2D);
        } else {
          alert('تعذر تحميل محرك الإكسل، يرجى حفظ الملف كـ CSV وإعادة اختياره.');
        }
      } catch (err) {
        alert('حدث خطأ أثناء قراءة ملف الإكسل: ' + err.message);
      }
    };
    reader.readAsArrayBuffer(file);
  } else {
    // CSV / TXT reader
    reader.onload = (e) => {
      try {
        const text = e.target.result;
        const raw2D = parseCSVTo2DArray(text);
        processExcel2DArray(raw2D);
      } catch (err) {
        alert('حدث خطأ أثناء قراءة ملف CSV: ' + err.message);
      }
    };
    reader.readAsText(file, 'UTF-8');
  }

  // Reset file input
  event.target.value = '';
}

function parseCSVTo2DArray(text) {
  const lines = text.split(/\r\n|\n/).filter(l => l.trim().length > 0);
  return lines.map(line => {
    return line.split(',').map(cell => cell.replace(/^["']|["']$/g, '').trim());
  });
}

function processExcel2DArray(raw2D) {
  if (!raw2D || raw2D.length === 0) {
    alert('الملف فارغ ولا يحتوي على أي بيانات!');
    return;
  }

  // 1. Find Header Row index or determine column mapping
  let headerRowIndex = -1;
  let nameCol = -1, barcodeCol = -1, catCol = -1, costCol = -1, priceCol = -1, wholesaleCol = -1;
  let cartonCostCol = -1, cartonPriceCol = -1, piecesPerCartonCol = -1, cartonsCol = -1, stockCol = -1, supCol = -1;

  for (let r = 0; r < Math.min(6, raw2D.length); r++) {
    const row = raw2D[r].map(c => String(c).toLowerCase().trim());
    for (let c = 0; c < row.length; c++) {
      const val = row[c];
      if (val.includes('اسم') || val.includes('مادة') || val.includes('صنف') || val.includes('منتج') || val.includes('name') || val.includes('item') || val.includes('title')) {
        headerRowIndex = r;
        break;
      }
    }
    if (headerRowIndex !== -1) break;
  }

  if (headerRowIndex !== -1) {
    const headerRow = raw2D[headerRowIndex].map(c => String(c).toLowerCase().trim());
    headerRow.forEach((h, idx) => {
      if (h.includes('اسم') || h.includes('مادة') || h.includes('صنف') || h.includes('منتج') || h.includes('name') || h.includes('item')) nameCol = idx;
      else if (h.includes('باركود') || h.includes('barcode') || h.includes('كود') || h.includes('code')) barcodeCol = idx;
      else if (h.includes('تصنيف') || h.includes('قسم') || h.includes('category')) catCol = idx;
      else if (h.includes('شراء كرتون') || h.includes('cartoncost') || h.includes('carton_cost')) cartonCostCol = idx;
      else if (h.includes('بيع كرتون') || h.includes('cartonprice') || h.includes('carton_price')) cartonPriceCol = idx;
      else if (h.includes('تكلفة') || h.includes('شراء') || h.includes('cost') || h.includes('buy')) costCol = idx;
      else if (h.includes('مفرد') || h.includes('بيع') || h.includes('price') || h.includes('sell')) priceCol = idx;
      else if (h.includes('جملة') || h.includes('wholesale')) wholesaleCol = idx;
      else if (h.includes('قطع بالكرتون') || h.includes('تعبئة') || h.includes('pieces')) piecesPerCartonCol = idx;
      else if (h.includes('كراتين') || h.includes('عدد كراتين') || h.includes('cartons')) cartonsCol = idx;
      else if (h.includes('رصيد') || h.includes('كمية') || h.includes('stock') || h.includes('qty')) stockCol = idx;
      else if (h.includes('مندوب') || h.includes('مورد') || h.includes('شركة') || h.includes('supplier')) supCol = idx;
    });
  }

  // Default fallback column mapping if no explicit header row was found
  if (nameCol === -1) nameCol = 0;
  if (barcodeCol === -1 && raw2D[0].length > 1) barcodeCol = 1;
  if (catCol === -1 && raw2D[0].length > 2) catCol = 2;
  if (costCol === -1 && raw2D[0].length > 3) costCol = 3;
  if (priceCol === -1 && raw2D[0].length > 4) priceCol = 4;

  const startRow = headerRowIndex !== -1 ? headerRowIndex + 1 : 0;
  parsedExcelProductsList = [];
  let barcodeCounter = Date.now().toString().slice(-6);

  for (let r = startRow; r < raw2D.length; r++) {
    const row = raw2D[r];
    if (!row || row.length === 0) continue;

    // Extract product name from designated column or first non-empty text cell
    let name = nameCol !== -1 && row[nameCol] ? String(row[nameCol]).trim() : '';
    if (!name) {
      for (let c = 0; c < row.length; c++) {
        const text = String(row[c] || '').trim();
        if (text && isNaN(text) && text.length > 1) {
          name = text;
          break;
        }
      }
    }
    if (!name) continue; // Skip completely blank row

    // Extract or generate barcode
    let barcode = barcodeCol !== -1 && row[barcodeCol] ? String(row[barcodeCol]).replace(/[^0-9a-zA-Z]/g, '').trim() : '';
    if (!barcode) {
      barcode = '200245' + String(barcodeCounter++).padStart(6, '0');
    }

    const category = catCol !== -1 && row[catCol] ? String(row[catCol]).trim() : 'عام';
    const supplierName = supCol !== -1 && row[supCol] ? String(row[supCol]).trim() : '';

    const cost = costCol !== -1 ? parseFloat(String(row[costCol]).replace(/[^0-9.]/g, '')) || 0 : 0;
    let price = priceCol !== -1 ? parseFloat(String(row[priceCol]).replace(/[^0-9.]/g, '')) || 0 : 0;
    if (price === 0 && cost > 0) price = cost * 1.2;

    const wholesalePrice = wholesaleCol !== -1 ? parseFloat(String(row[wholesaleCol]).replace(/[^0-9.]/g, '')) || price : price;
    const cartonPurchasePrice = cartonCostCol !== -1 ? parseFloat(String(row[cartonCostCol]).replace(/[^0-9.]/g, '')) || 0 : 0;
    const cartonSellingPrice = cartonPriceCol !== -1 ? parseFloat(String(row[cartonPriceCol]).replace(/[^0-9.]/g, '')) || 0 : 0;

    const piecesPerCarton = piecesPerCartonCol !== -1 ? parseInt(String(row[piecesPerCartonCol]).replace(/[^0-9]/g, '')) || 1 : 1;
    const cartonsCount = cartonsCol !== -1 ? parseInt(String(row[cartonsCol]).replace(/[^0-9]/g, '')) || 0 : 0;
    let stockQuantity = stockCol !== -1 ? parseInt(String(row[stockCol]).replace(/[^0-9]/g, '')) || (cartonsCount * piecesPerCarton) : (cartonsCount * piecesPerCarton);
    if (stockQuantity <= 0 && cartonsCount > 0) stockQuantity = cartonsCount * piecesPerCarton;

    parsedExcelProductsList.push({
      name,
      barcode,
      category: category || 'عام',
      supplierName,
      cost,
      price,
      wholesalePrice,
      cartonPurchasePrice,
      cartonSellingPrice,
      piecesPerCarton: Math.max(1, piecesPerCarton),
      cartonsCount: Math.max(0, cartonsCount),
      stockQuantity: stockQuantity,
      minStockAlert: 5
    });
  }

  if (parsedExcelProductsList.length === 0) {
    alert('لم يتم العثور على أي صفوف صالحة للمواد داخل الملف!\nيرجى التأكد من أن الملف يحتوي على أسماء المواد.');
    return;
  }

  // Update Settings preview summary box
  const statusBox = document.getElementById('excelImportStatusBox');
  const countEl = document.getElementById('excelParsedCount');
  if (statusBox) statusBox.classList.remove('hidden');
  if (countEl) countEl.innerText = `${parsedExcelProductsList.length} مادة جاهزة للاستيراد`;

  // Open Preview Modal
  openExcelPreviewModal();
}

function openExcelPreviewModal() {
  const modal = document.getElementById('excelPreviewModal');
  const summary = document.getElementById('epm-summary');
  if (summary) summary.innerText = `تم اكتشاف (${parsedExcelProductsList.length}) مادة جاهزة للحفظ في المخزن`;
  renderExcelPreviewTable(parsedExcelProductsList);
  if (modal) modal.classList.remove('hidden');
}

function closeExcelPreviewModal() {
  document.getElementById('excelPreviewModal')?.classList.add('hidden');
}

function renderExcelPreviewTable(list) {
  const tbody = document.getElementById('epm-tableBody');
  if (!tbody) return;

  tbody.innerHTML = '';
  list.slice(0, 100).forEach((p, idx) => {
    const tr = document.createElement('tr');
    tr.className = 'hover:bg-slate-50 dark:hover:bg-slate-800/50';
    tr.innerHTML = `
      <td class="p-2.5 text-center text-slate-400 font-bold">${idx + 1}</td>
      <td class="p-2.5 font-bold text-slate-800 dark:text-white font-sans">${p.name}</td>
      <td class="p-2.5 text-sky-500 font-bold">${p.barcode}</td>
      <td class="p-2.5 font-sans">${p.category}</td>
      <td class="p-2.5 text-center text-slate-500">${Number(p.cost).toLocaleString()}</td>
      <td class="p-2.5 text-center text-emerald-500 font-bold">${Number(p.price).toLocaleString()}</td>
      <td class="p-2.5 text-center text-blue-500">${Number(p.wholesalePrice).toLocaleString()}</td>
      <td class="p-2.5 text-center font-bold">${p.stockQuantity}</td>
    `;
    tbody.appendChild(tr);
  });
}

function filterExcelPreviewTable() {
  const q = (document.getElementById('epm-search')?.value || '').toLowerCase().trim();
  const filtered = parsedExcelProductsList.filter(p => 
    p.name.toLowerCase().includes(q) || p.barcode.includes(q) || p.category.toLowerCase().includes(q)
  );
  renderExcelPreviewTable(filtered);
}

async function confirmExcelImportFromModal() {
  closeExcelPreviewModal();
  await confirmExcelImport();
}

async function confirmExcelImport() {
  if (parsedExcelProductsList.length === 0) {
    alert('لا توجد مواد محملة للاستيراد.');
    return;
  }

  const btn = document.getElementById('btnConfirmExcelImport');
  if (btn) {
    btn.disabled = true;
    btn.innerText = '⏳ جاري الحفظ والتوريد للمخزن...';
  }

  const res = await callBackend('import_excel_products', { products: parsedExcelProductsList });

  if (btn) {
    btn.disabled = false;
    btn.innerText = '✔ حفظ وتوريد المواد إلى المخزن الآن';
  }

  if (res && res.success) {
    alert(`🎉 تم استيراد وتحديث (${res.importedCount || parsedExcelProductsList.length}) مادة بنجاح في المخزن وقاعدة البيانات!`);
    parsedExcelProductsList = [];
    document.getElementById('excelImportStatusBox')?.classList.add('hidden');
    
    await loadInventory();
    await loadProducts();
    await loadDashboard();
  } else {
    alert('حدث خطأ أثناء حفظ المواد: ' + (res?.message || 'خطأ غير معروف'));
  }
}

function downloadExcelSampleTemplate() {
  const headers = [
    'اسم المادة',
    'الباركود',
    'التصنيف',
    'سعر الشراء (التكلفة)',
    'سعر المفرد',
    'سعر الجملة',
    'سعر شراء الكرتون',
    'سعر بيع الكرتون',
    'عدد القطع بالكرتون',
    'عدد الكراتين',
    'الرصيد الكلي بالقطع',
    'اسم المندوب'
  ];

  const sampleRows = [
    ['عصير راني برتقال 240مل', '200245000101', 'مشروبات وعصائر', '400', '500', '450', '9600', '11000', '24', '10', '240', 'شركة الروان'],
    ['بسكويت دايجستف 400غ', '200245000102', 'بسكويت وحلويات', '1250', '1500', '1400', '15000', '17000', '12', '5', '60', 'المندوب أحمد'],
    ['شاي ليبتون 100 خيط', '200245000103', 'شاي وقهوة', '3000', '3500', '3300', '36000', '40000', '12', '8', '96', 'شركة الخيرات']
  ];

  let csvContent = '\uFEFF'; // UTF-8 BOM for Arabic support in Excel
  csvContent += headers.join(',') + '\r\n';
  sampleRows.forEach(r => {
    csvContent += r.map(cell => `"${cell}"`).join(',') + '\r\n';
  });

  const blob = new Blob([csvContent], { type: 'text/csv;charset=utf-8;' });
  const url = URL.createObjectURL(blob);
  const link = document.createElement('a');
  link.setAttribute('href', url);
  link.setAttribute('download', 'نموذج_استيراد_مواد_7amoPOS.csv');
  document.body.appendChild(link);
  link.click();
  document.body.removeChild(link);
}

async function backupDatabase() {
  const res = await callBackend('backup_database');
  if (res && res.success) {
    alert(`✔ تم حفظ نسخة احتياطية من قاعدة البيانات بنجاح!\nالمسار: ${res.backupPath}`);
  } else {
    alert('تعذر أخذ نسخة احتياطية: ' + (res?.message || 'خطأ غير معروف'));
  }
}

// ========================================================
// 2. CUSTOMERS & CREDIT DEBTS (الزبائن وحسابات الآجل)
// ========================================================
let customersList = [];
let selectedCustomerForPayment = null;

async function loadCustomers() {
  const res = await callBackend('get_customers');
  if (res && res.success) {
    customersList = res.customers || [];
    renderCustomersTable(customersList);

    const totalDebts = customersList.reduce((acc, c) => acc + (c.totalDebt || 0), 0);
    const totalDebtsEl = document.getElementById('cust-totalDebts');
    if (totalDebtsEl) totalDebtsEl.innerText = `${Number(totalDebts).toLocaleString()} د.ع`;

    const totalCountEl = document.getElementById('cust-totalCount');
    if (totalCountEl) totalCountEl.innerText = `${customersList.length} زبون`;
  }
}

function renderCustomersTable(list) {
  const tbody = document.getElementById('customersTableBody');
  if (!tbody) return;

  if (list.length === 0) {
    tbody.innerHTML = '<tr><td colspan="6" class="text-center py-8 text-slate-400 font-bold">لا يوجد زبائن أو ديون مسجلة حالياً</td></tr>';
    return;
  }

  tbody.innerHTML = '';
  list.forEach((c, idx) => {
    const tr = document.createElement('tr');
    tr.className = 'hover:bg-slate-50 dark:hover:bg-slate-800/40 transition';
    tr.innerHTML = `
      <td class="p-3 text-center text-slate-400 font-bold">${idx + 1}</td>
      <td class="p-3 font-bold text-slate-800 dark:text-white">${c.customerName}</td>
      <td class="p-3 font-mono text-slate-500">${c.phone || 'بدون هاتف'}</td>
      <td class="p-3 text-center font-black font-mono text-rose-500">${Number(c.totalDebt).toLocaleString()} د.ع</td>
      <td class="p-3 text-center text-slate-400 font-mono text-[11px]">${c.lastDebtDate || '--'}</td>
      <td class="p-3 text-center">
        <button onclick="openPayCustomerDebtModal('${c.customerName}', ${c.totalDebt})" class="px-3 py-1 bg-emerald-600 hover:bg-emerald-500 text-white font-bold text-[11px] rounded-lg shadow-sm">
          💵 سداد دين
        </button>
      </td>
    `;
    tbody.appendChild(tr);
  });
}

function filterCustomersTable() {
  const q = (document.getElementById('cust-searchInput')?.value || '').toLowerCase().trim();
  const filtered = customersList.filter(c => 
    c.customerName.toLowerCase().includes(q) || (c.phone && c.phone.includes(q))
  );
  renderCustomersTable(filtered);
}

function openAddCustomerDebtModal() {
  document.getElementById('acd-name').value = '';
  document.getElementById('acd-phone').value = '';
  document.getElementById('acd-amount').value = '';
  document.getElementById('acd-notes').value = '';
  document.getElementById('addCustomerDebtModal')?.classList.remove('hidden');
}

function closeAddCustomerDebtModal() {
  document.getElementById('addCustomerDebtModal')?.classList.add('hidden');
}

async function saveNewCustomerDebt() {
  const name = document.getElementById('acd-name')?.value.trim();
  const phone = document.getElementById('acd-phone')?.value.trim();
  const amount = parseFloat(document.getElementById('acd-amount')?.value) || 0;
  const notes = document.getElementById('acd-notes')?.value.trim();

  if (!name || amount <= 0) {
    alert('يرجى إدخال اسم الزبون ومبلغ الدين بشكل صحيح!');
    return;
  }

  const res = await callBackend('add_customer_debt', { name, phone, amount, notes });
  if (res && res.success) {
    alert('✔ تم تسجيل دين الزبون بنجاح!');
    closeAddCustomerDebtModal();
    await loadCustomers();
  }
}

function openPayCustomerDebtModal(customerName, currentDebt) {
  selectedCustomerForPayment = customerName;
  const nameEl = document.getElementById('pcd-customerName');
  const debtEl = document.getElementById('pcd-currentDebt');
  const payInput = document.getElementById('pcd-payAmount');

  if (nameEl) nameEl.innerText = customerName;
  if (debtEl) debtEl.innerText = `${Number(currentDebt).toLocaleString()} د.ع`;
  if (payInput) {
    payInput.value = currentDebt;
    payInput.max = currentDebt;
  }

  document.getElementById('payCustomerDebtModal')?.classList.remove('hidden');
}

function closePayCustomerDebtModal() {
  document.getElementById('payCustomerDebtModal')?.classList.add('hidden');
}

async function confirmCustomerPayment() {
  if (!selectedCustomerForPayment) return;
  const amount = parseFloat(document.getElementById('pcd-payAmount')?.value) || 0;
  if (amount <= 0) {
    alert('يرجى إدخال مبلغ سداد صحيح!');
    return;
  }

  const res = await callBackend('pay_customer_debt', { customerName: selectedCustomerForPayment, amount });
  if (res && res.success) {
    alert('✔ تم تسجيل سداد الزبون بنجاح!');
    closePayCustomerDebtModal();
    await loadCustomers();
  }
}

// ========================================================
// 3. STOCK AUDIT (الجرد ومطابقة الرفوف)
// ========================================================
let auditProductsList = [];
let auditFilteredList = [];

async function loadStockAudit(showAlert = false) {
  const res = await callBackend('get_stock_audit');
  if (res && res.success) {
    auditProductsList = res.products || [];
    filterAuditTable();
    if (showAlert) alert('✔ تم تحديث قائمة جرد الرفوف بنجاح!');
  }
}

function filterAuditTable() {
  const q = (document.getElementById('audit-searchInput')?.value || '').toLowerCase().trim();
  auditFilteredList = auditProductsList.filter(p => 
    (p.name && p.name.toLowerCase().includes(q)) || (p.barcode && p.barcode.includes(q))
  );
  renderAuditTable();
}

function renderAuditTable() {
  const tbody = document.getElementById('auditTableBody');
  const summaryEl = document.getElementById('auditCountSummary');
  const showAllBtn = document.getElementById('auditShowAllBtn');
  if (!tbody) return;

  if (auditFilteredList.length === 0) {
    tbody.innerHTML = '<tr><td colspan="7" class="text-center py-8 text-slate-400 font-bold">لا توجد مواد مطابقة للبحث للجرد</td></tr>';
    if (summaryEl) summaryEl.innerText = 'يتم عرض 0 مادة';
    if (showAllBtn) showAllBtn.classList.add('hidden');
    return;
  }

  const limitVal = document.getElementById('auditDisplayLimit')?.value || '1000';
  let limit = auditFilteredList.length;
  if (limitVal !== 'all') {
    limit = parseInt(limitVal, 10) || 1000;
  }

  const displayItems = auditFilteredList.slice(0, limit);

  let rowsHtml = '';
  for (let i = 0; i < displayItems.length; i++) {
    const p = displayItems[i];
    const prodId = p.id || p.Id;
    rowsHtml += `
      <tr class="hover:bg-slate-50 dark:hover:bg-slate-800/40 transition">
        <td class="p-3 text-center text-slate-400 font-bold">${i + 1}</td>
        <td class="p-3 font-bold text-slate-800 dark:text-white">${p.name || p.Name}</td>
        <td class="p-3 text-sky-500 font-mono font-bold">${p.barcode || p.Barcode || '--'}</td>
        <td class="p-3 text-center font-black font-mono">${p.stockQuantity} قطعة</td>
        <td class="p-3 text-center">
          <input id="audit-input-${prodId}" type="number" value="${p.stockQuantity}" oninput="calcAuditDiff('${prodId}', ${p.stockQuantity})" class="w-24 bg-slate-50 dark:bg-slate-900 border border-slate-200 dark:border-slate-800 rounded-xl px-2 py-1 text-center font-black font-mono text-xs">
        </td>
        <td class="p-3 text-center font-bold font-mono text-xs" id="audit-diff-${prodId}">0 (مطابق)</td>
        <td class="p-3 text-center">
          <button onclick="quickSaveAuditStock('${prodId}')" class="px-3 py-1 bg-emerald-600 hover:bg-emerald-500 text-white font-bold text-[11px] rounded-lg shadow-sm">
            💾 حفظ
          </button>
        </td>
      </tr>
    `;
  }

  tbody.innerHTML = rowsHtml;

  if (summaryEl) {
    summaryEl.innerText = `يتم عرض ${displayItems.length.toLocaleString()} مادة من إجمالي ${auditFilteredList.length.toLocaleString()} مادة`;
  }

  if (showAllBtn) {
    if (auditFilteredList.length > displayItems.length) {
      showAllBtn.innerText = `👁 عرض كافة المواد (${auditFilteredList.length.toLocaleString()} مادة) دفعة واحدة`;
      showAllBtn.classList.remove('hidden');
    } else {
      showAllBtn.classList.add('hidden');
    }
  }
}

function showAllAuditItems() {
  const limitSelect = document.getElementById('auditDisplayLimit');
  if (limitSelect) limitSelect.value = 'all';
  renderAuditTable();
}

function calcAuditDiff(id, sysStock) {
  const input = document.getElementById(`audit-input-${id}`);
  const diffEl = document.getElementById(`audit-diff-${id}`);
  if (!input || !diffEl) return;

  const actual = parseFloat(input.value) || 0;
  const diff = actual - sysStock;

  if (diff === 0) {
    diffEl.innerText = '0 (مطابق)';
    diffEl.className = 'p-3 text-center font-bold font-mono text-xs text-slate-400';
  } else if (diff > 0) {
    diffEl.innerText = `+${diff} (زيادة)`;
    diffEl.className = 'p-3 text-center font-bold font-mono text-xs text-emerald-500';
  } else {
    diffEl.innerText = `${diff} (نقص)`;
    diffEl.className = 'p-3 text-center font-bold font-mono text-xs text-rose-500';
  }
}

async function quickSaveAuditStock(id) {
  const input = document.getElementById(`audit-input-${id}`);
  if (!input) return;
  const actualStock = parseFloat(input.value) || 0;

  const res = await callBackend('update_stock_audit', { productId: id, actualStock });
  if (res && res.success) {
    alert('✔ تم تحديث رصيد المادة الفعلي بالمخزن بنجاح!');
    await loadInventory();
    await loadProducts();
  }
}

function filterAuditTable() {
  const q = (document.getElementById('audit-searchInput')?.value || '').toLowerCase().trim();
  const filtered = auditProductsList.filter(p => 
    (p.name && p.name.toLowerCase().includes(q)) || (p.barcode && p.barcode.includes(q))
  );
  renderAuditTable(filtered);
}

// ========================================================
// 4. DAMAGED & EXPIRED ITEMS (المواد التالفة والمنتهية)
// ========================================================
let damagedItemsList = [];

async function loadDamagedItems() {
  const res = await callBackend('get_damaged_items');
  if (res && res.success) {
    damagedItemsList = res.items || [];
    renderDamagedTable(damagedItemsList);

    const lossEl = document.getElementById('damaged-totalLoss');
    if (lossEl) lossEl.innerText = `${Number(res.totalLoss || 0).toLocaleString()} د.ع`;

    const countEl = document.getElementById('damaged-totalCount');
    if (countEl) countEl.innerText = `${damagedItemsList.length} مادة`;
  }
}

function renderDamagedTable(list) {
  const tbody = document.getElementById('damagedTableBody');
  if (!tbody) return;

  if (list.length === 0) {
    tbody.innerHTML = '<tr><td colspan="8" class="text-center py-8 text-slate-400 font-bold">لا توجد مواد تالفة مسجلة</td></tr>';
    return;
  }

  tbody.innerHTML = '';
  list.forEach((d, idx) => {
    const tr = document.createElement('tr');
    tr.className = 'hover:bg-slate-50 dark:hover:bg-slate-800/40 transition';
    tr.innerHTML = `
      <td class="p-3 text-center text-slate-400 font-bold">${idx + 1}</td>
      <td class="p-3 font-bold text-slate-800 dark:text-white">${d.productName}</td>
      <td class="p-3 font-mono text-sky-500">${d.barcode || '--'}</td>
      <td class="p-3 text-center font-bold text-rose-500 font-mono">${d.quantity} قطعة</td>
      <td class="p-3 text-center font-bold text-rose-500 font-mono">${Number(d.lossAmount).toLocaleString()} د.ع</td>
      <td class="p-3 text-slate-600 dark:text-slate-300">${d.reason}</td>
      <td class="p-3 text-center"><span class="px-2 py-0.5 rounded-md bg-slate-100 dark:bg-slate-800 text-[10px] font-bold">${d.actionTaken}</span></td>
      <td class="p-3 text-center text-slate-400 font-mono text-[11px]">${d.date}</td>
    `;
    tbody.appendChild(tr);
  });
}

function openAddDamagedModal() {
  const select = document.getElementById('adm-productSelect');
  if (select) {
    select.innerHTML = '<option value="">-- اختر مادة من المخزن --</option>';
    state.products.forEach(p => {
      select.innerHTML += `<option value="${p.id}">${p.name} (رصيد: ${p.stockQuantity})</option>`;
    });
  }
  document.getElementById('adm-quantity').value = '1';
  document.getElementById('addDamagedModal')?.classList.remove('hidden');
}

function closeAddDamagedModal() {
  document.getElementById('addDamagedModal')?.classList.add('hidden');
}

async function saveNewDamagedItem() {
  const prodId = document.getElementById('adm-productSelect')?.value;
  const qty = parseFloat(document.getElementById('adm-quantity')?.value) || 0;
  const reason = document.getElementById('adm-reason')?.value;
  const actionTaken = document.getElementById('adm-action')?.value;

  if (!prodId || qty <= 0) {
    alert('يرجى اختيار مادة وإدخال كمية صحيحة!');
    return;
  }

  const res = await callBackend('add_damaged_item', { productId: prodId, quantity: qty, reason, actionTaken });
  if (res && res.success) {
    alert('✔ تم تسجيل المادة التالفة وخصمها من رصيد المخزن بنجاح!');
    closeAddDamagedModal();
    await loadDamagedItems();
    await loadInventory();
    await loadProducts();
  }
}

// ========================================================
// 5. REPORTS & FINANCIAL ANALYTICS (التقارير والأرباح)
// ========================================================
async function loadReports() {
  const res = await callBackend('get_reports');
  if (res && res.success) {
    const todaySalesEl = document.getElementById('rep-todaySales');
    if (todaySalesEl) todaySalesEl.innerText = `${Number(res.todayTotal || 0).toLocaleString()} د.ع`;

    const todayProfitEl = document.getElementById('rep-todayProfit');
    if (todayProfitEl) todayProfitEl.innerText = `+${Number(res.todayProfit || 0).toLocaleString()} د.ع`;

    const todayInvoicesEl = document.getElementById('rep-todayInvoices');
    if (todayInvoicesEl) todayInvoicesEl.innerText = `${res.todayInvoicesCount || 0} فواتير`;

    const monthSalesEl = document.getElementById('rep-monthSales');
    if (monthSalesEl) monthSalesEl.innerText = `${Number(res.monthTotal || 0).toLocaleString()} د.ع`;

    const monthProfitEl = document.getElementById('rep-monthProfit');
    if (monthProfitEl) monthProfitEl.innerText = `+${Number(res.monthProfit || 0).toLocaleString()} د.ع`;

    const monthInvoicesEl = document.getElementById('rep-monthInvoices');
    if (monthInvoicesEl) monthInvoicesEl.innerText = `${res.monthInvoicesCount || 0} فواتير`;

    // Render Top Items
    const topTbody = document.getElementById('rep-topItemsTable');
    if (topTbody) {
      if (!res.topItems || res.topItems.length === 0) {
        topTbody.innerHTML = '<tr><td colspan="4" class="text-center py-6 text-slate-400">لا توجد مبيعات مسجلة حتى الآن</td></tr>';
      } else {
        topTbody.innerHTML = '';
        res.topItems.forEach((it, idx) => {
          topTbody.innerHTML += `
            <tr class="hover:bg-slate-50 dark:hover:bg-slate-800/40">
              <td class="p-3 text-center text-slate-400 font-bold">${idx + 1}</td>
              <td class="p-3 font-bold text-slate-800 dark:text-white">${it.name}</td>
              <td class="p-3 text-center font-bold text-emerald-500 font-mono">${it.qty} قطعة</td>
              <td class="p-3 text-center font-black text-sky-500 font-mono">${Number(it.total).toLocaleString()} د.ع</td>
            </tr>
          `;
        });
      }
    }
  }
}

// ========================================================
// 6. PRINTING & RECEIPT SETTINGS (إعدادات الطابعة والوصل)
// ========================================================
function loadPrintingSettings() {
  const saved = localStorage.getItem('pos_printer_settings');
  if (saved) {
    try {
      const config = JSON.parse(saved);
      if (config.marketName) document.getElementById('pr-marketName').value = config.marketName;
      if (config.marketSub) document.getElementById('pr-marketSub').value = config.marketSub;
      if (config.marketPhone) document.getElementById('pr-marketPhone').value = config.marketPhone;
      if (config.marketFooter) document.getElementById('pr-marketFooter').value = config.marketFooter;
      if (config.paperType) document.getElementById('pr-paperType').value = config.paperType;
    } catch { }
  }
  updateReceiptLivePreview();
}

function updateReceiptLivePreview() {
  const name = document.getElementById('pr-marketName')?.value || '7amo Market';
  const sub = document.getElementById('pr-marketSub')?.value || 'نظام إدارة السوبرماركت';
  const phone = document.getElementById('pr-marketPhone')?.value || '0750 000 0000';
  const footer = document.getElementById('pr-marketFooter')?.value || 'شكراً لزيارتكم!';

  const pName = document.getElementById('prev-marketName');
  const pSub = document.getElementById('prev-marketSub');
  const pPhone = document.getElementById('prev-marketPhone');
  const pFooter = document.getElementById('prev-marketFooter');

  if (pName) pName.innerText = name;
  if (pSub) pSub.innerText = sub;
  if (pPhone) pPhone.innerText = `هاتف: ${phone}`;
  if (pFooter) pFooter.innerText = footer;
}

function savePrintingSettings() {
  const config = {
    marketName: document.getElementById('pr-marketName')?.value,
    marketSub: document.getElementById('pr-marketSub')?.value,
    marketPhone: document.getElementById('pr-marketPhone')?.value,
    marketFooter: document.getElementById('pr-marketFooter')?.value,
    paperType: document.getElementById('pr-paperType')?.value
  };
  localStorage.setItem('pos_printer_settings', JSON.stringify(config));
  alert('✔ تم حفظ إعدادات وترويسة وصل البيع بنجاح!');
}


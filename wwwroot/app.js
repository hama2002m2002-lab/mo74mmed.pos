// ========================================================
// POS NEXT-GEN STATE & CORE LOGIC
// ========================================================

const state = {
  activeTab: 'dashboard',
  language: 'ar',
  cart: [],
  products: [],
  filteredProducts: [],
  categories: [],
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
      // Mock for browser testing
      console.log(`[C# Bridge Call] Action: ${action}`, payload);
      resolve({ success: true, message: "Browser Mock Mode" });
    }
  });
}

// ========================================================
// INITIALIZATION
// ========================================================
document.addEventListener('DOMContentLoaded', async () => {
  lucide.createIcons();
  startClock();
  setupEventListeners();
  await loadDashboard();
  await loadPosProducts();
  await loadRepOrders();
  setInterval(loadRepOrders, 4000); // Auto poll rep orders
});

function startClock() {
  const update = () => {
    const now = new Date();
    const clockEl = document.getElementById('liveClock');
    if (clockEl) clockEl.innerText = now.toLocaleTimeString('ar-IQ');
  };
  update();
  setInterval(update, 1000);
}

function setupEventListeners() {
  // Listen for barcode enter in POS
  const posInput = document.getElementById('posSearchInput');
  if (posInput) {
    posInput.addEventListener('keydown', (e) => {
      if (e.key === 'Enter') {
        const query = posInput.value.trim();
        if (!query) return;
        const match = state.products.find(p => p.barcode === query || p.name.toLowerCase() === query.toLowerCase());
        if (match) {
          addToCart(match);
          posInput.value = '';
          filterPosProducts();
        }
      }
    });
  }
}

// ========================================================
// TAB SWITCHING
// ========================================================
function switchTab(tabId) {
  state.activeTab = tabId;
  
  // Hide all tabs
  document.querySelectorAll('.tab-content').forEach(el => el.classList.add('hidden'));
  document.querySelectorAll('.nav-item').forEach(el => {
    el.classList.remove('bg-slate-800/90', 'text-blue-400', 'border', 'border-blue-500/30');
    el.classList.add('text-slate-300');
  });

  // Show selected tab
  const tabEl = document.getElementById(`tab-${tabId}`);
  if (tabEl) tabEl.classList.remove('hidden');

  const navEl = document.getElementById(`nav-${tabId}`);
  if (navEl) {
    navEl.classList.add('bg-slate-800/90', 'text-blue-400', 'border', 'border-blue-500/30');
    navEl.classList.remove('text-slate-300');
  }

  // Refresh tab data
  if (tabId === 'dashboard') loadDashboard();
  if (tabId === 'pos') loadPosProducts();
  if (tabId === 'repOrders') loadRepOrders();
  if (tabId === 'inventory') loadInventory();
  if (tabId === 'suppliers') loadSuppliers();
  if (tabId === 'users') loadUsers();

  lucide.createIcons();
}

// ========================================================
// DASHBOARD LOGIC
// ========================================================
async function loadDashboard() {
  const res = await callBackend('get_dashboard_data');
  if (!res || !res.success) return;

  // KPIs
  document.getElementById('kpiTodayRevenue').innerText = Number(res.todayRevenue || 0).toLocaleString();
  document.getElementById('kpiTodayInvoices').innerText = Number(res.todayInvoices || 0).toLocaleString();
  document.getElementById('kpiMonthlyRevenue').innerText = Number(res.monthlyRevenue || 0).toLocaleString();
  document.getElementById('kpiLowStock').innerText = Number(res.lowStockCount || 0).toLocaleString();

  // Weekly Chart
  renderWeeklyChart(res.weeklyTrend || []);

  // Payments Chart
  renderPaymentChart(res.payments || { cash: 0, card: 0, debt: 0 });
}

function renderWeeklyChart(data) {
  const ctx = document.getElementById('weeklyChart');
  if (!ctx) return;

  if (state.weeklyChart) state.weeklyChart.destroy();

  const labels = data.map(d => d.dayName + ' (' + d.shortDate + ')');
  const values = data.map(d => d.revenue);

  state.weeklyChart = new Chart(ctx, {
    type: 'bar',
    data: {
      labels: labels,
      datasets: [{
        label: 'المبيعات (د.ع)',
        data: values,
        backgroundColor: 'rgba(59, 130, 246, 0.7)',
        borderColor: '#3B82F6',
        borderWidth: 1.5,
        borderRadius: 8
      }]
    },
    options: {
      responsive: true,
      maintainAspectRatio: false,
      plugins: { legend: { display: false } },
      scales: {
        y: { grid: { color: 'rgba(255,255,255,0.05)' }, ticks: { color: '#94A3B8' } },
        x: { grid: { display: false }, ticks: { color: '#94A3B8' } }
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
      cutout: '72%',
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
// POS CASHIER LOGIC
// ========================================================
async function loadPosProducts() {
  const res = await callBackend('get_pos_products');
  if (!res || !res.success) return;

  state.products = res.products || [];
  state.categories = res.categories || [];

  // Populate categories dropdown
  const catFilter = document.getElementById('posCatFilter');
  if (catFilter) {
    catFilter.innerHTML = '<option value="ALL">جميع الأصناف</option>';
    state.categories.forEach(c => {
      catFilter.innerHTML += `<option value="${c}">${c}</option>`;
    });
  }

  filterPosProducts();
}

function filterPosProducts() {
  const search = (document.getElementById('posSearchInput')?.value || '').trim().toLowerCase();
  const cat = document.getElementById('posCatFilter')?.value || 'ALL';

  let list = state.products;
  if (cat !== 'ALL') {
    list = list.filter(p => p.category === cat);
  }
  if (search) {
    list = list.filter(p => p.name.toLowerCase().includes(search) || (p.barcode && p.barcode.includes(search)));
  }

  state.filteredProducts = list;
  renderPosGrid();
}

function renderPosGrid() {
  const grid = document.getElementById('posProductsGrid');
  if (!grid) return;

  grid.innerHTML = '';
  state.filteredProducts.forEach(p => {
    const card = document.createElement('div');
    card.className = 'glass-card p-3 cursor-pointer hover:border-emerald-500 flex flex-col justify-between';
    card.onclick = () => addToCart(p);
    card.innerHTML = `
      <div>
        <div class="flex items-center justify-between mb-1">
          <span class="text-[11px] bg-slate-800 text-slate-400 px-2 py-0.5 rounded-full font-bold">${p.category || 'عام'}</span>
          <span class="text-[11px] ${p.stockQuantity <= 5 ? 'text-rose-400 font-black animate-pulse' : 'text-slate-400'}">المخزون: ${p.stockQuantity}</span>
        </div>
        <h4 class="text-xs font-bold text-white line-clamp-2">${p.name}</h4>
      </div>
      <div class="mt-3 flex items-center justify-between">
        <span class="text-sm font-black text-emerald-400">${Number(p.price).toLocaleString()} د.ع</span>
        <button class="w-6 h-6 rounded-lg bg-emerald-500/20 text-emerald-400 font-bold text-xs flex items-center justify-center">+</button>
      </div>
    `;
    grid.appendChild(card);
  });
}

function addToCart(product) {
  const existing = state.cart.find(item => item.id === product.id);
  if (existing) {
    existing.qty += 1;
  } else {
    state.cart.push({
      id: product.id,
      name: product.name,
      price: product.price,
      cost: product.cost,
      qty: 1
    });
  }
  renderCart();
}

function updateCartQty(id, delta) {
  const item = state.cart.find(i => i.id === id);
  if (item) {
    item.qty += delta;
    if (item.qty <= 0) {
      state.cart = state.cart.filter(i => i.id !== id);
    }
  }
  renderCart();
}

function clearCart() {
  state.cart = [];
  renderCart();
}

function renderCart() {
  const container = document.getElementById('posCartList');
  if (!container) return;

  if (state.cart.length === 0) {
    container.innerHTML = '<div class="text-center text-xs text-slate-500 py-10">السلة فارغة، اختر المواد لإضافتها</div>';
    calculateCartTotal();
    return;
  }

  container.innerHTML = '';
  state.cart.forEach(item => {
    const el = document.createElement('div');
    el.className = 'bg-slate-900/90 border border-slate-800 rounded-xl p-2.5 flex items-center justify-between text-xs';
    el.innerHTML = `
      <div class="flex-1 pr-1">
        <div class="font-bold text-slate-200">${item.name}</div>
        <div class="text-[11px] text-emerald-400">${Number(item.price).toLocaleString()} د.ع × ${item.qty}</div>
      </div>
      <div class="flex items-center gap-1.5 bg-slate-800 px-1.5 py-1 rounded-lg border border-slate-700">
        <button onclick="updateCartQty('${item.id}', -1)" class="w-5 h-5 bg-slate-700 hover:bg-slate-600 rounded text-rose-400 font-black">-</button>
        <span class="font-bold text-white px-1.5">${item.qty}</span>
        <button onclick="updateCartQty('${item.id}', 1)" class="w-5 h-5 bg-slate-700 hover:bg-slate-600 rounded text-emerald-400 font-black">+</button>
      </div>
      <div class="font-black text-slate-100 mr-3">${Number(item.price * item.qty).toLocaleString()} د.ع</div>
    `;
    container.appendChild(el);
  });

  calculateCartTotal();
}

function calculateCartTotal() {
  const subTotal = state.cart.reduce((sum, i) => sum + (i.price * i.qty), 0);
  const discount = Number(document.getElementById('posDiscountInput')?.value || 0);
  const finalTotal = Math.max(0, subTotal - discount);

  document.getElementById('posSubTotal').innerText = Number(subTotal).toLocaleString() + ' د.ع';
  document.getElementById('posTotalAmount').innerText = Number(finalTotal).toLocaleString() + ' د.ع';
}

async function completeSale() {
  if (state.cart.length === 0) {
    alert('السلة فارغة!');
    return;
  }

  const discount = Number(document.getElementById('posDiscountInput')?.value || 0);
  const payload = {
    paymentMethod: 'Cash',
    discount: discount,
    items: state.cart
  };

  const res = await callBackend('complete_sale', payload);
  if (res && res.success) {
    alert(`✔ تم إتمام البيع بنجاح! رقم الفاتورة: ${res.invoiceNumber}\nالمبلغ: ${Number(res.total).toLocaleString()} د.ع`);
    clearCart();
    loadDashboard();
  }
}

// ========================================================
// REP ORDERS LOGIC
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
      <td class="p-3.5 font-mono font-bold text-blue-400">${o.orderNumber}</td>
      <td class="p-3.5 font-bold text-white">${o.marketName || '--'}</td>
      <td class="p-3.5 text-cyan-400 font-semibold">${o.representativeName || '--'}</td>
      <td class="p-3.5 text-slate-400">${o.marketCity || ''} - ${o.marketPhone || ''}</td>
      <td class="p-3.5 font-bold text-amber-400">${o.itemsCount} مواد</td>
      <td class="p-3.5 font-black text-emerald-400">${Number(o.totalAmount).toLocaleString()} د.ع</td>
      <td class="p-3.5">
        <span class="px-2 py-0.5 rounded-md font-bold text-[10px] ${o.status === 'Pending' ? 'bg-amber-500/20 text-amber-400 border border-amber-500/30' : 'bg-emerald-500/20 text-emerald-400 border border-emerald-500/30'}">${o.status === 'Pending' ? 'جديد قيد الانتظار' : o.status}</span>
      </td>
      <td class="p-3.5 text-center">
        <button onclick="updateOrderStatus('${o.id}', 'Delivered')" class="bg-emerald-600/20 hover:bg-emerald-600/40 text-emerald-400 border border-emerald-500/30 px-2 py-1 rounded text-[11px] font-bold">تسليم</button>
      </td>
    `;
    tbody.appendChild(tr);
  });
}

async function updateOrderStatus(id, status) {
  await callBackend('update_order_status', { id, status });
  loadRepOrders();
}

// ========================================================
// INVENTORY LOGIC
// ========================================================
async function loadInventory() {
  const res = await callBackend('get_inventory');
  if (!res || !res.success) return;

  document.getElementById('invTotalCost').innerText = Number(res.totalCostValue || 0).toLocaleString() + ' د.ع';
  document.getElementById('invTotalSell').innerText = Number(res.totalSellingValue || 0).toLocaleString() + ' د.ع';
  document.getElementById('invTotalProfit').innerText = Number(res.expectedProfit || 0).toLocaleString() + ' د.ع';

  const tbody = document.getElementById('inventoryTableBody');
  if (!tbody) return;

  tbody.innerHTML = '';
  (res.products || []).forEach(p => {
    const tr = document.createElement('tr');
    tr.className = 'hover:bg-slate-800/40';
    tr.innerHTML = `
      <td class="p-3.5 font-bold text-white">${p.name}</td>
      <td class="p-3.5 font-mono text-slate-400">${p.barcode || '--'}</td>
      <td class="p-3.5 text-slate-400">${p.category || 'عام'}</td>
      <td class="p-3.5 font-bold text-blue-400">${Number(p.cost).toLocaleString()} د.ع</td>
      <td class="p-3.5 font-bold text-emerald-400">${Number(p.price).toLocaleString()} د.ع</td>
      <td class="p-3.5 font-black ${p.stockQuantity <= p.minStockAlert ? 'text-rose-400' : 'text-slate-200'}">${p.stockQuantity}</td>
      <td class="p-3.5 font-black text-cyan-400">${Number(p.totalCost).toLocaleString()} د.ع</td>
      <td class="p-3.5 text-center">
        <button onclick="deleteProduct('${p.id}')" class="text-rose-400 hover:text-rose-300 p-1">🗑</button>
      </td>
    `;
    tbody.appendChild(tr);
  });
}

async function deleteProduct(id) {
  if (confirm('هل أنت متأكد من حذف هذه المادة؟')) {
    await callBackend('delete_product', { id });
    loadInventory();
  }
}

// ========================================================
// SUPPLIERS & USERS
// ========================================================
async function loadSuppliers() {
  const res = await callBackend('get_suppliers');
  if (!res || !res.success) return;

  const grid = document.getElementById('suppliersCardsGrid');
  if (!grid) return;

  grid.innerHTML = '';
  (res.suppliers || []).forEach(s => {
    const card = document.createElement('div');
    card.className = 'glass-card p-4 flex flex-col justify-between';
    card.innerHTML = `
      <div>
        <div class="flex items-center justify-between mb-2">
          <h4 class="font-black text-base text-white">${s.name}</h4>
          <span class="text-[11px] bg-blue-500/20 text-blue-400 px-2 py-0.5 rounded-full font-bold">مندوب</span>
        </div>
        <p class="text-xs text-slate-400 mb-1">الشركة: ${s.company || 'غير محدد'}</p>
        <p class="text-xs text-slate-400">الهاتف: ${s.phone || '--'}</p>
      </div>
      <div class="mt-4 pt-3 border-t border-slate-800 flex items-center justify-between">
        <span class="text-xs text-slate-400">الرصيد المستحق:</span>
        <span class="text-sm font-black text-amber-400">${Number(s.balance).toLocaleString()} د.ع</span>
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
    card.className = 'glass-card p-4';
    card.innerHTML = `
      <div class="flex items-center gap-3 mb-2">
        <div class="w-10 h-10 rounded-xl bg-purple-500/20 text-purple-400 flex items-center justify-center font-bold">👤</div>
        <div>
          <h4 class="font-bold text-sm text-white">${u.fullName}</h4>
          <span class="text-xs text-slate-400">@${u.username} (${u.role})</span>
        </div>
      </div>
      <div class="mt-3 flex items-center justify-between text-xs">
        <span class="text-slate-400">الحالة:</span>
        <span class="font-bold ${u.isActive ? 'text-emerald-400' : 'text-rose-400'}">${u.isActive ? 'نشط ومفعل ✔' : 'معطل ✕'}</span>
      </div>
    `;
    grid.appendChild(card);
  });
}

// Product Modal Helpers
function openProductModal(prod = null) {
  const modal = document.getElementById('productModal');
  if (!modal) return;

  if (prod) {
    document.getElementById('productModalTitle').innerText = 'تعديل بيانات المادة';
    document.getElementById('modalProdId').value = prod.id;
    document.getElementById('modalProdName').value = prod.name;
    document.getElementById('modalProdBarcode').value = prod.barcode || '';
    document.getElementById('modalProdCategory').value = prod.category || 'عام';
    document.getElementById('modalProdCost').value = prod.cost;
    document.getElementById('modalProdPrice').value = prod.price;
    document.getElementById('modalProdStock').value = prod.stockQuantity;
    document.getElementById('modalProdMinStock').value = prod.minStockAlert || 5;
  } else {
    document.getElementById('productModalTitle').innerText = 'إضافة مادة جديدة للمخزن';
    document.getElementById('modalProdId').value = '';
    document.getElementById('modalProdName').value = '';
    document.getElementById('modalProdBarcode').value = '';
    document.getElementById('modalProdCategory').value = 'عام';
    document.getElementById('modalProdCost').value = '0';
    document.getElementById('modalProdPrice').value = '0';
    document.getElementById('modalProdStock').value = '10';
    document.getElementById('modalProdMinStock').value = '5';
  }

  modal.classList.remove('hidden');
}

function closeProductModal() {
  document.getElementById('productModal')?.classList.add('hidden');
}

async function saveProductFromModal() {
  const id = document.getElementById('modalProdId').value;
  const name = document.getElementById('modalProdName').value.trim();
  if (!name) {
    alert('يرجى إدخال اسم المادة!');
    return;
  }

  const payload = {
    id: id || undefined,
    name: name,
    barcode: document.getElementById('modalProdBarcode').value.trim(),
    category: document.getElementById('modalProdCategory').value.trim() || 'عام',
    cost: Number(document.getElementById('modalProdCost').value || 0),
    price: Number(document.getElementById('modalProdPrice').value || 0),
    stockQuantity: Number(document.getElementById('modalProdStock').value || 0),
    minStockAlert: Number(document.getElementById('modalProdMinStock').value || 5)
  };

  const res = await callBackend('save_product', payload);
  if (res && res.success) {
    closeProductModal();
    loadInventory();
    loadPosProducts();
    loadDashboard();
  }
}

// Modal helper
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


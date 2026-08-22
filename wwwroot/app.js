// ========================================================
// POS CLEAN & FAST APP LOGIC
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
      console.log(`[Mock Bridge] ${action}`, payload);
      resolve({ success: true, message: "Mock Mode" });
    }
  });
}

// Initializer
document.addEventListener('DOMContentLoaded', async () => {
  lucide.createIcons();
  startClock();
  setupBarcodeListener();
  await loadDashboard();
  await loadPosProducts();
  await loadRepOrders();
  setInterval(loadRepOrders, 4000); // Live poll rep orders
});

function startClock() {
  const update = () => {
    const el = document.getElementById('liveClock');
    if (el) el.innerText = new Date().toLocaleTimeString('ar-IQ');
  };
  update();
  setInterval(update, 1000);
}

function setupBarcodeListener() {
  const input = document.getElementById('posSearch');
  if (input) {
    input.addEventListener('keydown', (e) => {
      if (e.key === 'Enter') {
        const val = input.value.trim();
        if (!val) return;
        const match = state.products.find(p => p.barcode === val || p.name.toLowerCase() === val.toLowerCase());
        if (match) {
          addToCart(match);
          input.value = '';
          filterPosProducts();
        }
      }
    });
  }
}

// Tab Switching
function switchTab(tabId) {
  state.activeTab = tabId;

  document.querySelectorAll('.tab-pane').forEach(el => el.classList.add('hidden'));
  document.querySelectorAll('.nav-btn').forEach(el => {
    el.classList.remove('bg-blue-600/15', 'text-blue-400', 'border', 'border-blue-500/30');
    el.classList.add('text-slate-300');
  });

  const pane = document.getElementById(`pane-${tabId}`);
  if (pane) pane.classList.remove('hidden');

  const nav = document.getElementById(`nav-${tabId}`);
  if (nav) {
    nav.classList.add('bg-blue-600/15', 'text-blue-400', 'border', 'border-blue-500/30');
    nav.classList.remove('text-slate-300');
  }

  if (tabId === 'dashboard') loadDashboard();
  if (tabId === 'pos') loadPosProducts();
  if (tabId === 'repOrders') loadRepOrders();
  if (tabId === 'inventory') loadInventory();
  if (tabId === 'suppliers') loadSuppliers();
  if (tabId === 'salesHistory') loadSalesHistory();
  if (tabId === 'users') loadUsers();

  lucide.createIcons();
}

// ========================================================
// 1. DASHBOARD
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
      labels: data.map(d => d.dayName + ' (' + d.shortDate + ')'),
      datasets: [{
        label: 'المبيعات',
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
      cutout: '70%',
      plugins: { legend: { display: false } }
    }
  });

  const legend = document.getElementById('paymentLegend');
  if (legend) {
    legend.innerHTML = `
      <span class="text-emerald-400">💵 ${Number(payments.cash || 0).toLocaleString()}</span>
      <span class="text-blue-400">💳 ${Number(payments.card || 0).toLocaleString()}</span>
      <span class="text-amber-400">📝 ${Number(payments.debt || 0).toLocaleString()}</span>
    `;
  }
}

// ========================================================
// 2. POS CASHIER
// ========================================================
async function loadPosProducts() {
  const res = await callBackend('get_pos_products');
  if (!res || !res.success) return;

  state.products = res.products || [];
  state.categories = res.categories || [];

  const catSelect = document.getElementById('posCategory');
  if (catSelect) {
    catSelect.innerHTML = '<option value="ALL">جميع الأصناف</option>';
    state.categories.forEach(c => {
      catSelect.innerHTML += `<option value="${c}">${c}</option>`;
    });
  }

  filterPosProducts();
}

function filterPosProducts() {
  const query = (document.getElementById('posSearch')?.value || '').trim().toLowerCase();
  const cat = document.getElementById('posCategory')?.value || 'ALL';

  let list = state.products;
  if (cat !== 'ALL') {
    list = list.filter(p => p.category === cat);
  }
  if (query) {
    list = list.filter(p => p.name.toLowerCase().includes(query) || (p.barcode && p.barcode.includes(query)));
  }

  state.filteredProducts = list;
  renderPosGrid();
}

function renderPosGrid() {
  const grid = document.getElementById('posGrid');
  if (!grid) return;

  grid.innerHTML = '';
  state.filteredProducts.forEach(p => {
    const card = document.createElement('div');
    card.className = 'clean-card p-3 cursor-pointer flex flex-col justify-between hover:border-emerald-500';
    card.onclick = () => addToCart(p);
    card.innerHTML = `
      <div>
        <div class="flex items-center justify-between mb-1">
          <span class="text-[10px] bg-[#0A0F1D] text-slate-400 px-2 py-0.5 rounded font-bold">${p.category || 'عام'}</span>
          <span class="text-[10px] ${p.stockQuantity <= 5 ? 'text-rose-400 font-bold' : 'text-slate-400'}">رصيد: ${p.stockQuantity}</span>
        </div>
        <h4 class="text-xs font-bold text-white line-clamp-2">${p.name}</h4>
      </div>
      <div class="mt-2.5 flex items-center justify-between">
        <span class="text-xs font-black text-emerald-400">${Number(p.price).toLocaleString()} د.ع</span>
        <button class="w-5 h-5 rounded bg-emerald-500/20 text-emerald-400 font-bold text-xs flex items-center justify-center">+</button>
      </div>
    `;
    grid.appendChild(card);
  });
}

function addToCart(p) {
  const existing = state.cart.find(i => i.id === p.id);
  if (existing) {
    existing.qty += 1;
  } else {
    state.cart.push({ id: p.id, name: p.name, price: p.price, cost: p.cost, qty: 1 });
  }
  renderCart();
}

function updateCartQty(id, delta) {
  const item = state.cart.find(i => i.id === id);
  if (item) {
    item.qty += delta;
    if (item.qty <= 0) state.cart = state.cart.filter(i => i.id !== id);
  }
  renderCart();
}

function clearCart() {
  state.cart = [];
  renderCart();
}

function renderCart() {
  const list = document.getElementById('posCartList');
  if (!list) return;

  if (state.cart.length === 0) {
    list.innerHTML = '<div class="text-center text-xs text-slate-500 py-10">السلة فارغة</div>';
    calculateCartTotal();
    return;
  }

  list.innerHTML = '';
  state.cart.forEach(i => {
    const el = document.createElement('div');
    el.className = 'bg-[#0A0F1D] border border-[#233559] rounded-lg p-2 flex items-center justify-between text-xs';
    el.innerHTML = `
      <div class="flex-1 pr-1">
        <div class="font-bold text-slate-200">${i.name}</div>
        <div class="text-[10px] text-emerald-400">${Number(i.price).toLocaleString()} د.ع</div>
      </div>
      <div class="flex items-center gap-1 bg-[#16223B] px-1 py-0.5 rounded border border-[#233559]">
        <button onclick="updateCartQty('${i.id}', -1)" class="w-4 h-4 text-rose-400 font-bold">-</button>
        <span class="font-bold text-white px-1">${i.qty}</span>
        <button onclick="updateCartQty('${i.id}', 1)" class="w-4 h-4 text-emerald-400 font-bold">+</button>
      </div>
      <div class="font-black text-slate-200 mr-2">${Number(i.price * i.qty).toLocaleString()}</div>
    `;
    list.appendChild(el);
  });

  calculateCartTotal();
}

function calculateCartTotal() {
  const subTotal = state.cart.reduce((s, i) => s + (i.price * i.qty), 0);
  const discount = Number(document.getElementById('posDiscount')?.value || 0);
  const total = Math.max(0, subTotal - discount);

  document.getElementById('posSubTotal').innerText = Number(subTotal).toLocaleString() + ' د.ع';
  document.getElementById('posTotal').innerText = Number(total).toLocaleString() + ' د.ع';
}

async function completeSale() {
  if (state.cart.length === 0) {
    alert('السلة فارغة!');
    return;
  }

  const discount = Number(document.getElementById('posDiscount')?.value || 0);
  const res = await callBackend('complete_sale', {
    paymentMethod: 'Cash',
    discount: discount,
    items: state.cart
  });

  if (res && res.success) {
    alert(`✔ تم حفظ الفاتورة بنجاح!\nرقم الفاتورة: ${res.invoiceNumber}\nالمبلغ: ${Number(res.total).toLocaleString()} د.ع`);
    clearCart();
    loadDashboard();
  }
}

// ========================================================
// 3. REP ORDERS
// ========================================================
async function loadRepOrders() {
  const res = await callBackend('get_supplier_orders');
  if (!res || !res.success) return;

  const orders = res.orders || [];
  const pending = orders.filter(o => o.status === 'Pending').length;

  const b1 = document.getElementById('repBadge');
  const b2 = document.getElementById('repSideBadge');
  if (b1 && b2) {
    if (pending > 0) {
      b1.innerText = pending;
      b1.classList.remove('hidden');
      b2.innerText = pending;
      b2.classList.remove('hidden');
    } else {
      b1.classList.add('hidden');
      b2.classList.add('hidden');
    }
  }

  const tbody = document.getElementById('repOrdersTbody');
  if (!tbody) return;

  tbody.innerHTML = '';
  orders.forEach(o => {
    const tr = document.createElement('tr');
    tr.className = 'hover:bg-[#16223B]/50';
    tr.innerHTML = `
      <td class="p-3 font-mono font-bold text-blue-400">${o.orderNumber}</td>
      <td class="p-3 font-bold text-white">${o.marketName || '--'}</td>
      <td class="p-3 text-cyan-400">${o.representativeName || '--'}</td>
      <td class="p-3 text-slate-400">${o.marketCity || ''} ${o.marketPhone || ''}</td>
      <td class="p-3 font-bold text-amber-400">${o.itemsCount} مواد</td>
      <td class="p-3 font-black text-emerald-400">${Number(o.totalAmount).toLocaleString()} د.ع</td>
      <td class="p-3">
        <span class="px-2 py-0.5 rounded text-[10px] font-bold ${o.status === 'Pending' ? 'bg-amber-500/20 text-amber-400' : 'bg-emerald-500/20 text-emerald-400'}">${o.status === 'Pending' ? 'جديد قيد الانتظار' : o.status}</span>
      </td>
      <td class="p-3 text-center">
        <button onclick="updateOrderStatus('${o.id}', 'Delivered')" class="bg-emerald-600/20 hover:bg-emerald-600/40 text-emerald-400 border border-emerald-500/30 px-2 py-0.5 rounded text-[11px] font-bold">تسليم</button>
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
// 4. INVENTORY
// ========================================================
async function loadInventory() {
  const res = await callBackend('get_inventory');
  if (!res || !res.success) return;

  document.getElementById('invCost').innerText = Number(res.totalCostValue || 0).toLocaleString() + ' د.ع';
  document.getElementById('invSell').innerText = Number(res.totalSellingValue || 0).toLocaleString() + ' د.ع';
  document.getElementById('invProfit').innerText = Number(res.expectedProfit || 0).toLocaleString() + ' د.ع';

  const tbody = document.getElementById('inventoryTbody');
  if (!tbody) return;

  tbody.innerHTML = '';
  (res.products || []).forEach(p => {
    const tr = document.createElement('tr');
    tr.className = 'hover:bg-[#16223B]/50';
    tr.innerHTML = `
      <td class="p-3 font-bold text-white">${p.name}</td>
      <td class="p-3 font-mono text-slate-400">${p.barcode || '--'}</td>
      <td class="p-3 text-slate-400">${p.category || 'عام'}</td>
      <td class="p-3 font-bold text-blue-400">${Number(p.cost).toLocaleString()}</td>
      <td class="p-3 font-bold text-emerald-400">${Number(p.price).toLocaleString()}</td>
      <td class="p-3 font-black ${p.stockQuantity <= p.minStockAlert ? 'text-rose-400' : 'text-slate-200'}">${p.stockQuantity}</td>
      <td class="p-3 font-black text-cyan-400">${Number(p.totalCost).toLocaleString()}</td>
      <td class="p-3 text-center">
        <button onclick="deleteProduct('${p.id}')" class="text-rose-400 hover:text-rose-300 text-xs">🗑</button>
      </td>
    `;
    tbody.appendChild(tr);
  });
}

function openProductModal() {
  document.getElementById('prodId').value = '';
  document.getElementById('prodName').value = '';
  document.getElementById('prodBarcode').value = '';
  document.getElementById('prodCategory').value = 'عام';
  document.getElementById('prodCost').value = '0';
  document.getElementById('prodPrice').value = '0';
  document.getElementById('prodStock').value = '10';
  document.getElementById('prodMinStock').value = '5';
  document.getElementById('productModal')?.classList.remove('hidden');
}

function closeProductModal() {
  document.getElementById('productModal')?.classList.add('hidden');
}

async function saveProduct() {
  const name = document.getElementById('prodName')?.value.trim();
  if (!name) {
    alert('يرجى إدخال اسم المادة!');
    return;
  }

  const payload = {
    id: document.getElementById('prodId')?.value || undefined,
    name: name,
    barcode: document.getElementById('prodBarcode')?.value.trim(),
    category: document.getElementById('prodCategory')?.value.trim() || 'عام',
    cost: Number(document.getElementById('prodCost')?.value || 0),
    price: Number(document.getElementById('prodPrice')?.value || 0),
    stockQuantity: Number(document.getElementById('prodStock')?.value || 0),
    minStockAlert: Number(document.getElementById('prodMinStock')?.value || 5)
  };

  const res = await callBackend('save_product', payload);
  if (res && res.success) {
    closeProductModal();
    loadInventory();
    loadPosProducts();
    loadDashboard();
  }
}

async function deleteProduct(id) {
  if (confirm('هل أنت متأكد من حذف هذه المادة؟')) {
    await callBackend('delete_product', { id });
    loadInventory();
  }
}

// ========================================================
// 5. SUPPLIERS & 6. SALES HISTORY & 7. USERS
// ========================================================
async function loadSuppliers() {
  const res = await callBackend('get_suppliers');
  if (!res || !res.success) return;

  const grid = document.getElementById('suppliersGrid');
  if (!grid) return;

  grid.innerHTML = '';
  (res.suppliers || []).forEach(s => {
    const card = document.createElement('div');
    card.className = 'clean-card p-3.5 flex flex-col justify-between';
    card.innerHTML = `
      <div>
        <div class="flex items-center justify-between mb-1.5">
          <h4 class="font-black text-sm text-white">${s.name}</h4>
          <span class="text-[10px] bg-blue-500/20 text-blue-400 px-2 py-0.5 rounded font-bold">مندوب</span>
        </div>
        <p class="text-xs text-slate-400 mb-1">الشركة: ${s.company || 'غير محدد'}</p>
        <p class="text-xs text-slate-400">الهاتف: ${s.phone || '--'}</p>
      </div>
      <div class="mt-3 pt-2 border-t border-[#233559] flex items-center justify-between">
        <span class="text-xs text-slate-400">الرصيد:</span>
        <span class="text-xs font-black text-amber-400">${Number(s.balance).toLocaleString()} د.ع</span>
      </div>
    `;
    grid.appendChild(card);
  });
}

async function loadSalesHistory() {
  const res = await callBackend('get_sales_history');
  if (!res || !res.success) return;

  const tbody = document.getElementById('salesHistoryTbody');
  if (!tbody) return;

  tbody.innerHTML = '';
  (res.sales || []).forEach(s => {
    const tr = document.createElement('tr');
    tr.className = 'hover:bg-[#16223B]/50';
    tr.innerHTML = `
      <td class="p-3 font-mono font-bold text-blue-400">${s.invoiceNumber}</td>
      <td class="p-3 font-bold text-white">${s.customerName || 'زبون نقدي'}</td>
      <td class="p-3 text-slate-300">${s.paymentMethod}</td>
      <td class="p-3 text-slate-400">${s.createdAt}</td>
      <td class="p-3 font-bold text-purple-400">${s.itemsCount}</td>
      <td class="p-3 font-black text-emerald-400">${Number(s.totalAmount).toLocaleString()} د.ع</td>
      <td class="p-3"><span class="px-2 py-0.5 rounded text-[10px] font-bold bg-emerald-500/20 text-emerald-400">${s.status}</span></td>
    `;
    tbody.appendChild(tr);
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
    card.className = 'clean-card p-3.5';
    card.innerHTML = `
      <div class="flex items-center gap-2.5 mb-2">
        <div class="w-8 h-8 rounded-lg bg-purple-500/20 text-purple-400 flex items-center justify-center font-bold text-xs">👤</div>
        <div>
          <h4 class="font-bold text-xs text-white">${u.fullName}</h4>
          <span class="text-[11px] text-slate-400">@${u.username} (${u.role})</span>
        </div>
      </div>
      <div class="mt-2 text-xs flex justify-between">
        <span class="text-slate-400">الحالة:</span>
        <span class="font-bold ${u.isActive ? 'text-emerald-400' : 'text-rose-400'}">${u.isActive ? 'نشط ✔' : 'معطل'}</span>
      </div>
    `;
    grid.appendChild(card);
  });
}

function openRepModal() {
  document.getElementById('repModal')?.classList.remove('hidden');
}

function closeRepModal() {
  document.getElementById('repModal')?.classList.add('hidden');
}

function toggleLanguage() {
  state.language = state.language === 'ar' ? 'ku' : 'ar';
  document.getElementById('langBtnText').innerText = state.language === 'ar' ? 'العربية' : 'کوردی';
}

// ========================================================
// 7AMO POS - REACT 18 COMPONENT SUITE
// ========================================================

const { useState, useEffect, useRef } = React;

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

// ========================================================
// MAIN ROOT REACT APP
// ========================================================
function App() {
  const [activeTab, setActiveTab] = useState('dashboard');
  const [lang, setLang] = useState('ar');
  const [repCount, setRepCount] = useState(0);
  const [isRepModalOpen, setIsRepModalOpen] = useState(false);
  const [clock, setClock] = useState('');

  useEffect(() => {
    const updateTime = () => setClock(new Date().toLocaleTimeString('ar-IQ'));
    updateTime();
    const timer = setInterval(updateTime, 1000);
    return () => clearInterval(timer);
  }, []);

  // Poll Rep orders badge count
  useEffect(() => {
    const checkOrders = async () => {
      const res = await callBackend('get_supplier_orders');
      if (res && res.success && res.orders) {
        const pending = res.orders.filter(o => o.status === 'Pending').length;
        setRepCount(pending);
      }
    };
    checkOrders();
    const interval = setInterval(checkOrders, 4000);
    return () => clearInterval(interval);
  }, []);

  return (
    <div className="flex flex-col h-screen overflow-hidden bg-[#0D1322] text-slate-100">
      {/* Top App Bar */}
      <header className="h-14 bg-[#111A2E] border-b border-[#233559] flex items-center justify-between px-5 z-20">
        <div className="flex items-center gap-3">
          <div className="w-9 h-9 rounded-xl bg-blue-600 flex items-center justify-center text-white shadow-md shadow-blue-500/20 font-black text-lg">
            ⚡
          </div>
          <div>
            <h1 className="text-base font-extrabold text-white flex items-center gap-2">
              <span>7amo POS</span>
              <span className="text-[10px] bg-blue-500/20 text-blue-400 border border-blue-500/30 px-2 py-0.5 rounded-full font-bold">React PRO v1.4.0</span>
            </h1>
          </div>
        </div>

        <div className="flex items-center gap-3">
          <div className="text-xs font-semibold text-slate-300 bg-[#0A0F1D] px-3 py-1.5 rounded-lg border border-[#233559]">
            {clock}
          </div>
          <div className="text-xs font-bold text-emerald-400 bg-emerald-950/40 border border-emerald-800/50 px-3 py-1.5 rounded-lg">
            د.ع (العراق)
          </div>
          
          <button 
            onClick={() => setActiveTab('repOrders')}
            className="relative p-2 rounded-xl bg-[#0A0F1D] hover:bg-[#16223B] border border-[#233559] text-slate-300 hover:text-white transition"
          >
            🔔
            {repCount > 0 && (
              <span className="absolute -top-1 -right-1 w-5 h-5 bg-rose-600 text-[11px] font-bold text-white rounded-full flex items-center justify-center animate-pulse">
                {repCount}
              </span>
            )}
          </button>

          <button 
            onClick={() => setLang(l => l === 'ar' ? 'ku' : 'ar')}
            className="flex items-center gap-1.5 text-xs font-bold bg-blue-600/20 hover:bg-blue-600/30 text-blue-400 border border-blue-500/30 px-3 py-1.5 rounded-lg transition"
          >
            🌐 {lang === 'ar' ? 'العربية' : 'کوردی'}
          </button>

          <div className="flex items-center gap-2 bg-[#0A0F1D] border border-[#233559] px-3 py-1 rounded-xl text-xs font-bold text-slate-200">
            <span className="w-2 h-2 rounded-full bg-emerald-500"></span>
            <span>الكاشير الرئيسي</span>
          </div>
        </div>
      </header>

      {/* Main Workspace */}
      <div className="flex flex-1 overflow-hidden">
        {/* Sidebar */}
        <aside className="w-60 bg-[#111A2E] border-l border-[#233559] flex flex-col justify-between p-3 z-10">
          <nav className="space-y-1">
            <SidebarButton 
              id="dashboard" 
              active={activeTab} 
              onClick={setActiveTab} 
              icon="📊" 
              label={lang === 'ar' ? 'لوحة التحكم' : 'داشبۆرد'} 
            />
            <SidebarButton 
              id="pos" 
              active={activeTab} 
              onClick={setActiveTab} 
              icon="🛒" 
              label={lang === 'ar' ? 'نقطة البيع (الكاشير)' : 'کاشێر'} 
            />
            <SidebarButton 
              id="repOrders" 
              active={activeTab} 
              onClick={setActiveTab} 
              icon="🚚" 
              label={lang === 'ar' ? 'طلبيات المناديب' : 'داواکاری مەندووب'} 
              badge={repCount}
            />
            <SidebarButton 
              id="inventory" 
              active={activeTab} 
              onClick={setActiveTab} 
              icon="📦" 
              label={lang === 'ar' ? 'المخزن والمستودع' : 'کۆگا و مەخزەن'} 
            />
            <SidebarButton 
              id="suppliers" 
              active={activeTab} 
              onClick={setActiveTab} 
              icon="🤝" 
              label={lang === 'ar' ? 'المناديب والشركات' : 'مەندوب و دابینکەران'} 
            />
            <SidebarButton 
              id="salesHistory" 
              active={activeTab} 
              onClick={setActiveTab} 
              icon="📜" 
              label={lang === 'ar' ? 'سجل المبيعات' : 'مێژووی فرۆشتن'} 
            />
            <SidebarButton 
              id="users" 
              active={activeTab} 
              onClick={setActiveTab} 
              icon="👥" 
              label={lang === 'ar' ? 'المستخدمين' : 'بەکارهێنەران'} 
            />
          </nav>

          <div className="p-3 bg-[#0A0F1D] rounded-xl border border-[#233559] text-center space-y-2">
            <div className="text-[11px] font-bold text-blue-400">📲 بوابة المناديب بالسحابة</div>
            <button 
              onClick={() => setIsRepModalOpen(true)}
              className="w-full py-1.5 bg-blue-600 hover:bg-blue-500 text-white rounded-lg text-xs font-bold transition"
            >
              عرض باركود QR
            </button>
          </div>
        </aside>

        {/* View Component Switcher */}
        <main className="flex-1 overflow-y-auto p-5 bg-[#0D1322]">
          {activeTab === 'dashboard' && <DashboardView lang={lang} onNavigate={setActiveTab} />}
          {activeTab === 'pos' && <CashierView lang={lang} onSaleSuccess={() => {}} />}
          {activeTab === 'repOrders' && <RepOrdersView lang={lang} />}
          {activeTab === 'inventory' && <InventoryView lang={lang} />}
          {activeTab === 'suppliers' && <SuppliersView lang={lang} />}
          {activeTab === 'salesHistory' && <SalesHistoryView lang={lang} />}
          {activeTab === 'users' && <UsersView lang={lang} />}
        </main>
      </div>

      {/* QR Modal */}
      {isRepModalOpen && (
        <div className="fixed inset-0 bg-black/75 backdrop-blur-sm flex items-center justify-center z-50">
          <div className="clean-card w-[380px] p-5 text-center space-y-3 bg-[#111A2E] border border-[#233559]">
            <h3 className="text-sm font-black text-white">📲 بوابة المناديب والمحلات</h3>
            <p className="text-xs text-slate-300">امسح الباركود التالي بالموبايل لإرسال الطلبيات مباشرة</p>
            <div className="bg-white p-3 rounded-xl w-44 h-44 mx-auto flex items-center justify-center">
              <img src="https://api.qrserver.com/v1/create-qr-code/?size=160x160&data=https://hama2002m2002-lab.github.io/mo74mmed.pos/" alt="QR" className="w-full h-full" />
            </div>
            <p className="text-[11px] font-mono text-blue-400 break-all bg-[#0A0F1D] p-2 rounded border border-[#233559]">https://hama2002m2002-lab.github.io/mo74mmed.pos/</p>
            <button onClick={() => setIsRepModalOpen(false)} className="w-full py-2 bg-[#16223B] text-slate-300 rounded-lg text-xs font-bold">إغلاق</button>
          </div>
        </div>
      )}
    </div>
  );
}

function SidebarButton({ id, active, onClick, icon, label, badge }) {
  const isSelected = active === id;
  return (
    <button 
      onClick={() => onClick(id)}
      className={`w-full flex items-center justify-between px-3.5 py-2.5 rounded-xl font-bold text-xs transition ${
        isSelected ? 'bg-blue-600/20 text-blue-400 border border-blue-500/30' : 'text-slate-300 hover:bg-[#16223B] hover:text-white'
      }`}
    >
      <div className="flex items-center gap-3">
        <span className="text-sm">{icon}</span>
        <span>{label}</span>
      </div>
      {badge > 0 && (
        <span className="text-[10px] bg-rose-600 text-white font-bold px-2 py-0.5 rounded-full">
          {badge}
        </span>
      )}
    </button>
  );
}

// ========================================================
// 1. DASHBOARD VIEW COMPONENT
// ========================================================
function DashboardView({ lang, onNavigate }) {
  const [data, setData] = useState(null);
  const chartRef = useRef(null);
  const paymentRef = useRef(null);

  const loadData = async () => {
    const res = await callBackend('get_dashboard_data');
    if (res && res.success) {
      setData(res);
      renderCharts(res);
    }
  };

  useEffect(() => {
    loadData();
  }, []);

  const renderCharts = (d) => {
    if (chartRef.current && d.weeklyTrend) {
      new Chart(chartRef.current, {
        type: 'bar',
        data: {
          labels: d.weeklyTrend.map(w => w.dayName + ' (' + w.shortDate + ')'),
          datasets: [{
            label: 'المبيعات',
            data: d.weeklyTrend.map(w => w.revenue),
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

    if (paymentRef.current && d.payments) {
      new Chart(paymentRef.current, {
        type: 'doughnut',
        data: {
          labels: ['نقداً', 'بطاقة', 'آجل'],
          datasets: [{
            data: [d.payments.cash || 0, d.payments.card || 0, d.payments.debt || 0],
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
    }
  };

  return (
    <div className="space-y-5">
      <div className="flex items-center justify-between">
        <div>
          <h2 className="text-xl font-black text-white">{lang === 'ar' ? 'لوحة التحكم والمؤشرات' : 'داشبۆرد و ئامارەکان'}</h2>
          <p className="text-xs text-slate-400 mt-0.5">مراقبة حية للمبيعات اليومية والشهرية ونواقص المخزون</p>
        </div>
        <button onClick={loadData} className="flex items-center gap-2 bg-[#16223B] hover:bg-[#1C2B4A] border border-[#233559] text-slate-200 px-3.5 py-1.5 rounded-xl text-xs font-bold transition">
          🔄 تحديث
        </button>
      </div>

      <div className="grid grid-cols-4 gap-4">
        <div className="clean-card p-4 flex flex-col justify-between border-t-4 border-t-emerald-500">
          <span className="text-xs font-bold text-slate-400">مبيعات اليوم</span>
          <div className="mt-2">
            <span className="text-2xl font-black text-emerald-400">{Number(data?.todayRevenue || 0).toLocaleString()}</span>
            <span className="text-xs font-bold text-emerald-500"> د.ع</span>
          </div>
        </div>

        <div className="clean-card p-4 flex flex-col justify-between border-t-4 border-t-blue-500">
          <span className="text-xs font-bold text-slate-400">فواتير اليوم</span>
          <div className="mt-2">
            <span className="text-2xl font-black text-blue-400">{Number(data?.todayInvoices || 0).toLocaleString()}</span>
            <span className="text-xs text-slate-400"> فاتورة</span>
          </div>
        </div>

        <div className="clean-card p-4 flex flex-col justify-between border-t-4 border-t-purple-500">
          <span className="text-xs font-bold text-slate-400">مبيعات الشهر</span>
          <div className="mt-2">
            <span className="text-2xl font-black text-purple-400">{Number(data?.monthlyRevenue || 0).toLocaleString()}</span>
            <span className="text-xs font-bold text-purple-500"> د.ع</span>
          </div>
        </div>

        <div className="clean-card p-4 flex flex-col justify-between border-t-4 border-t-amber-500">
          <span className="text-xs font-bold text-slate-400">نواقص المخزن</span>
          <div className="mt-2">
            <span className="text-2xl font-black text-amber-400">{Number(data?.lowStockCount || 0).toLocaleString()}</span>
            <span className="text-xs text-slate-400"> أصناف بحاجة للطلب</span>
          </div>
        </div>
      </div>

      <div className="grid grid-cols-12 gap-4">
        <div className="col-span-8 clean-card p-4">
          <h3 className="text-xs font-bold text-slate-300 mb-3">مبيعات الأسبوع الأخير (د.ع)</h3>
          <div className="h-52">
            <canvas ref={chartRef}></canvas>
          </div>
        </div>

        <div className="col-span-4 clean-card p-4 flex flex-col justify-between">
          <h3 className="text-xs font-bold text-slate-300 mb-2">توزيع طرق الدفع</h3>
          <div className="h-40 flex items-center justify-center">
            <canvas ref={paymentRef}></canvas>
          </div>
          <div className="flex justify-around text-[11px] font-bold mt-2 pt-2 border-t border-[#233559]">
            <span className="text-emerald-400">💵 {Number(data?.payments?.cash || 0).toLocaleString()}</span>
            <span className="text-blue-400">💳 {Number(data?.payments?.card || 0).toLocaleString()}</span>
            <span className="text-amber-400">📝 {Number(data?.payments?.debt || 0).toLocaleString()}</span>
          </div>
        </div>
      </div>
    </div>
  );
}

// ========================================================
// 2. CASHIER / POS COMPONENT
// ========================================================
function CashierView({ lang }) {
  const [products, setProducts] = useState([]);
  const [categories, setCategories] = useState([]);
  const [selectedCat, setSelectedCat] = useState('ALL');
  const [search, setSearch] = useState('');
  const [cart, setCart] = useState([]);
  const [discount, setDiscount] = useState(0);

  const loadProducts = async () => {
    const res = await callBackend('get_pos_products');
    if (res && res.success) {
      setProducts(res.products || []);
      setCategories(res.categories || []);
    }
  };

  useEffect(() => {
    loadProducts();
  }, []);

  const filtered = products.filter(p => {
    const matchCat = selectedCat === 'ALL' || p.category === selectedCat;
    const matchSearch = !search || p.name.toLowerCase().includes(search.toLowerCase()) || (p.barcode && p.barcode.includes(search));
    return matchCat && matchSearch;
  });

  const addToCart = (p) => {
    setCart(prev => {
      const found = prev.find(i => i.id === p.id);
      if (found) return prev.map(i => i.id === p.id ? { ...i, qty: i.qty + 1 } : i);
      return [...prev, { id: p.id, name: p.name, price: p.price, cost: p.cost, qty: 1 }];
    });
  };

  const updateQty = (id, delta) => {
    setCart(prev => prev.map(i => i.id === id ? { ...i, qty: i.qty + delta } : i).filter(i => i.qty > 0));
  };

  const subTotal = cart.reduce((s, i) => s + (i.price * i.qty), 0);
  const total = Math.max(0, subTotal - discount);

  const handleCompleteSale = async () => {
    if (cart.length === 0) return alert('السلة فارغة!');
    const res = await callBackend('complete_sale', {
      paymentMethod: 'Cash',
      discount: Number(discount),
      items: cart
    });
    if (res && res.success) {
      alert(`✔ تم حفظ الفاتورة بنجاح!\nرقم الفاتورة: ${res.invoiceNumber}\nالمبلغ: ${Number(res.total).toLocaleString()} د.ع`);
      setCart([]);
      setDiscount(0);
      loadProducts();
    }
  };

  return (
    <div className="h-full flex gap-4">
      {/* Catalog */}
      <div className="flex-1 flex flex-col clean-card p-3.5 overflow-hidden">
        <div className="flex gap-2 mb-3">
          <input 
            type="text" 
            placeholder="مسح الباركود أو اسم المادة..." 
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            className="clean-input flex-1 px-3 py-1.5 text-xs text-white"
          />
          <select 
            value={selectedCat} 
            onChange={(e) => setSelectedCat(e.target.value)}
            className="clean-input text-xs px-3 font-bold text-slate-300"
          >
            <option value="ALL">جميع الأصناف</option>
            {categories.map(c => <option key={c} value={c}>{c}</option>)}
          </select>
        </div>

        <div className="flex-1 grid grid-cols-4 gap-2.5 overflow-y-auto pr-1">
          {filtered.map(p => (
            <div 
              key={p.id} 
              onClick={() => addToCart(p)}
              className="clean-card p-3 cursor-pointer flex flex-col justify-between hover:border-emerald-500 transition"
            >
              <div>
                <div className="flex items-center justify-between mb-1">
                  <span className="text-[10px] bg-[#0A0F1D] text-slate-400 px-2 py-0.5 rounded font-bold">{p.category}</span>
                  <span className={`text-[10px] font-bold ${p.stockQuantity <= 5 ? 'text-rose-400' : 'text-slate-400'}`}>رصيد: {p.stockQuantity}</span>
                </div>
                <h4 className="text-xs font-bold text-white line-clamp-2">{p.name}</h4>
              </div>
              <div className="mt-2.5 flex items-center justify-between">
                <span className="text-xs font-black text-emerald-400">{Number(p.price).toLocaleString()} د.ع</span>
                <span className="w-5 h-5 rounded bg-emerald-500/20 text-emerald-400 font-bold text-xs flex items-center justify-center">+</span>
              </div>
            </div>
          ))}
        </div>
      </div>

      {/* Cart Drawer */}
      <div className="w-80 clean-card p-3.5 flex flex-col border border-blue-500/30">
        <div className="flex items-center justify-between pb-2 border-b border-[#233559]">
          <h3 className="font-extrabold text-sm text-white flex items-center gap-1.5">🛒 سلة الفاتورة</h3>
          <button onClick={() => setCart([])} className="text-[11px] text-rose-400 hover:text-rose-300 font-bold">تفريغ</button>
        </div>

        <div className="flex-1 overflow-y-auto py-2 space-y-1.5 pr-1">
          {cart.length === 0 ? (
            <div className="text-center text-xs text-slate-500 py-10">السلة فارغة</div>
          ) : (
            cart.map(i => (
              <div key={i.id} className="bg-[#0A0F1D] border border-[#233559] rounded-lg p-2 flex items-center justify-between text-xs">
                <div className="flex-1 pr-1">
                  <div className="font-bold text-slate-200">{i.name}</div>
                  <div className="text-[10px] text-emerald-400">{Number(i.price).toLocaleString()} د.ع</div>
                </div>
                <div className="flex items-center gap-1 bg-[#16223B] px-1 py-0.5 rounded border border-[#233559]">
                  <button onClick={() => updateQty(i.id, -1)} className="w-4 h-4 text-rose-400 font-bold">-</button>
                  <span className="font-bold text-white px-1">{i.qty}</span>
                  <button onClick={() => updateQty(i.id, 1)} className="w-4 h-4 text-emerald-400 font-bold">+</button>
                </div>
                <div className="font-black text-slate-200 mr-2">{Number(i.price * i.qty).toLocaleString()}</div>
              </div>
            ))
          )}
        </div>

        <div className="pt-2 border-t border-[#233559] space-y-2 text-xs">
          <div className="flex justify-between text-slate-400">
            <span>المجموع:</span>
            <span className="font-bold text-white">{Number(subTotal).toLocaleString()} د.ع</span>
          </div>
          <div className="flex justify-between items-center text-amber-400">
            <span>خصم (د.ع):</span>
            <input 
              type="number" 
              value={discount} 
              onChange={(e) => setDiscount(e.target.value)} 
              className="clean-input w-20 text-right px-2 py-0.5 text-xs text-amber-400" 
            />
          </div>
          <div className="flex justify-between text-sm font-black text-emerald-400 pt-1.5 border-t border-[#233559]">
            <span>الصافي المطلوب:</span>
            <span className="text-base">{Number(total).toLocaleString()} د.ع</span>
          </div>

          <button onClick={handleCompleteSale} className="btn-success w-full py-2.5 mt-2 flex items-center justify-center gap-2 text-xs">
            ✔ إتمام البيع وحفظ الفاتورة
          </button>
        </div>
      </div>
    </div>
  );
}

// ========================================================
// 3. REP ORDERS VIEW COMPONENT
// ========================================================
function RepOrdersView({ lang }) {
  const [orders, setOrders] = useState([]);

  const loadOrders = async () => {
    const res = await callBackend('get_supplier_orders');
    if (res && res.success) setOrders(res.orders || []);
  };

  useEffect(() => {
    loadOrders();
  }, []);

  const updateStatus = async (id, status) => {
    await callBackend('update_order_status', { id, status });
    loadOrders();
  };

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <div>
          <h2 className="text-xl font-black text-white">طلبيات المناديب والمحلات</h2>
          <p className="text-xs text-slate-400 mt-0.5">الطلبيات الواردة مباشرة من تطبيق المندوب بالسحابة</p>
        </div>
        <button onClick={loadOrders} className="flex items-center gap-2 bg-[#16223B] border border-[#233559] text-slate-200 px-3.5 py-1.5 rounded-xl text-xs font-bold">
          🔄 تحديث
        </button>
      </div>

      <div className="clean-card overflow-hidden">
        <table className="w-full text-right text-xs">
          <thead className="bg-[#111A2E] text-slate-400 font-bold border-b border-[#233559]">
            <tr>
              <th className="p-3">رقم الطلبية</th>
              <th className="p-3">اسم الماركت</th>
              <th className="p-3">المندوب</th>
              <th className="p-3">الهاتف والمدينة</th>
              <th className="p-3">المواد</th>
              <th className="p-3">الإجمالي (د.ع)</th>
              <th className="p-3">الحالة</th>
              <th className="p-3 text-center">إجراء</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-[#233559]/60 text-slate-200">
            {orders.map(o => (
              <tr key={o.id} className="hover:bg-[#16223B]/50">
                <td className="p-3 font-mono font-bold text-blue-400">{o.orderNumber}</td>
                <td className="p-3 font-bold text-white">{o.marketName || '--'}</td>
                <td className="p-3 text-cyan-400">{o.representativeName || '--'}</td>
                <td className="p-3 text-slate-400">{o.marketCity || ''} {o.marketPhone || ''}</td>
                <td className="p-3 font-bold text-amber-400">{o.itemsCount} مواد</td>
                <td className="p-3 font-black text-emerald-400">{Number(o.totalAmount).toLocaleString()} د.ع</td>
                <td className="p-3">
                  <span className={`px-2 py-0.5 rounded text-[10px] font-bold ${o.status === 'Pending' ? 'bg-amber-500/20 text-amber-400' : 'bg-emerald-500/20 text-emerald-400'}`}>
                    {o.status === 'Pending' ? 'جديد قيد الانتظار' : o.status}
                  </span>
                </td>
                <td className="p-3 text-center">
                  <button onClick={() => updateStatus(o.id, 'Delivered')} className="bg-emerald-600/20 hover:bg-emerald-600/40 text-emerald-400 border border-emerald-500/30 px-2 py-0.5 rounded text-[11px] font-bold">
                    تسليم
                  </button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}

// ========================================================
// 4. INVENTORY VIEW COMPONENT
// ========================================================
function InventoryView({ lang }) {
  const [data, setData] = useState(null);
  const [modalOpen, setModalOpen] = useState(false);
  const [form, setForm] = useState({ id: '', name: '', barcode: '', category: 'عام', cost: 0, price: 0, stockQuantity: 10, minStockAlert: 5 });

  const loadData = async () => {
    const res = await callBackend('get_inventory');
    if (res && res.success) setData(res);
  };

  useEffect(() => {
    loadData();
  }, []);

  const handleSave = async () => {
    if (!form.name.trim()) return alert('يرجى كتابة اسم المادة!');
    const res = await callBackend('save_product', {
      ...form,
      cost: Number(form.cost),
      price: Number(form.price),
      stockQuantity: Number(form.stockQuantity),
      minStockAlert: Number(form.minStockAlert)
    });
    if (res && res.success) {
      setModalOpen(false);
      loadData();
    }
  };

  const handleDelete = async (id) => {
    if (confirm('هل أنت متأكد من حذف هذه المادة؟')) {
      await callBackend('delete_product', { id });
      loadData();
    }
  };

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <div>
          <h2 className="text-xl font-black text-white">المخزن والمستودع</h2>
          <p className="text-xs text-slate-400 mt-0.5">مراقبة كميات المواد وأسعار الشراء والبيع</p>
        </div>
        <button 
          onClick={() => { setForm({ id: '', name: '', barcode: '', category: 'عام', cost: 0, price: 0, stockQuantity: 10, minStockAlert: 5 }); setModalOpen(true); }}
          className="btn-primary flex items-center gap-2 px-3.5 py-1.5 text-xs"
        >
          ➕ إضافة مادة
        </button>
      </div>

      <div className="grid grid-cols-3 gap-3">
        <div className="clean-card p-3 border-r-4 border-r-blue-500">
          <span className="text-[11px] text-slate-400">قيمة التكلفة الإجمالية</span>
          <div className="text-lg font-black text-blue-400 mt-0.5">{Number(data?.totalCostValue || 0).toLocaleString()} د.ع</div>
        </div>
        <div className="clean-card p-3 border-r-4 border-r-emerald-500">
          <span className="text-[11px] text-slate-400">القيمة البيعية المتوقعة</span>
          <div className="text-lg font-black text-emerald-400 mt-0.5">{Number(data?.totalSellingValue || 0).toLocaleString()} د.ع</div>
        </div>
        <div className="clean-card p-3 border-r-4 border-r-purple-500">
          <span className="text-[11px] text-slate-400">الأرباح التقديرية</span>
          <div className="text-lg font-black text-purple-400 mt-0.5">{Number(data?.expectedProfit || 0).toLocaleString()} د.ع</div>
        </div>
      </div>

      <div className="clean-card overflow-hidden">
        <table className="w-full text-right text-xs">
          <thead className="bg-[#111A2E] text-slate-400 font-bold border-b border-[#233559]">
            <tr>
              <th className="p-3">اسم المادة</th>
              <th className="p-3">الباركود</th>
              <th className="p-3">التصنيف</th>
              <th className="p-3">سعر الشراء</th>
              <th className="p-3">سعر البيع</th>
              <th className="p-3">الرصيد</th>
              <th className="p-3">إجمالي التكلفة</th>
              <th className="p-3 text-center">إجراءات</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-[#233559]/60 text-slate-200">
            {(data?.products || []).map(p => (
              <tr key={p.id} className="hover:bg-[#16223B]/50">
                <td className="p-3 font-bold text-white">{p.name}</td>
                <td className="p-3 font-mono text-slate-400">{p.barcode || '--'}</td>
                <td className="p-3 text-slate-400">{p.category || 'عام'}</td>
                <td className="p-3 font-bold text-blue-400">{Number(p.cost).toLocaleString()}</td>
                <td className="p-3 font-bold text-emerald-400">{Number(p.price).toLocaleString()}</td>
                <td className={`p-3 font-black ${p.stockQuantity <= p.minStockAlert ? 'text-rose-400' : 'text-slate-200'}`}>{p.stockQuantity}</td>
                <td className="p-3 font-black text-cyan-400">{Number(p.totalCost).toLocaleString()}</td>
                <td className="p-3 text-center flex justify-center gap-2">
                  <button onClick={() => { setForm(p); setModalOpen(true); }} className="text-blue-400 hover:text-blue-300">✏️</button>
                  <button onClick={() => handleDelete(p.id)} className="text-rose-400 hover:text-rose-300">🗑</button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {/* Product Modal */}
      {modalOpen && (
        <div className="fixed inset-0 bg-black/75 backdrop-blur-sm flex items-center justify-center z-50">
          <div className="clean-card w-[480px] p-5 space-y-3 bg-[#111A2E] border border-[#233559]">
            <div className="flex items-center justify-between pb-2 border-b border-[#233559]">
              <h3 className="text-sm font-extrabold text-white">{form.id ? 'تعديل بيانات المادة' : 'إضافة مادة جديدة'}</h3>
              <button onClick={() => setModalOpen(false)} className="text-slate-400 hover:text-white font-bold text-sm">✕</button>
            </div>
            <div className="grid grid-cols-2 gap-2.5 text-xs">
              <div className="col-span-2">
                <label className="block text-slate-400 mb-1 font-bold">اسم المادة *</label>
                <input type="text" value={form.name} onChange={e => setForm({ ...form, name: e.target.value })} className="clean-input w-full px-3 py-1.5 text-xs" />
              </div>
              <div>
                <label className="block text-slate-400 mb-1 font-bold">الباركود</label>
                <input type="text" value={form.barcode} onChange={e => setForm({ ...form, barcode: e.target.value })} className="clean-input w-full px-3 py-1.5 text-xs font-mono" />
              </div>
              <div>
                <label className="block text-slate-400 mb-1 font-bold">التصنيف</label>
                <input type="text" value={form.category} onChange={e => setForm({ ...form, category: e.target.value })} className="clean-input w-full px-3 py-1.5 text-xs" />
              </div>
              <div>
                <label className="block text-slate-400 mb-1 font-bold">سعر الشراء (د.ع)</label>
                <input type="number" value={form.cost} onChange={e => setForm({ ...form, cost: e.target.value })} className="clean-input w-full px-3 py-1.5 text-xs text-blue-400 font-bold" />
              </div>
              <div>
                <label className="block text-slate-400 mb-1 font-bold">سعر البيع (د.ع)</label>
                <input type="number" value={form.price} onChange={e => setForm({ ...form, price: e.target.value })} className="clean-input w-full px-3 py-1.5 text-xs text-emerald-400 font-bold" />
              </div>
              <div>
                <label className="block text-slate-400 mb-1 font-bold">الرصيد المتوفر</label>
                <input type="number" value={form.stockQuantity} onChange={e => setForm({ ...form, stockQuantity: e.target.value })} className="clean-input w-full px-3 py-1.5 text-xs" />
              </div>
              <div>
                <label className="block text-slate-400 mb-1 font-bold">تنبيه النواقص</label>
                <input type="number" value={form.minStockAlert} onChange={e => setForm({ ...form, minStockAlert: e.target.value })} className="clean-input w-full px-3 py-1.5 text-xs text-amber-400 font-bold" />
              </div>
            </div>
            <div className="flex gap-2 pt-2 border-t border-[#233559]">
              <button onClick={handleSave} className="btn-primary flex-1 py-2 text-xs">حفظ</button>
              <button onClick={() => setModalOpen(false)} className="px-4 py-2 bg-[#16223B] text-slate-300 rounded-lg text-xs font-bold">إلغاء</button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}

// ========================================================
// 5. SUPPLIERS & 6. SALES HISTORY & 7. USERS
// ========================================================
function SuppliersView() {
  const [suppliers, setSuppliers] = useState([]);

  useEffect(() => {
    (async () => {
      const res = await callBackend('get_suppliers');
      if (res && res.success) setSuppliers(res.suppliers || []);
    })();
  }, []);

  return (
    <div className="space-y-4">
      <h2 className="text-xl font-black text-white">المناديب والشركات الموردة</h2>
      <div className="grid grid-cols-3 gap-3">
        {suppliers.map(s => (
          <div key={s.id} className="clean-card p-3.5 flex flex-col justify-between">
            <div>
              <div className="flex items-center justify-between mb-1.5">
                <h4 className="font-black text-sm text-white">{s.name}</h4>
                <span className="text-[10px] bg-blue-500/20 text-blue-400 px-2 py-0.5 rounded font-bold">مندوب</span>
              </div>
              <p className="text-xs text-slate-400 mb-1">الشركة: {s.company || 'غير محدد'}</p>
              <p className="text-xs text-slate-400">الهاتف: {s.phone || '--'}</p>
            </div>
            <div className="mt-3 pt-2 border-t border-[#233559] flex items-center justify-between">
              <span className="text-xs text-slate-400">الرصيد المستحق:</span>
              <span className="text-xs font-black text-amber-400">{Number(s.balance).toLocaleString()} د.ع</span>
            </div>
          </div>
        ))}
      </div>
    </div>
  );
}

function SalesHistoryView() {
  const [sales, setSales] = useState([]);

  useEffect(() => {
    (async () => {
      const res = await callBackend('get_sales_history');
      if (res && res.success) setSales(res.sales || []);
    })();
  }, []);

  return (
    <div className="space-y-4">
      <h2 className="text-xl font-black text-white">سجل المبيعات والفواتير</h2>
      <div className="clean-card overflow-hidden">
        <table className="w-full text-right text-xs">
          <thead className="bg-[#111A2E] text-slate-400 font-bold border-b border-[#233559]">
            <tr>
              <th className="p-3">رقم الفاتورة</th>
              <th className="p-3">العميل</th>
              <th className="p-3">طريقة الدفع</th>
              <th className="p-3">التاريخ والوقت</th>
              <th className="p-3">المواد</th>
              <th className="p-3">المبلغ الإجمالي</th>
              <th className="p-3">الحالة</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-[#233559]/60 text-slate-200">
            {sales.map(s => (
              <tr key={s.id} className="hover:bg-[#16223B]/50">
                <td className="p-3 font-mono font-bold text-blue-400">{s.invoiceNumber}</td>
                <td className="p-3 font-bold text-white">{s.customerName || 'زبون نقدي'}</td>
                <td className="p-3 text-slate-300">{s.paymentMethod}</td>
                <td className="p-3 text-slate-400">{s.createdAt}</td>
                <td className="p-3 font-bold text-purple-400">{s.itemsCount}</td>
                <td className="p-3 font-black text-emerald-400">{Number(s.totalAmount).toLocaleString()} د.ع</td>
                <td className="p-3"><span className="px-2 py-0.5 rounded text-[10px] font-bold bg-emerald-500/20 text-emerald-400">{s.status}</span></td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}

function UsersView() {
  const [users, setUsers] = useState([]);

  useEffect(() => {
    (async () => {
      const res = await callBackend('get_users');
      if (res && res.success) setUsers(res.users || []);
    })();
  }, []);

  return (
    <div className="space-y-4">
      <h2 className="text-xl font-black text-white">حسابات الكاشير والمستخدمين</h2>
      <div className="grid grid-cols-3 gap-3">
        {users.map(u => (
          <div key={u.id} className="clean-card p-3.5">
            <div className="flex items-center gap-2.5 mb-2">
              <div className="w-8 h-8 rounded-lg bg-purple-500/20 text-purple-400 flex items-center justify-center font-bold text-xs">👤</div>
              <div>
                <h4 className="font-bold text-xs text-white">{u.fullName}</h4>
                <span className="text-[11px] text-slate-400">@{u.username} ({u.role})</span>
              </div>
            </div>
            <div className="mt-2 text-xs flex justify-between">
              <span className="text-slate-400">الحالة:</span>
              <span className={`font-bold ${u.isActive ? 'text-emerald-400' : 'text-rose-400'}`}>{u.isActive ? 'نشط ✔' : 'معطل'}</span>
            </div>
          </div>
        ))}
      </div>
    </div>
  );
}

// Render React App into DOM
const root = ReactDOM.createRoot(document.getElementById('root'));
root.render(<App />);

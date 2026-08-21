import sqlite3
import uuid
import json
import os
from datetime import datetime, date
from typing import List, Dict, Any, Optional
from config import DB_PATH

class Database:
    def __init__(self, db_path: str = DB_PATH):
        self.db_path = db_path
        self.init_db()

    def get_connection(self):
        conn = sqlite3.connect(self.db_path)
        conn.row_factory = sqlite3.Row
        return conn

    def init_db(self):
        """Ensure all required tables and columns exist while preserving existing data."""
        with self.get_connection() as conn:
            cursor = conn.cursor()
            
            # Products Table
            cursor.execute("""
            CREATE TABLE IF NOT EXISTS Products (
                Id TEXT PRIMARY KEY,
                Name TEXT NOT NULL,
                Barcode TEXT,
                Price REAL NOT NULL DEFAULT 0,
                Cost REAL NOT NULL DEFAULT 0,
                StockQuantity REAL NOT NULL DEFAULT 0,
                MinStockAlert REAL NOT NULL DEFAULT 5,
                CategoryId TEXT,
                SupplierId TEXT,
                ExpiryDate TEXT,
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT
            )
            """)

            # Categories Table
            cursor.execute("""
            CREATE TABLE IF NOT EXISTS Categories (
                Id TEXT PRIMARY KEY,
                Name TEXT NOT NULL,
                Icon TEXT,
                CreatedAt TEXT NOT NULL
            )
            """)

            # Sales Table
            cursor.execute("""
            CREATE TABLE IF NOT EXISTS Sales (
                Id TEXT PRIMARY KEY,
                InvoiceNumber TEXT NOT NULL,
                TotalAmount REAL NOT NULL DEFAULT 0,
                DiscountAmount REAL NOT NULL DEFAULT 0,
                PaidAmount REAL NOT NULL DEFAULT 0,
                RemainingAmount REAL NOT NULL DEFAULT 0,
                PaymentMethod TEXT NOT NULL DEFAULT 'Cash',
                Status TEXT NOT NULL DEFAULT 'Completed',
                CashierId TEXT,
                CashierName TEXT,
                CustomerName TEXT,
                Notes TEXT,
                CreatedAt TEXT NOT NULL
            )
            """)

            # SaleItems Table
            cursor.execute("""
            CREATE TABLE IF NOT EXISTS SaleItems (
                Id TEXT PRIMARY KEY,
                SaleId TEXT NOT NULL,
                ProductId TEXT NOT NULL,
                ProductName TEXT NOT NULL,
                UnitPrice REAL NOT NULL DEFAULT 0,
                Quantity REAL NOT NULL DEFAULT 1,
                TotalPrice REAL NOT NULL DEFAULT 0,
                CostPrice REAL NOT NULL DEFAULT 0,
                FOREIGN KEY (SaleId) REFERENCES Sales (Id)
            )
            """)

            # Suppliers Table
            cursor.execute("""
            CREATE TABLE IF NOT EXISTS Suppliers (
                Id TEXT PRIMARY KEY,
                Name TEXT NOT NULL,
                Company TEXT,
                Phone TEXT,
                Address TEXT,
                Balance REAL NOT NULL DEFAULT 0,
                OpeningBalance REAL NOT NULL DEFAULT 0,
                CreatedAt TEXT NOT NULL
            )
            """)

            # SupplierOrders Table
            cursor.execute("""
            CREATE TABLE IF NOT EXISTS SupplierOrders (
                Id TEXT PRIMARY KEY,
                OrderNumber TEXT NOT NULL,
                RepName TEXT,
                RepPhone TEXT,
                StoreName TEXT,
                CustomerName TEXT,
                StoreAddress TEXT,
                Status TEXT NOT NULL DEFAULT 'Pending',
                TotalAmount REAL NOT NULL DEFAULT 0,
                Notes TEXT,
                RepCode TEXT,
                CreatedAt TEXT NOT NULL,
                DeliveredAt TEXT
            )
            """)

            # SupplierOrderItems Table
            cursor.execute("""
            CREATE TABLE IF NOT EXISTS SupplierOrderItems (
                Id TEXT PRIMARY KEY,
                OrderId TEXT NOT NULL,
                ProductId TEXT,
                ProductName TEXT NOT NULL,
                UnitPrice REAL NOT NULL DEFAULT 0,
                Quantity REAL NOT NULL DEFAULT 1,
                TotalPrice REAL NOT NULL DEFAULT 0,
                FOREIGN KEY (OrderId) REFERENCES SupplierOrders (Id)
            )
            """)

            # PurchaseInvoices Table
            cursor.execute("""
            CREATE TABLE IF NOT EXISTS PurchaseInvoices (
                Id TEXT PRIMARY KEY,
                InvoiceNumber TEXT NOT NULL,
                SupplierId TEXT NOT NULL,
                TotalAmount REAL NOT NULL DEFAULT 0,
                PaidAmount REAL NOT NULL DEFAULT 0,
                RemainingAmount REAL NOT NULL DEFAULT 0,
                PaymentMethod TEXT DEFAULT 'Credit',
                ReceiptImagePath TEXT,
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT
            )
            """)

            # SupplierTransactions Table
            cursor.execute("""
            CREATE TABLE IF NOT EXISTS SupplierTransactions (
                Id TEXT PRIMARY KEY,
                SupplierId TEXT NOT NULL,
                TransactionType TEXT NOT NULL,
                Amount REAL NOT NULL DEFAULT 0,
                ReceiptNumber TEXT,
                Notes TEXT,
                CreatedAt TEXT NOT NULL
            )
            """)

            # Users Table
            cursor.execute("""
            CREATE TABLE IF NOT EXISTS Users (
                Id TEXT PRIMARY KEY,
                Username TEXT NOT NULL UNIQUE,
                FullName TEXT NOT NULL,
                PasswordHash TEXT NOT NULL,
                Role TEXT NOT NULL DEFAULT 'Cashier',
                IsActive INTEGER NOT NULL DEFAULT 1,
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT
            )
            """)

            # Settings Table
            cursor.execute("""
            CREATE TABLE IF NOT EXISTS Settings (
                Key TEXT PRIMARY KEY,
                Value TEXT
            )
            """)

            # Insert default admin user if none exists
            cursor.execute("SELECT COUNT(*) FROM Users")
            if cursor.fetchone()[0] == 0:
                cursor.execute("""
                INSERT INTO Users (Id, Username, FullName, PasswordHash, Role, IsActive, CreatedAt)
                VALUES (?, ?, ?, ?, ?, 1, ?)
                """, (str(uuid.uuid4()), "admin", "مدير النظام", "admin123", "Admin", datetime.utcnow().isoformat()))

            conn.commit()

    # -------------------------------------------------------------
    # Products API
    # -------------------------------------------------------------
    def get_products(self, search: str = "", category_id: Optional[str] = None) -> List[Dict[str, Any]]:
        with self.get_connection() as conn:
            cursor = conn.cursor()
            query = "SELECT * FROM Products WHERE 1=1"
            params = []
            if search:
                query += " AND (Name LIKE ? OR Barcode LIKE ?)"
                params.extend([f"%{search}%", f"%{search}%"])
            if category_id:
                query += " AND CategoryId = ?"
                params.append(category_id)
            query += " ORDER BY Name ASC"
            cursor.execute(query, params)
            return [dict(row) for row in cursor.fetchall()]

    def get_product_by_barcode(self, barcode: str) -> Optional[Dict[str, Any]]:
        with self.get_connection() as conn:
            cursor = conn.cursor()
            cursor.execute("SELECT * FROM Products WHERE Barcode = ?", (barcode,))
            row = cursor.fetchone()
            return dict(row) if row else None

    def save_product(self, product: Dict[str, Any]) -> str:
        with self.get_connection() as conn:
            cursor = conn.cursor()
            prod_id = product.get("Id") or str(uuid.uuid4())
            now = datetime.utcnow().isoformat()
            cursor.execute("""
            INSERT INTO Products (Id, Name, Barcode, Price, Cost, StockQuantity, MinStockAlert, CategoryId, SupplierId, CreatedAt, UpdatedAt)
            VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
            ON CONFLICT(Id) DO UPDATE SET
                Name=excluded.Name,
                Barcode=excluded.Barcode,
                Price=excluded.Price,
                Cost=excluded.Cost,
                StockQuantity=excluded.StockQuantity,
                MinStockAlert=excluded.MinStockAlert,
                CategoryId=excluded.CategoryId,
                SupplierId=excluded.SupplierId,
                UpdatedAt=excluded.UpdatedAt
            """, (
                prod_id,
                product.get("Name", ""),
                product.get("Barcode", ""),
                float(product.get("Price", 0)),
                float(product.get("Cost", 0)),
                float(product.get("StockQuantity", 0)),
                float(product.get("MinStockAlert", 5)),
                product.get("CategoryId"),
                product.get("SupplierId"),
                product.get("CreatedAt", now),
                now
            ))
            conn.commit()
            return prod_id

    def delete_product(self, prod_id: str):
        with self.get_connection() as conn:
            cursor = conn.cursor()
            cursor.execute("DELETE FROM Products WHERE Id = ?", (prod_id,))
            conn.commit()

    # -------------------------------------------------------------
    # Categories API
    # -------------------------------------------------------------
    def get_categories(self) -> List[Dict[str, Any]]:
        with self.get_connection() as conn:
            cursor = conn.cursor()
            cursor.execute("SELECT * FROM Categories ORDER BY Name ASC")
            return [dict(row) for row in cursor.fetchall()]

    # -------------------------------------------------------------
    # Sales API
    # -------------------------------------------------------------
    def create_sale(self, sale_data: Dict[str, Any], items: List[Dict[str, Any]]) -> str:
        sale_id = str(uuid.uuid4())
        now = datetime.utcnow().isoformat()
        inv_num = sale_data.get("InvoiceNumber") or f"INV-{datetime.now().strftime('%Y%m%d%H%M%S')}"

        with self.get_connection() as conn:
            cursor = conn.cursor()
            cursor.execute("""
            INSERT INTO Sales (Id, InvoiceNumber, TotalAmount, DiscountAmount, PaidAmount, RemainingAmount, PaymentMethod, Status, CashierId, CashierName, CustomerName, Notes, CreatedAt)
            VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
            """, (
                sale_id,
                inv_num,
                float(sale_data.get("TotalAmount", 0)),
                float(sale_data.get("DiscountAmount", 0)),
                float(sale_data.get("PaidAmount", 0)),
                float(sale_data.get("RemainingAmount", 0)),
                sale_data.get("PaymentMethod", "Cash"),
                "Completed",
                sale_data.get("CashierId", ""),
                sale_data.get("CashierName", "محمد الكاشير"),
                sale_data.get("CustomerName", "زبون نقدي"),
                sale_data.get("Notes", ""),
                now
            ))

            for item in items:
                item_id = str(uuid.uuid4())
                cursor.execute("""
                INSERT INTO SaleItems (Id, SaleId, ProductId, ProductName, UnitPrice, Quantity, TotalPrice, CostPrice)
                VALUES (?, ?, ?, ?, ?, ?, ?, ?)
                """, (
                    item_id,
                    sale_id,
                    item.get("ProductId", ""),
                    item.get("ProductName", ""),
                    float(item.get("UnitPrice", 0)),
                    float(item.get("Quantity", 1)),
                    float(item.get("TotalPrice", 0)),
                    float(item.get("CostPrice", 0))
                ))

                # Deduct stock quantity
                if item.get("ProductId"):
                    cursor.execute("""
                    UPDATE Products SET StockQuantity = StockQuantity - ? WHERE Id = ?
                    """, (float(item.get("Quantity", 1)), item.get("ProductId")))

            conn.commit()
            return sale_id

    def get_sales(self, limit: int = 100, search: str = "") -> List[Dict[str, Any]]:
        with self.get_connection() as conn:
            cursor = conn.cursor()
            query = "SELECT * FROM Sales WHERE 1=1"
            params = []
            if search:
                query += " AND (InvoiceNumber LIKE ? OR CustomerName LIKE ? OR CashierName LIKE ?)"
                params.extend([f"%{search}%", f"%{search}%", f"%{search}%"])
            query += " ORDER BY CreatedAt DESC LIMIT ?"
            params.append(limit)
            cursor.execute(query, params)
            return [dict(row) for row in cursor.fetchall()]

    def get_sale_items(self, sale_id: str) -> List[Dict[str, Any]]:
        with self.get_connection() as conn:
            cursor = conn.cursor()
            cursor.execute("SELECT * FROM SaleItems WHERE SaleId = ?", (sale_id,))
            return [dict(row) for row in cursor.fetchall()]

    # -------------------------------------------------------------
    # Dashboard Stats API
    # -------------------------------------------------------------
    def get_dashboard_stats(self) -> Dict[str, Any]:
        with self.get_connection() as conn:
            cursor = conn.cursor()
            today_str = datetime.utcnow().strftime("%Y-%m-%d")
            month_str = datetime.utcnow().strftime("%Y-%m")

            # Today Revenue & Count
            cursor.execute("""
            SELECT COALESCE(SUM(TotalAmount), 0), COUNT(*) FROM Sales 
            WHERE CreatedAt LIKE ? AND Status = 'Completed'
            """, (f"{today_str}%",))
            today_rev, today_count = cursor.fetchone()

            # Monthly Revenue
            cursor.execute("""
            SELECT COALESCE(SUM(TotalAmount), 0) FROM Sales 
            WHERE CreatedAt LIKE ? AND Status = 'Completed'
            """, (f"{month_str}%",))
            month_rev = cursor.fetchone()[0]

            # Total Products
            cursor.execute("SELECT COUNT(*) FROM Products")
            total_prods = cursor.fetchone()[0]

            # Low Stock Count & List
            cursor.execute("SELECT * FROM Products WHERE StockQuantity <= MinStockAlert LIMIT 10")
            low_stock_list = [dict(row) for row in cursor.fetchall()]
            low_stock_count = len(low_stock_list)

            # Recent Sales
            cursor.execute("SELECT * FROM Sales ORDER BY CreatedAt DESC LIMIT 8")
            recent_sales = [dict(row) for row in cursor.fetchall()]

            # Payment distribution
            cursor.execute("""
            SELECT PaymentMethod, SUM(TotalAmount) as Total FROM Sales 
            WHERE Status = 'Completed' GROUP BY PaymentMethod
            """)
            payments = [{"Method": row[0], "Amount": row[1]} for row in cursor.fetchall()]

            return {
                "TodayRevenue": today_rev,
                "TodayInvoicesCount": today_count,
                "MonthlyRevenue": month_rev,
                "TotalProductsCount": total_prods,
                "LowStockCount": low_stock_count,
                "LowStockProducts": low_stock_list,
                "RecentSales": recent_sales,
                "PaymentDistribution": payments
            }

    # -------------------------------------------------------------
    # Suppliers & Reps API
    # -------------------------------------------------------------
    def get_suppliers(self, search: str = "") -> List[Dict[str, Any]]:
        with self.get_connection() as conn:
            cursor = conn.cursor()
            query = "SELECT * FROM Suppliers WHERE 1=1"
            params = []
            if search:
                query += " AND (Name LIKE ? OR Company LIKE ? OR Phone LIKE ?)"
                params.extend([f"%{search}%", f"%{search}%", f"%{search}%"])
            query += " ORDER BY Name ASC"
            cursor.execute(query, params)
            suppliers = [dict(row) for row in cursor.fetchall()]

            # Fetch extra stats per supplier
            for s in suppliers:
                cursor.execute("SELECT COUNT(*) FROM Products WHERE SupplierId = ?", (s["Id"],))
                s["ProductsCount"] = cursor.fetchone()[0]
                cursor.execute("SELECT COUNT(*) FROM PurchaseInvoices WHERE SupplierId = ?", (s["Id"],))
                s["InvoicesCount"] = cursor.fetchone()[0]

            return suppliers

    def save_supplier(self, sup: Dict[str, Any]) -> str:
        with self.get_connection() as conn:
            cursor = conn.cursor()
            sup_id = sup.get("Id") or str(uuid.uuid4())
            now = datetime.utcnow().isoformat()
            cursor.execute("""
            INSERT INTO Suppliers (Id, Name, Company, Phone, Address, Balance, OpeningBalance, CreatedAt)
            VALUES (?, ?, ?, ?, ?, ?, ?, ?)
            ON CONFLICT(Id) DO UPDATE SET
                Name=excluded.Name,
                Company=excluded.Company,
                Phone=excluded.Phone,
                Address=excluded.Address,
                Balance=excluded.Balance,
                OpeningBalance=excluded.OpeningBalance
            """, (
                sup_id,
                sup.get("Name", ""),
                sup.get("Company", ""),
                sup.get("Phone", ""),
                sup.get("Address", ""),
                float(sup.get("Balance", 0)),
                float(sup.get("OpeningBalance", 0)),
                sup.get("CreatedAt", now)
            ))
            conn.commit()
            return sup_id

    def add_supplier_payment(self, supplier_id: str, amount: float, notes: str = "", receipt_num: str = ""):
        with self.get_connection() as conn:
            cursor = conn.cursor()
            tx_id = str(uuid.uuid4())
            now = datetime.utcnow().isoformat()
            cursor.execute("""
            INSERT INTO SupplierTransactions (Id, SupplierId, TransactionType, Amount, ReceiptNumber, Notes, CreatedAt)
            VALUES (?, ?, 'Payment', ?, ?, ?, ?)
            """, (tx_id, supplier_id, amount, receipt_num, notes, now))

            # Deduct balance
            cursor.execute("""
            UPDATE Suppliers SET Balance = Balance - ? WHERE Id = ?
            """, (amount, supplier_id))

            conn.commit()

    # -------------------------------------------------------------
    # Supplier / Rep Orders API
    # -------------------------------------------------------------
    def get_rep_orders(self, search: str = "", status: Optional[str] = None) -> List[Dict[str, Any]]:
        with self.get_connection() as conn:
            cursor = conn.cursor()
            query = "SELECT * FROM SupplierOrders WHERE 1=1"
            params = []
            if search:
                query += " AND (OrderNumber LIKE ? OR RepName LIKE ? OR StoreName LIKE ? OR CustomerName LIKE ?)"
                params.extend([f"%{search}%", f"%{search}%", f"%{search}%", f"%{search}%"])
            if status:
                query += " AND Status = ?"
                params.append(status)
            query += " ORDER BY CreatedAt DESC"
            cursor.execute(query, params)
            orders = [dict(row) for row in cursor.fetchall()]

            for o in orders:
                cursor.execute("SELECT * FROM SupplierOrderItems WHERE OrderId = ?", (o["Id"],))
                o["Items"] = [dict(i) for i in cursor.fetchall()]

            return orders

    def update_order_status(self, order_id: str, new_status: str):
        with self.get_connection() as conn:
            cursor = conn.cursor()
            delivered_at = datetime.utcnow().isoformat() if new_status == "Delivered" else None
            cursor.execute("""
            UPDATE SupplierOrders SET Status = ?, DeliveredAt = ? WHERE Id = ?
            """, (new_status, delivered_at, order_id))
            conn.commit()

    # -------------------------------------------------------------
    # Users API
    # -------------------------------------------------------------
    def get_users(self) -> List[Dict[str, Any]]:
        with self.get_connection() as conn:
            cursor = conn.cursor()
            cursor.execute("SELECT * FROM Users ORDER BY FullName ASC")
            return [dict(row) for row in cursor.fetchall()]

    def save_user(self, user: Dict[str, Any]) -> str:
        with self.get_connection() as conn:
            cursor = conn.cursor()
            uid = user.get("Id") or str(uuid.uuid4())
            now = datetime.utcnow().isoformat()
            cursor.execute("""
            INSERT INTO Users (Id, Username, FullName, PasswordHash, Role, IsActive, CreatedAt, UpdatedAt)
            VALUES (?, ?, ?, ?, ?, ?, ?, ?)
            ON CONFLICT(Id) DO UPDATE SET
                Username=excluded.Username,
                FullName=excluded.FullName,
                PasswordHash=excluded.PasswordHash,
                Role=excluded.Role,
                IsActive=excluded.IsActive,
                UpdatedAt=excluded.UpdatedAt
            """, (
                uid,
                user.get("Username", ""),
                user.get("FullName", ""),
                user.get("PasswordHash", "1234"),
                user.get("Role", "Cashier"),
                1 if user.get("IsActive", True) else 0,
                user.get("CreatedAt", now),
                now
            ))
            conn.commit()
            return uid

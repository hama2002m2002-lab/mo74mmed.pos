import requests
import base64
import json
import uuid
from datetime import datetime
from typing import List, Dict, Any
from config import GITHUB_REPO, GITHUB_TOKEN
from database import Database

class CloudSyncService:
    def __init__(self, db: Database):
        self.db = db
        self.headers = {
            "Authorization": f"token {GITHUB_TOKEN}",
            "Accept": "application/vnd.github.v3+json",
            "User-Agent": "7amo-POS-Python"
        }

    def sync_orders(self) -> int:
        """Fetch pending orders from GitHub repo docs/orders.json and save them locally."""
        url = f"https://api.github.com/repos/{GITHUB_REPO}/contents/docs/orders.json"
        try:
            res = requests.get(url, headers=self.headers, timeout=10)
            if res.status_code == 200:
                data = res.json()
                content_str = base64.b64decode(data["content"]).decode("utf-8")
                orders_list = json.loads(content_str)

                new_count = 0
                with self.db.get_connection() as conn:
                    cursor = conn.cursor()
                    for ord_data in orders_list:
                        ord_num = ord_data.get("OrderNumber") or ord_data.get("id")
                        if not ord_num:
                            continue

                        # Check if order exists
                        cursor.execute("SELECT Id FROM SupplierOrders WHERE OrderNumber = ?", (ord_num,))
                        existing = cursor.fetchone()
                        if not existing:
                            order_id = str(uuid.uuid4())
                            cursor.execute("""
                            INSERT INTO SupplierOrders (Id, OrderNumber, RepName, RepPhone, StoreName, CustomerName, StoreAddress, Status, TotalAmount, Notes, RepCode, CreatedAt)
                            VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
                            """, (
                                order_id,
                                ord_num,
                                ord_data.get("RepName", "المندوب"),
                                ord_data.get("RepPhone", ""),
                                ord_data.get("StoreName", "المحل"),
                                ord_data.get("CustomerName", ord_data.get("StoreName", "")),
                                ord_data.get("StoreAddress", ""),
                                ord_data.get("Status", "Pending"),
                                float(ord_data.get("TotalAmount", 0)),
                                ord_data.get("Notes", ""),
                                ord_data.get("RepCode", ""),
                                ord_data.get("CreatedAt", datetime.utcnow().isoformat())
                            ))

                            for item in ord_data.get("Items", []):
                                item_id = str(uuid.uuid4())
                                cursor.execute("""
                                INSERT INTO SupplierOrderItems (Id, OrderId, ProductId, ProductName, UnitPrice, Quantity, TotalPrice)
                                VALUES (?, ?, ?, ?, ?, ?, ?)
                                """, (
                                    item_id,
                                    order_id,
                                    item.get("ProductId", ""),
                                    item.get("ProductName", ""),
                                    float(item.get("UnitPrice", 0)),
                                    float(item.get("Quantity", 1)),
                                    float(item.get("TotalPrice", 0))
                                ))
                            new_count += 1
                    conn.commit()
                return new_count
        except Exception as e:
            print(f"CloudSync error: {e}")
        return 0

    def sync_products_to_cloud(self):
        """Upload current inventory products to docs/products.json for rep mobile portal."""
        products = self.db.get_products()
        payload = []
        for p in products:
            payload.append({
                "Id": p["Id"],
                "Name": p["Name"],
                "Barcode": p["Barcode"],
                "Price": p["Price"],
                "Cost": p["Cost"],
                "StockQuantity": p["StockQuantity"]
            })

        content_json = json.dumps(payload, ensure_ascii=False, indent=2)
        encoded_content = base64.b64encode(content_json.encode("utf-8")).decode("utf-8")

        url = f"https://api.github.com/repos/{GITHUB_REPO}/contents/docs/products.json"
        try:
            # Get sha
            get_res = requests.get(url, headers=self.headers, timeout=10)
            sha = get_res.json().get("sha") if get_res.status_code == 200 else None

            body = {
                "message": "sync: update products catalog for rep portal",
                "content": encoded_content
            }
            if sha:
                body["sha"] = sha

            requests.put(url, headers=self.headers, json=body, timeout=10)
        except Exception as e:
            print(f"Upload products to cloud error: {e}")

# 🧪 راهنمای کامل تست API

این فایل راهنمای کامل برای تست API و سایر بخش‌های برنامه است.

---

## 🚀 راه‌اندازی API

### 1. اجرای API

```bash
cd API
dotnet run
```

**خروجی:**
```
============================================================
  🧪 Testing Database Connection and Setup...
============================================================
✅ Database connection successful!
✅ Database 'GsSample' exists.
✅ Table 'Persons' exists.
✅ Current record count in Persons table: 10
📝 Inserting sample data...
✅ Sample data inserted! Total records: 10

============================================================
  🚀 Starting Person Feature Test...
============================================================
🎉 All tests passed successfully!

============================================================
  ✅ All tests passed! Starting API...
============================================================

Now listening on: http://localhost:5203
```

---

## 🌐 روش‌های تست API

### روش 1: Swagger UI (توصیه می‌شود) ⭐

بعد از راه‌اندازی API:

1. مرورگر را باز کنید
2. به آدرس زیر بروید:
   ```
   http://localhost:5203
   ```
3. Swagger UI نمایش داده می‌شود
4. می‌توانید تمام endpoints را مستقیماً تست کنید

**مزایا:**
- ✅ رابط کاربری ساده
- ✅ مستندات خودکار
- ✅ تست مستقیم از مرورگر
- ✅ نمایش Request/Response

---

### روش 2: Visual Studio Code REST Client

1. Extension **REST Client** را نصب کنید
2. فایل `API/API.http` را باز کنید
3. روی هر request کلیک کنید و **Send Request** را بزنید

**مثال:**
```http
### دریافت همه Persons
GET http://localhost:5203/api/Person
Accept: application/json
```

---

### روش 3: Postman

1. Postman را باز کنید
2. یک Collection جدید بسازید
3. Requestهای زیر را اضافه کنید:

#### GET - دریافت همه Persons
```
GET http://localhost:5203/api/Person
Headers:
  Accept: application/json
```

#### GET - دریافت Person با ID
```
GET http://localhost:5203/api/Person/1
Headers:
  Accept: application/json
```

#### POST - ایجاد Person جدید
```
POST http://localhost:5203/api/Person
Headers:
  Content-Type: application/json

Body (JSON):
{
  "firstName": "تست",
  "lastName": "کاربر",
  "dateOfBirth": "2000-01-01T00:00:00Z"
}
```

#### PUT - به‌روزرسانی Person
```
PUT http://localhost:5203/api/Person/1
Headers:
  Content-Type: application/json

Body (JSON):
{
  "firstName": "علی",
  "lastName": "محمدی به‌روزرسانی شده",
  "dateOfBirth": "1990-01-15T00:00:00Z"
}
```

#### DELETE - حذف Person
```
DELETE http://localhost:5203/api/Person/1
Headers:
  Accept: application/json
```

---

### روش 4: curl (Command Line)

#### دریافت همه Persons:
```bash
curl -X GET "http://localhost:5203/api/Person" -H "Accept: application/json"
```

#### دریافت Person با ID:
```bash
curl -X GET "http://localhost:5203/api/Person/1" -H "Accept: application/json"
```

#### ایجاد Person جدید:
```bash
curl -X POST "http://localhost:5203/api/Person" \
  -H "Content-Type: application/json" \
  -d "{\"firstName\":\"تست\",\"lastName\":\"کاربر\",\"dateOfBirth\":\"2000-01-01T00:00:00Z\"}"
```

#### به‌روزرسانی Person:
```bash
curl -X PUT "http://localhost:5203/api/Person/1" \
  -H "Content-Type: application/json" \
  -d "{\"firstName\":\"علی\",\"lastName\":\"محمدی\",\"dateOfBirth\":\"1990-01-15T00:00:00Z\"}"
```

#### حذف Person:
```bash
curl -X DELETE "http://localhost:5203/api/Person/1" -H "Accept: application/json"
```

---

### روش 5: PowerShell

#### دریافت همه Persons:
```powershell
Invoke-RestMethod -Uri "http://localhost:5203/api/Person" -Method Get -ContentType "application/json"
```

#### ایجاد Person جدید:
```powershell
$body = @{
    firstName = "تست"
    lastName = "کاربر"
    dateOfBirth = "2000-01-01T00:00:00Z"
} | ConvertTo-Json

Invoke-RestMethod -Uri "http://localhost:5203/api/Person" -Method Post -Body $body -ContentType "application/json"
```

---

## 📋 لیست کامل Endpoints

### 1. GET /api/Person
**توضیح:** دریافت همه Persons

**Request:**
```http
GET http://localhost:5203/api/Person
```

**Response (200 OK):**
```json
{
  "persons": [
    {
      "id": 1,
      "firstName": "علی",
      "lastName": "محمدی",
      "dateOfBirth": "1990-01-15T00:00:00Z"
    },
    ...
  ]
}
```

---

### 2. GET /api/Person/{id}
**توضیح:** دریافت Person با ID مشخص

**Request:**
```http
GET http://localhost:5203/api/Person/1
```

**Response (200 OK):**
```json
{
  "person": {
    "id": 1,
    "firstName": "علی",
    "lastName": "محمدی",
    "dateOfBirth": "1990-01-15T00:00:00Z"
  }
}
```

**Response (404 Not Found):**
```json
"Person with Id 999 not found"
```

---

### 3. POST /api/Person
**توضیح:** ایجاد Person جدید

**Request:**
```http
POST http://localhost:5203/api/Person
Content-Type: application/json

{
  "firstName": "تست",
  "lastName": "کاربر",
  "dateOfBirth": "2000-01-01T00:00:00Z"
}
```

**Response (201 Created):**
```json
{
  "id": 11
}
```

**Response (400 Bad Request):**
```json
{
  "errors": {
    "firstName": ["The firstName field is required."]
  }
}
```

---

### 4. PUT /api/Person/{id}
**توضیح:** به‌روزرسانی Person موجود

**Request:**
```http
PUT http://localhost:5203/api/Person/1
Content-Type: application/json

{
  "firstName": "علی",
  "lastName": "محمدی به‌روزرسانی شده",
  "dateOfBirth": "1990-01-15T00:00:00Z"
}
```

**Response (200 OK):**
```json
{}
```

**Response (404 Not Found):**
```json
"Person with Id 999 not found"
```

---

### 5. DELETE /api/Person/{id}
**توضیح:** حذف Person

**Request:**
```http
DELETE http://localhost:5203/api/Person/1
```

**Response (200 OK):**
```json
{
  "success": true
}
```

**Response (404 Not Found):**
```json
"Person with Id 999 not found"
```

---

## 🧪 سناریوهای تست

### سناریو 1: CRUD کامل

```bash
# 1. دریافت همه
GET http://localhost:5203/api/Person

# 2. ایجاد جدید
POST http://localhost:5203/api/Person
{
  "firstName": "تست",
  "lastName": "کاربر",
  "dateOfBirth": "2000-01-01T00:00:00Z"
}
# Response: {"id": 11}

# 3. دریافت با ID
GET http://localhost:5203/api/Person/11

# 4. به‌روزرسانی
PUT http://localhost:5203/api/Person/11
{
  "firstName": "تست به‌روزرسانی شده",
  "lastName": "کاربر",
  "dateOfBirth": "2000-01-01T00:00:00Z"
}

# 5. حذف
DELETE http://localhost:5203/api/Person/11
```

---

### سناریو 2: تست Error Handling

```bash
# 1. دریافت Person که وجود ندارد
GET http://localhost:5203/api/Person/99999
# Expected: 404 Not Found

# 2. به‌روزرسانی Person که وجود ندارد
PUT http://localhost:5203/api/Person/99999
{
  "firstName": "تست",
  "lastName": "کاربر",
  "dateOfBirth": "2000-01-01T00:00:00Z"
}
# Expected: 404 Not Found

# 3. حذف Person که وجود ندارد
DELETE http://localhost:5203/api/Person/99999
# Expected: 404 Not Found
```

---

### سناریو 3: تست با داده‌های فارسی

```bash
POST http://localhost:5203/api/Person
{
  "firstName": "احمد",
  "lastName": "رضایی",
  "dateOfBirth": "1985-05-20T00:00:00Z"
}
```

---

## 🔍 تست Read/Write Separation

### تست Read (از Read Replica):

```bash
# این query باید از Read Connection استفاده کند
GET http://localhost:5203/api/Person
```

### تست Write (به Write DB):

```bash
# این command باید به Write DB برود
POST http://localhost:5203/api/Person
{
  "firstName": "تست",
  "lastName": "Write",
  "dateOfBirth": "2000-01-01T00:00:00Z"
}
```

**نکته:** برای مشاهده routing، می‌توانید در SQL Server Profiler یا Extended Events بررسی کنید.

---

## 🧪 تست Listener (RabbitMQ)

### 1. راه‌اندازی Listener

```bash
cd Listener
dotnet run
```

### 2. ارسال پیام به RabbitMQ

از طریق API یا مستقیماً در RabbitMQ Management:

**Queue:** `Person`

**Message:**
```json
{
  "firstName": "RabbitMQ",
  "lastName": "Test",
  "dateOfBirth": "1995-05-15T00:00:00Z"
}
```

### 3. بررسی در دیتابیس

```sql
USE GsSample;
SELECT * FROM Persons ORDER BY Id DESC;
```

باید Person جدید را ببینید که از طریق RabbitMQ ایجاد شده است.

---

## 📊 تست Performance

### تست با چند Request همزمان:

```bash
# اجرای همزمان 10 request
for i in {1..10}; do
  curl -X GET "http://localhost:5203/api/Person" &
done
wait
```

---

## ✅ چک‌لیست تست

- [ ] ✅ API راه‌اندازی می‌شود
- [ ] ✅ GET /api/Person کار می‌کند
- [ ] ✅ GET /api/Person/{id} کار می‌کند
- [ ] ✅ POST /api/Person کار می‌کند
- [ ] ✅ PUT /api/Person/{id} کار می‌کند
- [ ] ✅ DELETE /api/Person/{id} کار می‌کند
- [ ] ✅ Error handling کار می‌کند (404)
- [ ] ✅ داده‌های فارسی درست کار می‌کنند
- [ ] ✅ Swagger UI در دسترس است
- [ ] ✅ Listener کار می‌کند (اگر RabbitMQ در دسترس باشد)

---

## 🎯 نکات مهم

1. **Port:** API روی `http://localhost:5203` اجرا می‌شود
2. **Swagger:** در `http://localhost:5203` در دسترس است
3. **Database:** باید دیتابیس و جدول ایجاد شده باشند
4. **Connection String:** باید در `appsettings.json` صحیح باشد

---

## 🐛 عیب‌یابی

### مشکل: "Cannot connect to API"

**راه‌حل:**
1. مطمئن شوید API در حال اجرا است
2. Port را بررسی کنید (5203)
3. Firewall را بررسی کنید

### مشکل: "404 Not Found"

**راه‌حل:**
1. مطمئن شوید route درست است: `/api/Person`
2. مطمئن شوید API در حال اجرا است

### مشکل: "500 Internal Server Error"

**راه‌حل:**
1. Logs را بررسی کنید
2. Connection String را بررسی کنید
3. دیتابیس را بررسی کنید

---

**همه چیز آماده است! 🚀**


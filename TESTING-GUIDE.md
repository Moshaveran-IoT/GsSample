# 🧪 راهنمای تست و اجرای پروژه

این فایل توضیح می‌دهد که چگونه پروژه را تست کنید و روی دیتابیس SQL Server محلی اجرا کنید.

---

## 📋 پیش‌نیازها

1. **SQL Server** - باید SQL Server روی سیستم شما نصب و در حال اجرا باشد
2. **.NET 9.0 SDK** - باید نصب باشد
3. **Connection String** - باید در `appsettings.json` تنظیم شود

---

## 🔧 تنظیمات اولیه

### 1. تنظیم Connection String

**فایل:** `API/appsettings.json`

Connection string خود را تنظیم کنید:

```json
{
  "ConnectionStrings": {
    "ApplicationConnection": "Server=localhost;Database=GsSample;User Id=sa;Password=YourPassword;TrustServerCertificate=True;",
    "ApplicationReadConnection": "Server=localhost;Database=GsSample;User Id=sa;Password=YourPassword;TrustServerCertificate=True;"
  }
}
```

**نکات مهم:**
- `Server`: آدرس SQL Server (معمولاً `localhost` یا `localhost\\SQLEXPRESS`)
- `Database`: نام دیتابیس (می‌تواند `GsSample` یا هر نام دیگری باشد)
- `User Id`: نام کاربری SQL Server (معمولاً `sa`)
- `Password`: رمز عبور SQL Server
- `TrustServerCertificate=True`: برای SQL Server 2019+ ضروری است

### 2. ایجاد دیتابیس (اختیاری)

اگر می‌خواهید دیتابیس را دستی ایجاد کنید:

```sql
CREATE DATABASE GsSample;
```

**یا** اجازه دهید برنامه به صورت خودکار دیتابیس را ایجاد کند.

---

## 🚀 اجرای تست

### روش 1: اجرای مستقیم API (توصیه می‌شود)

وقتی API را اجرا می‌کنید، به صورت خودکار:

1. ✅ اتصال به دیتابیس را تست می‌کند
2. ✅ دیتابیس را ایجاد می‌کند (در صورت عدم وجود)
3. ✅ جدول `Persons` را ایجاد می‌کند (در صورت عدم وجود)
4. ✅ تمام عملیات CRUD را تست می‌کند
5. ✅ API را راه‌اندازی می‌کند

**دستور:**
```bash
cd API
dotnet run
```

**خروجی نمونه:**
```
============================================================
  🧪 Testing Database Connection and Setup...
============================================================

✅ Database connection successful!
✅ Database 'GsSample' exists.
✅ Table 'Persons' exists.
✅ Current record count in Persons table: 0

============================================================
  🚀 Starting Person Feature Test...
============================================================

📋 Step 1: Testing Database Connection...
✅ Database setup complete!

📋 Step 2: Setting up DI Container...
✅ DI Container setup complete!

📋 Step 3: Testing PersonRepository...
  → Creating a new Person...
  ✅ Person created with ID: 1
  → Getting Person by ID: 1...
  ✅ Person retrieved: Test User
  → Updating Person...
  ✅ Person updated successfully!
  → Getting all Persons...
  ✅ Retrieved 1 person(s)

📋 Step 4: Testing MediatR Handlers...
  → Testing GetAllPersonQuery...
  ✅ GetAllPersonQuery returned 1 person(s)
  → Testing GetPersonByIdQuery...
  ✅ GetPersonByIdQuery returned: Updated Test User
  → Testing CreatePersonCommand...
  ✅ CreatePersonCommand created Person with ID: 2
  → Testing UpdatePersonCommand...
  ✅ UpdatePersonCommand updated Person successfully!
  → Testing DeletePersonCommand...
  ✅ DeletePersonCommand deleted Person: True

📋 Step 5: Cleaning up test data...
  ✅ Test data cleaned up!

🎉 All tests passed successfully!

============================================================
  ✅ All tests passed! Starting API...
============================================================
```

### روش 2: اجرای دستی SQL Script

اگر می‌خواهید جدول را دستی ایجاد کنید:

1. SQL Server Management Studio (SSMS) را باز کنید
2. به دیتابیس `GsSample` متصل شوید
3. فایل `Infrastructure/Database/CreatePersonsTable.sql` را اجرا کنید

---

## 🧪 تست API Endpoints

بعد از اجرای موفقیت‌آمیز، می‌توانید API را تست کنید:

### 1. دریافت همه Persons

```http
GET http://localhost:5000/api/Person
```

### 2. دریافت Person با ID

```http
GET http://localhost:5000/api/Person/1
```

### 3. ایجاد Person جدید

```http
POST http://localhost:5000/api/Person
Content-Type: application/json

{
  "firstName": "Ali",
  "lastName": "Naderi",
  "dateOfBirth": "1990-01-01T00:00:00Z"
}
```

### 4. به‌روزرسانی Person

```http
PUT http://localhost:5000/api/Person/1
Content-Type: application/json

{
  "firstName": "Ali",
  "lastName": "Mohammad Naderi",
  "dateOfBirth": "1990-01-01T00:00:00Z"
}
```

### 5. حذف Person

```http
DELETE http://localhost:5000/api/Person/1
```

---

## 🔍 بررسی نتایج در دیتابیس

برای بررسی داده‌های ایجاد شده در دیتابیس:

```sql
-- مشاهده همه Persons
SELECT * FROM Persons;

-- شمارش تعداد Persons
SELECT COUNT(*) FROM Persons;

-- مشاهده ساختار جدول
SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
WHERE TABLE_NAME = 'Persons';
```

---

## ❌ عیب‌یابی

### مشکل: "Database test failed"

**راه‌حل:**
1. مطمئن شوید SQL Server در حال اجرا است
2. Connection string را بررسی کنید
3. مطمئن شوید که SQL Server Authentication فعال است
4. رمز عبور را بررسی کنید

### مشکل: "Cannot open database"

**راه‌حل:**
1. دیتابیس را دستی ایجاد کنید:
   ```sql
   CREATE DATABASE GsSample;
   ```
2. یا Connection string را بررسی کنید

### مشکل: "Table already exists"

**راه‌حل:**
این یک warning است و مشکلی ایجاد نمی‌کند. جدول قبلاً ایجاد شده است.

---

## 📊 خروجی تست

تست شامل موارد زیر است:

1. ✅ **Database Connection Test** - تست اتصال به دیتابیس
2. ✅ **Database Creation** - ایجاد دیتابیس در صورت نیاز
3. ✅ **Table Creation** - ایجاد جدول Persons در صورت نیاز
4. ✅ **Repository Tests** - تست تمام متدهای Repository
5. ✅ **MediatR Handler Tests** - تست تمام Handlerها
6. ✅ **Transaction Tests** - تست Transaction support
7. ✅ **Read/Write Separation** - تست Read/Write routing

---

## 🎯 نتیجه

اگر همه تست‌ها موفق باشند، شما خواهید دید:

```
🎉 All tests passed successfully!
✅ All tests passed! Starting API...
```

این یعنی:
- ✅ دیتابیس متصل است
- ✅ جدول ایجاد شده است
- ✅ Repository کار می‌کند
- ✅ Handlers کار می‌کنند
- ✅ API آماده استفاده است

---

**نکته:** بعد از اجرای تست، API به صورت خودکار راه‌اندازی می‌شود و می‌توانید از endpoints استفاده کنید.


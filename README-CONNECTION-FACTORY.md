# Refactoring ConnectionFactory در GsSample

این فایل توضیح می‌دهد که چه تغییراتی در پروژه GsSample انجام شده است.

---

## 📋 تغییرات انجام شده

### ✅ 1. ConnectionFactory Stateless

**فایل:** `Infrastructure/Factories/ConnectionFactory.cs`

- ✅ کاملاً stateless (بدون cache)
- ✅ کاملاً async/await (بدون `.Result`)
- ✅ بدون lock (بدون deadlock)
- ✅ آماده برای Read Replicas

### ✅ 2. TransactionContext

**فایل:** `Infrastructure/Factories/TransactionContext.cs`

- ✅ مدیریت transaction مشترک با `AsyncLocal`
- ✅ Thread-safe و async-compatible
- ✅ بدون deadlock

### ✅ 3. RabbitMQListenerRepository (Refactored)

**فایل:** `Listener/RabbitMQListenerRepository.cs`

- ✅ استفاده از ConnectionFactory و TransactionContext
- ✅ از Write Connection استفاده می‌کند (چون Command است)
- ✅ اگر transaction فعال باشد، از connection مشترک استفاده می‌کند

### ✅ 4. DI Registration

**فایل:** `Infrastructure/Extensions/ServiceCollectionExtensions.cs`

- ✅ ConnectionFactory: Scoped
- ✅ TransactionContext: Scoped
- ✅ Repositoryها: باید توسط شما پیاده‌سازی و ثبت شوند

### ✅ 5. Listener Program

**فایل:** `Listener/Program.cs`

- ✅ ثبت ConnectionFactory و TransactionContext
- ✅ RabbitMQ ConnectionFactory (برای RabbitMQ) جدا از SQL ConnectionFactory

---

 

# Person Feature - پیاده‌سازی کامل

این فایل توضیح می‌دهد که چگونه Person Feature به صورت کامل پیاده‌سازی شده است.

---

## 📋 ساختار فایل‌ها

### 1. Domain Layer

#### `Domain/Models/Person.cs`
```csharp
public sealed class Person
{
    public int Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public DateTime DateOfBirth { get; set; }
}
```

#### `Domain/Features/Person.cs`
شامل Command/Query classes:
- `GetAllPersonQuery` / `GetAllPersonQueryResponse`
- `GetPersonByIdQuery` / `GetPersonByIdQueryResponse`
- `CreatePersonCommand` / `CreatePersonCommandResponse`
- `UpdatePersonCommand` / `UpdatePersonCommandResponse`
- `DeletePersonCommand` / `DeletePersonCommandResponse`

---

### 2. Infrastructure Layer

#### `Infrastructure/IPersonRepository.cs`
Interface برای Person Repository:
```csharp
public interface IPersonRepository
{
    Task<Person> GetById(int id, CancellationToken cancellationToken);
    Task<IEnumerable<Person>> GetAll(CancellationToken cancellationToken);
    Task CreatePerson(Person person, CancellationToken cancellationToken);
    Task UpdatePerson(int id, Person person, CancellationToken cancellationToken);
    Task DeletePerson(int id, CancellationToken cancellationToken);
}
```

#### `Infrastructure/Repositories/PersonRepository.cs`
پیاده‌سازی کامل PersonRepository با:
- ✅ استفاده از `ConnectionFactory` و `TransactionContext`
- ✅ Read/Write Separation
- ✅ استفاده از Helper Methods (`ConnectionExtensions`)
- ✅ مدیریت صحیح connection disposal

---

### 3. Application Layer

#### Handlers:
- `Application/GetAllPersonQueryHandler.cs` - دریافت همه Persons
- `Application/Handlers/GetPersonByIdQueryHandler.cs` - دریافت Person با ID
- `Application/Handlers/CreatePersonCommandHandler.cs` - ایجاد Person جدید
- `Application/Handlers/UpdatePersonCommandHandler.cs` - به‌روزرسانی Person
- `Application/Handlers/DeletePersonCommandHandler.cs` - حذف Person

**نکته:** همه Command Handlers از `TransactionContext` استفاده می‌کنند برای اطمینان از atomicity.

---

### 4. API Layer

#### `API/Controllers/PersonController.cs`
RESTful API Controller با endpoints:
- `GET /api/Person` - دریافت همه Persons
- `GET /api/Person/{id}` - دریافت Person با ID
- `POST /api/Person` - ایجاد Person جدید
- `PUT /api/Person/{id}` - به‌روزرسانی Person
- `DELETE /api/Person/{id}` - حذف Person

---

## 🔧 تنظیمات

### 1. Database

**فایل:** `Infrastructure/Database/CreatePersonsTable.sql`

این script را در دیتابیس اجرا کنید:
```sql
CREATE TABLE [dbo].[Persons] (
    [Id] INT IDENTITY(1,1) PRIMARY KEY,
    [FirstName] NVARCHAR(100) NOT NULL,
    [LastName] NVARCHAR(100) NOT NULL,
    [DateOfBirth] DATETIME2 NOT NULL,
    [CreatedAt] DATETIME2 DEFAULT GETUTCDATE(),
    [UpdatedAt] DATETIME2 DEFAULT GETUTCDATE()
);
```

### 2. Connection Strings

**فایل:** `API/appsettings.json` و `Listener/appsettings.json`

```json
{
  "ConnectionStrings": {
    "ApplicationConnection": "Server=localhost;Database=GsSample;User Id=sa;Password=YourPassword123;TrustServerCertificate=True;",
    "ApplicationReadConnection": "Server=localhost;Database=GsSample;User Id=sa;Password=YourPassword123;TrustServerCertificate=True;"
  }
}
```

**نکته:** اگر Read Replica ندارید، هر دو را به یک دیتابیس اشاره دهید.

### 3. DI Registration

**فایل:** `Infrastructure/Extensions/ServiceCollectionExtensions.cs`

```csharp
services.AddRepositories(); // PersonRepository را ثبت می‌کند
```

**فایل:** `API/Startup.cs`

```csharp
services.AddConnectionFactory(this._configuration);
services.AddRepositories();
services.AddMediatR(...);
```

---

## 🚀 نحوه استفاده

### 1. از طریق API

#### دریافت همه Persons:
```http
GET /api/Person
```

#### دریافت Person با ID:
```http
GET /api/Person/1
```

#### ایجاد Person جدید:
```http
POST /api/Person
Content-Type: application/json

{
  "firstName": "Ali",
  "lastName": "Naderi",
  "dateOfBirth": "1990-01-01T00:00:00Z"
}
```

#### به‌روزرسانی Person:
```http
PUT /api/Person/1
Content-Type: application/json

{
  "firstName": "Ali",
  "lastName": "Mohammad Naderi",
  "dateOfBirth": "1990-01-01T00:00:00Z"
}
```

#### حذف Person:
```http
DELETE /api/Person/1
```

### 2. از طریق MediatR (در Handlerها)

```csharp
public class MyHandler : IRequestHandler<MyCommand>
{
    private readonly IMediator _mediator;
    
    public async Task Handle(MyCommand request, CancellationToken ct)
    {
        // ایجاد Person
        var createCommand = new CreatePersonCommand(new Person 
        { 
            FirstName = "Ali",
            LastName = "Naderi",
            DateOfBirth = DateTime.Now
        });
        var createResponse = await _mediator.Send(createCommand, ct);
        
        // دریافت Person
        var getQuery = new GetPersonByIdQuery(createResponse.Id);
        var getResponse = await _mediator.Send(getQuery, ct);
    }
}
```

### 3. از طریق Repository (مستقیم)

```csharp
public class MyService
{
    private readonly IPersonRepository _personRepository;
    private readonly ITransactionContext _transactionContext;
    
    public async Task CreatePersonWithTransaction(CancellationToken ct)
    {
        // ✅ استفاده از Transaction برای چند عملیات
        await using var transaction = await _transactionContext.BeginTransactionAsync(ct);
        try
        {
            var person = new Person 
            { 
                FirstName = "Ali",
                LastName = "Naderi",
                DateOfBirth = DateTime.Now
            };
            
            await _personRepository.CreatePerson(person, ct);
            await _personRepository.UpdatePerson(person.Id, person, ct);
            
            await transaction.CommitAsync(ct);
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }
}
```

---

## 📊 Read/Write Separation

### داخل Transaction:
```csharp
await using var transaction = await _transactionContext.BeginTransactionAsync(ct);
try
{
    // ✅ Read: از Write DB استفاده می‌کند (برای consistency)
    var person = await _personRepository.GetById(1, ct);
    
    // ✅ Write: از Write DB استفاده می‌کند
    await _personRepository.UpdatePerson(1, person, ct);
    
    await transaction.CommitAsync(ct);
}
```

### خارج Transaction:
```csharp
// ✅ Read: از Read Replica استفاده می‌کند
var persons = await _personRepository.GetAll(ct);

// ✅ Write: از Write DB استفاده می‌کند
await _personRepository.CreatePerson(person, ct);
```

---

## ✅ ویژگی‌ها

1. ✅ **CQRS Pattern**: استفاده از MediatR برای Commands و Queries
2. ✅ **Read/Write Separation**: Routing خودکار بین Read Replica و Write DB
3. ✅ **Transaction Support**: پشتیبانی از transaction مشترک بین چند Repository
4. ✅ **Async/Await**: کاملاً async
5. ✅ **Error Handling**: مدیریت خطا در Handlerها
6. ✅ **RESTful API**: API endpoints استاندارد
7. ✅ **Dependency Injection**: همه چیز در DI ثبت شده

---

## 🔗 فایل‌های مرتبط

- `Domain/Models/Person.cs` - Entity
- `Domain/Features/Person.cs` - Commands/Queries
- `Infrastructure/IPersonRepository.cs` - Interface
- `Infrastructure/Repositories/PersonRepository.cs` - Implementation
- `Application/Handlers/*.cs` - Handlers
- `API/Controllers/PersonController.cs` - API Controller
- `Infrastructure/Database/CreatePersonsTable.sql` - Database Script

---

## 📝 نکات مهم

1. **Transaction Management**: Command Handlers به صورت خودکار از Transaction استفاده می‌کنند.
2. **Connection Disposal**: Helper Methods به صورت خودکار connection را dispose می‌کنند (مگر اینکه transaction فعال باشد).
3. **Read/Write Routing**: Routing به صورت خودکار انجام می‌شود.
4. **Error Handling**: اگر Person پیدا نشود، `KeyNotFoundException` throw می‌شود.

---

## 🎯 مثال کامل End-to-End

```csharp
// 1. ایجاد Person
var createCommand = new CreatePersonCommand(new Person 
{ 
    FirstName = "Ali",
    LastName = "Naderi",
    DateOfBirth = new DateTime(1990, 1, 1)
});
var createResponse = await _mediator.Send(createCommand, ct);
// createResponse.Id = 1

// 2. دریافت Person
var getQuery = new GetPersonByIdQuery(createResponse.Id);
var getResponse = await _mediator.Send(getQuery, ct);
// getResponse.Person = Person object

// 3. به‌روزرسانی Person
var updateCommand = new UpdatePersonCommand(
    createResponse.Id,
    new Person 
    { 
        FirstName = "Ali",
        LastName = "Mohammad Naderi",
        DateOfBirth = new DateTime(1990, 1, 1)
    });
await _mediator.Send(updateCommand, ct);

// 4. حذف Person
var deleteCommand = new DeletePersonCommand(createResponse.Id);
var deleteResponse = await _mediator.Send(deleteCommand, ct);
// deleteResponse.Success = true
```

---

همه چیز آماده است! 🚀


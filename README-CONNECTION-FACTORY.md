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

## 🔧 کدهای Refactored

### RabbitMQListenerRepository

**قبل:**
```csharp
internal class RabbitMQListenerRepository : IRabbitMQListenerRepository
{
    public Task CreatePerson(Person person, CancellationToken cancellationToken) 
        => Task.CompletedTask;
}
```

**بعد:**
```csharp
internal class RabbitMQListenerRepository : IRabbitMQListenerRepository
{
    private readonly IConnectionFactory _connectionFactory;
    private readonly ITransactionContext _transactionContext;

    public async Task CreatePerson(Person person, CancellationToken cancellationToken)
    {
        // ✅ اگر transaction فعال باشد، از connection مشترک استفاده می‌کند
        var currentConnection = _transactionContext.GetCurrentConnection();
        
        if (currentConnection != null)
        {
            // استفاده از connection مشترک
            const string sql = "INSERT INTO Persons ...";
            person.Id = await currentConnection.QuerySingleAsync<int>(sql, person, ct);
            return;
        }
        
        // ✅ استفاده از Write Connection
        await using var db = await _connectionFactory.CreateWriteConnection(cancellationToken);
        const string sql = "INSERT INTO Persons ...";
        person.Id = await db.QuerySingleAsync<int>(sql, person, ct);
    }
}
```

---

## 📝 کارهای باقی‌مانده (باید توسط شما انجام شود)

### 1. پیاده‌سازی IPersonRepository

**فایل:** `Infrastructure/IPersonRepository.cs` (interface موجود است)

شما باید implementation این interface را بنویسید:

```csharp
public class PersonRepository : IPersonRepository
{
    private readonly IConnectionFactory _connectionFactory;
    private readonly ITransactionContext _transactionContext;

    public PersonRepository(
        IConnectionFactory connectionFactory,
        ITransactionContext transactionContext)
    {
        _connectionFactory = connectionFactory;
        _transactionContext = transactionContext;
    }

    public async Task<Person> GetById(int id, CancellationToken cancellationToken)
    {
        // ✅ اگر transaction فعال باشد، از Write DB استفاده می‌کند
        // ✅ در غیر این صورت، از Read Replica استفاده می‌کند
        var currentConnection = _transactionContext.GetCurrentConnection() 
            ?? await _connectionFactory.CreateReadConnection(cancellationToken);
        
        // استفاده از connection
        // ...
    }

    // سایر متدها...
}
```

### 2. ثبت Repository در DI

**فایل:** `Infrastructure/Extensions/ServiceCollectionExtensions.cs`

بعد از پیاده‌سازی Repository، آن را در DI ثبت کنید:

```csharp
public static IServiceCollection AddRepositories(this IServiceCollection services)
{
    services.AddScoped<IPersonRepository, PersonRepository>();
    return services;
}
```

### 3. Handlerها (اگر نیاز دارید)

اگر می‌خواهید Handlerهایی با Transaction بنویسید:

```csharp
public class CreatePersonCommandHandler : IRequestHandler<CreatePersonCommand, ...>
{
    private readonly IPersonRepository _personRepository;
    private readonly ITransactionContext _transactionContext;

    public async Task<...> Handle(CreatePersonCommand request, CancellationToken ct)
    {
        await using var transaction = await _transactionContext.BeginTransactionAsync(ct);
        try
        {
            await _personRepository.CreatePerson(request.Person, ct);
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

## 🚀 نحوه استفاده

### 1. تنظیم Connection Strings

**فایل:** `API/appsettings.json` و `Listener/appsettings.json`

```json
{
  "ConnectionStrings": {
    "ApplicationConnection": "Server=localhost;Database=GsSample;User Id=sa;Password=YourPassword123;TrustServerCertificate=True;",
    "ApplicationReadConnection": "Server=localhost;Database=GsSample;User Id=sa;Password=YourPassword123;TrustServerCertificate=True;"
  }
}
```

**نکته:** اگر Read Replica ندارید، می‌توانید هر دو را به یک دیتابیس اشاره دهید.

### 2. استفاده در Repository

```csharp
public class MyRepository : IMyRepository
{
    private readonly IConnectionFactory _connectionFactory;
    private readonly ITransactionContext _transactionContext;

    public async Task<MyEntity> GetById(int id, CancellationToken ct)
    {
        // ✅ اگر transaction فعال باشد، از Write DB استفاده می‌کند
        // ✅ در غیر این صورت، از Read Replica استفاده می‌کند
        var db = _transactionContext.GetCurrentConnection() 
            ?? await _connectionFactory.CreateReadConnection(ct);
        
        // استفاده از connection
    }

    public async Task Create(MyEntity entity, CancellationToken ct)
    {
        // ✅ اگر transaction فعال باشد، از connection مشترک استفاده می‌کند
        var db = _transactionContext.GetCurrentConnection() 
            ?? await _connectionFactory.CreateWriteConnection(ct);
        
        // استفاده از connection
    }
}
```

### 3. استفاده در Handler (با Transaction)

```csharp
public async Task<Result<int>> Handle(MyCommand cmd, CancellationToken ct)
{
    await using var transaction = await _transactionContext.BeginTransactionAsync(ct);
    try
    {
        await _repo1.SaveAsync(data1, ct);
        await _repo2.SaveAsync(data2, ct);
        await transaction.CommitAsync(ct);
        return Result<int>.Success(1);
    }
    catch
    {
        await transaction.RollbackAsync(ct);
        throw;
    }
}
```

---

## 📊 Read/Write Routing

### داخل Transaction

```csharp
await using var transaction = await _transactionContext.BeginTransactionAsync(ct);
try
{
    // ✅ Read: از Write DB استفاده می‌کند (برای consistency)
    var entity = await _repository.GetById(id, ct);
    
    // ✅ Write: از Write DB استفاده می‌کند
    await _repository.UpdateAsync(entity, ct);
    
    await transaction.CommitAsync(ct);
}
```

### خارج Transaction

```csharp
// ✅ Read: از Read Replica استفاده می‌کند
var entities = await _repository.GetAll(ct);

// ✅ Write: از Write DB استفاده می‌کند
await _repository.CreateAsync(entity, ct);
```

---



---

using Infrastructure.Factories;
using Infrastructure.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.Extensions;

/// <summary>
/// Extension methods برای ثبت ConnectionFactory و TransactionContext در DI Container
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// ثبت ConnectionFactory و TransactionContext در DI Container.
    /// 
    /// استفاده:
    /// services.AddConnectionFactory(configuration);
    /// </summary>
    public static IServiceCollection AddConnectionFactory(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // دریافت connection strings
        var writeConnectionString = configuration.GetConnectionString("ApplicationConnection")
            ?? throw new InvalidOperationException("ApplicationConnection connection string is required.");
        
        var readConnectionString = configuration.GetConnectionString("ApplicationReadConnection")
            ?? writeConnectionString; // Fallback به write connection اگر read وجود نداشت

        // ثبت ConnectionFactory به صورت Scoped (برای هر request یک instance)
        services.AddScoped<IConnectionFactory>(sp =>
            new ConnectionFactory(writeConnectionString, readConnectionString));

        // ثبت TransactionContext به صورت Scoped
        services.AddScoped<ITransactionContext>(sp =>
        {
            var connectionFactory = sp.GetRequiredService<IConnectionFactory>();
            return new TransactionContext(connectionFactory);
        });

        return services;
    }

    /// <summary>
    /// ثبت Repositoryها در DI Container.
    /// توجه: Implementation Repositoryها باید توسط شما اضافه شود.
    /// </summary>
    public static IServiceCollection AddRepositories(this IServiceCollection services)
    {
        // TODO: Repository implementations را اینجا ثبت کنید
        // مثال:
        // services.AddScoped<IPersonRepository, PersonRepository>();
        return services;
    }
}


using Domain;
using Infrastructure.Extensions;
using RabbitMQ.Client;

namespace Listener;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);
        builder.AddServices();

        var host = builder.Build();
        listenToQueues(host);

        host.Run();
    }

    private static void listenToQueues(IHost host)
    {
        var queueName = MqttQueue.Person.ToString();
        var listener = host.Services.GetRequiredKeyedService<RabbitMQListenerService>(MqttQueue.Person);
        listener.ListenToQueue(queueName);
    }
}

public static class ServiceCollectionExtensions
{

    private static IServiceCollection AddLogging(this IServiceCollection services)
        => services.AddLogging(configure => configure.AddConsole());

    public static void AddServices(this HostApplicationBuilder builder)
    {
        _ = builder.Services.AddLogging();
        
        // ✅ ثبت ConnectionFactory و TransactionContext برای SQL Server
        _ = builder.Services.AddConnectionFactory(builder.Configuration);
        
        // ✅ ثبت RabbitMQListenerRepository به صورت Scoped
        _ = builder.Services.AddScoped<IRabbitMQListenerRepository, RabbitMQListenerRepository>();
        _ = builder.Services.AddKeyedSingleton<RabbitMQListenerService>(MqttQueue.Person);
        _ = builder.Services.AddHostedService<Worker>();

        AddRabbitMq(builder);
    }

    private static void AddRabbitMq(this HostApplicationBuilder builder) =>
    builder.Services.AddSingleton<RabbitMQ.Client.IConnectionFactory>(sp =>
    {
        var configuration = sp.GetRequiredService<IConfiguration>();
        return new RabbitMQ.Client.ConnectionFactory
        {
            HostName = configuration["RabbitMQ:Host"],
            UserName = configuration["RabbitMQ:UserName"],
            Password = configuration["RabbitMQ:Password"],
            VirtualHost = configuration["RabbitMQ:VirtualHost"],
        };
    });
}
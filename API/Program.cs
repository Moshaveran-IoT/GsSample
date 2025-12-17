using API;
using Infrastructure.Database;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MQTT;

public class Program
{
    private static IConfigurationBuilder _configuration;
    
    public static async Task Main(string[] args)
    {
        // Load configuration
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";
        var configBuilder = new ConfigurationBuilder();
        configBuilder.SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .AddJsonFile($"appsettings.{environment}.json", optional: true, reloadOnChange: true);
        _configuration = configBuilder;
        var configuration = configBuilder.Build();

        // ✅ تست اتصال به دیتابیس و ایجاد جدول در صورت نیاز
        var loggerFactory = LoggerFactory.Create(b => b.AddConsole());
        var logger = loggerFactory.CreateLogger<Program>();
        
        Console.WriteLine("\n" + new string('=', 60));
        Console.WriteLine("  🧪 Testing Database Connection and Setup...");
        Console.WriteLine(new string('=', 60) + "\n");

        var dbTestSuccess = await TestDatabaseConnection.TestAndSetupDatabaseAsync(
            configuration, 
            logger, 
            CancellationToken.None);

        if (!dbTestSuccess)
        {
            Console.WriteLine("\n❌ Database test failed! Please check your connection string.");
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
            return;
        }

        Console.WriteLine("\n" + new string('=', 60));
        Console.WriteLine("  🚀 Starting Person Feature Test...");
        Console.WriteLine(new string('=', 60) + "\n");

        var testSuccess = await TestPersonFeature.RunTestAsync(
            configuration,
            logger,
            CancellationToken.None);

        if (testSuccess)
        {
            Console.WriteLine("\n" + new string('=', 60));
            Console.WriteLine("  ✅ Person Feature tests passed!");
            Console.WriteLine(new string('=', 60) + "\n");
        }
        else
        {
            Console.WriteLine("\n❌ Some Person Feature tests failed! Check the logs above.");
        }

        // ✅ تست Transaction Lifecycle
        Console.WriteLine("\n" + new string('=', 60));
        Console.WriteLine("  🔄 Starting Transaction Lifecycle Test...");
        Console.WriteLine(new string('=', 60) + "\n");

        var transactionTestSuccess = await TestTransactionLifecycle.RunTransactionLifecycleTestAsync(
            configuration,
            logger,
            CancellationToken.None);

        if (transactionTestSuccess)
        {
            Console.WriteLine("\n" + new string('=', 60));
            Console.WriteLine("  ✅ All tests passed! Starting API...");
            Console.WriteLine(new string('=', 60) + "\n");
        }
        else
        {
            Console.WriteLine("\n❌ Transaction lifecycle test failed! Check the logs above.");
        }

        // ادامه با راه‌اندازی API
        var host = CreateHostBuilder(args).Build();

        ILogger appLogger;
        using (var scope = host.Services.CreateScope())
        {
            var services = scope.ServiceProvider;
            var appLoggerFactory = services.GetRequiredService<ILoggerFactory>();
            appLogger = appLoggerFactory.CreateLogger("app");
            appLogger.LogInformation("MQTT Application Starting");
        }

        await host.RunAsync();
    }

    public static IHostBuilder CreateHostBuilder(string[] args)
        => Host.CreateDefaultBuilder(args)
            .ConfigureAppConfiguration((context, config) => getConfig(config))
            .ConfigureWebHostDefaults(webBuilder =>
            {
                var config = getConfig(null).Build();
                webBuilder.UseConfiguration(config);
                var MQTTPort = int.Parse(config["MQTTPort"] ?? "1884");
                _ = webBuilder.UseKestrel(o =>
                {
                    //o.ListenAnyIP(MQTTPort, l => l.UseMqtt());
                });

                _ = webBuilder.UseStartup<Startup>();
            });
            
    private static IConfigurationBuilder getConfig(IConfigurationBuilder? config)
    {
        if (_configuration is null || config is not null)
        {
            var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";
            config ??= new ConfigurationBuilder();
            config.Sources.Clear();
            config.SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddJsonFile($"appsettings.{environment}.json", optional: true, reloadOnChange: true);
            _configuration = config;
        }
        return _configuration;
    }
}

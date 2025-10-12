using MQTT;

namespace API
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var host = CreateHostBuilder(args).Build();

            ILogger logger;
            using (var scope = host.Services.CreateScope())
            {
                var services = scope.ServiceProvider;
                var loggerFactory = services.GetRequiredService<ILoggerFactory>();
                logger = loggerFactory.CreateLogger("app");
                logger.LogInformation("MQTT Application Starting");
            }
            var config = _configuration.Build();
            await host.RunAsync();
        }

        private static IConfigurationBuilder _configuration;
        public static IHostBuilder CreateHostBuilder(string[] args)
            => Host.CreateDefaultBuilder(args)
                .ConfigureAppConfiguration((context, config) => getConfig(config))
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    var config = getConfig(null).Build();
                    webBuilder.UseConfiguration(config);
                    var MQTTPort = int.Parse(config["MQTTPort"]);
                    _ = webBuilder.UseKestrel(o =>
                    {
                        //o.ListenAnyIP(MQTTPort, l => l.UseMqtt());
                    });

                    _ = webBuilder.UseStartup<Startup>();
                });
        private static IConfigurationBuilder getConfig(IConfigurationBuilder config)
        {
            if (_configuration is null || config is not null)
            {
                var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "DEVELOPMENT";
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
}

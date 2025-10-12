using API;

namespace MQTT;

public sealed partial class Startup(IConfiguration configuration, IWebHostEnvironment env)
{
    private readonly IConfiguration _configuration = configuration;

    public void Configure(IApplicationBuilder app)
    {
        if (env.IsDevelopment())
        {
            _ = app.UseDeveloperExceptionPage();
        }

        _ = app.UseRouting();
        _ = app
            .UseAuthentication()
            .UseAuthorization();
        _ = app.UseCors(x => x.AllowAnyMethod().AllowAnyHeader().SetIsOriginAllowed(origin => true).AllowCredentials());

        _ = app.UseStaticFiles();

        //app.UseRouting();
        _ = app.UseEndpoints(endpoints =>
        {
            _ = endpoints.MapControllers();
        });

        _ = app.UseMqtt();
    }

    public void ConfigureServices(IServiceCollection services)
    {
        _ = services.AddControllers();

        _ = services.AddMqtt();
        _ = services.AddRabbitMq(this._configuration);
    }
}

public static class StartupExtensions
{
    public static IServiceCollection AddMqtt(this IServiceCollection services) => services;

    public static IServiceCollection AddRabbitMq(this IServiceCollection services, IConfiguration configuration)
    {
        var rabbitMqHost = configuration["RabbitMQ:Host"];
        var rabbitMqUserName = configuration["RabbitMQ:UserName"];
        var rabbitMqPassword = configuration["RabbitMQ:Password"];
        var rabbitMqVirtualHost = configuration["RabbitMQ:VirtualHost"];

        _ = services.AddSingleton<RabbitMQ.Client.IConnectionFactory>(sp => new RabbitMQ.Client.ConnectionFactory
        {
            HostName = rabbitMqHost,
            UserName = rabbitMqUserName,
            Password = rabbitMqPassword,
            VirtualHost = rabbitMqVirtualHost
        });
        _ = services.AddSingleton<IRabbitMQService, RabbitMQService>();
        return services;
    }

    public static IApplicationBuilder UseMqtt(this IApplicationBuilder app)
        => app;
}
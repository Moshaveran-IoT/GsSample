using API;
using Application;
using Infrastructure.Extensions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

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

        // ✅ فعال‌سازی Swagger برای تست API
        _ = app.UseSwagger();
        _ = app.UseSwaggerUI(c =>
        {
            c.SwaggerEndpoint("/swagger/v1/swagger.json", "GsSample API v1");
            c.RoutePrefix = string.Empty; // Swagger UI در root
        });

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

        // ✅ ثبت ConnectionFactory و TransactionContext برای SQL Server
        _ = services.AddConnectionFactory(this._configuration);
        
        // ✅ ثبت Repositoryها (اگر implementation وجود دارد)
        _ = services.AddRepositories();
        
        // ✅ ثبت MediatR
        _ = services.AddMediatR(Assembly.GetExecutingAssembly());
        _ = services.AddMediatR(typeof(GetAllPersonQueryHandler).Assembly);

        // ✅ ثبت Swagger برای تست API
        _ = services.AddEndpointsApiExplorer();
        _ = services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
            {
                Title = "GsSample API",
                Version = "v1",
                Description = "API for Person Management with Read/Write Separation"
            });
        });

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
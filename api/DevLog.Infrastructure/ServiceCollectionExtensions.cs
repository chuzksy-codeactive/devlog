using DevLog.Infrastructure.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DevLog.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDevLogInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<ICurrentTenantAccessor, CurrentTenantAccessor>();
        services.AddScoped<TenantConnectionInterceptor>();

        services.AddDbContext<DevLogDbContext>((serviceProvider, options) =>
        {
            var interceptor = serviceProvider.GetRequiredService<TenantConnectionInterceptor>();
            options.UseNpgsql(configuration.GetConnectionString("DevLogDb"))
                   .UseSnakeCaseNamingConvention()
                   .AddInterceptors(interceptor);
        });

        return services;
    }
}

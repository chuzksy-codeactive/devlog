using DevLog.Infrastructure.Ai;
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

        // LLM Gateway — architecture doc Section 5's "Internal ILlmProvider
        // abstraction over HttpClientFactory." Named client keeps the base
        // address/auth header configured once, here, rather than per-call.
        services.Configure<AnthropicOptions>(configuration.GetSection(AnthropicOptions.SectionName));
        services.AddHttpClient<ILlmProvider, AnthropicLlmProvider>((serviceProvider, client) =>
        {
            var options = configuration.GetSection(AnthropicOptions.SectionName).Get<AnthropicOptions>()
                ?? new AnthropicOptions();

            client.BaseAddress = new Uri("https://api.anthropic.com");
            client.DefaultRequestHeaders.Add("x-api-key", options.ApiKey);
            client.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
        });

        services.AddScoped<SanitizationPipelineService>();

        return services;
    }
}

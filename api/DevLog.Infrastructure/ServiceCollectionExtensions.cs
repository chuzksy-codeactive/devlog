using DevLog.Infrastructure.Ai;
using DevLog.Infrastructure.BetaAccess;
using DevLog.Infrastructure.Email;
using DevLog.Infrastructure.Identity;
using DevLog.Infrastructure.Interviews;
using DevLog.Infrastructure.MultiTenancy;
using DevLog.Infrastructure.Onboarding;
using DevLog.Infrastructure.Security;
using DevLog.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using System.Net.Http.Headers;

namespace DevLog.Infrastructure;

public static class ServiceCollectionExtensions
{
    private static readonly HashSet<string> WeakIpHashingSecrets = new(StringComparer.OrdinalIgnoreCase)
    {
        "changeme",
        "change-me",
        "default",
        "dev",
        "development",
        "password",
        "secret",
        "devlog-local-ip-hash-secret-change-me"
    };

    public static IServiceCollection AddDevLogInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        bool isDevelopment)
    {
        services.AddScoped<ICurrentTenantAccessor, CurrentTenantAccessor>();
        services.AddScoped<TenantConnectionInterceptor>();
        services.AddScoped<TenantProvisioningService>();
        services.AddHttpContextAccessor();
        services.Configure<PrivateBetaOptions>(configuration.GetSection(PrivateBetaOptions.SectionName));
        services.AddScoped<IBetaAccessService, BetaAccessService>();

        var dataSourceBuilder = new NpgsqlDataSourceBuilder(configuration.GetConnectionString("DevLogDbRuntime"));
        dataSourceBuilder.UseVector();
        services.AddSingleton(dataSourceBuilder.Build());

        services.AddDbContext<DevLogDbContext>((serviceProvider, options) =>
        {
            var interceptor = serviceProvider.GetRequiredService<TenantConnectionInterceptor>();
            var dataSource = serviceProvider.GetRequiredService<NpgsqlDataSource>();
            options.UseNpgsql(dataSource, npgsqlOptions => npgsqlOptions.UseVector())
                   .UseSnakeCaseNamingConvention()
                   .AddInterceptors(interceptor);
        });

        services.Configure<AnthropicOptions>(configuration.GetSection(AnthropicOptions.SectionName));
        services.AddHttpClient<AnthropicLlmProvider>((serviceProvider, client) =>
        {
            var options = configuration.GetSection(AnthropicOptions.SectionName).Get<AnthropicOptions>()
                ?? new AnthropicOptions();

            client.BaseAddress = new Uri("https://api.anthropic.com");
            client.DefaultRequestHeaders.Add("x-api-key", options.ApiKey);
            client.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
        });

        var openAiApiKey = configuration[$"{OpenAiOptions.SectionName}:ApiKey"];

        services.Configure<OpenAiOptions>(configuration.GetSection(OpenAiOptions.SectionName));
        services.AddHttpClient<OpenAiLlmProvider>((serviceProvider, client) =>
        {
            var options = configuration.GetSection(OpenAiOptions.SectionName).Get<OpenAiOptions>()
                ?? new OpenAiOptions();

            client.BaseAddress = new Uri("https://api.openai.com");
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", options.ApiKey.Trim());
        });

        var generationProvider = configuration["Ai:GenerationProvider"] ?? "OpenAI";
        services.AddScoped<ILlmProvider>(serviceProvider =>
            generationProvider.Equals("Anthropic", StringComparison.OrdinalIgnoreCase)
                ? serviceProvider.GetRequiredService<AnthropicLlmProvider>()
                : serviceProvider.GetRequiredService<OpenAiLlmProvider>());

        services.AddHttpClient<IEmbeddingProvider, OpenAiEmbeddingProvider>((serviceProvider, client) =>
        {
            var options = configuration.GetSection(OpenAiOptions.SectionName).Get<OpenAiOptions>()
                ?? new OpenAiOptions();

            client.BaseAddress = new Uri("https://api.openai.com");
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", options.ApiKey.Trim());
        });

        services.Configure<PostmarkOptions>(configuration.GetSection(PostmarkOptions.SectionName));
        services.AddHttpClient<IEmailProvider, PostmarkEmailProvider>((serviceProvider, client) =>
        {
            var options = configuration.GetSection(PostmarkOptions.SectionName).Get<PostmarkOptions>()
                ?? new PostmarkOptions();

            client.BaseAddress = new Uri("https://api.postmarkapp.com/");
            client.DefaultRequestHeaders.Add("Accept", "application/json");
            client.DefaultRequestHeaders.Add("X-Postmark-Server-Token", options.ApiKey);
        });

        services.Configure<FrontendOptions>(configuration.GetSection(FrontendOptions.SectionName));

        services.AddOptions<IpHashingOptions>()
            .Bind(configuration.GetSection(IpHashingOptions.SectionName))
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.Secret),
                "IpHashing:Secret must be configured.")
            .Validate(
                options => (options.Secret?.Trim().Length ?? 0) >= 32,
                "IpHashing:Secret must be at least 32 characters.")
            .Validate(
                options => !WeakIpHashingSecrets.Contains(options.Secret?.Trim() ?? string.Empty),
                "IpHashing:Secret is using a weak/default value and must be replaced.")
            .ValidateOnStart();
        services.AddSingleton<IIpHashingService, HmacIpHashingService>();

        services.AddScoped<EmbeddingPipelineService>();
        services.AddScoped<PromptInjectionTelemetry>();
        services.AddScoped<SanitizationPipelineService>();
        services.AddScoped<AskAiService>();
        services.AddScoped<InterviewRequestService>();

        services.AddHttpClient<GitHubClient>(client =>
        {
            client.BaseAddress = new Uri("https://api.github.com");
            client.DefaultRequestHeaders.Add("User-Agent", "DevLog-App");
            client.DefaultRequestHeaders.Add("Accept", "application/vnd.github+json");
            client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
        });
        services.AddScoped<OnboardingImportService>();

        services.Configure<StorageOptions>(configuration.GetSection(StorageOptions.SectionName));
        var storageProvider = configuration[$"{StorageOptions.SectionName}:Provider"] ?? "AzureBlob";
        if (storageProvider.Equals("AzureBlob", StringComparison.OrdinalIgnoreCase))
        {
            services.AddScoped<IFileStorageProvider, AzureBlobFileStorageProvider>();
        }
        else
        {
            services.AddScoped<IFileStorageProvider, LocalDiskFileStorageProvider>();
        }

        services.Configure<MalwareScanningOptions>(configuration.GetSection(MalwareScanningOptions.SectionName));
        var malwareScannerProvider = configuration[$"{MalwareScanningOptions.SectionName}:Provider"] ?? "None";

        if (!isDevelopment && storageProvider.Equals("AzureBlob", StringComparison.OrdinalIgnoreCase)
            && !malwareScannerProvider.Equals("AzureDefenderStorage", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "In non-development environments, Storage:Provider=AzureBlob requires MalwareScanning:Provider=AzureDefenderStorage.");
        }

        if (malwareScannerProvider.Equals("AzureDefenderStorage", StringComparison.OrdinalIgnoreCase))
        {
            services.AddScoped<IMalwareScanner, AzureDefenderStorageMalwareScanner>();
        }
        else
        {
            services.AddScoped<IMalwareScanner, NoOpMalwareScanner>();
        }

        services.Configure<ClerkOptions>(configuration.GetSection(ClerkOptions.SectionName));
        services.AddHttpClient<ClerkUserService>(client =>
        {
            client.BaseAddress = new Uri("https://api.clerk.com/");
        });

        return services;
    }
}

using DevLog.Infrastructure.Ai;
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

    public static IServiceCollection AddDevLogInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<ICurrentTenantAccessor, CurrentTenantAccessor>();
        services.AddScoped<TenantConnectionInterceptor>();
        services.AddScoped<TenantProvisioningService>();

        // Built via NpgsqlDataSourceBuilder (rather than passing the
        // connection string straight to UseNpgsql) specifically for
        // .UseVector() — this is what teaches Npgsql to map the Pgvector
        // `Vector` CLR type to Postgres's `vector` column type. Registered
        // as a singleton, which is the documented Npgsql lifetime for a
        // data source (it pools connections internally).
        //
        // DevLogDbRuntime, not DevLogDb — Milestone 8's RLS force-on-owner
        // fix. Real app traffic now connects as `devlog_app`, a deliberately
        // unprivileged, non-owner, non-superuser role (sql/005), so RLS
        // policies actually apply to it. `DevLogDb` (the `devlog` owner
        // role) still exists in config, but only DevLogDbContextFactory
        // (used by `dotnet ef` for migrations, which need DDL rights this
        // role doesn't have) reads it now.
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

        // LLM Gateway (generation) — architecture doc Section 5's "Internal
        // ILlmProvider abstraction over HttpClientFactory." Both providers
        // are registered as themselves (typed HttpClients), and
        // Ai:GenerationProvider decides which one actually gets resolved as
        // ILlmProvider — this is what makes the provider swap a config
        // change, not a code change. Added 2026-07-05 when Chika couldn't
        // get an Anthropic console API key working and needed OpenAI as a
        // real option, not just a "the architecture allows it" claim.
        services.Configure<AnthropicOptions>(configuration.GetSection(AnthropicOptions.SectionName));
        services.AddHttpClient<AnthropicLlmProvider>((serviceProvider, client) =>
        {
            var options = configuration.GetSection(AnthropicOptions.SectionName).Get<AnthropicOptions>()
                ?? new AnthropicOptions();

            client.BaseAddress = new Uri("https://api.anthropic.com");
            client.DefaultRequestHeaders.Add("x-api-key", options.ApiKey);
            client.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
        });

        services.Configure<OpenAiOptions>(configuration.GetSection(OpenAiOptions.SectionName));
        services.AddHttpClient<OpenAiLlmProvider>((serviceProvider, client) =>
        {
            var options = configuration.GetSection(OpenAiOptions.SectionName).Get<OpenAiOptions>()
                ?? new OpenAiOptions();

            client.BaseAddress = new Uri("https://api.openai.com");
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {options.ApiKey}");
        });

        var generationProvider = configuration["Ai:GenerationProvider"] ?? "OpenAI";
        services.AddScoped<ILlmProvider>(serviceProvider =>
            generationProvider.Equals("Anthropic", StringComparison.OrdinalIgnoreCase)
                ? serviceProvider.GetRequiredService<AnthropicLlmProvider>()
                : serviceProvider.GetRequiredService<OpenAiLlmProvider>());

        // LLM Gateway (embeddings) — always OpenAI for now (Anthropic
        // doesn't offer an embeddings endpoint at all, so there's no
        // provider switch to make here); shares OpenAiOptions with
        // OpenAiLlmProvider above since it's the same API key either way.
        services.AddHttpClient<IEmbeddingProvider, OpenAiEmbeddingProvider>((serviceProvider, client) =>
        {
            var options = configuration.GetSection(OpenAiOptions.SectionName).Get<OpenAiOptions>()
                ?? new OpenAiOptions();

            client.BaseAddress = new Uri("https://api.openai.com");
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {options.ApiKey}");
        });

        // Email (transactional) — backs InterviewRequestService's creator
        // notification. Same typed-HttpClient shape as the LLM providers
        // above, for the same swap-without-rewrite reason.
        services.Configure<PostmarkOptions>(configuration.GetSection(PostmarkOptions.SectionName));
        services.AddHttpClient<IEmailProvider, PostmarkEmailProvider>((serviceProvider, client) =>
        {
            var options = configuration.GetSection(PostmarkOptions.SectionName).Get<PostmarkOptions>()
                ?? new PostmarkOptions();

            client.BaseAddress = new Uri("https://api.postmarkapp.com/");
            client.DefaultRequestHeaders.Add("Accept", "application/json");
            client.DefaultRequestHeaders.Add("X-Postmark-Server-Token", options.ApiKey);
        });

        // Milestone 10 — InterviewRequestService needs the frontend's own
        // base URL to build the confirmation link it emails a recruiter.
        services.Configure<FrontendOptions>(configuration.GetSection(FrontendOptions.SectionName));

        // HMAC-based IP hashing for anonymous-event logging (Ask AI asker,
        // portfolio viewer) so stored values are keyed pseudonymous digests,
        // not raw IPs or unsalted plain hashes.
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
        services.AddScoped<SanitizationPipelineService>();
        services.AddScoped<AskAiService>();
        services.AddScoped<InterviewRequestService>();

        // Milestone 13 — guided onboarding's GitHub import. Unauthenticated
        // (public-username-lookup, decided over OAuth — see GitHubClient's
        // doc comment), so unlike every other typed HttpClient in this file
        // there's no API key/options class to configure. GitHub requires a
        // User-Agent header on every request or it rejects them outright.
        services.AddHttpClient<GitHubClient>(client =>
        {
            client.BaseAddress = new Uri("https://api.github.com");
            client.DefaultRequestHeaders.Add("User-Agent", "DevLog-App");
            client.DefaultRequestHeaders.Add("Accept", "application/vnd.github+json");
            client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
        });
        services.AddScoped<OnboardingImportService>();

        // Milestone 13b — resume upload's storage abstraction. Only
        // LocalDiskFileStorageProvider exists today ("local only, for
        // now"); see that class's doc comment for the Azure Blob Storage
        // swap-in story.
        services.Configure<StorageOptions>(configuration.GetSection(StorageOptions.SectionName));
        services.AddScoped<IFileStorageProvider, LocalDiskFileStorageProvider>();

        // Milestone 19 — GitHub link verification. Same "Clerk" config
        // section Program.cs already reads Authority/AuthorizedParty from
        // for JWT validation, now also bound as options for SecretKey (see
        // ClerkOptions' doc comment for why a secret is newly needed here).
        services.Configure<ClerkOptions>(configuration.GetSection(ClerkOptions.SectionName));
        services.AddHttpClient<ClerkUserService>(client =>
        {
            client.BaseAddress = new Uri("https://api.clerk.com/");
        });

        return services;
    }
}

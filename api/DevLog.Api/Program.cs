using Azure.Identity;
using Azure.Monitor.OpenTelemetry.AspNetCore;
using DevLog.Api.Middleware;
using DevLog.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.IdentityModel.Tokens;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

var keyVaultEnabled = builder.Configuration.GetValue<bool>("KeyVault:Enabled");
var keyVaultUriValue = builder.Configuration["KeyVault:VaultUri"];
if (keyVaultEnabled)
{
    if (!Uri.TryCreate(keyVaultUriValue, UriKind.Absolute, out var keyVaultUri))
    {
        throw new InvalidOperationException("KeyVault is enabled but KeyVault:VaultUri is missing or invalid.");
    }

    builder.Configuration.AddAzureKeyVault(
        keyVaultUri,
        new DefaultAzureCredential(new DefaultAzureCredentialOptions
        {
            ExcludeInteractiveBrowserCredential = true
        }));
}

var applicationInsightsConnectionString =
    builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"]
    ?? builder.Configuration["ApplicationInsights:ConnectionString"];

if (!string.IsNullOrWhiteSpace(applicationInsightsConnectionString))
{
    builder.Services
        .AddOpenTelemetry()
        .WithMetrics(metrics => metrics.AddMeter("DevLog.Security"))
        .UseAzureMonitor(options =>
    {
        options.ConnectionString = applicationInsightsConnectionString;
    });
}

builder.Services.AddDevLogInfrastructure(builder.Configuration, builder.Environment.IsDevelopment());

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

    var knownProxies = builder.Configuration.GetSection("ForwardedHeaders:KnownProxies").Get<string[]>()
        ?? Array.Empty<string>();

    foreach (var proxy in knownProxies)
    {
        if (System.Net.IPAddress.TryParse(proxy, out var ipAddress))
        {
            options.KnownProxies.Add(ipAddress);
        }
    }
});

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = builder.Configuration["Clerk:Authority"]; // e.g. https://your-app.clerk.accounts.dev
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateAudience = false,
            ValidateIssuer = true,
        };

        options.MapInboundClaims = false;
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = context =>
            {
                var azp = context.Principal?.FindFirst("azp")?.Value;
                var allowedOrigin = builder.Configuration["Clerk:AuthorizedParty"];

                if (string.IsNullOrEmpty(azp) || !string.Equals(azp, allowedOrigin, StringComparison.Ordinal))
                {
                    context.Fail("Invalid azp claim.");
                }

                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
    });

const string FrontendCorsPolicy = "FrontendCors";
var frontendOrigins = builder.Configuration.GetSection("Cors:FrontendOrigins").Get<string[]>()
    ?? new[] { "http://localhost:3000" };

builder.Services.AddCors(options =>
{
    options.AddPolicy(FrontendCorsPolicy, policy =>
    {
        policy.WithOrigins(frontendOrigins)
              .WithHeaders("Authorization", "Content-Type")
              .WithMethods("GET", "POST", "PUT", "DELETE");
    });
});

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy("AskAi", httpContext => RateLimitPartition.GetFixedWindowLimiter(
        PartitionKey(httpContext),
        _ => new FixedWindowRateLimiterOptions
        {
            Window = TimeSpan.FromMinutes(1),
            PermitLimit = 10,
            QueueLimit = 0
        }));

    options.AddPolicy("InterviewRequestCreate", httpContext => RateLimitPartition.GetFixedWindowLimiter(
        PartitionKey(httpContext),
        _ => new FixedWindowRateLimiterOptions
        {
            Window = TimeSpan.FromMinutes(10),
            PermitLimit = 5,
            QueueLimit = 0
        }));

    options.AddPolicy("InterviewRequestVerify", httpContext => RateLimitPartition.GetFixedWindowLimiter(
        PartitionKey(httpContext),
        _ => new FixedWindowRateLimiterOptions
        {
            Window = TimeSpan.FromMinutes(1),
            PermitLimit = 20,
            QueueLimit = 0
        }));

    options.AddPolicy("PublicCvAccess", httpContext => RateLimitPartition.GetFixedWindowLimiter(
        PartitionKey(httpContext),
        _ => new FixedWindowRateLimiterOptions
        {
            Window = TimeSpan.FromMinutes(1),
            PermitLimit = 10,
            QueueLimit = 0
        }));
});

static string PartitionKey(HttpContext httpContext)
{
    var ip = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    var slug = httpContext.Request.RouteValues.TryGetValue("slug", out var slugValue)
        ? slugValue?.ToString()
        : null;
    return $"{ip}:{slug ?? "no-slug"}";
}

var app = builder.Build();

app.UseForwardedHeaders();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["Referrer-Policy"] = "no-referrer";
    context.Response.Headers["Permissions-Policy"] = "geolocation=(), microphone=(), camera=()";
    await next();
});

app.UseCors(FrontendCorsPolicy);

var storageOptions = app.Configuration.GetSection("Storage").Get<DevLog.Infrastructure.Storage.StorageOptions>()
    ?? new DevLog.Infrastructure.Storage.StorageOptions();
if (storageOptions.Provider.Equals("LocalDisk", StringComparison.OrdinalIgnoreCase))
{
    Directory.CreateDirectory(storageOptions.LocalDiskPath);
    var uploadsRequestPath = "/" + new Uri(storageOptions.PublicBaseUrl).AbsolutePath.Trim('/');
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(
            Path.GetFullPath(storageOptions.LocalDiskPath)),
        RequestPath = uploadsRequestPath
    });
}

app.UseAuthentication();
app.UseMiddleware<TenantResolutionMiddleware>();
app.UseAuthorization();
app.UseRateLimiter();
app.MapControllers();

app.MapGet("/health", () => Results.Ok(new { status = "healthy" })).AllowAnonymous();

app.Run();

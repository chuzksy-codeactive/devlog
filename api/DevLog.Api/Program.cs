using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using DevLog.Api.Middleware;
using DevLog.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDevLogInfrastructure(builder.Configuration);

// Forwarded headers (X-Forwarded-For / X-Forwarded-Proto) for proxy/LB
// deployments. Trusted proxies are explicit (ForwardedHeaders:KnownProxies)
// rather than blanket-trusting all forwarded headers.
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

// --- Clerk JWT bearer setup ---
// SPIKE THIS FIRST, per the Sprint 0 checklist, before trusting anything
// else built on top of it. Clerk validates the "azp" (authorized party)
// claim rather than a conventional "aud" claim — get a real Clerk-issued
// test token authenticating against this exact config before relying on it.
//
// Clerk:AuthorizedParty must equal the origin that actually MINTS the
// token, not just "a Clerk-related URL" — this has bitten this project
// twice now. Sprint 0's spike token came from Clerk's hosted Account
// Portal, so AuthorizedParty was set to that portal's own origin
// (https://<instance>.accounts.dev). Milestone 11 wired up real frontend
// auth (useAuth().getToken() in web/src/lib/use-api.ts), so tokens are now
// minted by the actual running Next.js app instead — meaning azp is
// whatever origin that app runs on (http://localhost:3000 for local dev),
// not the Account Portal. Every dashboard call 401'd until this was
// updated to match. If this ever 401s again after changing how/where the
// frontend runs, check this value against the app's real origin first.
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = builder.Configuration["Clerk:Authority"]; // e.g. https://your-app.clerk.accounts.dev
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateAudience = false,
            ValidateIssuer = true,
        };

        // Without this, System.IdentityModel.Tokens.Jwt silently renames
        // short JWT claim names to legacy WS-Federation/SAML URIs when it
        // builds the ClaimsPrincipal — "sub" becomes
        // "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier",
        // and every FindFirst("sub") in this codebase (TenantResolutionMiddleware,
        // EntriesController's approvedBy lookup) comes back null. Discovered
        // only once a real Clerk-issued token from the actual running app
        // reached this code for the first time (Milestone 11's dashboard) —
        // authentication itself succeeded (valid signature/issuer/azp), but
        // tenant resolution silently failed downstream, surfacing as
        // PortfolioController's "No tenant context resolved" 401. This keeps
        // claim names exactly as Clerk issues them.
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

// Milestone 11 discovery: nothing here ever configured System.Text.Json to
// serialize enums as strings, so the default all along has been the
// underlying int (PortfolioVisibility.Unlisted -> 1, not "Unlisted"). Every
// curl example in GETTING-STARTED.md that sends an enum as a JSON string
// (e.g. Milestone 2's "entryType":"SprintEntry", Milestone 7's
// "visibility":"Unlisted") would have failed to deserialize without this —
// and the dashboard built in this milestone reads status/type/visibility
// fields back as strings throughout. Adding JsonStringEnumConverter
// globally rather than per-DTO so every controller (existing and future)
// gets consistent behavior in both directions.
// ReferenceHandler.IgnoreCycles is a safety net, not the primary fix — the
// actual fix (Milestone 11) is that every controller action now shapes an
// explicit DTO/anonymous object instead of returning a tracked entity
// directly. But the underlying cause is structural, not a one-off typo:
// several entity pairs are genuinely bidirectional EF navigations
// (Tenant<->Portfolio, Tenant<->Subscription, RawEntry<->PublishedEntry,
// PublishedEntry<->SanitizationAudit), and EF's change-tracker fixup wires
// them up across ANYTHING tracked in the same request-scoped DbContext —
// not just what a given query explicitly .Include()d. That means a future
// action returning a raw entity can throw "a possible object cycle was
// detected" (or worse, silently include an unintended nav property) purely
// because some earlier, unrelated code in the same request happened to
// load the other side first — exactly what happened to
// PortfolioController.UpdateMyPortfolio. IgnoreCycles turns that failure
// mode from a 500 into a truncated-but-valid response (the second
// occurrence of a repeated reference serializes as null) if it ever
// recurs — defense in depth, same principle as the CSP header, not a
// substitute for shaping DTOs deliberately.
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
    });

// Section 9 Phase-1 must: "explicitly scoped to the Next.js frontend
// origin(s) — the anonymous-browsing endpoints are public by design, which
// makes a permissive CORS policy an easy mistake to make here specifically."
// This is the first endpoint (Ask AI) actually called cross-origin from the
// browser, so it's the right moment to add this rather than defer it.
const string FrontendCorsPolicy = "FrontendCors";
var frontendOrigins = builder.Configuration.GetSection("Cors:FrontendOrigins").Get<string[]>()
    ?? new[] { "http://localhost:3000" };

builder.Services.AddCors(options =>
{
    options.AddPolicy(FrontendCorsPolicy, policy =>
    {
        policy.WithOrigins(frontendOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// Milestone 20 — three fixed-window policies, one per anonymous endpoint
// that does real work per call (email send, LLM call, DB write). Partitioned
// by (client IP, portfolio slug) rather than IP alone, so one bad actor
// hammering a single candidate's page can't also exhaust the budget for
// every other candidate sharing that IP (e.g. an office NAT, a campus
// network). RejectionStatusCode makes ASP.NET Core return 429 itself for
// anything over the limit — no per-action code needed beyond the
// [EnableRateLimiting("...")] attribute.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // Ask AI: layered on top of AskAiService's own per-tenant hourly cap
    // (see AskAiController) — that protects a tenant's shared quota from
    // being drained by anyone; this stops a single source from doing the
    // draining fast enough for the DB-backed cap to matter.
    options.AddPolicy("AskAi", httpContext => RateLimitPartition.GetFixedWindowLimiter(
        PartitionKey(httpContext),
        _ => new FixedWindowRateLimiterOptions
        {
            Window = TimeSpan.FromMinutes(1),
            PermitLimit = 10,
            QueueLimit = 0
        }));

    // Interview request creation: tightest limit — each call sends a real
    // email (verification, then creator notification once confirmed), a
    // cost and an inbox-spam surface Ask AI doesn't have.
    options.AddPolicy("InterviewRequestCreate", httpContext => RateLimitPartition.GetFixedWindowLimiter(
        PartitionKey(httpContext),
        _ => new FixedWindowRateLimiterOptions
        {
            Window = TimeSpan.FromMinutes(10),
            PermitLimit = 5,
            QueueLimit = 0
        }));

    // Interview request verification: looser — no email sent, and a
    // recruiter retrying a mistyped/expired token is normal usage, not abuse.
    options.AddPolicy("InterviewRequestVerify", httpContext => RateLimitPartition.GetFixedWindowLimiter(
        PartitionKey(httpContext),
        _ => new FixedWindowRateLimiterOptions
        {
            Window = TimeSpan.FromMinutes(1),
            PermitLimit = 20,
            QueueLimit = 0
        }));
});

// Shared partition key for all three policies above: (IP, slug) rather than
// IP alone, for the reasoning in the AddRateLimiter comment. RouteValues is
// used (not a bound parameter) because this runs as part of endpoint
// selection, before model binding.
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

// Milestone 20 — HTTPS redirect + HSTS, gated to non-Development.
// Local dev genuinely runs over plain HTTP: launchSettings.json exposes
// http://localhost:61726 alongside the HTTPS port, and web/.env.local's
// NEXT_PUBLIC_API_BASE_URL points at that HTTP port — the frontend has never
// talked to the API over HTTPS locally. Turning this on unconditionally
// would redirect every request the running dev frontend makes and break the
// local flow outright. Production deployments should terminate/enforce TLS
// (either here or at the hosting platform's edge, e.g. a load balancer) —
// this covers the case where DevLog's own Kestrel process is the one
// deciding.
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

// Security response headers — unconditional, since none of these affect
// local HTTP traffic or break the dev flow the way HSTS/redirect would.
// nosniff and the restrictive Permissions-Policy cost nothing functionally;
// Referrer-Policy just trims what leaks to any external link a viewer/
// recruiter clicks from a rendered portfolio page.
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["Referrer-Policy"] = "no-referrer";
    context.Response.Headers["Permissions-Policy"] = "geolocation=(), microphone=(), camera=()";
    await next();
});

app.UseCors(FrontendCorsPolicy);

// Milestone 13b — serves whatever LocalDiskFileStorageProvider writes
// (currently just resumes) back out at Storage:PublicBaseUrl's path.
// RequestPath must match that config value's path component exactly, or
// LocalDiskFileStorageProvider's returned URLs 404. "/uploads" is the
// default for both, so this only needs revisiting if either config value
// changes.
var storageOptions = app.Configuration.GetSection("Storage").Get<DevLog.Infrastructure.Storage.StorageOptions>()
    ?? new DevLog.Infrastructure.Storage.StorageOptions();
Directory.CreateDirectory(storageOptions.LocalDiskPath);
var uploadsRequestPath = "/" + new Uri(storageOptions.PublicBaseUrl).AbsolutePath.Trim('/');
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(
        Path.GetFullPath(storageOptions.LocalDiskPath)),
    RequestPath = uploadsRequestPath
});

app.UseAuthentication();
app.UseMiddleware<TenantResolutionMiddleware>();
app.UseAuthorization();
app.UseRateLimiter();
app.MapControllers();

app.Run();

public partial class Program;

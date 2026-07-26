using System.Text.Json.Serialization;
using DevLog.Api.Middleware;
using DevLog.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDevLogInfrastructure(builder.Configuration);

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

var app = builder.Build();

app.UseCors(FrontendCorsPolicy);
app.UseAuthentication();
app.UseMiddleware<TenantResolutionMiddleware>();
app.UseAuthorization();
app.MapControllers();

app.Run();

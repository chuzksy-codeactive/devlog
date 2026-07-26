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
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = builder.Configuration["Clerk:Authority"]; // e.g. https://your-app.clerk.accounts.dev
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateAudience = false,
            ValidateIssuer = true,
        };
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
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
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

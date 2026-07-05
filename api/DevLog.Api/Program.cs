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
builder.Services.AddControllers();

var app = builder.Build();

app.UseAuthentication();
app.UseMiddleware<TenantResolutionMiddleware>();
app.UseAuthorization();
app.MapControllers();

app.Run();

namespace DevLog.Infrastructure.Identity;

// Bound from configuration section "Clerk" — the same section Program.cs
// already reads Authority/AuthorizedParty from directly (those two stay as
// plain string reads there, unrelated to this class; IOptions binding and a
// couple of manual builder.Configuration[...] reads against the same
// section coexist fine). SecretKey is new as of Milestone 19: every prior
// Clerk touchpoint in this backend only ever validated an already-issued
// JWT (JWKS-based, no secret needed) — this is the first time DevLog.Api
// itself calls Clerk's own Backend API, which requires the instance's
// secret key as a bearer credential. Get it from the Clerk Dashboard's API
// Keys page (same instance as Authority/AuthorizedParty above) — it's
// already sitting in web/.env.local as CLERK_SECRET_KEY for the Next.js
// server side; this is the same value, just also needed on the .NET side
// now that verification happens here instead of trusting the browser.
public class ClerkOptions
{
    public const string SectionName = "Clerk";

    public string Authority { get; set; } = string.Empty;
    public string AuthorizedParty { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
}

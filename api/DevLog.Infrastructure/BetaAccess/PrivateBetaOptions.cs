using System.Security.Claims;

namespace DevLog.Infrastructure.BetaAccess;

public class PrivateBetaOptions
{
    public const string SectionName = "PrivateBeta";

    public bool Enabled { get; set; } = false;

    public bool RequireVerifiedEmail { get; set; } = true;

    public string[] AdminEmails { get; set; } = [];

    public string[] EmailClaimTypes { get; set; } =
    [
        "email",
        "email_address",
        ClaimTypes.Email
    ];

    public string[] EmailVerifiedClaimTypes { get; set; } =
    [
        "email_verified",
        "verified_email"
    ];
}

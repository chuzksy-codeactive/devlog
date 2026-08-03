namespace DevLog.Domain.Entities;

// A single "proof link" — one entry in Portfolio.ProofLinks, the multi-role
// expansion's generalized supplement to the fixed GitHubUrl/LinkedInUrl/CvFileUrl
// fields (see Portfolio.cs's doc comment for why those stay as-is rather than
// being replaced). Not its own database table — stored as a jsonb list on
// Portfolio, same pattern as CoreTechnologies and Experience.Technologies,
// since this is unstructured, creator-ordered data with no need for its own
// RLS policy or independent lifecycle. A record (not a class) specifically
// so DevLogDbContext's value comparer for the jsonb list gets structural
// equality for free — see that file's proofLinkListComparer.
public record ProofLink
{
    public string Label { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
}

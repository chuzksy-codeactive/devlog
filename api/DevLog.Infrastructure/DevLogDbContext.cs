using System.Text.Json;
using DevLog.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace DevLog.Infrastructure;

public class DevLogDbContext : DbContext
{
    public DevLogDbContext(DbContextOptions<DevLogDbContext> options) : base(options) { }

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<Portfolio> Portfolios => Set<Portfolio>();
    public DbSet<Experience> Experiences => Set<Experience>();
    public DbSet<RawEntry> RawEntries => Set<RawEntry>();
    public DbSet<PublishedEntry> PublishedEntries => Set<PublishedEntry>();
    public DbSet<SanitizationAudit> SanitizationAudits => Set<SanitizationAudit>();
    public DbSet<Subscription> Subscriptions => Set<Subscription>();
    public DbSet<Embedding> Embeddings => Set<Embedding>();
    public DbSet<AiQueryLog> AiQueryLogs => Set<AiQueryLog>();
    public DbSet<InterviewRequest> InterviewRequests => Set<InterviewRequest>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Required once, for the migration to emit CREATE EXTENSION — pgvector
        // is preinstalled in the docker-compose image but still needs to be
        // enabled per-database.
        modelBuilder.HasPostgresExtension("vector");

        var stringListComparer = new ValueComparer<List<string>>(
            (a, b) => (a ?? new List<string>()).SequenceEqual(b ?? new List<string>()),
            v => v.Aggregate(0, (hash, s) => HashCode.Combine(hash, s.GetHashCode())),
            v => v.ToList());

        modelBuilder.Entity<Tenant>(e =>
        {
            e.ToTable("tenants");
            e.HasKey(x => x.Id);
            e.Property(x => x.Slug).IsRequired();
            e.HasIndex(x => x.Slug).IsUnique();

            e.HasOne(x => x.Portfolio)
                .WithOne(p => p!.Tenant)
                .HasForeignKey<Portfolio>(p => p.TenantId);

            e.HasOne(x => x.Subscription)
                .WithOne(s => s!.Tenant)
                .HasForeignKey<Subscription>(s => s.TenantId);
        });

        modelBuilder.Entity<Portfolio>(e =>
        {
            e.ToTable("portfolios");
            e.HasKey(x => x.TenantId);
            e.Property(x => x.Visibility).HasConversion<string>();
            e.Property(x => x.CoreTechnologies)
                .HasColumnType("jsonb")
                .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new List<string>())
                .Metadata.SetValueComparer(stringListComparer);
        });

        modelBuilder.Entity<Experience>(e =>
        {
            e.ToTable("experiences");
            e.HasKey(x => x.Id);
            e.Property(x => x.ExperienceType).HasConversion<string>();
            e.Property(x => x.Technologies)
                .HasColumnType("jsonb")
                .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new List<string>())
                .Metadata.SetValueComparer(stringListComparer);

            e.HasOne(x => x.Tenant)
                .WithMany(t => t.Experiences)
                .HasForeignKey(x => x.TenantId);
        });

        modelBuilder.Entity<RawEntry>(e =>
        {
            e.ToTable("raw_entries");
            e.HasKey(x => x.Id);
            e.Property(x => x.EntryType).HasConversion<string>();
            e.Property(x => x.Status).HasConversion<string>();

            e.HasOne(x => x.Tenant)
                .WithMany(t => t.RawEntries)
                .HasForeignKey(x => x.TenantId);
        });

        modelBuilder.Entity<PublishedEntry>(e =>
        {
            e.ToTable("published_entries");
            e.HasKey(x => x.Id);
            e.Property(x => x.Tags)
                .HasColumnType("jsonb")
                .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new List<string>())
                .Metadata.SetValueComparer(stringListComparer);

            e.HasOne(x => x.RawEntry)
                .WithOne(r => r!.PublishedEntry)
                .HasForeignKey<PublishedEntry>(x => x.RawEntryId);
        });

        modelBuilder.Entity<SanitizationAudit>(e =>
        {
            e.ToTable("sanitization_audit");
            e.HasKey(x => x.Id);

            e.HasOne(x => x.PublishedEntry)
                .WithOne(p => p!.SanitizationAudit)
                .HasForeignKey<SanitizationAudit>(x => x.PublishedEntryId);
        });

        modelBuilder.Entity<Subscription>(e =>
        {
            e.ToTable("subscriptions");
            e.HasKey(x => x.TenantId);
        });

        modelBuilder.Entity<Embedding>(e =>
        {
            e.ToTable("embeddings");
            e.HasKey(x => x.Id);
            e.Property(x => x.SourceType).HasConversion<string>();
            e.Property(x => x.Vector).HasColumnType("vector(1536)");
            // Looked up by (tenant, source) on re-embedding (delete-then-insert)
            // and scanned per-tenant during vector search — see
            // EmbeddingPipelineService and AskAiController.
            e.HasIndex(x => new { x.TenantId, x.SourceType, x.SourceId });

            e.HasOne(x => x.Tenant)
                .WithMany()
                .HasForeignKey(x => x.TenantId);
        });

        modelBuilder.Entity<AiQueryLog>(e =>
        {
            e.ToTable("ai_query_logs");
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.TenantId, x.CreatedAt });

            e.HasOne(x => x.Tenant)
                .WithMany()
                .HasForeignKey(x => x.TenantId);
        });

        modelBuilder.Entity<InterviewRequest>(e =>
        {
            e.ToTable("interview_requests");
            e.HasKey(x => x.Id);
            e.Property(x => x.Status).HasConversion<string>();
            e.HasIndex(x => new { x.TenantId, x.CreatedAt });

            e.HasOne(x => x.Tenant)
                .WithMany()
                .HasForeignKey(x => x.TenantId);
        });
    }
}

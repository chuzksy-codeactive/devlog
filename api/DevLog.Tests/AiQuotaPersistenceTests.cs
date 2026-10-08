using DevLog.Api.Controllers;
using DevLog.Domain.Entities;
using DevLog.Infrastructure;
using DevLog.Infrastructure.Ai;
using DevLog.Infrastructure.Ai.CostControl;
using DevLog.Infrastructure.MultiTenancy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Pgvector;
using Xunit;

namespace DevLog.Tests;

public class AiQuotaPersistenceTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Sanitization_DoesNotPersistIntermediateStateOnQuotaFailure(bool unavailable)
    {
        var options = DatabaseOptions();
        var entry = new RawEntry { Id = Guid.NewGuid(), TenantId = Guid.NewGuid(), RawContent = "Draft content." };
        var failure = QuotaFailure(unavailable);
        await using (var db = new InMemoryQuotaDbContext(options))
        {
            db.RawEntries.Add(entry);
            await db.SaveChangesAsync();
            var service = Pipeline(db, new StubLlmProvider(failure), new StubEmbeddingProvider());

            Assert.Same(failure, await Assert.ThrowsAnyAsync<Exception>(() => service.SanitizeAsync(entry.Id)));
        }

        await using var check = new InMemoryQuotaDbContext(options);
        Assert.Equal(RawEntryStatus.Draft, (await check.RawEntries.SingleAsync()).Status);
        Assert.Empty(await check.SanitizationRunLogs.ToListAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Approval_OnlyPublishesWhenEmbeddingsSucceed(bool deny)
    {
        var options = DatabaseOptions();
        var entry = new RawEntry
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            RawContent = "Private draft.",
            SanitizedPreview = "Approved public content.",
            Status = RawEntryStatus.PendingApproval
        };
        await using (var db = new InMemoryQuotaDbContext(options))
        {
            db.RawEntries.Add(entry);
            await db.SaveChangesAsync();
            var embedding = new StubEmbeddingProvider(deny ? QuotaFailure(false) : null);
            var service = Pipeline(db, new StubLlmProvider(), embedding);
            if (deny)
            {
                await Assert.ThrowsAsync<AiQuotaRejectedException>(() => service.ApproveAsync(entry.Id, "creator"));
            }
            else
            {
                await service.ApproveAsync(entry.Id, "creator");
            }
        }

        await using var check = new InMemoryQuotaDbContext(options);
        Assert.Equal(deny ? RawEntryStatus.PendingApproval : RawEntryStatus.Approved,
            (await check.RawEntries.SingleAsync()).Status);
        Assert.Equal(deny ? 0 : 1, await check.PublishedEntries.CountAsync());
        Assert.Equal(deny ? 0 : 1, await check.SanitizationAudits.CountAsync());
        Assert.Equal(deny ? 0 : 1, await check.Embeddings.CountAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExperienceCreate_OnlyPersistsWhenEmbeddingsSucceed(bool deny)
    {
        var options = DatabaseOptions();
        await using (var db = new InMemoryQuotaDbContext(options))
        {
            var controller = new ExperienceController(db, new TenantAccessor(),
                new EmbeddingPipelineService(db, new StubEmbeddingProvider(deny ? QuotaFailure(false) : null)));
            if (deny)
            {
                await Assert.ThrowsAsync<AiQuotaRejectedException>(() => controller.Create(Request("New company")));
            }
            else
            {
                Assert.IsType<CreatedAtActionResult>(await controller.Create(Request("New company")));
            }
        }

        await using var check = new InMemoryQuotaDbContext(options);
        Assert.Equal(deny ? 0 : 1, await check.Experiences.CountAsync());
        Assert.Equal(deny ? 0 : 1, await check.Embeddings.CountAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExperienceUpdate_PreservesPreviousDataAndVectorsOnQuotaFailure(bool unavailable)
    {
        var options = DatabaseOptions();
        var tenant = new TenantAccessor();
        var experience = new Experience
        {
            Id = Guid.NewGuid(), TenantId = tenant.TenantId!.Value, CompanyLabel = "Existing company"
        };
        var failure = QuotaFailure(unavailable);
        await using (var db = new InMemoryQuotaDbContext(options))
        {
            db.Experiences.Add(experience);
            db.Embeddings.Add(new Embedding
            {
                Id = Guid.NewGuid(),
                TenantId = experience.TenantId,
                SourceType = EmbeddingSourceType.Experience,
                SourceId = experience.Id,
                ChunkText = "Existing vector content",
                Vector = new Vector(new float[1536])
            });
            await db.SaveChangesAsync();
            var controller = new ExperienceController(db, tenant,
                new EmbeddingPipelineService(db, new StubEmbeddingProvider(failure)));

            Assert.Same(failure, await Assert.ThrowsAnyAsync<Exception>(() =>
                controller.Update(experience.Id, Request("Changed company"))));
        }

        await using var check = new InMemoryQuotaDbContext(options);
        Assert.Equal("Existing company", (await check.Experiences.SingleAsync()).CompanyLabel);
        Assert.Equal("Existing vector content", (await check.Embeddings.SingleAsync()).ChunkText);
    }

    private static DbContextOptions<DevLogDbContext> DatabaseOptions() =>
        new DbContextOptionsBuilder<DevLogDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;

    private static Exception QuotaFailure(bool unavailable) => unavailable
        ? new AiQuotaUnavailableException("Quota database unavailable.")
        : new AiQuotaRejectedException("ai_daily_allowance_reached");

    private static SanitizationPipelineService Pipeline(
        DevLogDbContext db, ILlmProvider llm, IEmbeddingProvider embedding) =>
        new(db, llm, new EmbeddingPipelineService(db, embedding),
            new PromptInjectionTelemetry(NullLogger<PromptInjectionTelemetry>.Instance));

    private static ExperienceController.ExperienceRequest Request(string company) =>
        new(default, company, "Engineer", new DateOnly(2025, 1, 1), null, ["PostgreSQL"], "Shipped a feature.");

    private sealed class InMemoryQuotaDbContext(DbContextOptions<DevLogDbContext> options) : DevLogDbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<Embedding>().Property(e => e.Vector)
                .HasConversion(vector => vector.ToString(), value => new Vector(value));
        }
    }

    private sealed class TenantAccessor : ICurrentTenantAccessor
    {
        public Guid? TenantId { get; } = Guid.NewGuid();
    }

    private sealed class StubEmbeddingProvider(Exception? failure = null) : IEmbeddingProvider
    {
        public Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken = default) =>
            failure is null ? Task.FromResult(new float[1536]) : Task.FromException<float[]>(failure);

        public Task<IReadOnlyList<float[]>> EmbedBatchAsync(
            IReadOnlyList<string> texts, CancellationToken cancellationToken = default) =>
            failure is null
                ? Task.FromResult<IReadOnlyList<float[]>>(texts.Select(_ => new float[1536]).ToArray())
                : Task.FromException<IReadOnlyList<float[]>>(failure);
    }

    private sealed class StubLlmProvider(Exception? failure = null) : ILlmProvider
    {
        public Task<SanitizationResult> SanitizeAsync(
            string rawContent, IReadOnlyList<string> blocklistTerms, CancellationToken cancellationToken = default) =>
            failure is null
                ? Task.FromResult(new SanitizationResult("Sanitized content.", "test"))
                : Task.FromException<SanitizationResult>(failure);

        public Task<string> AnswerQuestionAsync(
            string question, IReadOnlyList<string> contextChunks, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public Task<string> SuggestOnboardingContentAsync(
            string gitHubContext, bool anonymizeProjectNames = false, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public Task<string> SuggestOnboardingContentFromResumeAsync(
            string resumeText, bool anonymizeCompanyNames = false, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}

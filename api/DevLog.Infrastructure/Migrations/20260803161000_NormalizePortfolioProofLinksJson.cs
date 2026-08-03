using DevLog.Infrastructure;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DevLog.Infrastructure.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(DevLogDbContext))]
    [Migration("20260803161000_NormalizePortfolioProofLinksJson")]
    public partial class NormalizePortfolioProofLinksJson : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Some databases may already have run an earlier scaffolded form of
            // AddDisciplineAndProofLinksToPortfolio where proof_links was
            // backfilled as a JSON string instead of the JSON array required by
            // Portfolio.ProofLinks' List<ProofLink> converter. Normalize those
            // rows before reads try to materialize Portfolio again.
            migrationBuilder.Sql("""
                UPDATE portfolios
                SET proof_links = '[]'::jsonb
                WHERE proof_links IS NULL
                   OR jsonb_typeof(proof_links) <> 'array';
                """);

            migrationBuilder.Sql("""
                ALTER TABLE portfolios
                ALTER COLUMN proof_links SET DEFAULT '[]'::jsonb;
                """);

            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1
                        FROM pg_constraint
                        WHERE conname = 'ck_portfolios_proof_links_is_array'
                    ) THEN
                        ALTER TABLE portfolios
                        ADD CONSTRAINT ck_portfolios_proof_links_is_array
                        CHECK (jsonb_typeof(proof_links) = 'array');
                    END IF;
                END $$;
                """);

            // The same original scaffold issue could also have left the paired
            // discipline column as an empty string, which would fail enum
            // conversion immediately after proof_links is fixed.
            migrationBuilder.Sql("""
                UPDATE portfolios
                SET discipline = 'SoftwareEngineer'
                WHERE discipline IS NULL
                   OR discipline = '';
                """);

            migrationBuilder.Sql("""
                ALTER TABLE portfolios
                ALTER COLUMN discipline SET DEFAULT 'SoftwareEngineer';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE portfolios
                DROP CONSTRAINT IF EXISTS ck_portfolios_proof_links_is_array;
                """);
        }
    }
}

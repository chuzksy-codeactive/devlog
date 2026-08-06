using DevLog.Infrastructure;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DevLog.Infrastructure.Migrations
{
    [DbContext(typeof(DevLogDbContext))]
    [Migration("20260803161000_NormalizePortfolioProofLinksJson")]
    public partial class NormalizePortfolioProofLinksJson : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE portfolios
                DROP CONSTRAINT IF EXISTS ck_portfolios_proof_links_is_array;
                """);
        }
    }
}

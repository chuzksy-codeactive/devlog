using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DevLog.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDisciplineAndProofLinksToPortfolio : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // EF's migration scaffolder generates a column's default from the
            // CLR default of the *provider* type (empty string for `string`),
            // not by running the model's actual default (PortfolioDiscipline.
            // SoftwareEngineer / new List<ProofLink>()) through the value
            // converter — so the scaffolded defaultValue of "" is wrong for
            // both columns and corrupts every existing row on backfill.
            // "" isn't a valid PortfolioDiscipline enum name, and for the
            // jsonb column Npgsql writes it as the JSON string "\"\"" (valid
            // JSON, but a String token, not the Array token
            // List<ProofLink>'s converter expects) — both fail to
            // deserialize on the very next read. Corrected here to the real
            // provider-side defaults.
            migrationBuilder.AddColumn<string>(
                name: "discipline",
                table: "portfolios",
                type: "text",
                nullable: false,
                defaultValue: "SoftwareEngineer");

            migrationBuilder.AddColumn<string>(
                name: "proof_links",
                table: "portfolios",
                type: "jsonb",
                nullable: false,
                defaultValue: "[]");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "discipline",
                table: "portfolios");

            migrationBuilder.DropColumn(
                name: "proof_links",
                table: "portfolios");
        }
    }
}

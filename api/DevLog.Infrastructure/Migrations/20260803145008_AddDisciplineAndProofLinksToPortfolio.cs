using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DevLog.Infrastructure.Migrations
{
    public partial class AddDisciplineAndProofLinksToPortfolio : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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

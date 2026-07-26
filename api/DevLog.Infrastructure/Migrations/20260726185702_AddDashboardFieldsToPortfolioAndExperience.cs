using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DevLog.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDashboardFieldsToPortfolioAndExperience : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "display_name",
                table: "portfolios",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "photo_url",
                table: "portfolios",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "positioning_line",
                table: "portfolios",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "title",
                table: "experiences",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "display_name",
                table: "portfolios");

            migrationBuilder.DropColumn(
                name: "photo_url",
                table: "portfolios");

            migrationBuilder.DropColumn(
                name: "positioning_line",
                table: "portfolios");

            migrationBuilder.DropColumn(
                name: "title",
                table: "experiences");
        }
    }
}

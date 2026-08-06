using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DevLog.Infrastructure.Migrations
{
    public partial class AddGitHubVerifiedToPortfolio : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "git_hub_verified",
                table: "portfolios",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "git_hub_verified",
                table: "portfolios");
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DevLog.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantPrimaryEmail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "normalized_primary_email",
                table: "tenants",
                type: "character varying(320)",
                maxLength: 320,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "primary_email",
                table: "tenants",
                type: "character varying(320)",
                maxLength: 320,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_tenants_normalized_primary_email",
                table: "tenants",
                column: "normalized_primary_email",
                unique: true,
                filter: "normalized_primary_email IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_tenants_normalized_primary_email",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "normalized_primary_email",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "primary_email",
                table: "tenants");
        }
    }
}

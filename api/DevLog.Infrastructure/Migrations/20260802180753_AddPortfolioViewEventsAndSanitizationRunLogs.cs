using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DevLog.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPortfolioViewEventsAndSanitizationRunLogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "portfolio_view_events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    viewer_hash = table.Column<string>(type: "text", nullable: true),
                    viewed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_portfolio_view_events", x => x.id);
                    table.ForeignKey(
                        name: "fk_portfolio_view_events_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "sanitization_run_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sanitization_run_logs", x => x.id);
                    table.ForeignKey(
                        name: "fk_sanitization_run_logs_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_portfolio_view_events_tenant_id_viewed_at",
                table: "portfolio_view_events",
                columns: new[] { "tenant_id", "viewed_at" });

            migrationBuilder.CreateIndex(
                name: "ix_sanitization_run_logs_tenant_id_created_at",
                table: "sanitization_run_logs",
                columns: new[] { "tenant_id", "created_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "portfolio_view_events");

            migrationBuilder.DropTable(
                name: "sanitization_run_logs");
        }
    }
}

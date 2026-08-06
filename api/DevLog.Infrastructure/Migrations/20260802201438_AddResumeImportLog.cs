using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DevLog.Infrastructure.Migrations
{
    public partial class AddResumeImportLog : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "resume_import_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_resume_import_logs", x => x.id);
                    table.ForeignKey(
                        name: "fk_resume_import_logs_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_resume_import_logs_tenant_id_created_at",
                table: "resume_import_logs",
                columns: new[] { "tenant_id", "created_at" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "resume_import_logs");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DevLog.Infrastructure.Migrations
{
    public partial class InitialSchema : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "tenants",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    slug = table.Column<string>(type: "text", nullable: false),
                    plan_tier = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tenants", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "experiences",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    experience_type = table.Column<string>(type: "text", nullable: false),
                    company_label = table.Column<string>(type: "text", nullable: false),
                    start_date = table.Column<DateOnly>(type: "date", nullable: false),
                    end_date = table.Column<DateOnly>(type: "date", nullable: true),
                    technologies = table.Column<string>(type: "jsonb", nullable: false),
                    key_achievement = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_experiences", x => x.id);
                    table.ForeignKey(
                        name: "fk_experiences_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "portfolios",
                columns: table => new
                {
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    headline = table.Column<string>(type: "text", nullable: false),
                    current_role = table.Column<string>(type: "text", nullable: true),
                    years_experience = table.Column<string>(type: "text", nullable: true),
                    core_technologies = table.Column<string>(type: "jsonb", nullable: false),
                    linked_in_url = table.Column<string>(type: "text", nullable: true),
                    git_hub_url = table.Column<string>(type: "text", nullable: true),
                    cv_file_url = table.Column<string>(type: "text", nullable: true),
                    visibility = table.Column<string>(type: "text", nullable: false),
                    discoverable = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_portfolios", x => x.tenant_id);
                    table.ForeignKey(
                        name: "fk_portfolios_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "raw_entries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    entry_type = table.Column<string>(type: "text", nullable: false),
                    raw_content = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_raw_entries", x => x.id);
                    table.ForeignKey(
                        name: "fk_raw_entries_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "subscriptions",
                columns: table => new
                {
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    plan = table.Column<string>(type: "text", nullable: false),
                    ai_model_tier = table.Column<string>(type: "text", nullable: false),
                    sanitization_quota = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_subscriptions", x => x.tenant_id);
                    table.ForeignKey(
                        name: "fk_subscriptions_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "published_entries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    raw_entry_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sanitized_content = table.Column<string>(type: "text", nullable: false),
                    tags = table.Column<string>(type: "jsonb", nullable: false),
                    published_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_published_entries", x => x.id);
                    table.ForeignKey(
                        name: "fk_published_entries_raw_entries_raw_entry_id",
                        column: x => x.raw_entry_id,
                        principalTable: "raw_entries",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_published_entries_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "sanitization_audit",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    published_entry_id = table.Column<Guid>(type: "uuid", nullable: false),
                    model_version = table.Column<string>(type: "text", nullable: false),
                    diff = table.Column<string>(type: "text", nullable: false),
                    approved_by = table.Column<string>(type: "text", nullable: false),
                    approved_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sanitization_audit", x => x.id);
                    table.ForeignKey(
                        name: "fk_sanitization_audit_published_entries_published_entry_id",
                        column: x => x.published_entry_id,
                        principalTable: "published_entries",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_experiences_tenant_id",
                table: "experiences",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_published_entries_raw_entry_id",
                table: "published_entries",
                column: "raw_entry_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_published_entries_tenant_id",
                table: "published_entries",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_raw_entries_tenant_id",
                table: "raw_entries",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_sanitization_audit_published_entry_id",
                table: "sanitization_audit",
                column: "published_entry_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_tenants_slug",
                table: "tenants",
                column: "slug",
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "experiences");

            migrationBuilder.DropTable(
                name: "portfolios");

            migrationBuilder.DropTable(
                name: "sanitization_audit");

            migrationBuilder.DropTable(
                name: "subscriptions");

            migrationBuilder.DropTable(
                name: "published_entries");

            migrationBuilder.DropTable(
                name: "raw_entries");

            migrationBuilder.DropTable(
                name: "tenants");
        }
    }
}

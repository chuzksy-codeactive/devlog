using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Pgvector;

#nullable disable

namespace DevLog.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSanitizedPreviewToRawEntry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:vector", ",,");

            migrationBuilder.AddColumn<string>(
                name: "sanitization_model_version",
                table: "raw_entries",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "sanitized_preview",
                table: "raw_entries",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ai_query_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    question = table.Column<string>(type: "text", nullable: false),
                    asker_hash = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ai_query_logs", x => x.id);
                    table.ForeignKey(
                        name: "fk_ai_query_logs_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "embeddings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_type = table.Column<string>(type: "text", nullable: false),
                    source_id = table.Column<Guid>(type: "uuid", nullable: false),
                    chunk_index = table.Column<int>(type: "integer", nullable: false),
                    chunk_text = table.Column<string>(type: "text", nullable: false),
                    vector = table.Column<Vector>(type: "vector(1536)", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_embeddings", x => x.id);
                    table.ForeignKey(
                        name: "fk_embeddings_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_ai_query_logs_tenant_id_created_at",
                table: "ai_query_logs",
                columns: new[] { "tenant_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_embeddings_tenant_id_source_type_source_id",
                table: "embeddings",
                columns: new[] { "tenant_id", "source_type", "source_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ai_query_logs");

            migrationBuilder.DropTable(
                name: "embeddings");

            migrationBuilder.DropColumn(
                name: "sanitization_model_version",
                table: "raw_entries");

            migrationBuilder.DropColumn(
                name: "sanitized_preview",
                table: "raw_entries");

            migrationBuilder.AlterDatabase()
                .OldAnnotation("Npgsql:PostgresExtension:vector", ",,");
        }
    }
}

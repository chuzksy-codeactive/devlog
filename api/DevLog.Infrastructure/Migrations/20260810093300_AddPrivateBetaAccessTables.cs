using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DevLog.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPrivateBetaAccessTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "invite_allowlist",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    normalized_email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    invited_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    notes = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_invite_allowlist", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "waitlist_submissions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    normalized_email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    use_case = table.Column<string>(type: "character varying(1200)", maxLength: 1200, nullable: true),
                    referrer = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    status = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    reviewed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_waitlist_submissions", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_invite_allowlist_normalized_email",
                table: "invite_allowlist",
                column: "normalized_email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_invite_allowlist_status",
                table: "invite_allowlist",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_waitlist_submissions_normalized_email_created_at",
                table: "waitlist_submissions",
                columns: new[] { "normalized_email", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_waitlist_submissions_status_created_at",
                table: "waitlist_submissions",
                columns: new[] { "status", "created_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "invite_allowlist");

            migrationBuilder.DropTable(
                name: "waitlist_submissions");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DevLog.Infrastructure.Migrations
{
    public partial class AddEmailVerificationToInterviewRequests : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "email_verified",
                table: "interview_requests",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "verification_token",
                table: "interview_requests",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "verification_token_expires_at",
                table: "interview_requests",
                type: "timestamp with time zone",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "email_verified",
                table: "interview_requests");

            migrationBuilder.DropColumn(
                name: "verification_token",
                table: "interview_requests");

            migrationBuilder.DropColumn(
                name: "verification_token_expires_at",
                table: "interview_requests");
        }
    }
}

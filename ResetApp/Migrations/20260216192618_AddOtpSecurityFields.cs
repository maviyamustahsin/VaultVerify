using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BasicResetApp.Migrations
{
    /// <inheritdoc />
    public partial class AddOtpSecurityFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "OtpAttempts",
                table: "Users",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "OtpLockedUntil",
                table: "Users",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OtpAttempts",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "OtpLockedUntil",
                table: "Users");
        }
    }
}

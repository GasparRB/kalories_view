using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kalories.Server.Migrations
{
    /// <inheritdoc />
    public partial class UserConsent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ConsentAcceptedAt",
                table: "AspNetUsers",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ConsentVersion",
                table: "AspNetUsers",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ConsentAcceptedAt",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "ConsentVersion",
                table: "AspNetUsers");
        }
    }
}

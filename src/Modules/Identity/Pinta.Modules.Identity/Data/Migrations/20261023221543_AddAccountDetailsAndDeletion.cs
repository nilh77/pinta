using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pinta.Modules.Identity.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAccountDetailsAndDeletion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "anonymized_at",
                schema: "identity",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "company_name",
                schema: "identity",
                table: "users",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "deletion_requested_at",
                schema: "identity",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "tax_id",
                schema: "identity",
                table: "users",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "anonymized_at",
                schema: "identity",
                table: "users");

            migrationBuilder.DropColumn(
                name: "company_name",
                schema: "identity",
                table: "users");

            migrationBuilder.DropColumn(
                name: "deletion_requested_at",
                schema: "identity",
                table: "users");

            migrationBuilder.DropColumn(
                name: "tax_id",
                schema: "identity",
                table: "users");
        }
    }
}

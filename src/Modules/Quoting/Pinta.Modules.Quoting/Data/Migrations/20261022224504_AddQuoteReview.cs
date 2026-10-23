using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pinta.Modules.Quoting.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddQuoteReview : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "confirmed_at",
                schema: "quoting",
                table: "quote_versions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "confirmed_by",
                schema: "quoting",
                table: "quote_versions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "note",
                schema: "quoting",
                table: "quote_versions",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "premium_override",
                schema: "quoting",
                table: "quote_versions",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "catalog_key",
                schema: "quoting",
                table: "quote_extras",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "name",
                schema: "quoting",
                table: "quote_extras",
                type: "jsonb",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "confirmed_at",
                schema: "quoting",
                table: "quote_versions");

            migrationBuilder.DropColumn(
                name: "confirmed_by",
                schema: "quoting",
                table: "quote_versions");

            migrationBuilder.DropColumn(
                name: "note",
                schema: "quoting",
                table: "quote_versions");

            migrationBuilder.DropColumn(
                name: "premium_override",
                schema: "quoting",
                table: "quote_versions");

            migrationBuilder.DropColumn(
                name: "catalog_key",
                schema: "quoting",
                table: "quote_extras");

            migrationBuilder.DropColumn(
                name: "name",
                schema: "quoting",
                table: "quote_extras");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pinta.Modules.Jobs.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSiteVisitReport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "site_visit_notes",
                schema: "jobs",
                table: "jobs",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "site_visit_reported_at",
                schema: "jobs",
                table: "jobs",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "site_visit_notes",
                schema: "jobs",
                table: "jobs");

            migrationBuilder.DropColumn(
                name: "site_visit_reported_at",
                schema: "jobs",
                table: "jobs");
        }
    }
}

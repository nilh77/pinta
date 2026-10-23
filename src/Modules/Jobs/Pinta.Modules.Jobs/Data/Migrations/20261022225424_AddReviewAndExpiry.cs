using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pinta.Modules.Jobs.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddReviewAndExpiry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "quote_valid_until",
                schema: "jobs",
                table: "jobs",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "site_visit_assignee_id",
                schema: "jobs",
                table: "jobs",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "site_visit_at",
                schema: "jobs",
                table: "jobs",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "site_visit_price",
                schema: "jobs",
                table: "jobs",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_jobs_status_quote_valid_until",
                schema: "jobs",
                table: "jobs",
                columns: new[] { "status", "quote_valid_until" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_jobs_status_quote_valid_until",
                schema: "jobs",
                table: "jobs");

            migrationBuilder.DropColumn(
                name: "quote_valid_until",
                schema: "jobs",
                table: "jobs");

            migrationBuilder.DropColumn(
                name: "site_visit_assignee_id",
                schema: "jobs",
                table: "jobs");

            migrationBuilder.DropColumn(
                name: "site_visit_at",
                schema: "jobs",
                table: "jobs");

            migrationBuilder.DropColumn(
                name: "site_visit_price",
                schema: "jobs",
                table: "jobs");
        }
    }
}

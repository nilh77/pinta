using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pinta.Modules.Quoting.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSiteVisitCredit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "site_visit_credit",
                schema: "quoting",
                table: "quote_versions",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "site_visit_credit",
                schema: "quoting",
                table: "quote_versions");
        }
    }
}

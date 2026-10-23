using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pinta.Modules.Catalog.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCompanyDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "company_address",
                schema: "catalog",
                table: "settings",
                type: "character varying(300)",
                maxLength: 300,
                nullable: false,
                defaultValue: "Avenida da Liberdade, 100, 1250-146 Lisboa");

            migrationBuilder.AddColumn<string>(
                name: "company_name",
                schema: "catalog",
                table: "settings",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "Pinta · NHIT-DEV");

            migrationBuilder.AddColumn<string>(
                name: "company_tax_id",
                schema: "catalog",
                table: "settings",
                type: "character varying(9)",
                maxLength: 9,
                nullable: false,
                defaultValue: "999999990");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "company_address",
                schema: "catalog",
                table: "settings");

            migrationBuilder.DropColumn(
                name: "company_name",
                schema: "catalog",
                table: "settings");

            migrationBuilder.DropColumn(
                name: "company_tax_id",
                schema: "catalog",
                table: "settings");
        }
    }
}

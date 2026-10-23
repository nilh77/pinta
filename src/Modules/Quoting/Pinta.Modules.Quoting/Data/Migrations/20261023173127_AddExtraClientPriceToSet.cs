using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pinta.Modules.Quoting.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddExtraClientPriceToSet : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "client_price_to_set",
                schema: "quoting",
                table: "quote_extras",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "client_price_to_set",
                schema: "quoting",
                table: "quote_extras");
        }
    }
}

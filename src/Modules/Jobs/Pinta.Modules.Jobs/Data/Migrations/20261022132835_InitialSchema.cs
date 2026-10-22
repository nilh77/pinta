using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pinta.Modules.Jobs.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(name: "jobs");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The schema also holds the migrations history table, so it is kept.
        }
    }
}

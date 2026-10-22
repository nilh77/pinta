using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pinta.Modules.Catalog.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddWaitlistAndLeadTime : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "min_lead_business_days",
                schema: "catalog",
                table: "settings",
                type: "integer",
                nullable: false,
                defaultValue: 3);

            migrationBuilder.CreateTable(
                name: "waitlist_entries",
                schema: "catalog",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    postal_code = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    locale = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_waitlist_entries", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_waitlist_entries_email_postal_code",
                schema: "catalog",
                table: "waitlist_entries",
                columns: new[] { "email", "postal_code" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "waitlist_entries",
                schema: "catalog");

            migrationBuilder.DropColumn(
                name: "min_lead_business_days",
                schema: "catalog",
                table: "settings");
        }
    }
}

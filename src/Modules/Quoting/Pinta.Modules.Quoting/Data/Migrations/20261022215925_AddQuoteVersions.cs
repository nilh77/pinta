using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pinta.Modules.Quoting.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddQuoteVersions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "quoting");

            migrationBuilder.CreateTable(
                name: "quote_versions",
                schema: "quoting",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    job_id = table.Column<Guid>(type: "uuid", nullable: false),
                    number = table.Column<int>(type: "integer", nullable: false),
                    origin = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    price_snapshot = table.Column<string>(type: "jsonb", nullable: false),
                    occupancy = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    area_m2 = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    material_cost = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    labour_price = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    subtotal = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    vat = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    total = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    painter_pay = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    margin = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    range_min = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    range_max = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    minimum_applied = table.Column<bool>(type: "boolean", nullable: false),
                    over_max = table.Column<bool>(type: "boolean", nullable: false),
                    premium = table.Column<bool>(type: "boolean", nullable: false),
                    manual_adjustment = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    adjustment_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    valid_until = table.Column<DateOnly>(type: "date", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_quote_versions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "quote_elements",
                schema: "quoting",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    quote_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    position = table.Column<int>(type: "integer", nullable: false),
                    client_key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    kind = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    room_type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    surface_type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    size = table.Column<string>(type: "character varying(1)", maxLength: 1, nullable: true),
                    length = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: true),
                    width = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: true),
                    height = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: true),
                    wall_a = table.Column<bool>(type: "boolean", nullable: false),
                    wall_b = table.Column<bool>(type: "boolean", nullable: false),
                    wall_c = table.Column<bool>(type: "boolean", nullable: false),
                    wall_d = table.Column<bool>(type: "boolean", nullable: false),
                    ceiling = table.Column<bool>(type: "boolean", nullable: false),
                    paint = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    has_damage = table.Column<bool>(type: "boolean", nullable: false),
                    area_m2 = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_quote_elements", x => x.id);
                    table.ForeignKey(
                        name: "fk_quote_elements_quote_versions_quote_version_id",
                        column: x => x.quote_version_id,
                        principalSchema: "quoting",
                        principalTable: "quote_versions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "quote_extras",
                schema: "quoting",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    quote_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    client_price = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    painter_pay = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_quote_extras", x => x.id);
                    table.ForeignKey(
                        name: "fk_quote_extras_quote_versions_quote_version_id",
                        column: x => x.quote_version_id,
                        principalSchema: "quoting",
                        principalTable: "quote_versions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_quote_elements_quote_version_id",
                schema: "quoting",
                table: "quote_elements",
                column: "quote_version_id");

            migrationBuilder.CreateIndex(
                name: "ix_quote_extras_quote_version_id",
                schema: "quoting",
                table: "quote_extras",
                column: "quote_version_id");

            migrationBuilder.CreateIndex(
                name: "ix_quote_versions_job_id_number",
                schema: "quoting",
                table: "quote_versions",
                columns: new[] { "job_id", "number" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "quote_elements",
                schema: "quoting");

            migrationBuilder.DropTable(
                name: "quote_extras",
                schema: "quoting");

            migrationBuilder.DropTable(
                name: "quote_versions",
                schema: "quoting");
        }
    }
}

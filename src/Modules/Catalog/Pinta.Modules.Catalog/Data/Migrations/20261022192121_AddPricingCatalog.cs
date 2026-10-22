using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pinta.Modules.Catalog.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPricingCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "catalog");

            migrationBuilder.CreateTable(
                name: "coverage_areas",
                schema: "catalog",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    postal_prefix = table.Column<string>(type: "character(4)", fixedLength: true, maxLength: 4, nullable: false),
                    municipality = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_coverage_areas", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "extra_items",
                schema: "catalog",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    client_price = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    painter_pay = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    key = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    name = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_extra_items", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "modifiers",
                schema: "catalog",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    group = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    factor = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    key = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    name = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_modifiers", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "paints",
                schema: "catalog",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    price_per_litre = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    coverage_m2per_litre = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    coats = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    key = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    name = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_paints", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "settings",
                schema: "catalog",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    waste_pct = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    vat_pct = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    min_job_amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    max_job_amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    premium_area_m2 = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    range_low_pct = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    range_high_pct = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    range_size_widening_pct = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    range_damage_widening_pct = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    deposit_pct = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    min_margin_pct = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    default_height = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    quote_validity_days = table.Column<int>(type: "integer", nullable: false),
                    site_visit_price = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    site_visit_deductible = table.Column<bool>(type: "boolean", nullable: false),
                    review_deadline_business_days = table.Column<int>(type: "integer", nullable: false),
                    full_refund_min_days = table.Column<int>(type: "integer", nullable: false),
                    late_refund_pct = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    max_photos_per_element = table.Column<int>(type: "integer", nullable: false),
                    max_videos_per_element = table.Column<int>(type: "integer", nullable: false),
                    max_image_size_mb = table.Column<int>(type: "integer", nullable: false),
                    max_video_size_mb = table.Column<int>(type: "integer", nullable: false),
                    client_phone_visible_to_painter = table.Column<bool>(type: "boolean", nullable: false),
                    painter_phone_visible_to_client = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_settings", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "surface_types",
                schema: "catalog",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    client_rate_per_m2 = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    painter_rate_per_m2 = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    difficulty_factor = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    key = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    name = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_surface_types", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "room_types",
                schema: "catalog",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    suggested_paint_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    key = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    name = table.Column<string>(type: "jsonb", nullable: false),
                    sizes = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_room_types", x => x.id);
                    table.ForeignKey(
                        name: "fk_room_types_paints_suggested_paint_id",
                        column: x => x.suggested_paint_id,
                        principalSchema: "catalog",
                        principalTable: "paints",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_coverage_areas_postal_prefix",
                schema: "catalog",
                table: "coverage_areas",
                column: "postal_prefix",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_extra_items_key",
                schema: "catalog",
                table: "extra_items",
                column: "key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_modifiers_group_key",
                schema: "catalog",
                table: "modifiers",
                columns: new[] { "group", "key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_paints_key",
                schema: "catalog",
                table: "paints",
                column: "key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_room_types_key",
                schema: "catalog",
                table: "room_types",
                column: "key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_room_types_suggested_paint_id",
                schema: "catalog",
                table: "room_types",
                column: "suggested_paint_id");

            migrationBuilder.CreateIndex(
                name: "ix_surface_types_key",
                schema: "catalog",
                table: "surface_types",
                column: "key",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "coverage_areas",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "extra_items",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "modifiers",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "room_types",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "settings",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "surface_types",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "paints",
                schema: "catalog");
        }
    }
}

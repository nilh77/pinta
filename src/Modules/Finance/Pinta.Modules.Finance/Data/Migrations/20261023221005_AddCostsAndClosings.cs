using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pinta.Modules.Finance.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCostsAndClosings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "monthly_closings",
                schema: "finance",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    period = table.Column<DateOnly>(type: "date", nullable: false),
                    income = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    vat = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    material = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    painter_pay = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    gross_margin = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    costs = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    profit = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    per_partner = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    closed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    closed_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_monthly_closings", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "operating_costs",
                schema: "finance",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    category = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    closing_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_operating_costs", x => x.id);
                    table.ForeignKey(
                        name: "fk_operating_costs_monthly_closings_closing_id",
                        column: x => x.closing_id,
                        principalSchema: "finance",
                        principalTable: "monthly_closings",
                        principalColumn: "id");
                });

            migrationBuilder.CreateIndex(
                name: "ix_monthly_closings_period",
                schema: "finance",
                table: "monthly_closings",
                column: "period",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_operating_costs_closing_id",
                schema: "finance",
                table: "operating_costs",
                column: "closing_id");

            migrationBuilder.CreateIndex(
                name: "ix_operating_costs_date",
                schema: "finance",
                table: "operating_costs",
                column: "date");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "operating_costs",
                schema: "finance");

            migrationBuilder.DropTable(
                name: "monthly_closings",
                schema: "finance");
        }
    }
}

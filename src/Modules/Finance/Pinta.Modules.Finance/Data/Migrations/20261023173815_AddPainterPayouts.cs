using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pinta.Modules.Finance.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPainterPayouts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "finance");

            migrationBuilder.CreateTable(
                name: "painter_payouts",
                schema: "finance",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    painter_id = table.Column<Guid>(type: "uuid", nullable: false),
                    job_id = table.Column<Guid>(type: "uuid", nullable: false),
                    job_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    paid_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_painter_payouts", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_painter_payouts_job_id",
                schema: "finance",
                table: "painter_payouts",
                column: "job_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_painter_payouts_painter_id_status",
                schema: "finance",
                table: "painter_payouts",
                columns: new[] { "painter_id", "status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "painter_payouts",
                schema: "finance");
        }
    }
}

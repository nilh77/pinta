using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pinta.Modules.Jobs.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAssignments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "assignments",
                schema: "jobs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    job_id = table.Column<Guid>(type: "uuid", nullable: false),
                    painter_id = table.Column<Guid>(type: "uuid", nullable: false),
                    scheduled_on = table.Column<DateOnly>(type: "date", nullable: false),
                    painter_pay = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    decline_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    offered_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    responded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    reminder_sent_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_assignments", x => x.id);
                    table.ForeignKey(
                        name: "fk_assignments_jobs_job_id",
                        column: x => x.job_id,
                        principalSchema: "jobs",
                        principalTable: "jobs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_assignments_job_id_status",
                schema: "jobs",
                table: "assignments",
                columns: new[] { "job_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_assignments_painter_id_status",
                schema: "jobs",
                table: "assignments",
                columns: new[] { "painter_id", "status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "assignments",
                schema: "jobs");
        }
    }
}

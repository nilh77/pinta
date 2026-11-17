using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pinta.Modules.Jobs.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddJobActivity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "job_activity",
                schema: "jobs",
                columns: table => new
                {
                    job_id = table.Column<Guid>(type: "uuid", nullable: false),
                    last_activity_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_job_activity", x => x.job_id);
                    table.ForeignKey(
                        name: "fk_job_activity_jobs_job_id",
                        column: x => x.job_id,
                        principalSchema: "jobs",
                        principalTable: "jobs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_job_activity_last_activity_at",
                schema: "jobs",
                table: "job_activity",
                column: "last_activity_at");

            // Existing jobs start from their latest state change, offer, answer or incident.
            migrationBuilder.Sql("""
                INSERT INTO jobs.job_activity (job_id, last_activity_at)
                SELECT j.id, GREATEST(
                    j.updated_at,
                    COALESCE((SELECT MAX(COALESCE(a.responded_at, a.offered_at)) FROM jobs.assignments a WHERE a.job_id = j.id), j.updated_at),
                    COALESCE((SELECT MAX(i.created_at) FROM jobs.incidents i WHERE i.job_id = j.id), j.updated_at))
                FROM jobs.jobs j;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "job_activity",
                schema: "jobs");
        }
    }
}

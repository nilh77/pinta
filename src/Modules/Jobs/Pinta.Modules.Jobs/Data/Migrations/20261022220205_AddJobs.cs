using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pinta.Modules.Jobs.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddJobs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "jobs");

            migrationBuilder.CreateTable(
                name: "code_counters",
                schema: "jobs",
                columns: table => new
                {
                    year = table.Column<int>(type: "integer", nullable: false),
                    last = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_code_counters", x => x.year);
                });

            migrationBuilder.CreateTable(
                name: "jobs",
                schema: "jobs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    client_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    address_line1 = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    floor = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    postal_code = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    locality = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    municipality = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    access_notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    preferred_asap = table.Column<bool>(type: "boolean", nullable: false),
                    preferred_from = table.Column<DateOnly>(type: "date", nullable: true),
                    preferred_to = table.Column<DateOnly>(type: "date", nullable: true),
                    is_premium = table.Column<bool>(type: "boolean", nullable: false),
                    requires_site_visit = table.Column<bool>(type: "boolean", nullable: false),
                    tax_id = table.Column<string>(type: "character varying(9)", maxLength: 9, nullable: true),
                    company_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    current_quote_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    review_due_on = table.Column<DateOnly>(type: "date", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_jobs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "status_history",
                schema: "jobs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    job_id = table.Column<Guid>(type: "uuid", nullable: false),
                    from = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    to = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    actor = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    changed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_status_history", x => x.id);
                    table.ForeignKey(
                        name: "fk_status_history_jobs_job_id",
                        column: x => x.job_id,
                        principalSchema: "jobs",
                        principalTable: "jobs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_jobs_client_id_status",
                schema: "jobs",
                table: "jobs",
                columns: new[] { "client_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_jobs_code",
                schema: "jobs",
                table: "jobs",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_status_history_job_id",
                schema: "jobs",
                table: "status_history",
                column: "job_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "code_counters",
                schema: "jobs");

            migrationBuilder.DropTable(
                name: "status_history",
                schema: "jobs");

            migrationBuilder.DropTable(
                name: "jobs",
                schema: "jobs");
        }
    }
}

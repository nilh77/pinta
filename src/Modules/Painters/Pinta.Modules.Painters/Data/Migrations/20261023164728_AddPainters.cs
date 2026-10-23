using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pinta.Modules.Painters.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPainters : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "painters");

            migrationBuilder.CreateTable(
                name: "painters",
                schema: "painters",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    phone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    tax_id = table.Column<string>(type: "character varying(9)", maxLength: 9, nullable: true),
                    specialties = table.Column<List<string>>(type: "text[]", nullable: false),
                    municipalities = table.Column<List<string>>(type: "text[]", nullable: false),
                    iban_encrypted = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    iban_last4 = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: true),
                    notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    rating = table.Column<decimal>(type: "numeric(2,1)", precision: 2, scale: 1, nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_painters", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "documents",
                schema: "painters",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    painter_id = table.Column<Guid>(type: "uuid", nullable: false),
                    file_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    expires_on = table.Column<DateOnly>(type: "date", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_documents", x => x.id);
                    table.ForeignKey(
                        name: "fk_documents_painters_painter_id",
                        column: x => x.painter_id,
                        principalSchema: "painters",
                        principalTable: "painters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_documents_painter_id",
                schema: "painters",
                table: "documents",
                column: "painter_id");

            migrationBuilder.CreateIndex(
                name: "ix_painters_email",
                schema: "painters",
                table: "painters",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_painters_user_id",
                schema: "painters",
                table: "painters",
                column: "user_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "documents",
                schema: "painters");

            migrationBuilder.DropTable(
                name: "painters",
                schema: "painters");
        }
    }
}

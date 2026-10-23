using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pinta.Modules.Media.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPainterFiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "painter_id",
                schema: "media",
                table: "files",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_files_painter_id",
                schema: "media",
                table: "files",
                column: "painter_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_files_painter_id",
                schema: "media",
                table: "files");

            migrationBuilder.DropColumn(
                name: "painter_id",
                schema: "media",
                table: "files");
        }
    }
}

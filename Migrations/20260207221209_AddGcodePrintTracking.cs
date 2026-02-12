using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PrintVault3D.Migrations
{
    /// <inheritdoc />
    public partial class AddGcodePrintTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Collections_Categories_CategoryId",
                table: "Collections");

            migrationBuilder.DropColumn(
                name: "PrintTime",
                table: "Gcodes");

            /*
            migrationBuilder.AddColumn<long>(
                name: "ActualPrintTimeTicks",
                table: "Gcodes",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PrintStatus",
                table: "Gcodes",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Rating",
                table: "Gcodes",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "TagLearnings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Pattern = table.Column<string>(type: "TEXT", nullable: false),
                    LearnedCategory = table.Column<string>(type: "TEXT", nullable: true),
                    LearnedTags = table.Column<string>(type: "TEXT", nullable: true),
                    UseCount = table.Column<int>(type: "INTEGER", nullable: false),
                    LastUsed = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TagLearnings", x => x.Id);
                });
            */

            migrationBuilder.AddForeignKey(
                name: "FK_Collections_Categories_CategoryId",
                table: "Collections",
                column: "CategoryId",
                principalTable: "Categories",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Collections_Categories_CategoryId",
                table: "Collections");

            migrationBuilder.DropTable(
                name: "TagLearnings");

            migrationBuilder.DropColumn(
                name: "ActualPrintTimeTicks",
                table: "Gcodes");

            migrationBuilder.DropColumn(
                name: "PrintStatus",
                table: "Gcodes");

            migrationBuilder.DropColumn(
                name: "Rating",
                table: "Gcodes");

            migrationBuilder.AddColumn<TimeSpan>(
                name: "PrintTime",
                table: "Gcodes",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Collections_Categories_CategoryId",
                table: "Collections",
                column: "CategoryId",
                principalTable: "Categories",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}

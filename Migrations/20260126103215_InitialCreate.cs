using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace PrintVault3D.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Categories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    AutoKeywords = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Categories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Models",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    CategoryId = table.Column<int>(type: "INTEGER", nullable: true),
                    SourceUrl = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    FilePath = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    ThumbnailPath = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    OriginalFileName = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    FileSize = table.Column<long>(type: "INTEGER", nullable: false),
                    FileType = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    AddedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ThumbnailGenerated = table.Column<bool>(type: "INTEGER", nullable: false),
                    Tags = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    IsFavorite = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Models", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Models_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Gcodes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ModelId = table.Column<int>(type: "INTEGER", nullable: true),
                    FilePath = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    OriginalFileName = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    PrintTimeTicks = table.Column<long>(type: "INTEGER", nullable: true),
                    FilamentWeight = table.Column<double>(type: "REAL", nullable: true),
                    FilamentLength = table.Column<double>(type: "REAL", nullable: true),
                    SlicerName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    SlicerVersion = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    LayerHeight = table.Column<double>(type: "REAL", nullable: true),
                    InfillPercentage = table.Column<int>(type: "INTEGER", nullable: true),
                    NozzleTemp = table.Column<int>(type: "INTEGER", nullable: true),
                    BedTemp = table.Column<int>(type: "INTEGER", nullable: true),
                    FileSize = table.Column<long>(type: "INTEGER", nullable: false),
                    AddedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    LinkConfidence = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Gcodes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Gcodes_Models_ModelId",
                        column: x => x.ModelId,
                        principalTable: "Models",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.InsertData(
                table: "Categories",
                columns: new[] { "Id", "AutoKeywords", "Description", "Name" },
                values: new object[,]
                {
                    { 1, null, "Default category for new models", "Uncategorized" },
                    { 2, "benchy,calibration,test,cube,temp,tower", "Calibration prints", "Calibration" },
                    { 3, "mount,bracket,holder,clip,hook,hinge,gear", "Functional and mechanical parts", "Functional Parts" },
                    { 4, "toy,game,figure,figurine,miniature,dice", "Toys, games, and figurines", "Toys & Games" },
                    { 5, "vase,art,decoration,decor,ornament,statue,bust", "Decorative items and art pieces", "Art & Decoration" },
                    { 6, "tool,wrench,screwdriver,organizer,box,tray", "Tools and utility items", "Tools" },
                    { 7, "case,enclosure,raspberry,arduino,esp32,stand,dock", "Electronics enclosures and mounts", "Electronics" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Categories_Name",
                table: "Categories",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Gcodes_AddedDate",
                table: "Gcodes",
                column: "AddedDate");

            migrationBuilder.CreateIndex(
                name: "IX_Gcodes_FilePath",
                table: "Gcodes",
                column: "FilePath",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Gcodes_ModelId",
                table: "Gcodes",
                column: "ModelId");

            migrationBuilder.CreateIndex(
                name: "IX_Models_AddedDate",
                table: "Models",
                column: "AddedDate");

            migrationBuilder.CreateIndex(
                name: "IX_Models_CategoryId",
                table: "Models",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Models_FilePath",
                table: "Models",
                column: "FilePath",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Models_IsFavorite",
                table: "Models",
                column: "IsFavorite");

            migrationBuilder.CreateIndex(
                name: "IX_Models_Name",
                table: "Models",
                column: "Name");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Gcodes");

            migrationBuilder.DropTable(
                name: "Models");

            migrationBuilder.DropTable(
                name: "Categories");
        }
    }
}

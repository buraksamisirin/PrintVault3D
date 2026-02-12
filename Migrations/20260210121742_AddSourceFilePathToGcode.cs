using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PrintVault3D.Migrations
{
    /// <inheritdoc />
    public partial class AddSourceFilePathToGcode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FileHash",
                table: "Gcodes",
                type: "TEXT",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceFilePath",
                table: "Gcodes",
                type: "TEXT",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Models_FileHash",
                table: "Models",
                column: "FileHash");

            migrationBuilder.CreateIndex(
                name: "IX_Gcodes_FileHash",
                table: "Gcodes",
                column: "FileHash");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Models_FileHash",
                table: "Models");

            migrationBuilder.DropIndex(
                name: "IX_Gcodes_FileHash",
                table: "Gcodes");

            migrationBuilder.DropColumn(
                name: "FileHash",
                table: "Gcodes");

            migrationBuilder.DropColumn(
                name: "SourceFilePath",
                table: "Gcodes");
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PrintVault3D.Migrations
{
    /// <inheritdoc />
    public partial class AddSourceFilePath : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SourceFilePath",
                table: "Models",
                type: "TEXT",
                maxLength: 1000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SourceFilePath",
                table: "Models");
        }
    }
}

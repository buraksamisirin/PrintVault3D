using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PrintVault3D.Migrations
{
    /// <inheritdoc />
    public partial class AddFileHashToModel3D : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FileHash",
                table: "Models",
                type: "TEXT",
                maxLength: 44,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FileHash",
                table: "Models");
        }
    }
}

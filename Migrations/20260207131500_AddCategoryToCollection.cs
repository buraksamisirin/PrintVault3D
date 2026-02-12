using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PrintVault3D.Migrations
{
    /// <inheritdoc />
    public partial class AddCategoryToCollection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CategoryId",
                table: "Collections",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Collections_CategoryId",
                table: "Collections",
                column: "CategoryId");

            migrationBuilder.AddForeignKey(
                name: "FK_Collections_Categories_CategoryId",
                table: "Collections",
                column: "CategoryId",
                principalTable: "Categories",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Collections_Categories_CategoryId",
                table: "Collections");

            migrationBuilder.DropIndex(
                name: "IX_Collections_CategoryId",
                table: "Collections");

            migrationBuilder.DropColumn(
                name: "CategoryId",
                table: "Collections");
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PizzaDeli.Migrations
{
    /// <inheritdoc />
    public partial class AddImageEmbedding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ImageEmbedding",
                table: "Products",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ImageEmbedding",
                table: "Products");
        }
    }
}

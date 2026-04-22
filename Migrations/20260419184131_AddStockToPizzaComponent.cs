using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PizzaDeli.Migrations
{
    /// <inheritdoc />
    public partial class AddStockToPizzaComponent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Stock",
                table: "PizzaComponents",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Stock",
                table: "PizzaComponents");
        }
    }
}

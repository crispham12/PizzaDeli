using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PizzaDeli.Migrations
{
    /// <inheritdoc />
    public partial class UpdateToppingModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "Toppings",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "Toppings");
        }
    }
}

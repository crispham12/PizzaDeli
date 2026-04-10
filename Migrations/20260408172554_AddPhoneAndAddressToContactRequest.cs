using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PizzaDeli.Migrations
{
    /// <inheritdoc />
    public partial class AddPhoneAndAddressToContactRequest : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Address",
                table: "ContactRequests",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Phone",
                table: "ContactRequests",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Address",
                table: "ContactRequests");

            migrationBuilder.DropColumn(
                name: "Phone",
                table: "ContactRequests");
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FoodLoop.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAdminNotesToStoreAndVerification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ReviewNote",
                table: "StoreVerifications",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AdminNote",
                table: "Stores",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReviewNote",
                table: "StoreVerifications");

            migrationBuilder.DropColumn(
                name: "AdminNote",
                table: "Stores");
        }
    }
}

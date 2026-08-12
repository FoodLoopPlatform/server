using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FoodLoop.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UpdateExpiryVerificationStateEnum : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE Products SET ExpiryVerificationState = '0' WHERE ExpiryVerificationState = 'Manual' OR ExpiryVerificationState IS NULL;");
            migrationBuilder.Sql("UPDATE Products SET ExpiryVerificationState = '1' WHERE ExpiryVerificationState = 'AiVerified';");
            migrationBuilder.Sql("UPDATE Products SET ExpiryVerificationState = '2' WHERE ExpiryVerificationState = 'AiLowConfidence';");

            migrationBuilder.AlterColumn<int>(
                name: "ExpiryVerificationState",
                table: "Products",
                type: "int",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "ExpiryVerificationState",
                table: "Products",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");
        }
    }
}

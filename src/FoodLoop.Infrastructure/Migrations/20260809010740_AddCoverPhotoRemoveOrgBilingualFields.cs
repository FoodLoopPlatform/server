using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FoodLoop.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCoverPhotoRemoveOrgBilingualFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NameAr",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "DescriptionAr",
                table: "Organizations");

            migrationBuilder.AddColumn<string>(
                name: "CoverPhoto",
                table: "Organizations",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CoverPhoto",
                table: "Organizations");

            migrationBuilder.AddColumn<string>(
                name: "DescriptionAr",
                table: "Organizations",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NameAr",
                table: "Organizations",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);
        }
    }
}

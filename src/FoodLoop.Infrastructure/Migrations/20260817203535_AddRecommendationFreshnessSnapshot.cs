using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FoodLoop.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRecommendationFreshnessSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "SnapshotOriginalPrice",
                table: "AiRiskAssessments",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SnapshotProductStatus",
                table: "AiRiskAssessments",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SnapshotQuantityAvailable",
                table: "AiRiskAssessments",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "SnapshotOriginalPrice",
                table: "AiPricingRecommendations",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SnapshotProductStatus",
                table: "AiPricingRecommendations",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SnapshotQuantityAvailable",
                table: "AiPricingRecommendations",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SnapshotOriginalPrice",
                table: "AiRiskAssessments");

            migrationBuilder.DropColumn(
                name: "SnapshotProductStatus",
                table: "AiRiskAssessments");

            migrationBuilder.DropColumn(
                name: "SnapshotQuantityAvailable",
                table: "AiRiskAssessments");

            migrationBuilder.DropColumn(
                name: "SnapshotOriginalPrice",
                table: "AiPricingRecommendations");

            migrationBuilder.DropColumn(
                name: "SnapshotProductStatus",
                table: "AiPricingRecommendations");

            migrationBuilder.DropColumn(
                name: "SnapshotQuantityAvailable",
                table: "AiPricingRecommendations");
        }
    }
}

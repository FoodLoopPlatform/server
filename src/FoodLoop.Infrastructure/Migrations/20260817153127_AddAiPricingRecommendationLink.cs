using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FoodLoop.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAiPricingRecommendationLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "RiskAssessmentId",
                table: "AiPricingRecommendations",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AiPricingRecommendations_RiskAssessmentId",
                table: "AiPricingRecommendations",
                column: "RiskAssessmentId",
                unique: true,
                filter: "[RiskAssessmentId] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_AiPricingRecommendations_AiRiskAssessments_RiskAssessmentId",
                table: "AiPricingRecommendations",
                column: "RiskAssessmentId",
                principalTable: "AiRiskAssessments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AiPricingRecommendations_AiRiskAssessments_RiskAssessmentId",
                table: "AiPricingRecommendations");

            migrationBuilder.DropIndex(
                name: "IX_AiPricingRecommendations_RiskAssessmentId",
                table: "AiPricingRecommendations");

            migrationBuilder.DropColumn(
                name: "RiskAssessmentId",
                table: "AiPricingRecommendations");
        }
    }
}

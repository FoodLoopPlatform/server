using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FoodLoop.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSystemSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SystemSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MaxDiscountPerCyclePercent = table.Column<int>(type: "int", nullable: false),
                    DefaultPriceFloorPolicy = table.Column<int>(type: "int", nullable: false),
                    NewBusinessDefaultAutomationMode = table.Column<int>(type: "int", nullable: false),
                    AutoVerifyPartnerStores = table.Column<bool>(type: "bit", nullable: false),
                    BulkProductUploadEnabled = table.Column<bool>(type: "bit", nullable: false),
                    PlatformCommissionPercent = table.Column<int>(type: "int", nullable: false),
                    ApiRequestRateLimitPerMinute = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SystemSettings", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "SystemSettings",
                columns: new[] { "Id", "ApiRequestRateLimitPerMinute", "AutoVerifyPartnerStores", "BulkProductUploadEnabled", "CreatedAt", "CreatedBy", "DefaultPriceFloorPolicy", "MaxDiscountPerCyclePercent", "NewBusinessDefaultAutomationMode", "PlatformCommissionPercent", "UpdatedAt", "UpdatedBy" },
                values: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), 120, false, true, new DateTimeOffset(new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, 0, 10, 1, 10, null, null });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SystemSettings");
        }
    }
}

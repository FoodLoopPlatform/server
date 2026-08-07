using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FoodLoop.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPriceHistoryDonationProductReport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AiAutoDiscountDaysBeforeExpiry",
                table: "Organizations",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "AiAutoDiscountEnabled",
                table: "Organizations",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "AiAutoDiscountPercent",
                table: "Organizations",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "AiAutoPricingEnabled",
                table: "Organizations",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "Donations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DonorOrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RecipientOrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Donations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Donations_Organizations_DonorOrganizationId",
                        column: x => x.DonorOrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Donations_Organizations_RecipientOrganizationId",
                        column: x => x.RecipientOrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Donations_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PriceHistories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OldOriginalPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    OldDiscountedPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    NewOriginalPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    NewDiscountedPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ChangeReason = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ChangedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PriceHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PriceHistories_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProductReports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReportedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Details = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsResolved = table.Column<bool>(type: "bit", nullable: false),
                    AdminNote = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ResolvedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductReports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductReports_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Donations_DonorOrganizationId",
                table: "Donations",
                column: "DonorOrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_Donations_ProductId",
                table: "Donations",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_Donations_RecipientOrganizationId",
                table: "Donations",
                column: "RecipientOrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_PriceHistories_ProductId",
                table: "PriceHistories",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductReports_ProductId",
                table: "ProductReports",
                column: "ProductId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Donations");

            migrationBuilder.DropTable(
                name: "PriceHistories");

            migrationBuilder.DropTable(
                name: "ProductReports");

            migrationBuilder.DropColumn(
                name: "AiAutoDiscountDaysBeforeExpiry",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "AiAutoDiscountEnabled",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "AiAutoDiscountPercent",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "AiAutoPricingEnabled",
                table: "Organizations");
        }
    }
}

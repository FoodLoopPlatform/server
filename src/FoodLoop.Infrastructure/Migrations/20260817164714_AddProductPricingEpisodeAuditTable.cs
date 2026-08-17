using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FoodLoop.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProductPricingEpisodeAuditTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProductPricingEpisodes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EventId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RecordedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    IngestedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    IngestionCorrelationId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Outcome = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    DiscountPercentage = table.Column<double>(type: "float", nullable: false),
                    SellThroughRate = table.Column<double>(type: "float", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductPricingEpisodes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductPricingEpisodes_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProductPricingEpisodes_ProductId_EventId",
                table: "ProductPricingEpisodes",
                columns: new[] { "ProductId", "EventId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductPricingEpisodes_ProductId_RecordedAt",
                table: "ProductPricingEpisodes",
                columns: new[] { "ProductId", "RecordedAt" });

            if (migrationBuilder.ActiveProvider == "Microsoft.EntityFrameworkCore.SqlServer")
            {
                migrationBuilder.Sql(@"
                    INSERT INTO [ProductPricingEpisodes] (
                        Id, 
                        ProductId, 
                        EventId, 
                        RecordedAt, 
                        IngestedAt, 
                        IngestionCorrelationId, 
                        Outcome, 
                        DiscountPercentage, 
                        SellThroughRate, 
                        CreatedAt
                    )
                    SELECT 
                        NEWID(), 
                        Id, 
                        CAST(Id AS nvarchar(36)) + '-nodisc', 
                        CreatedAt, 
                        COALESCE(IngestedAt, CreatedAt), 
                        COALESCE(IngestionCorrelationId, ''), 
                        'UNSOLD', 
                        0.0, 
                        0.0, 
                        COALESCE(IngestedAt, CreatedAt)
                    FROM [Products]
                    WHERE [IngestedAt] IS NOT NULL
                ");
            }

            migrationBuilder.DropIndex(
                name: "IX_Products_IngestedAt",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "IngestedAt",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "IngestionCorrelationId",
                table: "Products");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProductPricingEpisodes");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "IngestedAt",
                table: "Products",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IngestionCorrelationId",
                table: "Products",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Products_IngestedAt",
                table: "Products",
                column: "IngestedAt");
        }
    }
}

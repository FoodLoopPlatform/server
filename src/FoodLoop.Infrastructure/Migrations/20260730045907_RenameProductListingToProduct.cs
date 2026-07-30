using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FoodLoop.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RenameProductListingToProduct : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AIRecognitionResults_ProductListings_ListingId",
                table: "AIRecognitionResults");

            migrationBuilder.DropForeignKey(
                name: "FK_Favorites_ProductListings_ListingId",
                table: "Favorites");

            migrationBuilder.DropForeignKey(
                name: "FK_OrderItems_ProductListings_ListingId",
                table: "OrderItems");

            migrationBuilder.DropForeignKey(
                name: "FK_ProductImages_ProductListings_ListingId",
                table: "ProductImages");

            // Rename the table instead of dropping to avoid data loss
            migrationBuilder.RenameTable(
                name: "ProductListings",
                newName: "Products");

            migrationBuilder.RenameColumn(
                name: "ListingId",
                table: "ProductImages",
                newName: "ProductId");

            migrationBuilder.RenameIndex(
                name: "IX_ProductImages_ListingId",
                table: "ProductImages",
                newName: "IX_ProductImages_ProductId");

            migrationBuilder.RenameColumn(
                name: "ListingId",
                table: "OrderItems",
                newName: "ProductId");

            migrationBuilder.RenameIndex(
                name: "IX_OrderItems_ListingId",
                table: "OrderItems",
                newName: "IX_OrderItems_ProductId");

            migrationBuilder.RenameColumn(
                name: "ListingId",
                table: "Favorites",
                newName: "ProductId");

            migrationBuilder.RenameIndex(
                name: "IX_Favorites_ListingId",
                table: "Favorites",
                newName: "IX_Favorites_ProductId");

            migrationBuilder.RenameColumn(
                name: "ListingId",
                table: "AIRecognitionResults",
                newName: "ProductId");

            migrationBuilder.RenameIndex(
                name: "IX_AIRecognitionResults_ListingId",
                table: "AIRecognitionResults",
                newName: "IX_AIRecognitionResults_ProductId");

            // Rename indexes on the Products table
            migrationBuilder.RenameIndex(
                name: "IX_ProductListings_CategoryId",
                table: "Products",
                newName: "IX_Products_CategoryId");

            migrationBuilder.RenameIndex(
                name: "IX_ProductListings_DiscountedPrice",
                table: "Products",
                newName: "IX_Products_DiscountedPrice");

            migrationBuilder.RenameIndex(
                name: "IX_ProductListings_ExpirationDate",
                table: "Products",
                newName: "IX_Products_ExpirationDate");

            migrationBuilder.RenameIndex(
                name: "IX_ProductListings_Status",
                table: "Products",
                newName: "IX_Products_Status");

            migrationBuilder.RenameIndex(
                name: "IX_ProductListings_StoreId",
                table: "Products",
                newName: "IX_Products_StoreId");

            migrationBuilder.AddForeignKey(
                name: "FK_AIRecognitionResults_Products_ProductId",
                table: "AIRecognitionResults",
                column: "ProductId",
                principalTable: "Products",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Favorites_Products_ProductId",
                table: "Favorites",
                column: "ProductId",
                principalTable: "Products",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_OrderItems_Products_ProductId",
                table: "OrderItems",
                column: "ProductId",
                principalTable: "Products",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ProductImages_Products_ProductId",
                table: "ProductImages",
                column: "ProductId",
                principalTable: "Products",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AIRecognitionResults_Products_ProductId",
                table: "AIRecognitionResults");

            migrationBuilder.DropForeignKey(
                name: "FK_Favorites_Products_ProductId",
                table: "Favorites");

            migrationBuilder.DropForeignKey(
                name: "FK_OrderItems_Products_ProductId",
                table: "OrderItems");

            migrationBuilder.DropForeignKey(
                name: "FK_ProductImages_Products_ProductId",
                table: "ProductImages");

            // Rename the table back
            migrationBuilder.RenameTable(
                name: "Products",
                newName: "ProductListings");

            migrationBuilder.RenameColumn(
                name: "ProductId",
                table: "ProductImages",
                newName: "ListingId");

            migrationBuilder.RenameIndex(
                name: "IX_ProductImages_ProductId",
                table: "ProductImages",
                newName: "IX_ProductImages_ListingId");

            migrationBuilder.RenameColumn(
                name: "ProductId",
                table: "OrderItems",
                newName: "ListingId");

            migrationBuilder.RenameIndex(
                name: "IX_OrderItems_ProductId",
                table: "OrderItems",
                newName: "IX_OrderItems_ListingId");

            migrationBuilder.RenameColumn(
                name: "ProductId",
                table: "Favorites",
                newName: "ListingId");

            migrationBuilder.RenameIndex(
                name: "IX_Favorites_ProductId",
                table: "Favorites",
                newName: "IX_Favorites_ListingId");

            migrationBuilder.RenameColumn(
                name: "ProductId",
                table: "AIRecognitionResults",
                newName: "ListingId");

            migrationBuilder.RenameIndex(
                name: "IX_AIRecognitionResults_ProductId",
                table: "AIRecognitionResults",
                newName: "IX_AIRecognitionResults_ListingId");

            // Rename indexes back
            migrationBuilder.RenameIndex(
                name: "IX_Products_CategoryId",
                table: "ProductListings",
                newName: "IX_ProductListings_CategoryId");

            migrationBuilder.RenameIndex(
                name: "IX_Products_DiscountedPrice",
                table: "ProductListings",
                newName: "IX_ProductListings_DiscountedPrice");

            migrationBuilder.RenameIndex(
                name: "IX_Products_ExpirationDate",
                table: "ProductListings",
                newName: "IX_ProductListings_ExpirationDate");

            migrationBuilder.RenameIndex(
                name: "IX_Products_Status",
                table: "ProductListings",
                newName: "IX_ProductListings_Status");

            migrationBuilder.RenameIndex(
                name: "IX_Products_StoreId",
                table: "ProductListings",
                newName: "IX_ProductListings_StoreId");

            migrationBuilder.AddForeignKey(
                name: "FK_AIRecognitionResults_ProductListings_ListingId",
                table: "AIRecognitionResults",
                column: "ListingId",
                principalTable: "ProductListings",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Favorites_ProductListings_ListingId",
                table: "Favorites",
                column: "ListingId",
                principalTable: "ProductListings",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_OrderItems_ProductListings_ListingId",
                table: "OrderItems",
                column: "ListingId",
                principalTable: "ProductListings",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ProductImages_ProductListings_ListingId",
                table: "ProductImages",
                column: "ListingId",
                principalTable: "ProductListings",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}

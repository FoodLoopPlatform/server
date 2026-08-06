using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FoodLoop.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RenameStoreToOrganization : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Drop existing foreign keys
            migrationBuilder.DropForeignKey(
                name: "FK_Products_Stores_StoreId",
                table: "Products");

            migrationBuilder.DropForeignKey(
                name: "FK_Reviews_Stores_StoreId",
                table: "Reviews");

            migrationBuilder.DropForeignKey(
                name: "FK_StoreVerifications_Stores_StoreId",
                table: "StoreVerifications");

            // 2. Rename tables
            migrationBuilder.RenameTable(
                name: "Stores",
                newName: "Organizations");

            migrationBuilder.RenameTable(
                name: "StoreVerifications",
                newName: "OrganizationVerifications");

            // 3. Rename columns
            migrationBuilder.RenameColumn(
                name: "StoreId",
                table: "OrganizationVerifications",
                newName: "OrganizationId");

            migrationBuilder.RenameColumn(
                name: "StoreId",
                table: "Reviews",
                newName: "OrganizationId");

            // 4. Rename indexes
            migrationBuilder.RenameIndex(
                name: "IX_StoreVerifications_StoreId",
                table: "OrganizationVerifications",
                newName: "IX_OrganizationVerifications_OrganizationId");

            migrationBuilder.RenameIndex(
                name: "IX_Reviews_StoreId",
                table: "Reviews",
                newName: "IX_Reviews_OrganizationId");

            migrationBuilder.RenameIndex(
                name: "IX_Products_StoreId",
                table: "Products",
                newName: "IX_Products_StoreId"); // Keep index name as is or rename it to OrganizationId

            migrationBuilder.RenameColumn(
                name: "StoreId",
                table: "Products",
                newName: "OrganizationId");

            migrationBuilder.RenameIndex(
                name: "IX_Products_StoreId",
                table: "Products",
                newName: "IX_Products_OrganizationId");

            migrationBuilder.RenameIndex(
                name: "IX_Stores_OwnerId",
                table: "Organizations",
                newName: "IX_Organizations_OwnerId");

            migrationBuilder.RenameIndex(
                name: "IX_Stores_VerificationStatus",
                table: "Organizations",
                newName: "IX_Organizations_VerificationStatus");

            // 5. Add new foreign keys
            migrationBuilder.AddForeignKey(
                name: "FK_Products_Organizations_OrganizationId",
                table: "Products",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Reviews_Organizations_OrganizationId",
                table: "Reviews",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_OrganizationVerifications_Organizations_OrganizationId",
                table: "OrganizationVerifications",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // 1. Drop new foreign keys
            migrationBuilder.DropForeignKey(
                name: "FK_Products_Organizations_OrganizationId",
                table: "Products");

            migrationBuilder.DropForeignKey(
                name: "FK_Reviews_Organizations_OrganizationId",
                table: "Reviews");

            migrationBuilder.DropForeignKey(
                name: "FK_OrganizationVerifications_Organizations_OrganizationId",
                table: "OrganizationVerifications");

            // 2. Rename columns back
            migrationBuilder.RenameColumn(
                name: "OrganizationId",
                table: "Products",
                newName: "StoreId");

            migrationBuilder.RenameColumn(
                name: "OrganizationId",
                table: "Reviews",
                newName: "StoreId");

            migrationBuilder.RenameColumn(
                name: "OrganizationId",
                table: "OrganizationVerifications",
                newName: "StoreId");

            // 3. Rename indexes back
            migrationBuilder.RenameIndex(
                name: "IX_Products_OrganizationId",
                table: "Products",
                newName: "IX_Products_StoreId");

            migrationBuilder.RenameIndex(
                name: "IX_Reviews_OrganizationId",
                table: "Reviews",
                newName: "IX_Reviews_StoreId");

            migrationBuilder.RenameIndex(
                name: "IX_OrganizationVerifications_OrganizationId",
                table: "OrganizationVerifications",
                newName: "IX_StoreVerifications_StoreId");

            migrationBuilder.RenameIndex(
                name: "IX_Organizations_OwnerId",
                table: "Organizations",
                newName: "IX_Stores_OwnerId");

            migrationBuilder.RenameIndex(
                name: "IX_Organizations_VerificationStatus",
                table: "Organizations",
                newName: "IX_Stores_VerificationStatus");

            // 4. Rename tables back
            migrationBuilder.RenameTable(
                name: "Organizations",
                newName: "Stores");

            migrationBuilder.RenameTable(
                name: "OrganizationVerifications",
                newName: "StoreVerifications");

            // 5. Recreate original foreign keys
            migrationBuilder.AddForeignKey(
                name: "FK_Products_Stores_StoreId",
                table: "Products",
                column: "StoreId",
                principalTable: "Stores",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Reviews_Stores_StoreId",
                table: "Reviews",
                column: "StoreId",
                principalTable: "Stores",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_StoreVerifications_Stores_StoreId",
                table: "StoreVerifications",
                column: "StoreId",
                principalTable: "Stores",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}

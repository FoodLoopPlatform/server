using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FoodLoop.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UpdateStoreVerificationsToOrganizationVerifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            if (migrationBuilder.ActiveProvider == "Microsoft.EntityFrameworkCore.SqlServer")
            {
                return;
            }

            migrationBuilder.DropForeignKey(
                name: "FK_StoreVerifications_Organizations_OrganizationId",
                table: "StoreVerifications");

            migrationBuilder.DropPrimaryKey(
                name: "PK_StoreVerifications",
                table: "StoreVerifications");

            migrationBuilder.RenameTable(
                name: "StoreVerifications",
                newName: "OrganizationVerifications");

            migrationBuilder.RenameIndex(
                name: "IX_StoreVerifications_OrganizationId",
                table: "OrganizationVerifications",
                newName: "IX_OrganizationVerifications_OrganizationId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_OrganizationVerifications",
                table: "OrganizationVerifications",
                column: "Id");

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
            if (migrationBuilder.ActiveProvider == "Microsoft.EntityFrameworkCore.SqlServer")
            {
                return;
            }

            migrationBuilder.DropForeignKey(
                name: "FK_OrganizationVerifications_Organizations_OrganizationId",
                table: "OrganizationVerifications");

            migrationBuilder.DropPrimaryKey(
                name: "PK_OrganizationVerifications",
                table: "OrganizationVerifications");

            migrationBuilder.RenameTable(
                name: "OrganizationVerifications",
                newName: "StoreVerifications");

            migrationBuilder.RenameIndex(
                name: "IX_OrganizationVerifications_OrganizationId",
                table: "StoreVerifications",
                newName: "IX_StoreVerifications_OrganizationId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_StoreVerifications",
                table: "StoreVerifications",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_StoreVerifications_Organizations_OrganizationId",
                table: "StoreVerifications",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}

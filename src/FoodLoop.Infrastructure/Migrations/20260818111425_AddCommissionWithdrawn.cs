using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FoodLoop.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCommissionWithdrawn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "CommissionWithdrawn",
                table: "Organizations",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0.00m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CommissionWithdrawn",
                table: "Organizations");
        }
    }
}

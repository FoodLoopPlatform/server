using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FoodLoop.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMissingBuildingNo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // BuildingNo was in the model snapshot but the column was never actually
            // added to the DB because the original AddBuildingNoToStore migration had
            // an empty Up() body. This migration applies it directly.
            migrationBuilder.Sql(@"
                IF NOT EXISTS (
                    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
                    WHERE TABLE_NAME = 'Stores' AND COLUMN_NAME = 'BuildingNo'
                )
                BEGIN
                    ALTER TABLE [Stores] ADD [BuildingNo] nvarchar(max) NULL;
                END
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF EXISTS (
                    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
                    WHERE TABLE_NAME = 'Stores' AND COLUMN_NAME = 'BuildingNo'
                )
                BEGIN
                    ALTER TABLE [Stores] DROP COLUMN [BuildingNo];
                END
            ");
        }
    }
}

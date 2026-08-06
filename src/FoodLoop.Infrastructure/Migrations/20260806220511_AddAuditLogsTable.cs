using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FoodLoop.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditLogsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AuditLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    EventType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IpAddress = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_OrganizationId",
                table: "AuditLogs",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_UserId",
                table: "AuditLogs",
                column: "UserId");

            // Backfill existing user accounts
            migrationBuilder.Sql(@"
                INSERT INTO AuditLogs (Id, UserId, OrganizationId, EventType, Title, Description, CreatedAt)
                SELECT 
                    NEWID(), 
                    u.Id, 
                    o.Id, 
                    'AccountCreated', 
                    CASE 
                        WHEN r.Name = 'Merchant' THEN 'Merchant Account Created' 
                        WHEN r.Name = 'Charity' THEN 'Charity Account Created' 
                        ELSE 'Account Created' 
                    END,
                    CASE 
                        WHEN r.Name IN ('Merchant', 'Charity') THEN CONCAT('New account registered with email ', u.Email, ' for organization ''', o.Name, '''.')
                        ELSE CONCAT('New account registered with email ', u.Email, '.')
                    END,
                    u.CreatedAt
                FROM AspNetUsers u
                LEFT JOIN AspNetUserRoles ur ON u.Id = ur.UserId
                LEFT JOIN AspNetRoles r ON ur.RoleId = r.Id
                LEFT JOIN Organizations o ON u.Id = o.OwnerId;
            ");

            // Backfill organization profile updates
            migrationBuilder.Sql(@"
                INSERT INTO AuditLogs (Id, UserId, OrganizationId, EventType, Title, Description, CreatedAt)
                SELECT 
                    NEWID(), 
                    OwnerId, 
                    Id, 
                    'StoreProfileUpdated', 
                    'Organization Profile Updated', 
                    CONCAT('Updated organization settings, opening hours, or location coordinates for ''', Name, '''.'), 
                    UpdatedAt
                FROM Organizations
                WHERE UpdatedAt IS NOT NULL AND UpdatedAt <> CreatedAt;
            ");

            // Backfill document uploads
            migrationBuilder.Sql(@"
                INSERT INTO AuditLogs (Id, UserId, OrganizationId, EventType, Title, Description, CreatedAt)
                SELECT 
                    NEWID(), 
                    o.OwnerId, 
                    v.OrganizationId, 
                    'DocumentUploaded', 
                    'Document Uploaded', 
                    CONCAT('Uploaded ', v.VerificationType, ' document.'), 
                    v.CreatedAt
                FROM StoreVerifications v
                JOIN Organizations o ON v.OrganizationId = o.Id;
            ");

            // Backfill document reviews
            migrationBuilder.Sql(@"
                INSERT INTO AuditLogs (Id, UserId, OrganizationId, EventType, Title, Description, CreatedAt)
                SELECT 
                    NEWID(), 
                    v.ReviewedBy, 
                    v.OrganizationId, 
                    'DocumentVerified', 
                    'Document Reviewed', 
                    CONCAT(v.VerificationType, ' was marked ', v.Status, ' by admin.'), 
                    v.ReviewedAt
                FROM StoreVerifications v
                WHERE v.ReviewedAt IS NOT NULL;
            ");

            // Backfill product listings
            migrationBuilder.Sql(@"
                INSERT INTO AuditLogs (Id, UserId, OrganizationId, EventType, Title, Description, CreatedAt)
                SELECT 
                    NEWID(), 
                    o.OwnerId, 
                    p.OrganizationId, 
                    'ProductListed', 
                    'Product Listed', 
                    CONCAT('Listed new product ''', p.Title, '''.'), 
                    p.CreatedAt
                FROM Products p
                JOIN Organizations o ON p.OrganizationId = o.Id;
            ");

            // Backfill product updates
            migrationBuilder.Sql(@"
                INSERT INTO AuditLogs (Id, UserId, OrganizationId, EventType, Title, Description, CreatedAt)
                SELECT 
                    NEWID(), 
                    o.OwnerId, 
                    p.OrganizationId, 
                    'ProductUpdated', 
                    'Product Updated', 
                    CONCAT('Updated product details for ''', p.Title, '''.'), 
                    p.UpdatedAt
                FROM Products p
                JOIN Organizations o ON p.OrganizationId = o.Id
                WHERE p.UpdatedAt IS NOT NULL AND p.UpdatedAt <> p.CreatedAt;
            ");

            // Backfill product deletions
            migrationBuilder.Sql(@"
                INSERT INTO AuditLogs (Id, UserId, OrganizationId, EventType, Title, Description, CreatedAt)
                SELECT 
                    NEWID(), 
                    o.OwnerId, 
                    p.OrganizationId, 
                    'ProductDeleted', 
                    'Product Removed', 
                    CONCAT('Removed product ''', p.Title, '''.'), 
                    p.DeletedAt
                FROM Products p
                JOIN Organizations o ON p.OrganizationId = o.Id
                WHERE p.IsDeleted = 1 AND p.DeletedAt IS NOT NULL;
            ");

            // Backfill product image uploads
            migrationBuilder.Sql(@"
                INSERT INTO AuditLogs (Id, UserId, OrganizationId, EventType, Title, Description, CreatedAt)
                SELECT 
                    NEWID(), 
                    o.OwnerId, 
                    p.OrganizationId, 
                    'ProductImageUploaded', 
                    'Product Image Uploaded', 
                    CONCAT('Uploaded image for product ''', p.Title, '''.'), 
                    img.CreatedAt
                FROM ProductImages img
                JOIN Products p ON img.ProductId = p.Id
                JOIN Organizations o ON p.OrganizationId = o.Id;
            ");

            // Backfill support tickets
            migrationBuilder.Sql(@"
                INSERT INTO AuditLogs (Id, UserId, OrganizationId, EventType, Title, Description, CreatedAt)
                SELECT 
                    NEWID(), 
                    t.UserId, 
                    o.Id, 
                    'SupportTicket', 
                    'Support Ticket Opened', 
                    CONCAT('Ticket: ', t.Category, ' — ', t.Status, '.'), 
                    t.CreatedAt
                FROM SupportTickets t
                LEFT JOIN Organizations o ON t.UserId = o.OwnerId;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditLogs");
        }
    }
}

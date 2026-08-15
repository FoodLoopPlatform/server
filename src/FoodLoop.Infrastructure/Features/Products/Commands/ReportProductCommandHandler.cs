using FoodLoop.Application.Common.Exceptions;
using FoodLoop.Application.Features.Products.Commands;
using FoodLoop.Domain.Entities;
using FoodLoop.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Threading;
using System.Threading.Tasks;

namespace FoodLoop.Infrastructure.Features.Products.Commands;

public class ReportProductCommandHandler : IRequestHandler<ReportProductCommand, Unit>
{
    private readonly ApplicationDbContext _db;
    private readonly FoodLoop.Application.Common.Interfaces.IAuditLogService _auditLogService;

    public ReportProductCommandHandler(ApplicationDbContext db, FoodLoop.Application.Common.Interfaces.IAuditLogService auditLogService)
    {
        _db = db;
        _auditLogService = auditLogService;
    }

    public async Task<Unit> Handle(ReportProductCommand request, CancellationToken cancellationToken)
    {
        var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == request.ProductId && !p.IsDeleted, cancellationToken)
            ?? throw new NotFoundException("Product", request.ProductId);

        var validReasons = new[] { "MisleadingInfo", "WrongExpiry", "Spam", "Inappropriate", "Other" };
        if (!System.Array.Exists(validReasons, r => r == request.Reason))
            throw new ArgumentException($"Invalid reason. Allowed: {string.Join(", ", validReasons)}");

        var report = new ProductReport
        {
            ProductId = request.ProductId,
            ReportedBy = request.ReportedBy,
            Reason = request.Reason,
            Details = request.Details
        };

        _db.ProductReports.Add(report);
        await _db.SaveChangesAsync(cancellationToken);

        await _auditLogService.LogAsync(
            request.ReportedBy,
            product.OrganizationId,
            "ProductReported",
            "Product Reported by Customer",
            $"Customer reported product '{product.Title}'. Reason: {request.Reason}.",
            null,
            cancellationToken);

        return Unit.Value;
    }
}

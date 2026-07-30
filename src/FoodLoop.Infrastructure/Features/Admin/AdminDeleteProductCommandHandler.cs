using FoodLoop.Application.Common.Exceptions;
using FoodLoop.Application.Features.Admin.Commands;
using FoodLoop.Domain.Entities;
using FoodLoop.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace FoodLoop.Infrastructure.Features.Admin;

public class AdminDeleteProductCommandHandler : IRequestHandler<AdminDeleteProductCommand>
{
    private readonly ApplicationDbContext _context;

    public AdminDeleteProductCommandHandler(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task Handle(AdminDeleteProductCommand request, CancellationToken cancellationToken)
    {
        var product = await _context.Products
            .FirstOrDefaultAsync(l => l.Id == request.Id && !l.IsDeleted, cancellationToken);

        if (product == null)
        {
            throw new NotFoundException("Product", request.Id);
        }

        product.IsDeleted = true;
        product.DeletedAt = DateTimeOffset.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
    }
}

using FoodLoop.Application.DTOs.Admin;
using MediatR;
using System;

namespace FoodLoop.Application.Features.Admin.Commands;

public record ModerateProductCommand(
    Guid ProductId,
    string Action, // "Approve", "Reject", "RequestChanges"
    string? Note
) : IRequest<AdminProductDto>;

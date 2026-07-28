using FoodLoop.Application.DTOs.Admin;
using MediatR;

namespace FoodLoop.Application.Features.Admin.Commands;

/// <summary>PATCH /admin/stores/{id}/verify — approve or reject a store's verification.</summary>
public record VerifyStoreCommand(Guid StoreId, Guid AdminId, VerifyStoreRequest Request) : IRequest<AdminStoreDto>;

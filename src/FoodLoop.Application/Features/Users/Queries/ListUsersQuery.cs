using FoodLoop.Application.Common.Models;
using FoodLoop.Application.DTOs.Users;
using MediatR;

namespace FoodLoop.Application.Features.Users.Queries;

public record ListUsersQuery(
    string? Role = null,
    string? Status = null,
    string? SearchTerm = null,
    int Page = 1,
    int PageSize = 10
) : IRequest<PagedResult<UserDto>>;

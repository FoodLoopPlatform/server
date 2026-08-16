using FoodLoop.Application.DTOs.Admin;
using MediatR;

namespace FoodLoop.Application.Features.Admin.Queries;

/// <summary>GET /admin/system-settings — returns the current platform-wide settings.</summary>
public record GetSystemSettingsQuery : IRequest<SystemSettingsDto>;

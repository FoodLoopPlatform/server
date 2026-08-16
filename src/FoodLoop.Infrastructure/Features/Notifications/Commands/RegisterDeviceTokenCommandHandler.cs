using FoodLoop.Application.Common.Interfaces;
using FoodLoop.Application.Common.Models;
using FoodLoop.Application.Features.Notifications.Commands;
using MediatR;
using System.Threading;
using System.Threading.Tasks;

namespace FoodLoop.Infrastructure.Features.Notifications.Commands;

public class RegisterDeviceTokenCommandHandler : IRequestHandler<RegisterDeviceTokenCommand, Result>
{
    private readonly IUserDeviceTokenService _deviceTokenService;

    public RegisterDeviceTokenCommandHandler(IUserDeviceTokenService deviceTokenService)
    {
        _deviceTokenService = deviceTokenService;
    }

    public async Task<Result> Handle(RegisterDeviceTokenCommand request, CancellationToken cancellationToken)
    {
        await _deviceTokenService.UpsertAsync(request.UserId, request.Token, request.Platform, cancellationToken);
        return Result.Ok();
    }
}

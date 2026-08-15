using FoodLoop.Application.Common.Interfaces;
using FoodLoop.Application.Common.Models;
using FoodLoop.Application.Features.Auth.Commands;
using MediatR;

namespace FoodLoop.Infrastructure.Features.Auth.Commands;

public class LogoutCommandHandler : IRequestHandler<LogoutCommand, Result>
{
    private readonly IUnitOfWork _unitOfWork;

    public LogoutCommandHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(LogoutCommand command, CancellationToken cancellationToken)
    {
        var existingToken = await _unitOfWork.RefreshTokens.GetByTokenAsync(command.RefreshToken, cancellationToken);

        if (existingToken is { IsActive: true })
        {
            existingToken.RevokedAt = DateTimeOffset.UtcNow;
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return Result.Ok();
    }
}


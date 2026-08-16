using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace FoodLoop.Application.Common.Interfaces;

public interface IUserDeviceTokenService
{
    Task UpsertAsync(Guid userId, string token, string platform, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> GetActiveTokensAsync(Guid userId, CancellationToken cancellationToken = default);
}

using FoodLoop.Application.DTOs.Admin;
using MediatR;
using System;
using System.Collections.Generic;

namespace FoodLoop.Application.Features.Admin.Queries;

public record GetUserActivityLogQuery(Guid UserId) : IRequest<IReadOnlyList<ActivityLogEntryDto>>;

public record GetStoreActivityLogQuery(Guid StoreId) : IRequest<IReadOnlyList<ActivityLogEntryDto>>;

public record GetCharityActivityLogQuery(Guid StoreId) : IRequest<IReadOnlyList<ActivityLogEntryDto>>;

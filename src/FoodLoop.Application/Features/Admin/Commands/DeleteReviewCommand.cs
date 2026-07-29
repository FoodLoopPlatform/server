using MediatR;
using System;

namespace FoodLoop.Application.Features.Admin.Commands;

public record DeleteReviewCommand(Guid Id) : IRequest;

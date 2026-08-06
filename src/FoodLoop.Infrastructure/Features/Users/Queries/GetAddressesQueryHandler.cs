using FoodLoop.Application.Common.Interfaces;
using FoodLoop.Application.DTOs.Users;
using FoodLoop.Application.Features.Users.Queries;
using FoodLoop.Infrastructure.Mappings;
using MediatR;

namespace FoodLoop.Infrastructure.Features.Users.Queries;

public class GetAddressesQueryHandler : IRequestHandler<GetAddressesQuery, IReadOnlyList<AddressDto>>
{
    private readonly IUnitOfWork _unitOfWork;

    public GetAddressesQueryHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<IReadOnlyList<AddressDto>> Handle(GetAddressesQuery query, CancellationToken cancellationToken)
    {
        var addresses = await _unitOfWork.Addresses.GetByUserIdAsync(query.UserId, cancellationToken);
        return addresses.Select(a => a.ToDto()).ToList();
    }
}


using FoodLoop.Application.Common.Exceptions;
using FoodLoop.Application.Common.Models;
using FoodLoop.Application.DTOs.Users;
using FoodLoop.Application.Services;
using FoodLoop.Domain.Entities;
using FoodLoop.Infrastructure.Identity;
using FoodLoop.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace FoodLoop.Infrastructure.Services;

public class UserService : IUserService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ApplicationDbContext _dbContext;

    public UserService(UserManager<ApplicationUser> userManager, ApplicationDbContext dbContext)
    {
        _userManager = userManager;
        _dbContext = dbContext;
    }

    public async Task<UserDto> GetCurrentUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await FindUserOrThrowAsync(userId);
        return await MapToDtoAsync(user);
    }

    public async Task<UserDto> UpdateProfileAsync(Guid userId, UpdateProfileRequest request, CancellationToken cancellationToken = default)
    {
        var user = await FindUserOrThrowAsync(userId);

        if (!string.IsNullOrWhiteSpace(request.Name))
            user.FullName = request.Name.Trim();

        if (request.ProfileImage != null)
            user.ProfileImage = request.ProfileImage;

        if (!string.IsNullOrWhiteSpace(request.PreferredLanguage))
            user.Language = request.PreferredLanguage;

        user.UpdatedAt = DateTimeOffset.UtcNow;

        await _userManager.UpdateAsync(user);

        return await MapToDtoAsync(user);
    }

    public async Task<Result> UpdatePreferencesAsync(Guid userId, UpdatePreferencesRequest request, CancellationToken cancellationToken = default)
    {
        var user = await FindUserOrThrowAsync(userId);

        if (request.OrderUpdatesEnabled.HasValue)
            user.OrderUpdatesEnabled = request.OrderUpdatesEnabled.Value;

        if (request.MarketingNotificationsEnabled.HasValue)
            user.MarketingNotificationsEnabled = request.MarketingNotificationsEnabled.Value;

        if (!string.IsNullOrWhiteSpace(request.PreferredLanguage))
            user.Language = request.PreferredLanguage;

        user.UpdatedAt = DateTimeOffset.UtcNow;

        await _userManager.UpdateAsync(user);

        return Result.Ok();
    }

    public async Task<IReadOnlyList<AddressDto>> GetAddressesAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        // Projected inline (rather than via a ToDto(...) call) so EF Core can translate
        // the projection into SQL instead of requiring client-side evaluation.
        return await _dbContext.Addresses
            .Where(a => a.UserId == userId)
            .OrderByDescending(a => a.IsDefault)
            .ThenByDescending(a => a.CreatedAt)
            .Select(a => new AddressDto
            {
                Id = a.Id,
                AddressType = a.AddressType,
                City = a.City,
                District = a.District,
                Street = a.Street,
                BuildingNo = a.BuildingNo,
                Floor = a.Floor,
                ApartmentNo = a.ApartmentNo,
                Notes = a.Notes,
                Latitude = a.Latitude,
                Longitude = a.Longitude,
                IsDefault = a.IsDefault,
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<AddressDto> CreateAddressAsync(Guid userId, CreateAddressRequest request, CancellationToken cancellationToken = default)
    {
        if (request.IsDefault)
        {
            await ClearExistingDefaultAsync(userId, cancellationToken);
        }

        var address = new Address
        {
            UserId = userId,
            AddressType = request.AddressType,
            City = request.City,
            District = request.District,
            Street = request.Street,
            BuildingNo = request.BuildingNo,
            Floor = request.Floor,
            ApartmentNo = request.ApartmentNo,
            Notes = request.Notes,
            Latitude = request.Latitude,
            Longitude = request.Longitude,
            IsDefault = request.IsDefault,
        };

        _dbContext.Addresses.Add(address);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return ToDto(address);
    }

    public async Task<AddressDto> UpdateAddressAsync(Guid userId, Guid addressId, UpdateAddressRequest request, CancellationToken cancellationToken = default)
    {
        var address = await _dbContext.Addresses
            .FirstOrDefaultAsync(a => a.Id == addressId, cancellationToken)
            ?? throw new NotFoundException(nameof(Address), addressId);

        if (address.UserId != userId)
            throw new ForbiddenAccessException("You cannot modify another user's address.");

        if (request.AddressType.HasValue) address.AddressType = request.AddressType.Value;
        if (request.City != null) address.City = request.City;
        if (request.District != null) address.District = request.District;
        if (request.Street != null) address.Street = request.Street;
        if (request.BuildingNo != null) address.BuildingNo = request.BuildingNo;
        if (request.Floor != null) address.Floor = request.Floor;
        if (request.ApartmentNo != null) address.ApartmentNo = request.ApartmentNo;
        if (request.Notes != null) address.Notes = request.Notes;
        if (request.Latitude.HasValue) address.Latitude = request.Latitude.Value;
        if (request.Longitude.HasValue) address.Longitude = request.Longitude.Value;

        if (request.IsDefault.HasValue)
        {
            if (request.IsDefault.Value)
                await ClearExistingDefaultAsync(userId, cancellationToken, exceptAddressId: address.Id);

            address.IsDefault = request.IsDefault.Value;
        }

        address.UpdatedAt = DateTimeOffset.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return ToDto(address);
    }

    public async Task DeleteAddressAsync(Guid userId, Guid addressId, CancellationToken cancellationToken = default)
    {
        var address = await _dbContext.Addresses
            .FirstOrDefaultAsync(a => a.Id == addressId, cancellationToken)
            ?? throw new NotFoundException(nameof(Address), addressId);

        if (address.UserId != userId)
            throw new ForbiddenAccessException("You cannot delete another user's address.");

        _dbContext.Addresses.Remove(address);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task ClearExistingDefaultAsync(Guid userId, CancellationToken cancellationToken, Guid? exceptAddressId = null)
    {
        var currentDefaults = await _dbContext.Addresses
            .Where(a => a.UserId == userId && a.IsDefault && a.Id != exceptAddressId)
            .ToListAsync(cancellationToken);

        foreach (var addr in currentDefaults)
        {
            addr.IsDefault = false;
        }
    }

    private async Task<ApplicationUser> FindUserOrThrowAsync(Guid userId)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        return user ?? throw new NotFoundException(nameof(ApplicationUser), userId);
    }

    private async Task<UserDto> MapToDtoAsync(ApplicationUser user)
    {
        var roles = await _userManager.GetRolesAsync(user);
        return new UserDto
        {
            Id = user.Id,
            FullName = user.FullName,
            Email = user.Email!,
            PhoneNumber = user.PhoneNumber,
            ProfileImage = user.ProfileImage,
            Language = user.Language,
            Status = user.Status.ToString(),
            OrderUpdatesEnabled = user.OrderUpdatesEnabled,
            MarketingNotificationsEnabled = user.MarketingNotificationsEnabled,
            Roles = roles.ToArray(),
            CreatedAt = user.CreatedAt,
        };
    }

    private static AddressDto ToDto(Address a) => new()
    {
        Id = a.Id,
        AddressType = a.AddressType,
        City = a.City,
        District = a.District,
        Street = a.Street,
        BuildingNo = a.BuildingNo,
        Floor = a.Floor,
        ApartmentNo = a.ApartmentNo,
        Notes = a.Notes,
        Latitude = a.Latitude,
        Longitude = a.Longitude,
        IsDefault = a.IsDefault,
    };
}

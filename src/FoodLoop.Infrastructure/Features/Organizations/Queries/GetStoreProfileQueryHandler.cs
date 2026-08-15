using FoodLoop.Application.Common.Exceptions;
using FoodLoop.Application.Common.Interfaces;
using FoodLoop.Application.DTOs.Organizations;
using FoodLoop.Application.DTOs.Reviews;
using FoodLoop.Application.Features.Organizations.Queries;
using FoodLoop.Infrastructure.Identity;
using FoodLoop.Infrastructure.Persistence;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace FoodLoop.Infrastructure.Features.Organizations.Queries;

/// <summary>
/// Handler for GET /stores/{storeId} — public store profile screen.
/// Returns store details, location, reputation, and recent reviews.
/// </summary>
public class GetStoreProfileQueryHandler : IRequestHandler<GetStoreProfileQuery, StoreProfileDto>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILocalizationService _loc;

    public GetStoreProfileQueryHandler(
        IUnitOfWork unitOfWork,
        ApplicationDbContext db,
        UserManager<ApplicationUser> userManager,
        ILocalizationService loc)
    {
        _unitOfWork = unitOfWork;
        _db = db;
        _userManager = userManager;
        _loc = loc;
    }

    public async Task<StoreProfileDto> Handle(GetStoreProfileQuery request, CancellationToken cancellationToken)
    {
        // 1. Fetch the organization with reviews eagerly loaded
        var organization = await _unitOfWork.Organizations.GetByIdWithReviewsAsync(
            request.StoreId, cancellationToken)
            ?? throw new NotFoundException(_loc["StoreNotFoundById"]);

        // 2. Calculate review statistics
        var allReviews = organization.Reviews.ToList();
        var totalReviews = allReviews.Count;
        var averageRating = totalReviews > 0
            ? allReviews.Average(r => r.Rating)
            : 0.0;

        // 3. Build rating distribution (1–5 stars)
        var ratingDistribution = Enumerable.Range(1, 5)
            .Select(star => new RatingDistributionDto
            {
                Stars = star,
                Count = allReviews.Count(r => r.Rating == star)
            })
            .ToList();

        // 4. Fetch paginated recent reviews with user details
        var recentReviews = allReviews
            .OrderByDescending(r => r.CreatedAt)
            .Skip((request.ReviewsPageNumber - 1) * request.ReviewsPageSize)
            .Take(request.ReviewsPageSize)
            .ToList();

        // 5. Get reviewer names
        var userIds = recentReviews.Select(r => r.UserId).Distinct().ToList();
        var users = await _db.Users
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => new { u.FullName, u.ProfileImage }, cancellationToken);

        // 6. Build review DTOs
        var reviewDtos = recentReviews.Select(r => new ReviewDto
        {
            Id = r.Id,
            OrderId = r.OrderId,
            UserId = r.UserId,
            UserFullName = users.TryGetValue(r.UserId, out var user) ? user.FullName : string.Empty,
            OrganizationId = r.OrganizationId,
            OrganizationName = organization.Name,
            Rating = r.Rating,
            Comment = r.Comment,
            CreatedAt = r.CreatedAt
        }).ToList();

        // 7. Build and return the store profile DTO
        return new StoreProfileDto
        {
            Id = organization.Id,
            Name = organization.Name,
            Description = organization.Description,
            Logo = organization.Logo,
            CoverPhoto = organization.CoverPhoto,
            Phone = organization.Phone,
            Email = organization.Email,
            BusinessCategory = organization.BusinessCategory?.ToString(),
            Governorate = organization.Governorate,
            City = organization.City,
            Neighborhood = organization.Neighborhood,
            Street = organization.Street,
            BuildingNo = organization.BuildingNo,
            Latitude = organization.Latitude,
            Longitude = organization.Longitude,
            OpeningHours = organization.OpeningHours,
            VerificationStatus = organization.VerificationStatus.ToString(),
            AverageRating = averageRating,
            TotalReviews = totalReviews,
            RatingDistribution = ratingDistribution,
            RecentReviews = reviewDtos
        };
    }
}

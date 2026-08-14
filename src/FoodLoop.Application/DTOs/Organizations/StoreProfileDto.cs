using FoodLoop.Application.DTOs.Reviews;
using FoodLoop.Domain.Enums;

namespace FoodLoop.Application.DTOs.Organizations;

/// <summary>
/// Response DTO for GET /stores/{storeId} — the public Store Profile screen.
/// Includes store basic info, location, reputation metrics, and recent reviews.
/// </summary>
public class StoreProfileDto
{
    // ── Identity ──────────────────────────────────────────────────────────
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    // ── Images ────────────────────────────────────────────────────────────
    public string? Logo { get; set; }
    public string? CoverPhoto { get; set; }

    // ── Contact & Category ────────────────────────────────────────────────
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? BusinessCategory { get; set; }

    // ── Location ─────────────────────────────────────────────────────────
    public string? Governorate { get; set; }
    public string? City { get; set; }
    public string? Neighborhood { get; set; }
    public string? Street { get; set; }
    public string? BuildingNo { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }

    // ── Hours ─────────────────────────────────────────────────────────────
    /// <summary>JSON-encoded weekly schedule, as stored on the Organization entity.</summary>
    public string? OpeningHours { get; set; }

    // ── Status ────────────────────────────────────────────────────────────
    public string VerificationStatus { get; set; } = string.Empty;

    // ── Reputation ───────────────────────────────────────────────────────
    /// <summary>Average rating across all reviews (0.0 when no reviews exist).</summary>
    public double AverageRating { get; set; }

    /// <summary>Total number of reviews submitted for this store.</summary>
    public int TotalReviews { get; set; }

    /// <summary>
    /// Distribution of ratings (1–5 stars). Each entry holds the star value and
    /// the count of reviews with that rating, allowing the UI to render a
    /// rating breakdown bar chart.
    /// </summary>
    public IReadOnlyList<RatingDistributionDto> RatingDistribution { get; set; } = Array.Empty<RatingDistributionDto>();

    // ── Recent Reviews ────────────────────────────────────────────────────
    /// <summary>
    /// Most recent reviews, ordered by date descending.
    /// The caller controls page size via the <c>reviewsPageSize</c> query parameter (default 5).
    /// </summary>
    public IReadOnlyList<ReviewDto> RecentReviews { get; set; } = Array.Empty<ReviewDto>();
}

/// <summary>One bar in the star-rating distribution chart.</summary>
public class RatingDistributionDto
{
    /// <summary>Star value (1–5).</summary>
    public int Stars { get; set; }

    /// <summary>Number of reviews with this star rating.</summary>
    public int Count { get; set; }
}

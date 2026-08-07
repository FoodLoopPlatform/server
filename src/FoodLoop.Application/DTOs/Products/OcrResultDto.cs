using System;

namespace FoodLoop.Application.DTOs.Products;

public class OcrResultDto
{
    public Guid ProductId { get; set; }
    public string? DetectedProduct { get; set; }
    public double ConfidenceScore { get; set; }
    public DateOnly? ExtractedExpiryDate { get; set; }
    public string? ExtractedText { get; set; }
    public bool Reviewed { get; set; }
    public DateTimeOffset ScannedAt { get; set; }
}

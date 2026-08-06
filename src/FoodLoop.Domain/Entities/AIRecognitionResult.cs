using FoodLoop.Domain.Common;
using System;

namespace FoodLoop.Domain.Entities;

/// <summary>Organizations AI analysis for an uploaded product image (see AI Design & Workflow doc, section 8).</summary>
public class AIRecognitionResult : BaseEntity
{
    public Guid ProductId { get; set; }
    public Product? Product { get; set; }

    public string? DetectedProduct { get; set; }
    public double ConfidenceScore { get; set; }
    public DateOnly? ExtractedExpiryDate { get; set; }
    public string? ExtractedText { get; set; }
    public bool Reviewed { get; set; }
}


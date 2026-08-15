using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace FoodLoop.Application.Common.Interfaces;

public record OcrAnalysisResult(
    string DetectedProduct,
    DateOnly? ExpirationDate,
    double ConfidenceScore,
    string ExtractedText,
    string? SuggestedDescription = null,
    string? SuggestedCategory = null,
    string? PackageSize = null);

/// <summary>
/// Abstraction for AI / Vision OCR engine (Google Gemini Vision).
/// Analyzes product packaging images to extract product title, brand, expiration date, category, and description.
/// </summary>
public interface IOcrService
{
    Task<OcrAnalysisResult> AnalyzeProductImageAsync(
        Stream imageStream,
        string contentType,
        CancellationToken cancellationToken = default);
}

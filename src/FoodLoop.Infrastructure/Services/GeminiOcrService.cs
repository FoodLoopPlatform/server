using FoodLoop.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace FoodLoop.Infrastructure.Services;

public class GeminiOcrService : IOcrService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<GeminiOcrService> _logger;

    public GeminiOcrService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<GeminiOcrService> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<OcrAnalysisResult> AnalyzeProductImageAsync(
        Stream imageStream,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        var apiKey = _configuration["Gemini:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            apiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY");
        }
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            apiKey = Environment.GetEnvironmentVariable("GOOGLE_API_KEY");
        }
        apiKey = (apiKey ?? string.Empty).Trim().Trim('"', '\'', '\r', '\n');

        var configuredModel = _configuration["Gemini:Model"];
        if (string.IsNullOrWhiteSpace(configuredModel))
        {
            configuredModel = Environment.GetEnvironmentVariable("GEMINI_MODEL");
        }
        configuredModel = (configuredModel ?? "gemini-1.5-flash").Trim().Trim('"', '\'', '\r', '\n');
        if (string.IsNullOrWhiteSpace(configuredModel))
        {
            configuredModel = "gemini-1.5-flash";
        }

        // Read image bytes and convert to Base64
        using var memoryStream = new MemoryStream();
        if (imageStream.CanSeek)
        {
            imageStream.Position = 0;
        }
        await imageStream.CopyToAsync(memoryStream, cancellationToken);
        var imageBytes = memoryStream.ToArray();
        var base64Data = Convert.ToBase64String(imageBytes);

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            _logger.LogWarning("Gemini:ApiKey is not configured in appsettings.json or GEMINI_API_KEY env. Returning fallback scan.");
            return new OcrAnalysisResult(
                DetectedProduct: "Grocery Product Item",
                ExpirationDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7)),
                ConfidenceScore: 0.85,
                ExtractedText: "AI Vision analysis placeholder. Configure GEMINI_API_KEY in .env to enable live Google Gemini Vision.");
        }

        try
        {
            var promptText = @"You are a specialized grocery product packaging OCR and AI recognition assistant.
Analyze this food/grocery packaging image.
Extract:
1. 'detectedProduct': The exact brand name and product title (e.g. 'Juhayna Full Cream Milk 1L' or 'TBS Butter Croissant').
2. 'expirationDate': The printed expiration date, best-before date, or use-by date in 'YYYY-MM-DD' ISO format. If only month/year is given, use the last day of that month. If no date is found, set to null.
3. 'confidenceScore': Confidence score between 0.0 and 1.0 based on image clarity.
4. 'extractedText': A string containing all recognizable text and numbers printed on the packaging label.

Return ONLY a JSON object matching this schema:
{
  ""detectedProduct"": ""string"",
  ""expirationDate"": ""YYYY-MM-DD or null"",
  ""confidenceScore"": 0.95,
  ""extractedText"": ""string""
}";

            var cleanMimeType = string.IsNullOrWhiteSpace(contentType) ? "image/jpeg" : contentType.Split(';')[0].Trim();

            var requestBody = new
            {
                contents = new[]
                {
                    new
                    {
                        parts = new object[]
                        {
                            new { text = promptText },
                            new
                            {
                                inlineData = new
                                {
                                    mimeType = cleanMimeType,
                                    data = base64Data
                                }
                            }
                        }
                    }
                },
                generationConfig = new
                {
                    responseMimeType = "application/json",
                    temperature = 0.1
                }
            };

            var serializedBody = JsonSerializer.Serialize(requestBody);

            // Candidate endpoints to handle different Gemini model alias names and API versions
            var candidateUrls = new[]
            {
                $"https://generativelanguage.googleapis.com/v1beta/models/{configuredModel}:generateContent?key={apiKey}",
                $"https://generativelanguage.googleapis.com/v1beta/models/gemini-1.5-flash-latest:generateContent?key={apiKey}",
                $"https://generativelanguage.googleapis.com/v1/models/gemini-1.5-flash:generateContent?key={apiKey}",
                $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.0-flash:generateContent?key={apiKey}",
                $"https://generativelanguage.googleapis.com/v1beta/models/gemini-1.5-flash-8b:generateContent?key={apiKey}"
            };

            HttpResponseMessage? lastResponse = null;
            string lastErrorBody = string.Empty;

            foreach (var url in candidateUrls)
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, url)
                {
                    Content = new StringContent(serializedBody, Encoding.UTF8, "application/json")
                };
                request.Headers.TryAddWithoutValidation("x-goog-api-key", apiKey);

                var response = await _httpClient.SendAsync(request, cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    var responseString = await response.Content.ReadAsStringAsync(cancellationToken);
                    var responseJson = JsonNode.Parse(responseString);

                    var textResult = responseJson?["candidates"]?[0]?["content"]?["parts"]?[0]?["text"]?.ToString();
                    if (!string.IsNullOrWhiteSpace(textResult))
                    {
                        using var doc = JsonDocument.Parse(textResult);
                        var root = doc.RootElement;

                        var detectedProduct = root.TryGetProperty("detectedProduct", out var dp) ? dp.GetString() ?? "Product Item" : "Product Item";
                        
                        DateOnly? expiryDate = null;
                        if (root.TryGetProperty("expirationDate", out var expProp) && 
                            expProp.ValueKind == JsonValueKind.String && 
                            DateOnly.TryParse(expProp.GetString(), out var parsedDate))
                        {
                            expiryDate = parsedDate;
                        }

                        var confidence = root.TryGetProperty("confidenceScore", out var confProp) && confProp.TryGetDouble(out var conf) 
                            ? Math.Round(conf, 2) 
                            : 0.90;

                        var extractedText = root.TryGetProperty("extractedText", out var textProp) ? textProp.GetString() ?? "" : "";

                        return new OcrAnalysisResult(
                            DetectedProduct: detectedProduct,
                            ExpirationDate: expiryDate,
                            ConfidenceScore: confidence,
                            ExtractedText: extractedText);
                    }
                }
                else
                {
                    lastResponse = response;
                    lastErrorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                }
            }

            _logger.LogError("All Gemini model endpoints failed. Last Status: {StatusCode}, Error: {ErrorBody}", lastResponse?.StatusCode, lastErrorBody);
            return new OcrAnalysisResult(
                DetectedProduct: "Grocery Product Item",
                ExpirationDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5)),
                ConfidenceScore: 0.75,
                ExtractedText: $"Gemini API Error ({lastResponse?.StatusCode}): {lastErrorBody}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception occurred while calling Google Gemini Vision OCR API.");
            return new OcrAnalysisResult(
                DetectedProduct: "Grocery Product Item",
                ExpirationDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7)),
                ConfidenceScore: 0.70,
                ExtractedText: $"OCR Exception: {ex.Message}");
        }
    }
}

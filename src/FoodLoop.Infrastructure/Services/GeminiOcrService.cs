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
        configuredModel = (configuredModel ?? "gemini-flash-latest").Trim().Trim('"', '\'', '\r', '\n');
        if (string.IsNullOrWhiteSpace(configuredModel))
        {
            configuredModel = "gemini-flash-latest";
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
                ExtractedText: "AI Vision analysis placeholder. Configure GEMINI_API_KEY in .env to enable live Google Gemini Vision.",
                SuggestedDescription: "Food and grocery product item.",
                SuggestedCategory: "Canned & Pantry",
                PackageSize: null);
        }

        try
        {
            var promptText = @"You are a specialized grocery product packaging OCR and AI recognition assistant for the FoodLoop surplus food platform.
Analyze this food/grocery packaging image.
Extract the following fields accurately:
1. 'detectedProduct': The exact brand name and product title (e.g. 'Juhayna Full Cream Milk 1L' or 'Sara Lee Honey Wheat Bread' or 'Barilla Penne Rigate 500g').
2. 'suggestedDescription': A concise, appealing product description including key highlights, flavor, or main ingredients if visible.
3. 'suggestedCategory': Pick the BEST matching category from this exact list:
   - Bakery
   - Dairy & Eggs
   - Fruits & Vegetables
   - Meat & Poultry
   - Prepared Meals
   - Beverages
   - Canned & Pantry
   - Desserts & Sweets
4. 'packageSize': Net weight or volume if printed (e.g. '500g', '1L', '567g', '6 Pack').
5. 'expirationDate': The printed expiration date, best-before date, or use-by date in 'YYYY-MM-DD' ISO format. If only month/year is given, use the last day of that month. If no date is found, set to null.
6. 'confidenceScore': Confidence score between 0.0 and 1.0 based on image clarity and text certainty.
7. 'extractedText': A string containing all recognizable text and numbers printed on the packaging label.

Return ONLY a JSON object matching this schema:
{
  ""detectedProduct"": ""string"",
  ""suggestedDescription"": ""string"",
  ""suggestedCategory"": ""string"",
  ""packageSize"": ""string or null"",
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

            // Verified Google GenAI endpoints
            var candidateUrls = new[]
            {
                "https://generativelanguage.googleapis.com/v1beta/models/gemini-flash-latest:generateContent",
                $"https://generativelanguage.googleapis.com/v1beta/models/{configuredModel}:generateContent",
                "https://generativelanguage.googleapis.com/v1beta/models/gemini-2.0-flash:generateContent",
                "https://generativelanguage.googleapis.com/v1beta/models/gemini-1.5-flash-latest:generateContent"
            };

            HttpResponseMessage? lastResponse = null;
            string lastErrorBody = string.Empty;

            foreach (var url in candidateUrls)
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, url)
                {
                    Content = new StringContent(serializedBody, Encoding.UTF8, "application/json")
                };

                // Google GenAI official header authentication
                request.Headers.TryAddWithoutValidation("X-goog-api-key", apiKey);

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
                        var suggestedDesc = root.TryGetProperty("suggestedDescription", out var descP) ? descP.GetString() : null;
                        var suggestedCat = root.TryGetProperty("suggestedCategory", out var catP) ? catP.GetString() : null;
                        var packageSize = root.TryGetProperty("packageSize", out var sizeP) ? sizeP.GetString() : null;

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
                            ExtractedText: extractedText,
                            SuggestedDescription: suggestedDesc,
                            SuggestedCategory: suggestedCat,
                            PackageSize: packageSize);
                    }
                }
                else
                {
                    lastResponse = response;
                    lastErrorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                }
            }

            _logger.LogError("All Gemini candidate endpoints failed. Last Status: {StatusCode}, Error: {ErrorBody}", lastResponse?.StatusCode, lastErrorBody);
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

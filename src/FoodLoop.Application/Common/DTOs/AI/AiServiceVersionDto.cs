using System.Text.Json.Serialization;

namespace FoodLoop.Application.Common.DTOs.AI;

public record AiServiceVersionDto(
    [property: JsonPropertyName("app_name")] string? AppName,
    [property: JsonPropertyName("name")] string? Name,
    string Version,
    string Environment,
    [property: JsonPropertyName("embedding_provider")] string? EmbeddingProvider = null,
    [property: JsonPropertyName("vector_store_provider")] string? VectorStoreProvider = null
)
{
    public string ResolvedName => AppName ?? Name ?? "FoodLoop AI Service";
}

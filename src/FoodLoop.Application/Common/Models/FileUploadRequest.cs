namespace FoodLoop.Application.Common.Models;

/// <summary>
/// Framework-agnostic wrapper around an uploaded file so Application-layer services don't
/// need a dependency on ASP.NET Core's IFormFile (Clean Architecture: Application stays
/// free of web-framework types). Controllers adapt IFormFile to this before calling in.
/// </summary>
public class FileUploadRequest
{
    public required Stream Content { get; init; }
    public required string FileName { get; init; }
    public required string ContentType { get; init; }
}

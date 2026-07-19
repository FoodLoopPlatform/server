namespace FoodLoop.Application.Common.Models;

/// <summary>
/// Lightweight result wrapper so service methods can signal domain/validation
/// failures without throwing for expected, user-facing cases (e.g. "email already
/// registered"). Unexpected failures still throw and are handled by the global
/// exception middleware.
/// </summary>
public class Result
{
    public bool Success { get; init; }
    public string? Message { get; init; }
    public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();

    public static Result Ok() => new() { Success = true };
    public static Result Fail(string message, IEnumerable<string>? errors = null) =>
        new() { Success = false, Message = message, Errors = errors?.ToArray() ?? Array.Empty<string>() };
}

public class Result<T> : Result
{
    public T? Data { get; init; }

    public static Result<T> Ok(T data) => new() { Success = true, Data = data };
    public static new Result<T> Fail(string message, IEnumerable<string>? errors = null) =>
        new() { Success = false, Message = message, Errors = errors?.ToArray() ?? Array.Empty<string>() };
}

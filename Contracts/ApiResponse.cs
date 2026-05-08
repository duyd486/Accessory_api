namespace Accessory_api.Contracts;

public sealed class ApiResponse<T>
{
    public bool Status { get; init; }
    public T? Data { get; init; }
    public string? Message { get; init; }

    public static ApiResponse<T> Ok(T? data, string? message = null) => new()
    {
        Status = true,
        Data = data,
        Message = message
    };

    public static ApiResponse<T> Fail(string message) => new()
    {
        Status = false,
        Data = default,
        Message = message
    };
}

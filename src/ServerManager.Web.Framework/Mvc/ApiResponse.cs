namespace ServerManager.Web.Framework.Mvc;

public sealed class ApiResponse<T>
{
    public const string CurrentApiVersion = "1.0";

    public bool IsSuccess { get; init; }

    public int StatusCode { get; init; }

    public T? Data { get; init; }

    public string Message { get; init; } = string.Empty;

    public IReadOnlyList<string> ValidationMessages { get; init; } = [];

    /// <summary>Form alanı adına göre doğrulama hataları; boş anahtar alana bağlı olmayan hatalardır.</summary>
    public IReadOnlyDictionary<string, string[]> Errors { get; init; } = new Dictionary<string, string[]>();

    public DateTimeOffset TimeStamp { get; init; } = DateTimeOffset.Now;

    public string ApiVersion { get; init; } = CurrentApiVersion;

    public static ApiResponse<T> Success(T? data, string message = "", int statusCode = StatusCodes.Status200OK) => new()
    {
        IsSuccess = true,
        StatusCode = statusCode,
        Data = data,
        Message = message
    };

    public static ApiResponse<T> Fail(
        string message,
        int statusCode,
        IReadOnlyList<string>? validationMessages = null,
        IReadOnlyDictionary<string, string[]>? errors = null) => new()
    {
        IsSuccess = false,
        StatusCode = statusCode,
        Message = message,
        ValidationMessages = validationMessages ?? [],
        Errors = errors ?? new Dictionary<string, string[]>()
    };
}

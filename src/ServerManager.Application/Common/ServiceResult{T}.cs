using FluentValidation.Results;

namespace ServerManager.Application.Common;

public sealed class ServiceResult<T> : ServiceResult
{
    private ServiceResult(bool isSuccess, ServiceErrorType errorType, string? message, IReadOnlyList<ServiceError> errors, T? data)
        : base(isSuccess, errorType, message, errors)
    {
        Data = data;
    }

    public T? Data { get; }

    public static ServiceResult<T> Success(T data, string? message = null) =>
        new(true, ServiceErrorType.None, message, [], data);

    public static new ServiceResult<T> Failure(string message, ServiceErrorType errorType = ServiceErrorType.Failure) =>
        new(false, errorType, message, [], default);

    public static new ServiceResult<T> NotFound(string message = "Kayıt bulunamadı.") =>
        new(false, ServiceErrorType.NotFound, message, [], default);

    public static new ServiceResult<T> ValidationFailure(IEnumerable<ServiceError> errors) =>
        new(false, ServiceErrorType.Validation, "Lütfen formdaki hataları düzeltin.", errors.ToList(), default);

    public static new ServiceResult<T> ValidationFailure(ValidationResult validationResult) =>
        ValidationFailure(ToErrors(validationResult));

    public static new ServiceResult<T> ValidationFailure(string propertyName, string message) =>
        ValidationFailure([new ServiceError(propertyName, message)]);
}

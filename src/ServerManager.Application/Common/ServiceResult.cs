using FluentValidation.Results;

namespace ServerManager.Application.Common;

public class ServiceResult
{
    protected ServiceResult(bool isSuccess, ServiceErrorType errorType, string? message, IReadOnlyList<ServiceError> errors)
    {
        IsSuccess = isSuccess;
        ErrorType = errorType;
        Message = message;
        Errors = errors;
    }

    public bool IsSuccess { get; }

    public ServiceErrorType ErrorType { get; }

    public string? Message { get; }

    public IReadOnlyList<ServiceError> Errors { get; }

    public static ServiceResult Success(string? message = null) =>
        new(true, ServiceErrorType.None, message, []);

    public static ServiceResult Failure(string message, ServiceErrorType errorType = ServiceErrorType.Failure) =>
        new(false, errorType, message, []);

    public static ServiceResult NotFound(string message = "Kayıt bulunamadı.") =>
        new(false, ServiceErrorType.NotFound, message, []);

    public static ServiceResult ValidationFailure(IEnumerable<ServiceError> errors) =>
        new(false, ServiceErrorType.Validation, "Lütfen formdaki hataları düzeltin.", errors.ToList());

    public static ServiceResult ValidationFailure(ValidationResult validationResult) =>
        ValidationFailure(ToErrors(validationResult));

    public static ServiceResult ValidationFailure(string propertyName, string message) =>
        ValidationFailure([new ServiceError(propertyName, message)]);

    protected static IEnumerable<ServiceError> ToErrors(ValidationResult validationResult) =>
        validationResult.Errors.Select(e => new ServiceError(e.PropertyName, e.ErrorMessage));
}

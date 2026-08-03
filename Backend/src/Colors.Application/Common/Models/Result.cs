namespace Colors.Application.Common.Models;

/// <summary>
/// Why an operation did not succeed. An enum rather than a message string, so the
/// API layer can choose the right HTTP status without matching on English text.
/// </summary>
public enum ErrorCode
{
    None = 0,

    /// <summary>The employee number or password was wrong.</summary>
    InvalidCredentials,

    /// <summary>Too many failed attempts. The account is locked for a short time.</summary>
    AccountLocked,

    /// <summary>The person no longer works here. Specification section 3 — users are deactivated, never deleted.</summary>
    AccountInactive,

    /// <summary>The refresh token is unknown, expired, or already used.</summary>
    InvalidRefreshToken,

    /// <summary>The request was well formed but broke a business rule.</summary>
    ValidationFailed,

    /// <summary>The thing being asked for does not exist.</summary>
    NotFound,
}

/// <summary>
/// The outcome of an operation that can fail for an expected reason.
///
/// Expected failures — a wrong password, a locked account — are values, not exceptions.
/// Exceptions are kept for genuine faults such as the database being unreachable.
/// </summary>
public sealed class Result<T>
{
    private Result(bool isSuccess, T? value, ErrorCode errorCode, string? message)
    {
        IsSuccess = isSuccess;
        Value = value;
        ErrorCode = errorCode;
        Message = message;
    }

    public bool IsSuccess { get; }

    public bool IsFailure => !IsSuccess;

    public T? Value { get; }

    public ErrorCode ErrorCode { get; }

    /// <summary>A message safe to show a worker. Never contains internal detail.</summary>
    public string? Message { get; }

    public static Result<T> Success(T value) => new(true, value, ErrorCode.None, null);

    public static Result<T> Failure(ErrorCode code, string message) => new(false, default, code, message);
}

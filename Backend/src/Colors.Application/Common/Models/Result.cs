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
    private Result(
        bool isSuccess,
        T? value,
        ErrorCode errorCode,
        string? message,
        string? messageCode = null,
        IReadOnlyList<string>? messageArgs = null)
    {
        IsSuccess = isSuccess;
        Value = value;
        ErrorCode = errorCode;
        Message = message;
        MessageCode = messageCode;
        MessageArgs = messageArgs;
    }

    public bool IsSuccess { get; }

    public bool IsFailure => !IsSuccess;

    public T? Value { get; }

    public ErrorCode ErrorCode { get; }

    /// <summary>A message safe to show a worker. Never contains internal detail.</summary>
    public string? Message { get; }

    /// <summary>
    /// Which refusal this is, as a name the screens can look up in whichever language
    /// the man chose — <c>pallet.alreadyGone</c> (specification section 12).
    ///
    /// <b>The English message stays regardless.</b> It is what the tests read, what a
    /// log line shows, and what an older screen falls back to. The code is added beside
    /// it rather than in place of it, so a refusal that has not been given one yet still
    /// says something a person can act on instead of showing him a key.
    ///
    /// <b>Translated on the screens, not here.</b> Every Arabic word in this system lives
    /// in one file so that correcting a word the factory says differently is one edit. A
    /// second copy in the backend would undo that the day somebody fixed only one of them.
    /// </summary>
    public string? MessageCode { get; }

    /// <summary>
    /// The values that belong in the message — a pallet number, a material name — in the
    /// order the wording uses them. Numbers, never sentences: a sentence here would be a
    /// sentence in English no matter what the screen is set to.
    /// </summary>
    public IReadOnlyList<string>? MessageArgs { get; }

    public static Result<T> Success(T value) => new(true, value, ErrorCode.None, null);

    public static Result<T> Failure(ErrorCode code, string message) =>
        new(false, default, code, message);

    /// <summary>The same refusal, with a name the screens can translate.</summary>
    public static Result<T> Failure(
        ErrorCode code,
        string message,
        string messageCode,
        params string[] messageArgs) =>
        new(false, default, code, message, messageCode, messageArgs);
}

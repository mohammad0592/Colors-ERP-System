namespace Colors.Application.Common.Auditing;

/// <summary>
/// Who is doing the thing being audited.
///
/// The audit log is written deep in Infrastructure, by an interceptor that has no idea a
/// web request exists. This is how it asks — implemented in the API layer, where the
/// signed-in worker actually is.
///
/// <b>It deliberately knows nothing about the database.</b> The interceptor already has
/// the context it is saving through, so it looks the open shift up itself. An earlier
/// version asked this for the shift as well, which meant the context's own options
/// resolved something that needed the context — a circle that simply hung on startup.
///
/// The user may be null: a background job has none, and a failed login has none yet.
/// </summary>
public interface ICurrentActor
{
    int? UserId { get; }
}

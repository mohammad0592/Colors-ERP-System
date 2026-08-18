using Colors.Application.Common.Auditing;
using Colors.Domain.Enums;

namespace Colors.Api.Auditing;

/// <summary>
/// How the code arrived, read from a header the screens set (specification section 12).
///
/// <b>A header rather than a field on every request.</b> The alternative was adding a
/// property to each shape that carries a barcode — scanning a bag, shipping a pallet,
/// starting a run, tracing a label — and threading it through every service that handles
/// one. This way the fact travels beside the request it describes, every endpoint that
/// takes a code is covered at once, and one added tomorrow is covered without anybody
/// remembering to.
///
/// <b>It is not to be trusted, and does not need to be.</b> A worker could set the header
/// by hand and claim a scan he typed. That is worth nothing to him: the value changes no
/// rule, refuses nothing, and unlocks nothing. It exists so a supervisor can see who
/// types a lot, and a man determined to lie about it was going to be a problem anyway.
///
/// <b>No database here</b>, for the same reason as <see cref="CurrentActor"/>: the
/// context's own options resolve the interceptor, so anything the interceptor depends on
/// must not need the context back.
/// </summary>
public class CurrentEntry(IHttpContextAccessor http) : ICurrentEntry
{
    /// <summary>The header the screens set. Absent on anything that has no code on it.</summary>
    public const string HeaderName = "X-Entry-Method";

    public EntryMethod Method
    {
        get
        {
            var raw = http.HttpContext?.Request.Headers[HeaderName].ToString();

            return Enum.TryParse<EntryMethod>(raw, ignoreCase: true, out var parsed)
                   && Enum.IsDefined(parsed)
                ? parsed
                : EntryMethod.Unknown;
        }
    }
}

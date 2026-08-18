using Colors.Domain.Enums;

namespace Colors.Application.Common.Auditing;

/// <summary>
/// How the code in this request arrived — scanned, typed or picked from a list
/// (specification section 12).
///
/// The same shape as <see cref="ICurrentActor"/>, and for the same reason. By the time a
/// barcode reaches a service it is a string like <c>B004501</c>, and nothing about it
/// says whether a camera read it or a man typed it. Only the web layer knows, so only
/// the web layer can answer.
///
/// <b>It is never required.</b> A request that says nothing is <see cref="EntryMethod.Unknown"/>,
/// which is the honest answer for a seeder, a test, or a screen with no code on it at all.
/// </summary>
public interface ICurrentEntry
{
    EntryMethod Method { get; }
}

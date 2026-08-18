namespace Colors.Domain.Enums;

/// <summary>
/// How a barcode reached the system (specification section 12).
///
/// The specification allows a man to type a code when a label is torn — work must not
/// stop for a ruined label — but asks that typing be <i>marked</i>, so a report can show
/// typing rates per user. That controls the behaviour better than a ban the workers
/// would simply have to break.
///
/// Only the web layer can know this: by the time a service sees a barcode, a scanned one
/// and a typed one are the same string.
/// </summary>
public enum EntryMethod
{
    /// <summary>Nothing said. A request from somewhere that does not scan, or an older client.</summary>
    Unknown = 0,

    /// <summary>Read off the label by the camera.</summary>
    Scanned = 1,

    /// <summary>Typed by hand, which is what a torn label leads to.</summary>
    Typed = 2,

    /// <summary>Chosen from the list of what the screen already knows about.</summary>
    Picked = 3,
}

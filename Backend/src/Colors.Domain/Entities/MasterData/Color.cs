using Colors.Domain.Common;

namespace Colors.Domain.Entities.MasterData;

/// <summary>
/// A plate colour. The single-letter code goes into the roll code —
/// W in 01WN180726A means White (specification section 8).
/// </summary>
public class Color : MasterEntity
{
    /// <summary>One capital letter, unique: W, G, Y, B.</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// True for black, and only black. It decides which recipes may be used with this
    /// colour: a black-only recipe needs it, and every other recipe refuses it
    /// (specification section 5).
    ///
    /// A flag rather than a check on the name or the letter <c>B</c>, because renaming
    /// a colour — or adding Blue, which also starts with B — must never quietly change
    /// which recipes the factory is allowed to run.
    /// </summary>
    public bool IsBlack { get; set; }
}

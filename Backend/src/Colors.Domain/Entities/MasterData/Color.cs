using Colors.Domain.Common;

namespace Colors.Domain.Entities.MasterData;

/// <summary>
/// A plate colour. The single-letter code goes into the roll code —
/// W in 01WN180726A means White (specification section 8).
///
/// Any colour may be made on any recipe, black included (specification section 5).
/// </summary>
public class Color : MasterEntity
{
    /// <summary>One capital letter, unique: W, G, Y, B.</summary>
    public string Code { get; set; } = string.Empty;
}

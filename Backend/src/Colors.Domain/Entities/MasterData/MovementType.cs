using Colors.Domain.Common;

namespace Colors.Domain.Entities.MasterData;

/// <summary>
/// Why stock moved, and which way (specification section 4).
///
/// The direction lives here as data rather than in code. A balance is
/// <c>SUM(Quantity × Direction)</c> and <c>Quantity</c> is always positive, so a sign
/// error cannot be stored in the first place.
/// </summary>
public class MovementType : MasterEntity
{
    /// <summary>+1 into the store, −1 out of it. Never anything else.</summary>
    public int Direction { get; set; }

    public bool IsIncoming => Direction > 0;
}

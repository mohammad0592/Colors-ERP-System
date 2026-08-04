namespace Colors.Domain.Common;

/// <summary>
/// Base for master data — things production references, that change rarely and are
/// never deleted. A row is deactivated instead, so every historical record that
/// points at it keeps resolving (specification section 4).
/// </summary>
public abstract class MasterEntity
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>False hides it from pickers. Existing records keep referencing it.</summary>
    public bool IsActive { get; set; } = true;
}

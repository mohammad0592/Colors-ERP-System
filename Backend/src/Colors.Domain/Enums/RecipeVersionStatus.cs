namespace Colors.Domain.Enums;

/// <summary>
/// Where a recipe version stands. Specification section 5: a draft may be edited,
/// but once it becomes Current it is frozen for ever, because rolls point at it and
/// the exact formula that made them must never change.
/// </summary>
public enum RecipeVersionStatus
{
    /// <summary>Being written. Editable. No roll may use it.</summary>
    Draft = 0,

    /// <summary>In production. Frozen. Exactly one per family.</summary>
    Current = 1,

    /// <summary>Replaced by a newer version. Frozen, and kept for ever.</summary>
    Archived = 2,
}

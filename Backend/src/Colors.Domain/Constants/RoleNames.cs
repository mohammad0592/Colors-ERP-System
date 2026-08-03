namespace Colors.Domain.Constants;

/// <summary>
/// The nine roles from specification section 3.
///
/// These names are written into the database, into every JWT, and into every
/// <c>[Authorize(Roles = ...)]</c> attribute. A typo in a literal string would make an
/// endpoint silently deny everyone — no error, no log line, nothing to search for.
/// So the names live here once and nowhere else.
///
/// One person may hold several roles. Today the same man is usually both
/// <see cref="ExtruderOperator"/> and <see cref="ExtruderTestPerson"/>, and both
/// <see cref="ThermoOperator"/> and <see cref="PackagingOperator"/>. When the factory
/// hires someone, an administrator moves the role — no code change.
/// </summary>
public static class RoleNames
{
    /// <summary>Users, master data, recipes, system settings, backups, corrections.</summary>
    public const string Administrator = "Administrator";

    /// <summary>Recipe versions, inventory adjustments, opening and closing shifts, approving corrections.</summary>
    public const string Supervisor = "Supervisor";

    /// <summary>Receives materials, issues tickets, records returns.</summary>
    public const string InventoryManager = "InventoryManager";

    /// <summary>Line 1 — creates batches and rolls, prints roll barcodes.</summary>
    public const string ExtruderOperator = "ExtruderOperator";

    /// <summary>Line 1 — records roll test reports: weight, length, plate weight, four thickness readings.</summary>
    public const string ExtruderTestPerson = "ExtruderTestPerson";

    /// <summary>Line 2 — consumes rolls, records forming, creates produced bags.</summary>
    public const string ThermoOperator = "ThermoOperator";

    /// <summary>Line 2 — records thermo test reports: plate weight after forming, absorbent percentage.</summary>
    public const string ThermoTestPerson = "ThermoTestPerson";

    /// <summary>Line 2 — creates pallets, scans bags onto them, records packaging materials.</summary>
    public const string PackagingOperator = "PackagingOperator";

    /// <summary>Line 3 — records scrap weight and recycled output.</summary>
    public const string RecyclerOperator = "RecyclerOperator";

    /// <summary>Every role, in the order they appear in the specification. Used to seed the database.</summary>
    public static readonly IReadOnlyList<string> All =
    [
        Administrator,
        Supervisor,
        InventoryManager,
        ExtruderOperator,
        ExtruderTestPerson,
        ThermoOperator,
        ThermoTestPerson,
        PackagingOperator,
        RecyclerOperator,
    ];
}

namespace Colors.Domain.Constants;

/// <summary>
/// The seven roles from specification section 3.
///
/// These names are written into the database, into every JWT, and into every
/// <c>[Authorize(Roles = ...)]</c> attribute. A typo in a literal string would make an
/// endpoint silently deny everyone — no error, no log line, nothing to search for.
/// So the names live here once and nowhere else.
///
/// One role a line (section 19.31). Testing a line's output and packing it were once
/// roles of their own; the factory runs each line with one crew, so the line's operator
/// does all of its work, and one role is one less thing to get wrong in Users. A person
/// may still hold several roles — a man who runs two lines holds both.
/// </summary>
public static class RoleNames
{
    /// <summary>Users, master data, recipes, system settings, backups, corrections.</summary>
    public const string Administrator = "Administrator";

    /// <summary>Opening, closing and planning shifts, inventory adjustments, approving corrections.</summary>
    public const string Supervisor = "Supervisor";

    /// <summary>Receives materials, issues tickets, records returns.</summary>
    public const string InventoryManager = "InventoryManager";

    /// <summary>Line 1 — batches and rolls, weighing them and testing them.</summary>
    public const string ExtruderOperator = "ExtruderOperator";


    /// <summary>Line 2 — forming rolls, counting and testing what came out, pallets and packaging.</summary>
    public const string ThermoOperator = "ThermoOperator";


    /// <summary>Line 3 — records scrap weight and recycled output.</summary>
    public const string RecyclerOperator = "RecyclerOperator";

    /// <summary>
    /// The computer the label printer hangs off — it prints the labels the tablets send
    /// (specification section 19.13). Sees the print queue and nothing else.
    /// </summary>
    public const string LabelPrinter = "LabelPrinter";

    /// <summary>Every role, in the order they appear in the specification. Used to seed the database.</summary>
    public static readonly IReadOnlyList<string> All =
    [
        Administrator,
        Supervisor,
        InventoryManager,
        ExtruderOperator,
        ThermoOperator,
        RecyclerOperator,
        LabelPrinter,
    ];
}

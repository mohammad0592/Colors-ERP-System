namespace Colors.Domain.Constants;

/// <summary>
/// The movement types from specification section 4.
///
/// The rows themselves are master data an administrator can rename, but the system
/// has to be able to find the one it means when it posts a receive or an adjustment.
/// The names live here once so a typo is a build error, not an endpoint that fails at
/// the store counter.
/// </summary>
public static class MovementTypeNames
{
    public const string Receive = "Receive";
    public const string Issue = "Issue";
    public const string Return = "Return";
    public const string ProductionOutput = "Production";
    public const string AdjustmentIn = "Adjustment In";
    public const string AdjustmentOut = "Adjustment Out";
    public const string PackagingConsumption = "Packaging Consumption";

    /// <summary>Every type with its direction, in the order the specification lists them.</summary>
    public static readonly IReadOnlyList<(string Name, int Direction)> All =
    [
        (Receive, +1),
        (Issue, -1),
        (Return, +1),
        (ProductionOutput, +1),
        (AdjustmentIn, +1),
        (AdjustmentOut, -1),
        (PackagingConsumption, -1),
    ];
}

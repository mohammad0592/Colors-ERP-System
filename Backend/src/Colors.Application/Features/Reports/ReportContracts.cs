using Colors.Application.Common.Models;

namespace Colors.Application.Features.Reports;

/// <summary>Shapes crossing the API for reports. Specification section 13.</summary>

// ---------- material waste control ----------

/// <summary>
/// One material on the waste report: what left the store for this shift, what came back,
/// and what the recipe says should have been used.
/// </summary>
public sealed record MaterialWasteLineDto(
    int MaterialId,
    string MaterialCode,
    string MaterialName,
    string UnitSymbol,
    // True for the polymer that forms the recipe's 100% base — GPPS and recycle. The
    // additives are measured against it (specification section 5).
    bool IsBaseResin,
    decimal Issued,
    decimal Returned,
    decimal NetUsed,
    /// <summary>The recipe's target for this material, or null where it names none.</summary>
    decimal? TargetPercentage,
    /// <summary>Target percentage of the resin actually used. Null where unknown.</summary>
    decimal? Required,
    /// <summary>Used less required. Positive means more went in than the recipe asks.</summary>
    decimal? Difference,
    /// <summary>The difference as a share of what was required. Null where required is nothing.</summary>
    decimal? DifferencePercentage,
    /// <summary>True where the used share falls outside the min–max the supervisor set.</summary>
    bool OutsideRange);

public sealed record MaterialWasteReportDto(
    int ShiftReportId,
    DateOnly ProductionDate,
    string ShiftName,
    string Status,
    /// <summary>The recipe every roll on this shift was made to, where there is only one.</summary>
    int? RecipeNumber,
    string? RecipeFamilyName,
    int? RecipeVersionNumber,
    /// <summary>
    /// How many different recipes the shift's rolls used. More than one and there is no
    /// single requirement to compare against, so the required column is left empty.
    /// </summary>
    int RecipeCount,
    /// <summary>Base resin actually used — the 100% the percentages are shares of.</summary>
    decimal ResinUsed,
    /// <summary>What the extruder made, for reading the usage against.</summary>
    int RollsProduced,
    decimal RollWeightProduced,
    IReadOnlyList<MaterialWasteLineDto> Lines);

// ---------- shift production summary ----------

/// <summary>
/// One product made during the shift, in the shape of the paper form's summary block.
/// </summary>
public sealed record ShiftProductLineDto(
    int ProductId,
    string ProductName,
    int RollsUsed,
    decimal RollWeightUsed,
    int BagCount,
    int PieceCount,
    /// <summary>Pieces × each roll's own measured plate weight, never one shared figure.</summary>
    decimal ProductWeight,
    /// <summary>Roll weight less product weight — what the forming threw away.</summary>
    decimal LossWeight,
    /// <summary>Loss as a share of the roll weight. Null where no roll was weighed.</summary>
    decimal? LossPercentage);

public sealed record ShiftSummaryReportDto(
    int ShiftReportId,
    DateOnly ProductionDate,
    string ShiftName,
    string Status,
    string? SupervisorName,
    decimal? ElectricityUsed,
    // The extruder's side of the shift.
    int RollsProduced,
    decimal RollWeightProduced,
    // The thermo's side, then the same again per product.
    int RollsFormed,
    decimal RollWeightUsed,
    int BagCount,
    int PieceCount,
    decimal ProductWeight,
    decimal LossWeight,
    decimal? LossPercentage,
    // The packing and recycling sides.
    int PalletsBuilt,
    int PalletsCompleted,
    decimal RecycledMaterialProduced,
    IReadOnlyList<ShiftProductLineDto> Products);

/// <summary>
/// Reports (specification section 13).
///
/// Every one of these is a <b>read</b> over records that already exist. Nothing here
/// stores a figure, so no report can drift out of step with the data underneath it.
///
/// Declared here, implemented in Infrastructure.
/// </summary>
public interface IReportsService
{
    /// <summary>
    /// What the shift took out of the store against what its recipe asks for.
    ///
    /// Measured per <b>shift</b>, not per mix: an issue ticket names a shift line and
    /// never gained a batch, so the true sentence is "issued to the shift that made
    /// these rolls" (specification section 13).
    /// </summary>
    Task<Result<MaterialWasteReportDto>> GetMaterialWasteAsync(
        int shiftReportId,
        CancellationToken cancellationToken = default);

    /// <summary>The paper form's summary block, worked out rather than typed.</summary>
    Task<Result<ShiftSummaryReportDto>> GetShiftSummaryAsync(
        int shiftReportId,
        CancellationToken cancellationToken = default);
}

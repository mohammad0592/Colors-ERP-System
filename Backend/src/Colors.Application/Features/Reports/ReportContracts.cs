using Colors.Application.Common.Models;

namespace Colors.Application.Features.Reports;

// Shapes crossing the API for reports. Specification section 13.

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
    // The recipe's target for this material, or null where it names none.
    decimal? TargetPercentage,
    // Target percentage of the resin actually used. Null where unknown.
    decimal? Required,
    // Used less required. Positive means more went in than the recipe asks.
    decimal? Difference,
    // The difference as a share of what was required. Null where required is nothing.
    decimal? DifferencePercentage,
    // True where the used share falls outside the min–max the supervisor set.
    bool OutsideRange);

public sealed record MaterialWasteReportDto(
    int ShiftReportId,
    DateOnly ProductionDate,
    string ShiftName,
    string Status,
    // The recipe every roll on this shift was made to, where there is only one.
    int? RecipeNumber,
    string? RecipeFamilyName,
    int? RecipeVersionNumber,
    // How many different recipes the shift's rolls used. More than one and there is no
    // single requirement to compare against, so the required column is left empty.
    int RecipeCount,
    // Base resin actually used — the 100% the percentages are shares of.
    decimal ResinUsed,
    // What the extruder made, for reading the usage against.
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
    // Pieces × each roll's own measured plate weight, never one shared figure.
    decimal ProductWeight,
    // Roll weight less product weight — what the forming threw away.
    decimal LossWeight,
    // Loss as a share of the roll weight. Null where no roll was weighed.
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

// ---------- consumption ----------

/// <summary>How much of one material a group of shifts consumed.</summary>
public sealed record ConsumptionMaterialDto(
    int MaterialId,
    string MaterialCode,
    string MaterialName,
    string UnitSymbol,
    decimal Issued,
    decimal Returned,
    decimal NetUsed,
    // Used per kilogram of roll the group produced, so a long shift and a short one can
    // be compared. Null where nothing was weighed off the extruder.
    decimal? PerKilogramOfRoll);

/// <summary>One shift, or one recipe, with everything it consumed.</summary>
public sealed record ConsumptionGroupDto(
    string Label,
    int? ShiftReportId,
    DateOnly? ProductionDate,
    string? ShiftName,
    int? RecipeNumber,
    string? RecipeFamilyName,
    // How many shifts are behind this row. Always 1 when grouped by shift.
    int Shifts,
    int RollsProduced,
    decimal RollWeightProduced,
    decimal TotalUsed,
    IReadOnlyList<ConsumptionMaterialDto> Materials);

public sealed record ConsumptionReportDto(
    DateOnly From,
    DateOnly To,
    string GroupedBy,
    IReadOnlyList<ConsumptionGroupDto> Groups,
    // Shifts left out of a by-recipe report because they ran more than one recipe, so
    // their material cannot be attributed to either. Counted and said, never dropped in
    // silence.
    int MixedRecipeShifts);

/// <summary>How consumption is grouped.</summary>
public enum ConsumptionGrouping
{
    Shift = 1,
    Recipe = 2,
}

// ---------- pallet production ----------

/// <summary>One product, and the pallets of it that were finished.</summary>
public sealed record PalletProductLineDto(
    int ProductId,
    string ProductName,
    int PalletsCompleted,
    int Bags,
    int Pieces,
    decimal Weight,
    // The product's own bags-per-pallet, for reading the bag count against.
    int BagsPerPallet);

public sealed record PalletProductionReportDto(
    DateOnly From,
    DateOnly To,
    int PalletsStarted,
    int PalletsCompleted,
    // Started and given up on. Their wood went back, so they consumed nothing.
    int PalletsCancelled,
    // Started, still being filled, and not yet finished. They have no product until
    // their first bag lands, so they cannot be counted under one — shown on their own
    // rather than left out silently.
    int PalletsStillOpen,
    IReadOnlyList<PalletProductLineDto> Products);

// ---------- recycled material ----------

/// <summary>What one shift's recycler produced.</summary>
public sealed record RecycledShiftLineDto(
    int ShiftReportId,
    DateOnly ProductionDate,
    string ShiftName,
    string ProductionLineName,
    decimal Produced,
    string RecordedByName,
    string? Notes);

public sealed record RecycledMaterialReportDto(
    DateOnly From,
    DateOnly To,
    string? MaterialName,
    decimal TotalProduced,
    // How much of it the mixer took back out over the same days — the black recipes are
    // the only thing that consumes it (specification section 5).
    decimal TotalConsumed,
    // Produced less consumed. Negative means the pile is being drawn down.
    decimal Difference,
    // What the store holds now, which no date range can change.
    decimal InStock,
    IReadOnlyList<RecycledShiftLineDto> Shifts);

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

    /// <summary>
    /// What was consumed over a stretch of days, by shift or by recipe.
    ///
    /// By recipe only counts a shift whose rolls were all made to <b>one</b> recipe. A
    /// shift that switched recipe cannot say which of them its material went into, so it
    /// is left out and counted separately rather than guessed at.
    /// </summary>
    Task<Result<ConsumptionReportDto>> GetConsumptionAsync(
        DateOnly from,
        DateOnly to,
        ConsumptionGrouping grouping,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Pallets finished over a stretch of days, by the product on them.
    ///
    /// A pallet takes its product from its first bag, so one still empty belongs to no
    /// product yet and is counted on its own.
    /// </summary>
    Task<Result<PalletProductionReportDto>> GetPalletProductionAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Recycled material made over a stretch of days, against how much the mixer took
    /// back out — the black recipes are the only thing that consumes it.
    /// </summary>
    Task<Result<RecycledMaterialReportDto>> GetRecycledMaterialAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default);
}

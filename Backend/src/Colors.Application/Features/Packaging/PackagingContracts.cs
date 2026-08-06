using Colors.Application.Common.Models;

namespace Colors.Application.Features.Packaging;

/// <summary>Shapes crossing the API for packaging. Specification section 10.</summary>

/// <summary>
/// One packaging material on the form, ready to be filled in.
///
/// Three of them arrive already answered, because what the shift produced says how many
/// were used. The rest are blank and typed, as the paper form has them.
/// </summary>
public sealed record PackagingLineDto(
    int MaterialId,
    string MaterialCode,
    string MaterialName,
    string UnitSymbol,
    // LargeBag · SmallBag · WoodenPallet · None.
    string CountedAs,
    bool IsCounted,
    decimal Quantity,
    decimal? Weight,
    // Quantity × the material's unit weight, when it has one. The gap against the
    // weighed figure is packaging torn, wasted or used elsewhere.
    decimal? ExpectedWeight,
    decimal? WeightDifference,
    // In stock now, so a figure larger than the store holds is obvious before saving.
    decimal InStock);

public sealed record PackagingConsumptionDto(
    int Id,
    int ShiftLineId,
    string ProductionLineName,
    string ShiftName,
    DateOnly ProductionDate,
    string RecordedByName,
    DateTimeOffset RecordedAt,
    string? Notes,
    IReadOnlyList<PackagingLineDto> Lines);

/// <summary>
/// The form before it is saved: every packaging material, with the counted ones already
/// filled in from the shift's own production.
///
/// The wooden pallet is not on it. That one leaves the store as each pallet is started,
/// so there is nothing left to record here (specification section 10).
/// </summary>
public sealed record PackagingDraftDto(
    int ShiftLineId,
    string ProductionLineName,
    string ShiftName,
    DateOnly ProductionDate,
    // What the counts were worked out from, so the operator can see why they are what
    // they are rather than being handed a number to trust.
    int BagsProduced,
    // Shown for the shift's shape only — the wood for these is already out of the store.
    int PalletsStarted,
    bool AlreadyRecorded,
    IReadOnlyList<PackagingLineDto> Lines);

/// <summary>
/// One line as the operator leaves it. The counted ones come back unchanged — the
/// server works them out again rather than believing the screen.
/// </summary>
public sealed record SavePackagingLineRequest(
    int MaterialId,
    decimal Quantity,
    decimal? Weight);

public sealed record SavePackagingRequest(
    int ShiftLineId,
    IReadOnlyList<SavePackagingLineRequest> Lines,
    string? Notes);

/// <summary>
/// Packaging consumption (specification section 10).
///
/// Declared here, implemented in Infrastructure.
/// </summary>
public interface IPackagingService
{
    Task<IReadOnlyList<PackagingConsumptionDto>> GetAllAsync(
        int? shiftReportId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The form for one line of a shift, with the counted materials already worked out.
    /// </summary>
    Task<Result<PackagingDraftDto>> GetDraftAsync(
        int shiftLineId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Records what the shift used and takes it out of the store — every line, or none.
    /// </summary>
    Task<Result<PackagingConsumptionDto>> SaveAsync(
        SavePackagingRequest request,
        int userId,
        CancellationToken cancellationToken = default);
}

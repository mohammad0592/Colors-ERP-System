using Colors.Application.Common.Models;

namespace Colors.Application.Features.Recycler;

/// <summary>Shapes crossing the API for the recycler. Specification section 11.</summary>

public sealed record RecyclerProductionDto(
    int Id,
    int ShiftLineId,
    string ProductionLineName,
    string ShiftName,
    DateOnly ProductionDate,
    decimal ScrapWeight,
    decimal RecycledMaterialWeight,
    // Calculated, never stored. Null where no scrap was weighed, because a share of
    // nothing is not a number. Negative where more came out than went in.
    decimal? LossPercentage,
    string RecordedByName,
    DateTimeOffset RecordedAt,
    string? Notes);

/// <summary>
/// The form before it is saved.
///
/// It carries what the thermo already calculated, so the operator can see the free check
/// from section 11 while typing the weighed figure — shown, never enforced.
/// </summary>
public sealed record RecyclerDraftDto(
    int ShiftLineId,
    string ProductionLineName,
    string ShiftName,
    DateOnly ProductionDate,
    /// <summary>What the thermo lines of this shift lost, by their own calculation.</summary>
    decimal? ThermoCalculatedScrap,
    /// <summary>The material the output is added to, so the screen can name it.</summary>
    string? RecycledMaterialName,
    bool AlreadyRecorded,
    RecyclerProductionDto? Recorded);

/// <summary>
/// Scrap weighed in and recycled material weighed out, both in kilograms.
///
/// Recycled may be more than scrap: the recycler grinds what is in front of it, and that
/// can include a pile left from an earlier shift.
/// </summary>
public sealed record SaveRecyclerProductionRequest(
    int ShiftLineId,
    decimal ScrapWeight,
    decimal RecycledMaterialWeight,
    string? Notes);

/// <summary>
/// The recycler (specification section 11).
///
/// Declared here, implemented in Infrastructure.
/// </summary>
public interface IRecyclerService
{
    Task<IReadOnlyList<RecyclerProductionDto>> GetAllAsync(
        int? shiftReportId = null,
        CancellationToken cancellationToken = default);

    Task<Result<RecyclerDraftDto>> GetDraftAsync(
        int shiftLineId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes the record and posts one <c>Production</c> movement that adds the output
    /// to the store. Once per line of the shift.
    /// </summary>
    Task<Result<RecyclerProductionDto>> SaveAsync(
        SaveRecyclerProductionRequest request,
        int userId,
        CancellationToken cancellationToken = default);
}

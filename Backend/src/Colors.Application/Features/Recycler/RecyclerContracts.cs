using Colors.Application.Common.Models;

namespace Colors.Application.Features.Recycler;

/// <summary>Shapes crossing the API for the recycler. Specification section 11.</summary>

public sealed record RecyclerProductionDto(
    int Id,
    int ShiftLineId,
    string ProductionLineName,
    string ShiftName,
    DateOnly ProductionDate,
    // The whole record. What went into the grinder cannot be weighed — scrap lives in
    // two silos and is drawn out to be ground (specification section 11).
    decimal RecycledMaterialWeight,
    string RecordedByName,
    DateTimeOffset RecordedAt,
    string? Notes);

/// <summary>
/// The form before it is saved. One box on it.
///
/// It carries nothing about the thermo. Comparing calculated waste against weighed scrap
/// is not possible at all — the scrap is never on a scale — and the thermo's own waste
/// figure lives on the thermo's screens (specification sections 9 and 11).
/// </summary>
public sealed record RecyclerDraftDto(
    int ShiftLineId,
    string ProductionLineName,
    string ShiftName,
    DateOnly ProductionDate,
    /// <summary>The material the output is added to, so the screen can name it.</summary>
    string? RecycledMaterialName,
    bool AlreadyRecorded,
    RecyclerProductionDto? Recorded);

/// <summary>
/// Recycled material weighed out, in kilograms. Must be more than nothing — a record
/// saying the recycler produced zero is not a record.
/// </summary>
public sealed record SaveRecyclerProductionRequest(
    int ShiftLineId,
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

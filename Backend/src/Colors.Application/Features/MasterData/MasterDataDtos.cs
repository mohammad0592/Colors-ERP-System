namespace Colors.Application.Features.MasterData;

/// <summary>
/// Shapes crossing the API for master data. Times travel as "HH:mm" strings and
/// quantities as decimals; entity types never leave the server.
/// </summary>

/// <summary>A name-only master row: production line, category, plate size, product type.</summary>
public sealed record LookupDto(int Id, string Name, bool IsActive);

public sealed record SaveLookupRequest(string Name);

public sealed record UnitDto(int Id, string Name, string Symbol, bool IsActive);

public sealed record SaveUnitRequest(string Name, string Symbol);

public sealed record ColorDto(int Id, string Name, string Code, bool IsActive);

/// <param name="Code">One capital letter for the roll code: W, G, Y, B.</param>
public sealed record SaveColorRequest(string Name, string Code);

public sealed record ShiftDto(int Id, string Name, string StartTime, string EndTime, bool IsActive);

/// <param name="StartTime">"HH:mm", e.g. "08:00".</param>
/// <param name="EndTime">"HH:mm"; "00:00" means midnight at the end of the day.</param>
public sealed record SaveShiftRequest(string Name, string StartTime, string EndTime);

public sealed record MaterialPackagingDto(
    int Id,
    int UnitId,
    string UnitName,
    decimal QuantityInBaseUnit,
    bool IsDefaultReceiving);

public sealed record SaveMaterialPackagingRequest(
    int UnitId,
    decimal QuantityInBaseUnit,
    bool IsDefaultReceiving);

public sealed record MaterialDto(
    int Id,
    string Code,
    string Name,
    int CategoryId,
    string CategoryName,
    int BaseUnitId,
    string BaseUnitName,
    string BaseUnitSymbol,
    decimal MinQuantity,
    decimal? UnitWeight,
    string? Notes,
    bool IsActive,
    IReadOnlyList<MaterialPackagingDto> Packagings);

public sealed record SaveMaterialRequest(
    string Code,
    string Name,
    int CategoryId,
    int BaseUnitId,
    decimal MinQuantity,
    decimal? UnitWeight,
    string? Notes,
    IReadOnlyList<SaveMaterialPackagingRequest> Packagings);

/// <summary>Body of the activate/deactivate endpoint. Master data is never deleted.</summary>
public sealed record SetActiveRequest(bool IsActive);

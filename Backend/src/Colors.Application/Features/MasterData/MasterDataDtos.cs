namespace Colors.Application.Features.MasterData;

/// <summary>
/// Shapes crossing the API for master data. Times travel as "HH:mm" strings and
/// quantities as decimals; entity types never leave the server.
/// </summary>

/// <summary>A name-only master row: production line, category, plate size, product type.</summary>
/// <remarks>
/// CanDelete is false as soon as anything references the row. The screen hides the
/// delete button rather than offering one that always fails — only the client cannot
/// know what references a row, so the server says.
/// </remarks>
public sealed record LookupDto(int Id, string Name, bool IsActive, bool CanDelete);

public sealed record SaveLookupRequest(string Name);

/// <summary>
/// A production line. <c>RecordsMachineSettings</c> is true only for the thermo line —
/// it is what decides whether the shift report asks for speed, feed distance and
/// cycle time.
/// </summary>
public sealed record ProductionLineDto(
    int Id,
    string Name,
    bool RecordsMachineSettings,
    bool IsActive,
    bool CanDelete);

public sealed record SaveProductionLineRequest(string Name, bool RecordsMachineSettings);

/// <summary>
/// Something the factory makes. <c>IsAbsorbent</c> together with the mould is what the
/// thermo looks a product up by, so the pair is unique.
/// </summary>
public sealed record ProductDto(
    int Id,
    string Name,
    int MouldId,
    string MouldName,
    int ProductTypeId,
    string ProductTypeName,
    bool IsAbsorbent,
    int PiecesPerBag,
    int SmallBagsPerBag,
    int BagsPerPallet,
    bool IsActive,
    bool CanDelete);

public sealed record SaveProductRequest(
    string Name,
    int MouldId,
    int ProductTypeId,
    bool IsAbsorbent,
    int PiecesPerBag,
    int SmallBagsPerBag,
    int BagsPerPallet);

/// <summary>
/// A material category. <c>IssuedOnTickets</c> is what decides whether its materials
/// may go out on an issue ticket — true for raw material, false for packaging, which
/// the system counts from production instead.
/// </summary>
public sealed record MaterialCategoryDto(
    int Id,
    string Name,
    bool IssuedOnTickets,
    bool IsActive,
    bool CanDelete);

public sealed record SaveMaterialCategoryRequest(string Name, bool IssuedOnTickets);

public sealed record UnitDto(int Id, string Name, string Symbol, bool IsActive, bool CanDelete);

public sealed record SaveUnitRequest(string Name, string Symbol);

public sealed record ColorDto(int Id, string Name, string Code, bool IsActive, bool CanDelete);

// Code is one capital letter for the roll code: W, G, Y, B.
public sealed record SaveColorRequest(string Name, string Code);

public sealed record ShiftDto(
    int Id,
    string Name,
    string StartTime,
    string EndTime,
    bool IsActive,
    bool CanDelete);

// Times are "HH:mm"; "00:00" means midnight at the end of the day.
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
    bool CanDelete,
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

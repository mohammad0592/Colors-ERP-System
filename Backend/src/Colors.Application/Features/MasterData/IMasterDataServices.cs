using Colors.Application.Common.Models;

namespace Colors.Application.Features.MasterData;

/// <summary>
/// What every master data list can do. Declared here, implemented in Infrastructure —
/// this layer must not know how the data is stored (specification section 0.1).
///
/// Deletion follows the rule in specification section 4: allowed only while nothing
/// references the row. A referenced row can only be deactivated, so that history
/// keeps resolving.
/// </summary>
public interface IMasterListService<TDto, TUpsert>
{
    /// <summary>Active rows by default; the management screen asks for everything.</summary>
    Task<IReadOnlyList<TDto>> GetAllAsync(bool includeInactive = false, CancellationToken cancellationToken = default);

    Task<Result<TDto>> CreateAsync(TUpsert request, CancellationToken cancellationToken = default);

    Task<Result<TDto>> UpdateAsync(int id, TUpsert request, CancellationToken cancellationToken = default);

    Task<Result<TDto>> SetActiveAsync(int id, bool isActive, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a row that nothing references — a typo, a test. Fails with
    /// <see cref="ErrorCode.ValidationFailed"/> and a message naming what uses it
    /// when it is referenced.
    /// </summary>
    Task<Result<bool>> DeleteAsync(int id, CancellationToken cancellationToken = default);
}

// One named interface per list, so registration and injection stay explicit.

public interface IProductionLineService
    : IMasterListService<ProductionLineDto, SaveProductionLineRequest>;

public interface IShiftService : IMasterListService<ShiftDto, SaveShiftRequest>;

public interface IUnitService : IMasterListService<UnitDto, SaveUnitRequest>;

public interface IMaterialCategoryService
    : IMasterListService<MaterialCategoryDto, SaveMaterialCategoryRequest>;

public interface IColorService : IMasterListService<ColorDto, SaveColorRequest>;

public interface IMouldService : IMasterListService<LookupDto, SaveLookupRequest>;

public interface IProductService : IMasterListService<ProductDto, SaveProductRequest>;

public interface IProductTypeService : IMasterListService<LookupDto, SaveLookupRequest>;

public interface IMaterialService : IMasterListService<MaterialDto, SaveMaterialRequest>;

using System.Globalization;
using Colors.Application.Features.MasterData;
using Colors.Domain.Entities.MasterData;
using Colors.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Colors.Infrastructure.Services.MasterData;

/// <summary>
/// The concrete services for the simple lists. Each supplies only its mapping and
/// its validation; everything else comes from the base class.
/// </summary>

/// <summary>Shared by the name-only lists so their identical mapping is written once.</summary>
public abstract class NameOnlyService<TEntity>(ColorsDbContext db)
    : MasterListService<TEntity, LookupDto, SaveLookupRequest>(db)
    where TEntity : Domain.Common.MasterEntity, new()
{
    protected override LookupDto ToDto(TEntity entity, bool canDelete) =>
        new(entity.Id, entity.Name, entity.IsActive, canDelete);

    protected override void Apply(SaveLookupRequest request, TEntity entity) =>
        entity.Name = request.Name.Trim();

    protected override async Task<string?> ValidateAsync(
        SaveLookupRequest request,
        int? existingId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return "A name is required.";
        }

        if (await NameTakenAsync(request.Name, existingId, cancellationToken))
        {
            return "A row with this name already exists.";
        }

        return null;
    }
}

public class ProductionLineService(ColorsDbContext db)
    : MasterListService<ProductionLine, ProductionLineDto, SaveProductionLineRequest>(db),
      IProductionLineService
{
    protected override ProductionLineDto ToDto(ProductionLine entity, bool canDelete) =>
        new(entity.Id, entity.Name, entity.RecordsMachineSettings, entity.IsActive, canDelete);

    protected override void Apply(SaveProductionLineRequest request, ProductionLine entity)
    {
        entity.Name = request.Name.Trim();
        entity.RecordsMachineSettings = request.RecordsMachineSettings;
    }

    protected override async Task<string?> ValidateAsync(
        SaveProductionLineRequest request,
        int? existingId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return "A name is required.";
        }

        return await NameTakenAsync(request.Name, existingId, cancellationToken)
            ? "A production line with this name already exists."
            : null;
    }

    protected override async Task<string?> CanDeleteAsync(
        ProductionLine entity,
        CancellationToken cancellationToken)
    {
        var used = await Db.ShiftLines.CountAsync(
            l => l.ProductionLineId == entity.Id,
            cancellationToken);

        return used == 0
            ? null
            : $"Used by {used} shift{(used == 1 ? "" : "s")} — deactivate it instead.";
    }

    protected override async Task<HashSet<int>> ReferencedIdsAsync(CancellationToken cancellationToken) =>
        [.. await Db.ShiftLines.Select(l => l.ProductionLineId).Distinct().ToListAsync(cancellationToken)];
}

public class MaterialCategoryService(ColorsDbContext db) : NameOnlyService<MaterialCategory>(db), IMaterialCategoryService
{
    protected override async Task<string?> CanDeleteAsync(
        MaterialCategory entity,
        CancellationToken cancellationToken)
    {
        var used = await Db.Materials.CountAsync(m => m.CategoryId == entity.Id, cancellationToken);
        return used == 0
            ? null
            : $"Used by {used} material{(used == 1 ? "" : "s")} — deactivate it instead.";
    }

    protected override async Task<HashSet<int>> ReferencedIdsAsync(CancellationToken cancellationToken) =>
        [.. await Db.Materials.Select(m => m.CategoryId).Distinct().ToListAsync(cancellationToken)];
}

public class MouldService(ColorsDbContext db) : NameOnlyService<Mould>(db), IMouldService
{
    protected override async Task<string?> CanDeleteAsync(
        Mould entity,
        CancellationToken cancellationToken)
    {
        var products = await Db.Products.CountAsync(p => p.MouldId == entity.Id, cancellationToken);
        if (products > 0)
        {
            return $"Makes {products} product{(products == 1 ? "" : "s")} — deactivate it instead.";
        }

        var shifts = await Db.ShiftLines.CountAsync(l => l.MouldId == entity.Id, cancellationToken);
        return shifts == 0
            ? null
            : $"Mounted on {shifts} shift{(shifts == 1 ? "" : "s")} — deactivate it instead.";
    }

    protected override async Task<HashSet<int>> ReferencedIdsAsync(CancellationToken cancellationToken)
    {
        var byProducts = await Db.Products.Select(p => p.MouldId).Distinct().ToListAsync(cancellationToken);
        var byShifts = await Db.ShiftLines
            .Where(l => l.MouldId != null)
            .Select(l => l.MouldId!.Value)
            .Distinct()
            .ToListAsync(cancellationToken);

        return [.. byProducts, .. byShifts];
    }
}

public class ProductTypeService(ColorsDbContext db) : NameOnlyService<ProductType>(db), IProductTypeService
{
    protected override async Task<string?> CanDeleteAsync(
        ProductType entity,
        CancellationToken cancellationToken)
    {
        var families = await Db.RecipeFamilies.CountAsync(f => f.ProductTypeId == entity.Id, cancellationToken);
        if (families > 0)
        {
            return $"Used by {families} recipe famil{(families == 1 ? "y" : "ies")} — deactivate it instead.";
        }

        var products = await Db.Products.CountAsync(p => p.ProductTypeId == entity.Id, cancellationToken);
        return products == 0
            ? null
            : $"Used by {products} product{(products == 1 ? "" : "s")} — deactivate it instead.";
    }

    protected override async Task<HashSet<int>> ReferencedIdsAsync(CancellationToken cancellationToken)
    {
        var byFamilies = await Db.RecipeFamilies.Select(f => f.ProductTypeId).Distinct().ToListAsync(cancellationToken);
        var byProducts = await Db.Products.Select(p => p.ProductTypeId).Distinct().ToListAsync(cancellationToken);

        return [.. byFamilies, .. byProducts];
    }
}

public class UnitService(ColorsDbContext db)
    : MasterListService<Unit, UnitDto, SaveUnitRequest>(db), IUnitService
{
    protected override async Task<string?> CanDeleteAsync(Unit entity, CancellationToken cancellationToken)
    {
        var asBase = await Db.Materials.CountAsync(m => m.BaseUnitId == entity.Id, cancellationToken);
        if (asBase > 0)
        {
            return $"Used as the base unit of {asBase} material{(asBase == 1 ? "" : "s")} — deactivate it instead.";
        }

        var asPack = await Db.MaterialPackagings.CountAsync(p => p.UnitId == entity.Id, cancellationToken);
        return asPack == 0
            ? null
            : $"Used by {asPack} pack size{(asPack == 1 ? "" : "s")} — deactivate it instead.";
    }

    protected override async Task<HashSet<int>> ReferencedIdsAsync(CancellationToken cancellationToken)
    {
        var baseUnits = await Db.Materials.Select(m => m.BaseUnitId).Distinct().ToListAsync(cancellationToken);
        var packUnits = await Db.MaterialPackagings.Select(p => p.UnitId).Distinct().ToListAsync(cancellationToken);

        return [.. baseUnits, .. packUnits];
    }

    protected override UnitDto ToDto(Unit entity, bool canDelete) =>
        new(entity.Id, entity.Name, entity.Symbol, entity.IsActive, canDelete);

    protected override void Apply(SaveUnitRequest request, Unit entity)
    {
        entity.Name = request.Name.Trim();
        entity.Symbol = request.Symbol.Trim();
    }

    protected override async Task<string?> ValidateAsync(
        SaveUnitRequest request,
        int? existingId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return "A name is required.";
        }

        if (string.IsNullOrWhiteSpace(request.Symbol))
        {
            return "A symbol is required — it is shown after every quantity.";
        }

        if (await NameTakenAsync(request.Name, existingId, cancellationToken))
        {
            return "A unit with this name already exists.";
        }

        return null;
    }
}

public class ColorService(ColorsDbContext db)
    : MasterListService<Color, ColorDto, SaveColorRequest>(db), IColorService
{
    protected override ColorDto ToDto(Color entity, bool canDelete) =>
        new(entity.Id, entity.Name, entity.Code, entity.IsActive, canDelete);

    protected override void Apply(SaveColorRequest request, Color entity)
    {
        entity.Name = request.Name.Trim();
        entity.Code = request.Code.Trim().ToUpperInvariant();
    }

    protected override async Task<string?> ValidateAsync(
        SaveColorRequest request,
        int? existingId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return "A name is required.";
        }

        var code = request.Code.Trim().ToUpperInvariant();

        // The code goes into the roll code (W in 01WN180726A), so it must be one
        // letter and unique — two colours sharing a letter would make codes ambiguous.
        if (code.Length != 1 || code[0] < 'A' || code[0] > 'Z')
        {
            return "The code must be a single letter A–Z. It appears inside every roll code.";
        }

        if (await NameTakenAsync(request.Name, existingId, cancellationToken))
        {
            return "A colour with this name already exists.";
        }

        var codeTaken = await Db.Colors.AnyAsync(
            c => c.Code == code && (existingId == null || c.Id != existingId),
            cancellationToken);

        return codeTaken ? $"The letter {code} is already used by another colour." : null;
    }
}

public class ShiftService(ColorsDbContext db)
    : MasterListService<Shift, ShiftDto, SaveShiftRequest>(db), IShiftService
{
    protected override ShiftDto ToDto(Shift entity, bool canDelete) =>
        new(
            entity.Id,
            entity.Name,
            entity.StartTime.ToString("HH:mm", CultureInfo.InvariantCulture),
            entity.EndTime.ToString("HH:mm", CultureInfo.InvariantCulture),
            entity.IsActive,
            canDelete);

    protected override void Apply(SaveShiftRequest request, Shift entity)
    {
        entity.Name = request.Name.Trim();
        entity.StartTime = ParseTime(request.StartTime)!.Value;
        entity.EndTime = ParseTime(request.EndTime)!.Value;
    }

    protected override async Task<string?> ValidateAsync(
        SaveShiftRequest request,
        int? existingId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return "A name is required.";
        }

        var start = ParseTime(request.StartTime);
        var end = ParseTime(request.EndTime);

        if (start is null || end is null)
        {
            return "Times must be written as HH:mm, for example 08:00.";
        }

        if (start == end)
        {
            return "A shift cannot start and end at the same time.";
        }

        if (await NameTakenAsync(request.Name, existingId, cancellationToken))
        {
            return "A shift with this name already exists.";
        }

        return null;
    }

    protected override async Task<string?> CanDeleteAsync(
        Shift entity,
        CancellationToken cancellationToken)
    {
        var used = await Db.ShiftReports.CountAsync(r => r.ShiftId == entity.Id, cancellationToken);

        return used == 0
            ? null
            : $"Used by {used} shift report{(used == 1 ? "" : "s")} — deactivate it instead.";
    }

    protected override async Task<HashSet<int>> ReferencedIdsAsync(CancellationToken cancellationToken) =>
        [.. await Db.ShiftReports.Select(r => r.ShiftId).Distinct().ToListAsync(cancellationToken)];

    private static TimeOnly? ParseTime(string value) =>
        TimeOnly.TryParseExact(value.Trim(), "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var time)
            ? time
            : null;
}

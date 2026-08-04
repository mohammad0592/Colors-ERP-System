using Colors.Application.Features.MasterData;
using Colors.Domain.Entities.MasterData;
using Colors.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Colors.Infrastructure.Services.MasterData;

/// <summary>
/// Materials, with their pack sizes managed as part of the material itself:
/// the save request carries the full packaging list and this service reconciles it.
/// </summary>
public class MaterialService(ColorsDbContext db)
    : MasterListService<Material, MaterialDto, SaveMaterialRequest>(db), IMaterialService
{
    protected override IQueryable<Material> Query() =>
        Db.Materials
            .Include(m => m.Category)
            .Include(m => m.BaseUnit)
            .Include(m => m.Packagings)
            .ThenInclude(p => p.Unit);

    protected override MaterialDto ToDto(Material entity) =>
        new(
            entity.Id,
            entity.Code,
            entity.Name,
            entity.CategoryId,
            entity.Category.Name,
            entity.BaseUnitId,
            entity.BaseUnit.Name,
            entity.BaseUnit.Symbol,
            entity.MinQuantity,
            entity.UnitWeight,
            entity.Notes,
            entity.IsActive,
            entity.Packagings
                .OrderBy(p => p.QuantityInBaseUnit)
                .Select(p => new MaterialPackagingDto(
                    p.Id, p.UnitId, p.Unit.Name, p.QuantityInBaseUnit, p.IsDefaultReceiving))
                .ToList());

    protected override void Apply(SaveMaterialRequest request, Material entity)
    {
        entity.Code = request.Code.Trim().ToUpperInvariant();
        entity.Name = request.Name.Trim();
        entity.CategoryId = request.CategoryId;
        entity.BaseUnitId = request.BaseUnitId;
        entity.MinQuantity = request.MinQuantity;
        entity.UnitWeight = request.UnitWeight;
        entity.Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();

        // The request carries the complete packaging list; rows are matched by pack
        // unit. Missing ones go, existing ones are updated, new ones are added.
        var byUnit = request.Packagings.ToDictionary(p => p.UnitId);

        entity.Packagings.RemoveAll(existing => !byUnit.ContainsKey(existing.UnitId));

        foreach (var existing in entity.Packagings)
        {
            var incoming = byUnit[existing.UnitId];
            existing.QuantityInBaseUnit = incoming.QuantityInBaseUnit;
            existing.IsDefaultReceiving = incoming.IsDefaultReceiving;
        }

        var known = entity.Packagings.Select(p => p.UnitId).ToHashSet();
        foreach (var incoming in request.Packagings.Where(p => !known.Contains(p.UnitId)))
        {
            entity.Packagings.Add(new MaterialPackaging
            {
                UnitId = incoming.UnitId,
                QuantityInBaseUnit = incoming.QuantityInBaseUnit,
                IsDefaultReceiving = incoming.IsDefaultReceiving,
            });
        }
    }

    protected override async Task<string?> ValidateAsync(
        SaveMaterialRequest request,
        int? existingId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Code))
        {
            return "A code is required — it is the material's identity, e.g. MAT0001.";
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return "A name is required.";
        }

        if (request.MinQuantity < 0)
        {
            return "The minimum quantity cannot be negative.";
        }

        if (request.UnitWeight is <= 0)
        {
            return "The unit weight must be more than zero when it is given.";
        }

        var code = request.Code.Trim().ToUpperInvariant();
        var codeTaken = await Db.Materials.AnyAsync(
            m => m.Code == code && (existingId == null || m.Id != existingId),
            cancellationToken);
        if (codeTaken)
        {
            return $"The code {code} is already used by another material.";
        }

        if (!await Db.MaterialCategories.AnyAsync(c => c.Id == request.CategoryId && c.IsActive, cancellationToken))
        {
            return "Choose an active category.";
        }

        if (!await Db.Units.AnyAsync(u => u.Id == request.BaseUnitId && u.IsActive, cancellationToken))
        {
            return "Choose an active base unit.";
        }

        if (request.Packagings.Select(p => p.UnitId).Distinct().Count() != request.Packagings.Count)
        {
            return "Each pack unit may appear only once.";
        }

        if (request.Packagings.Any(p => p.QuantityInBaseUnit <= 0))
        {
            return "Every pack size must hold more than zero of the base unit.";
        }

        if (request.Packagings.Any(p => p.UnitId == request.BaseUnitId))
        {
            return "The base unit itself is not a pack size — receiving in it needs no conversion.";
        }

        if (request.Packagings.Count(p => p.IsDefaultReceiving) > 1)
        {
            return "Only one pack size can be the default for receiving.";
        }

        var unitIds = request.Packagings.Select(p => p.UnitId).ToList();
        var activeUnits = await Db.Units
            .CountAsync(u => unitIds.Contains(u.Id) && u.IsActive, cancellationToken);
        if (activeUnits != unitIds.Count)
        {
            return "Every pack size must use an active unit.";
        }

        return null;
    }
}

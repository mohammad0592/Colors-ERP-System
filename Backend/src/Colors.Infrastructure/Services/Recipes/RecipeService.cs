using Colors.Application.Common.Models;
using Colors.Application.Features.Recipes;
using Colors.Domain.Entities.Recipes;
using Colors.Domain.Enums;
using Colors.Infrastructure.Identity;
using Colors.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Colors.Infrastructure.Services.Recipes;

/// <summary>
/// Recipes, and the rules that keep production history honest.
///
/// Two rules matter above the rest (specification section 5):
/// a version is frozen the moment it leaves Draft, because rolls point at it; and
/// exactly one version per family is Current, so "what are we running?" has one answer.
/// </summary>
public class RecipeService(ColorsDbContext db, TimeProvider timeProvider) : IRecipeService
{
    // ---------- families ----------

    public async Task<IReadOnlyList<RecipeFamilyDto>> GetFamiliesAsync(
        bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        var families = await db.RecipeFamilies
            .Include(f => f.ProductType)
            .Include(f => f.Versions)
            .Where(f => includeInactive || f.IsActive)
            .OrderBy(f => f.Name)
            .ToListAsync(cancellationToken);

        return families.Select(ToFamilyDto).ToList();
    }

    public async Task<Result<RecipeFamilyDto>> CreateFamilyAsync(
        SaveRecipeFamilyRequest request,
        CancellationToken cancellationToken = default)
    {
        var error = await ValidateFamilyAsync(request, existingId: null, cancellationToken);
        if (error is not null)
        {
            return Result<RecipeFamilyDto>.Failure(ErrorCode.ValidationFailed, error);
        }

        var family = new RecipeFamily();
        ApplyFamily(request, family);
        family.IsActive = true;

        db.RecipeFamilies.Add(family);
        await db.SaveChangesAsync(cancellationToken);

        return Result<RecipeFamilyDto>.Success(await LoadFamilyDtoAsync(family.Id, cancellationToken));
    }

    public async Task<Result<RecipeFamilyDto>> UpdateFamilyAsync(
        int id,
        SaveRecipeFamilyRequest request,
        CancellationToken cancellationToken = default)
    {
        var family = await db.RecipeFamilies.FirstOrDefaultAsync(f => f.Id == id, cancellationToken);
        if (family is null)
        {
            return FamilyNotFound();
        }

        var error = await ValidateFamilyAsync(request, id, cancellationToken);
        if (error is not null)
        {
            return Result<RecipeFamilyDto>.Failure(ErrorCode.ValidationFailed, error);
        }

        ApplyFamily(request, family);
        await db.SaveChangesAsync(cancellationToken);

        return Result<RecipeFamilyDto>.Success(await LoadFamilyDtoAsync(id, cancellationToken));
    }

    public async Task<Result<RecipeFamilyDto>> SetFamilyActiveAsync(
        int id,
        bool isActive,
        CancellationToken cancellationToken = default)
    {
        var family = await db.RecipeFamilies.FirstOrDefaultAsync(f => f.Id == id, cancellationToken);
        if (family is null)
        {
            return FamilyNotFound();
        }

        family.IsActive = isActive;
        await db.SaveChangesAsync(cancellationToken);

        return Result<RecipeFamilyDto>.Success(await LoadFamilyDtoAsync(id, cancellationToken));
    }

    // ---------- versions ----------

    public async Task<IReadOnlyList<RecipeVersionSummaryDto>> GetVersionsAsync(
        int? familyId = null,
        CancellationToken cancellationToken = default)
    {
        var versions = await VersionQuery()
            .Where(v => familyId == null || v.RecipeFamilyId == familyId)
            .OrderByDescending(v => v.RecipeNumber)
            .ToListAsync(cancellationToken);

        var names = await CreatorNamesAsync(versions, cancellationToken);

        return versions
            .Select(v => new RecipeVersionSummaryDto(
                v.Id,
                v.RecipeNumber,
                v.RecipeFamilyId,
                v.Family.Name,
                v.VersionNumber,
                v.Status.ToString(),
                v.IsEditable,
                names.GetValueOrDefault(v.CreatedByUserId, "—"),
                v.CreatedAt,
                v.Notes,
                v.Ingredients.Count))
            .ToList();
    }

    public async Task<Result<RecipeVersionDto>> GetVersionAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        var version = await VersionQuery().FirstOrDefaultAsync(v => v.Id == id, cancellationToken);

        return version is null
            ? VersionNotFound()
            : Result<RecipeVersionDto>.Success(await ToVersionDtoAsync(version, cancellationToken));
    }

    public async Task<Result<RecipeVersionDto>> CreateVersionAsync(
        CreateRecipeVersionRequest request,
        int userId,
        CancellationToken cancellationToken = default)
    {
        var family = await db.RecipeFamilies
            .FirstOrDefaultAsync(f => f.Id == request.RecipeFamilyId, cancellationToken);
        if (family is null)
        {
            return Result<RecipeVersionDto>.Failure(ErrorCode.ValidationFailed, "Choose a recipe family.");
        }

        var error = await ValidateIngredientsAsync(request.Ingredients, cancellationToken);
        if (error is not null)
        {
            return Result<RecipeVersionDto>.Failure(ErrorCode.ValidationFailed, error);
        }

        var version = new RecipeVersion
        {
            RecipeFamilyId = family.Id,
            RecipeNumber = await NextRecipeNumberAsync(cancellationToken),
            VersionNumber = await NextVersionNumberAsync(family.Id, cancellationToken),
            Status = RecipeVersionStatus.Draft,
            CreatedByUserId = userId,
            CreatedAt = timeProvider.GetUtcNow(),
            Notes = Trimmed(request.Notes),
            Ingredients = ToIngredients(request.Ingredients),
        };

        db.RecipeVersions.Add(version);
        await db.SaveChangesAsync(cancellationToken);

        return await LoadVersionResultAsync(version.Id, cancellationToken);
    }

    public async Task<Result<RecipeVersionDto>> UpdateVersionAsync(
        int id,
        UpdateRecipeVersionRequest request,
        CancellationToken cancellationToken = default)
    {
        var version = await VersionQuery().FirstOrDefaultAsync(v => v.Id == id, cancellationToken);
        if (version is null)
        {
            return VersionNotFound();
        }

        // The heart of the rule: a version in production can never change, because
        // rolls point at it and the formula that made them must stay true.
        if (!version.IsEditable)
        {
            return Frozen(version);
        }

        var error = await ValidateIngredientsAsync(request.Ingredients, cancellationToken);
        if (error is not null)
        {
            return Result<RecipeVersionDto>.Failure(ErrorCode.ValidationFailed, error);
        }

        version.Notes = Trimmed(request.Notes);

        // A draft's formula is replaced wholesale — simpler than reconciling, and
        // nothing references these rows yet.
        db.RecipeIngredients.RemoveRange(version.Ingredients);
        version.Ingredients = ToIngredients(request.Ingredients);

        await db.SaveChangesAsync(cancellationToken);

        return await LoadVersionResultAsync(id, cancellationToken);
    }

    public async Task<Result<RecipeVersionDto>> CopyVersionAsync(
        int id,
        CopyRecipeVersionRequest request,
        int userId,
        CancellationToken cancellationToken = default)
    {
        var source = await VersionQuery().FirstOrDefaultAsync(v => v.Id == id, cancellationToken);
        if (source is null)
        {
            return VersionNotFound();
        }

        // How the factory actually improves a recipe: copy, change one percentage,
        // save as a new number. The original is never touched.
        var copy = new RecipeVersion
        {
            RecipeFamilyId = source.RecipeFamilyId,
            RecipeNumber = await NextRecipeNumberAsync(cancellationToken),
            VersionNumber = await NextVersionNumberAsync(source.RecipeFamilyId, cancellationToken),
            Status = RecipeVersionStatus.Draft,
            CreatedByUserId = userId,
            CreatedAt = timeProvider.GetUtcNow(),
            Notes = Trimmed(request.Notes) ?? $"Copied from recipe {source.RecipeNumber}.",
            Ingredients = source.Ingredients
                .Select(i => new RecipeIngredient
                {
                    MaterialId = i.MaterialId,
                    IsBaseResin = i.IsBaseResin,
                    TargetPercentage = i.TargetPercentage,
                    MinPercentage = i.MinPercentage,
                    MaxPercentage = i.MaxPercentage,
                })
                .ToList(),
        };

        db.RecipeVersions.Add(copy);
        await db.SaveChangesAsync(cancellationToken);

        return await LoadVersionResultAsync(copy.Id, cancellationToken);
    }

    public async Task<Result<RecipeVersionDto>> PromoteVersionAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        var version = await VersionQuery().FirstOrDefaultAsync(v => v.Id == id, cancellationToken);
        if (version is null)
        {
            return VersionNotFound();
        }

        if (version.Status == RecipeVersionStatus.Current)
        {
            return Result<RecipeVersionDto>.Failure(
                ErrorCode.ValidationFailed,
                $"Recipe {version.RecipeNumber} is already the one in production.");
        }

        if (version.Status == RecipeVersionStatus.Archived)
        {
            return Result<RecipeVersionDto>.Failure(
                ErrorCode.ValidationFailed,
                $"Recipe {version.RecipeNumber} has been replaced. Copy it to a new recipe instead.");
        }

        if (version.Ingredients.Count == 0)
        {
            return Result<RecipeVersionDto>.Failure(
                ErrorCode.ValidationFailed,
                "A recipe with no materials cannot go into production.");
        }

        // Retiring the old and promoting the new happen together, so there is never a
        // moment where a family has two Current versions or none.
        var previous = await db.RecipeVersions
            .Where(v => v.RecipeFamilyId == version.RecipeFamilyId
                        && v.Status == RecipeVersionStatus.Current)
            .ToListAsync(cancellationToken);

        foreach (var old in previous)
        {
            old.Status = RecipeVersionStatus.Archived;
        }

        version.Status = RecipeVersionStatus.Current;
        await db.SaveChangesAsync(cancellationToken);

        return await LoadVersionResultAsync(id, cancellationToken);
    }

    public async Task<Result<bool>> DeleteDraftAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        var version = await db.RecipeVersions.FirstOrDefaultAsync(v => v.Id == id, cancellationToken);
        if (version is null)
        {
            return Result<bool>.Failure(ErrorCode.NotFound, "This recipe does not exist.");
        }

        if (version.Status != RecipeVersionStatus.Draft)
        {
            return Result<bool>.Failure(
                ErrorCode.ValidationFailed,
                $"Recipe {version.RecipeNumber} has been used in production and is kept for ever.");
        }

        db.RecipeVersions.Remove(version);
        await db.SaveChangesAsync(cancellationToken);

        return Result<bool>.Success(true);
    }

    // ---------- validation ----------

    private async Task<string?> ValidateFamilyAsync(
        SaveRecipeFamilyRequest request,
        int? existingId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return "A name is required.";
        }

        var name = request.Name.Trim();
        var taken = await db.RecipeFamilies.AnyAsync(
            f => f.Name == name && (existingId == null || f.Id != existingId),
            cancellationToken);
        if (taken)
        {
            return "A recipe family with this name already exists.";
        }

        var productTypeOk = await db.ProductTypes
            .AnyAsync(p => p.Id == request.ProductTypeId && p.IsActive, cancellationToken);

        return productTypeOk ? null : "Choose an active product type.";
    }

    private async Task<string?> ValidateIngredientsAsync(
        IReadOnlyList<SaveRecipeIngredientRequest> ingredients,
        CancellationToken cancellationToken)
    {
        if (ingredients.Count == 0)
        {
            return "A recipe needs at least one material.";
        }

        if (ingredients.Select(i => i.MaterialId).Distinct().Count() != ingredients.Count)
        {
            return "Each material may appear only once.";
        }

        foreach (var ingredient in ingredients)
        {
            if (ingredient.MinPercentage < 0 || ingredient.MaxPercentage < 0 || ingredient.TargetPercentage < 0)
            {
                return "Percentages cannot be negative.";
            }

            if (ingredient.MinPercentage > ingredient.MaxPercentage)
            {
                return "A material's minimum cannot be above its maximum.";
            }

            if (ingredient.TargetPercentage < ingredient.MinPercentage
                || ingredient.TargetPercentage > ingredient.MaxPercentage)
            {
                return "Each target must sit between its own minimum and maximum.";
            }
        }

        var baseResin = ingredients.Where(i => i.IsBaseResin).ToList();
        if (baseResin.Count == 0)
        {
            return "Mark the base resin — the GPPS, and the recycle when there is any. "
                 + "Everything else is measured against it.";
        }

        // Parts per hundred resin: the polymer totals 100 and the additives sit on
        // top, which is why the whole list does not add up to 100.
        var baseTotal = baseResin.Sum(i => i.TargetPercentage);
        if (baseTotal != 100m)
        {
            return $"The base resin must total 100%, not {baseTotal}%. "
                 + "Additives are measured against it and are not part of that 100.";
        }

        var materialIds = ingredients.Select(i => i.MaterialId).ToList();
        var activeCount = await db.Materials
            .CountAsync(m => materialIds.Contains(m.Id) && m.IsActive, cancellationToken);

        return activeCount == materialIds.Count ? null : "Every material must be active.";
    }

    // ---------- helpers ----------

    private IQueryable<RecipeVersion> VersionQuery() =>
        db.RecipeVersions
            .Include(v => v.Family)
            .Include(v => v.Ingredients)
            .ThenInclude(i => i.Material);

    private async Task<int> NextRecipeNumberAsync(CancellationToken cancellationToken) =>
        await RecipeNumbers.NextAsync(db, cancellationToken);

    private async Task<int> NextVersionNumberAsync(int familyId, CancellationToken cancellationToken)
    {
        var highest = await db.RecipeVersions
            .Where(v => v.RecipeFamilyId == familyId)
            .Select(v => (int?)v.VersionNumber)
            .MaxAsync(cancellationToken);

        return (highest ?? 0) + 1;
    }

    private static List<RecipeIngredient> ToIngredients(
        IReadOnlyList<SaveRecipeIngredientRequest> requests) =>
        requests
            .Select(i => new RecipeIngredient
            {
                MaterialId = i.MaterialId,
                IsBaseResin = i.IsBaseResin,
                TargetPercentage = i.TargetPercentage,
                MinPercentage = i.MinPercentage,
                MaxPercentage = i.MaxPercentage,
            })
            .ToList();

    private static void ApplyFamily(SaveRecipeFamilyRequest request, RecipeFamily family)
    {
        family.Name = request.Name.Trim();
        family.ProductTypeId = request.ProductTypeId;
        family.UsesRecycle = request.UsesRecycle;
        family.IsAbsorbent = request.IsAbsorbent;
        family.Description = Trimmed(request.Description);
    }

    private static string? Trimmed(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static RecipeFamilyDto ToFamilyDto(RecipeFamily family) =>
        new(
            family.Id,
            family.Name,
            family.ProductTypeId,
            family.ProductType.Name,
            family.UsesRecycle,
            family.IsAbsorbent,
            family.Description,
            family.IsActive,
            family.Versions
                .Where(v => v.Status == RecipeVersionStatus.Current)
                .Select(v => (int?)v.RecipeNumber)
                .FirstOrDefault(),
            family.Versions.Count);

    private async Task<RecipeFamilyDto> LoadFamilyDtoAsync(int id, CancellationToken cancellationToken)
    {
        var family = await db.RecipeFamilies
            .Include(f => f.ProductType)
            .Include(f => f.Versions)
            .FirstAsync(f => f.Id == id, cancellationToken);

        return ToFamilyDto(family);
    }

    private async Task<Result<RecipeVersionDto>> LoadVersionResultAsync(
        int id,
        CancellationToken cancellationToken)
    {
        var saved = await VersionQuery().FirstAsync(v => v.Id == id, cancellationToken);
        return Result<RecipeVersionDto>.Success(await ToVersionDtoAsync(saved, cancellationToken));
    }

    private async Task<RecipeVersionDto> ToVersionDtoAsync(
        RecipeVersion version,
        CancellationToken cancellationToken)
    {
        var names = await CreatorNamesAsync([version], cancellationToken);

        return new RecipeVersionDto(
            version.Id,
            version.RecipeNumber,
            version.RecipeFamilyId,
            version.Family.Name,
            version.VersionNumber,
            version.Status.ToString(),
            version.IsEditable,
            names.GetValueOrDefault(version.CreatedByUserId, "—"),
            version.CreatedAt,
            version.Notes,
            version.Ingredients
                // Base resin first, then additives — the order a recipe is read in.
                .OrderByDescending(i => i.IsBaseResin)
                .ThenByDescending(i => i.TargetPercentage)
                .Select(i => new RecipeIngredientDto(
                    i.MaterialId,
                    i.Material.Code,
                    i.Material.Name,
                    i.IsBaseResin,
                    i.TargetPercentage,
                    i.MinPercentage,
                    i.MaxPercentage))
                .ToList());
    }

    /// <summary>
    /// Who wrote each version. Users live in Infrastructure and Domain refers to them
    /// by id only, so the names are fetched here rather than joined in the model.
    /// </summary>
    private async Task<Dictionary<int, string>> CreatorNamesAsync(
        IReadOnlyCollection<RecipeVersion> versions,
        CancellationToken cancellationToken)
    {
        if (versions.Count == 0)
        {
            return [];
        }

        var ids = versions.Select(v => v.CreatedByUserId).Distinct().ToList();

        return await db.Set<ApplicationUser>()
            .Where(u => ids.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.FullName, cancellationToken);
    }

    private static Result<RecipeFamilyDto> FamilyNotFound() =>
        Result<RecipeFamilyDto>.Failure(ErrorCode.NotFound, "This recipe family does not exist.");

    private static Result<RecipeVersionDto> VersionNotFound() =>
        Result<RecipeVersionDto>.Failure(ErrorCode.NotFound, "This recipe does not exist.");

    private static Result<RecipeVersionDto> Frozen(RecipeVersion version) =>
        Result<RecipeVersionDto>.Failure(
            ErrorCode.ValidationFailed,
            $"Recipe {version.RecipeNumber} is {version.Status.ToString().ToLowerInvariant()} and can no longer be changed. "
            + "Copy it to a new recipe instead — the rolls made with it must keep their exact formula.");
}

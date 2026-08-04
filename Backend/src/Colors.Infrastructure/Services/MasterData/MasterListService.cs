using Colors.Application.Common.Models;
using Colors.Application.Features.MasterData;
using Colors.Domain.Common;
using Colors.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Colors.Infrastructure.Services.MasterData;

/// <summary>
/// The behaviour every master data list shares — reading, creating, updating,
/// activating — written once. A concrete service supplies only its mapping and its
/// validation, so eight lists do not mean eight copies of the same loop.
/// </summary>
public abstract class MasterListService<TEntity, TDto, TUpsert>(ColorsDbContext db)
    : IMasterListService<TDto, TUpsert>
    where TEntity : MasterEntity, new()
{
    protected ColorsDbContext Db => db;

    /// <summary>Overridden where the DTO needs related rows loaded (materials).</summary>
    protected virtual IQueryable<TEntity> Query() => db.Set<TEntity>();

    protected abstract TDto ToDto(TEntity entity);

    /// <summary>Copies the request onto the entity. Runs only after validation passed.</summary>
    protected abstract void Apply(TUpsert request, TEntity entity);

    /// <summary>A message describing what is wrong, or null when the request is fine.</summary>
    protected abstract Task<string?> ValidateAsync(TUpsert request, int? existingId, CancellationToken cancellationToken);

    /// <summary>A message when the row must stay active — e.g. still used by a current recipe.</summary>
    protected virtual Task<string?> CanDeactivateAsync(TEntity entity, CancellationToken cancellationToken) =>
        Task.FromResult<string?>(null);

    /// <summary>
    /// A message naming what references the row, or null when nothing does and it may
    /// be deleted. Overridden per entity as later phases add referencing tables.
    /// </summary>
    protected virtual Task<string?> CanDeleteAsync(TEntity entity, CancellationToken cancellationToken) =>
        Task.FromResult<string?>(null);

    public async Task<IReadOnlyList<TDto>> GetAllAsync(
        bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        var rows = await Query()
            .Where(e => includeInactive || e.IsActive)
            .OrderBy(e => e.Name)
            .ToListAsync(cancellationToken);

        return rows.Select(ToDto).ToList();
    }

    public async Task<Result<TDto>> CreateAsync(TUpsert request, CancellationToken cancellationToken = default)
    {
        var error = await ValidateAsync(request, existingId: null, cancellationToken);
        if (error is not null)
        {
            return Result<TDto>.Failure(ErrorCode.ValidationFailed, error);
        }

        var entity = new TEntity();
        Apply(request, entity);
        entity.IsActive = true;

        db.Set<TEntity>().Add(entity);
        await db.SaveChangesAsync(cancellationToken);

        // Reloaded through Query() so a DTO needing related rows gets them.
        var saved = await Query().FirstAsync(e => e.Id == entity.Id, cancellationToken);
        return Result<TDto>.Success(ToDto(saved));
    }

    public async Task<Result<TDto>> UpdateAsync(int id, TUpsert request, CancellationToken cancellationToken = default)
    {
        var entity = await Query().FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
        if (entity is null)
        {
            return NotFound();
        }

        var error = await ValidateAsync(request, existingId: id, cancellationToken);
        if (error is not null)
        {
            return Result<TDto>.Failure(ErrorCode.ValidationFailed, error);
        }

        Apply(request, entity);
        await db.SaveChangesAsync(cancellationToken);

        // Reloaded through Query() because Apply may have added child rows whose
        // related entities are not loaded yet — a new pack size knows its UnitId
        // but not the Unit, and the DTO needs the unit's name.
        var saved = await Query().FirstAsync(e => e.Id == id, cancellationToken);
        return Result<TDto>.Success(ToDto(saved));
    }

    public async Task<Result<TDto>> SetActiveAsync(int id, bool isActive, CancellationToken cancellationToken = default)
    {
        var entity = await Query().FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
        if (entity is null)
        {
            return NotFound();
        }

        if (!isActive)
        {
            var blocked = await CanDeactivateAsync(entity, cancellationToken);
            if (blocked is not null)
            {
                return Result<TDto>.Failure(ErrorCode.ValidationFailed, blocked);
            }
        }

        entity.IsActive = isActive;
        await db.SaveChangesAsync(cancellationToken);

        return Result<TDto>.Success(ToDto(entity));
    }

    public async Task<Result<bool>> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await db.Set<TEntity>().FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
        if (entity is null)
        {
            return Result<bool>.Failure(ErrorCode.NotFound, "This item does not exist.");
        }

        // Specification section 4: delete is for typos and tests — rows nothing
        // references. Anything already used can only be deactivated, so that every
        // historical record keeps resolving.
        var referenced = await CanDeleteAsync(entity, cancellationToken);
        if (referenced is not null)
        {
            return Result<bool>.Failure(ErrorCode.ValidationFailed, referenced);
        }

        db.Set<TEntity>().Remove(entity);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // The database's restrict foreign keys are the backstop: a reference the
            // check above does not know about yet — or one added in the instant
            // between check and delete — lands here instead of breaking history.
            db.ChangeTracker.Clear();
            return Result<bool>.Failure(
                ErrorCode.ValidationFailed,
                "This row is used by other records, so it cannot be deleted. Deactivate it instead.");
        }

        return Result<bool>.Success(true);
    }

    /// <summary>True when another row already holds this name.</summary>
    protected async Task<bool> NameTakenAsync(string name, int? exceptId, CancellationToken cancellationToken)
    {
        var trimmed = name.Trim();
        return await db.Set<TEntity>()
            .AnyAsync(e => e.Name == trimmed && (exceptId == null || e.Id != exceptId), cancellationToken);
    }

    private static Result<TDto> NotFound() =>
        Result<TDto>.Failure(ErrorCode.NotFound, "This item does not exist.");
}

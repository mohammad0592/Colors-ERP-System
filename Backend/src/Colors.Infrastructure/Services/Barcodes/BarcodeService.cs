using Colors.Application.Common.Models;
using Colors.Application.Features.Barcodes;
using Colors.Domain.Entities.Barcodes;
using Colors.Domain.Enums;
using Colors.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Colors.Infrastructure.Services.Barcodes;

/// <summary>
/// Barcodes. Specification section 12.
///
/// Values come from database sequences, one per object type, for the same reason
/// recipe numbers do: a sequence never hands the same number to two callers and never
/// goes backwards, so a barcode cannot be duplicated by two tablets printing at once
/// and cannot be reused after the object it named is scrapped.
///
/// Short and typeable on purpose. When a label is torn a man has to read the code and
/// type it, and <c>B004501</c> is something he can get right.
/// </summary>
public class BarcodeService(ColorsDbContext db, TimeProvider timeProvider) : IBarcodeService
{
    public async Task<Result<BarcodeDto>> IssueAsync(
        BarcodeObjectType objectType,
        int objectId,
        CancellationToken cancellationToken = default)
    {
        if (objectId <= 0)
        {
            return Invalid("A barcode needs an object to belong to.");
        }

        // One object, one active barcode. Called twice — a retry, a double tap — this
        // returns what it already has rather than printing a second identity for the
        // same physical thing.
        var existing = await db.Barcodes
            .FirstOrDefaultAsync(
                b => b.ObjectType == objectType && b.ObjectId == objectId && b.IsActive,
                cancellationToken);

        if (existing is not null)
        {
            return Result<BarcodeDto>.Success(ToDto(existing));
        }

        var barcode = new Barcode
        {
            Value = await NextValueAsync(objectType, cancellationToken),
            ObjectType = objectType,
            ObjectId = objectId,
            CreatedAt = timeProvider.GetUtcNow(),
            IsActive = true,
        };

        db.Barcodes.Add(barcode);
        await db.SaveChangesAsync(cancellationToken);

        return Result<BarcodeDto>.Success(ToDto(barcode));
    }

    public async Task<Result<BarcodeLookupDto>> LookupAsync(
        string value,
        CancellationToken cancellationToken = default)
    {
        var cleaned = Clean(value);

        if (cleaned.Length == 0)
        {
            return Result<BarcodeLookupDto>.Success(
                new BarcodeLookupDto(false, string.Empty, null, null, false, "Scan a label."));
        }

        var barcode = await db.Barcodes
            .FirstOrDefaultAsync(b => b.Value == cleaned, cancellationToken);

        if (barcode is null)
        {
            // Deliberately not an error status: an unknown label is an ordinary event
            // on a factory floor, and the screen wants to show it, not blow up.
            return Result<BarcodeLookupDto>.Success(new BarcodeLookupDto(
                false,
                cleaned,
                null,
                null,
                false,
                $"No label in the system matches {cleaned}."));
        }

        var what = Describe(barcode);

        return Result<BarcodeLookupDto>.Success(new BarcodeLookupDto(
            true,
            barcode.Value,
            barcode.ObjectType.ToString(),
            barcode.ObjectId,
            barcode.IsActive,
            barcode.IsActive
                ? what
                : $"{what} — this label was replaced. Use the new one."));
    }

    public async Task<Result<BarcodeLookupDto>> LookupAsync(
        string value,
        BarcodeObjectType expected,
        CancellationToken cancellationToken = default)
    {
        var result = await LookupAsync(value, cancellationToken);
        if (!result.IsSuccess)
        {
            return result;
        }

        var found = result.Value!;

        if (!found.Found || found.ObjectType == expected.ToString())
        {
            return result;
        }

        // The answer a per-table column could never give: not "nothing found", but
        // "the wrong kind of thing".
        return Result<BarcodeLookupDto>.Success(found with
        {
            Message =
                $"That is {Article(found.ObjectType!)} {found.ObjectType!.ToLowerInvariant()}, "
                + $"not {Article(expected.ToString())} {expected.ToString().ToLowerInvariant()}.",
        });
    }

    public async Task<IReadOnlyList<BarcodeDto>> GetForObjectAsync(
        BarcodeObjectType objectType,
        int objectId,
        CancellationToken cancellationToken = default)
    {
        var rows = await db.Barcodes
            .Where(b => b.ObjectType == objectType && b.ObjectId == objectId)
            .OrderByDescending(b => b.Id)
            .ToListAsync(cancellationToken);

        return rows.Select(ToDto).ToList();
    }

    public async Task<Result<BarcodeDto>> ReplaceAsync(
        string value,
        CancellationToken cancellationToken = default)
    {
        var cleaned = Clean(value);

        var old = await db.Barcodes.FirstOrDefaultAsync(b => b.Value == cleaned, cancellationToken);
        if (old is null)
        {
            return Result<BarcodeDto>.Failure(
                ErrorCode.NotFound,
                $"No label in the system matches {cleaned}.");
        }

        if (!old.IsActive)
        {
            return Invalid("That label has already been replaced.");
        }

        // The old row stays. A bag found weeks later still carrying it is then
        // recognised, and the system can say the label was replaced rather than
        // shrugging at an unknown code.
        old.IsActive = false;

        var replacement = new Barcode
        {
            Value = await NextValueAsync(old.ObjectType, cancellationToken),
            ObjectType = old.ObjectType,
            ObjectId = old.ObjectId,
            CreatedAt = timeProvider.GetUtcNow(),
            IsActive = true,
        };

        db.Barcodes.Add(replacement);
        await db.SaveChangesAsync(cancellationToken);

        return Result<BarcodeDto>.Success(ToDto(replacement));
    }

    // ---------- helpers ----------

    /// <summary>
    /// The next value from this type's sequence. A sequence rather than MAX + 1: two
    /// tablets creating bags in the same millisecond must not be handed the same code,
    /// and a rolled-back transaction must not free a number for a different bag.
    /// </summary>
    private async Task<string> NextValueAsync(
        BarcodeObjectType objectType,
        CancellationToken cancellationToken)
    {
        var sequence = ColorsDbContext.BarcodeSequenceFor(objectType);

        var next = await db.Database
            .SqlQuery<long>($"SELECT nextval({sequence})::bigint AS \"Value\"")
            .ToListAsync(cancellationToken);

        return $"{Prefix(objectType)}{next[0]:D6}";
    }

    private static char Prefix(BarcodeObjectType objectType) => objectType switch
    {
        BarcodeObjectType.Roll => 'R',
        BarcodeObjectType.Bag => 'B',
        BarcodeObjectType.Pallet => 'P',
        _ => 'X',
    };

    /// <summary>
    /// Scanners append a newline, and a man typing a torn label uses whatever case is
    /// under his thumb. Neither should be the difference between finding a bag and not.
    /// </summary>
    private static string Clean(string value) => value.Trim().ToUpperInvariant();

    private static string Describe(Barcode barcode) =>
        $"{barcode.ObjectType} #{barcode.ObjectId}";

    private static string Article(string word) =>
        "AEIOU".Contains(char.ToUpperInvariant(word[0])) ? "an" : "a";

    private static BarcodeDto ToDto(Barcode barcode) =>
        new(
            barcode.Id,
            barcode.Value,
            barcode.ObjectType.ToString(),
            barcode.ObjectId,
            barcode.IsActive,
            barcode.CreatedAt);

    private static Result<BarcodeDto> Invalid(string message) =>
        Result<BarcodeDto>.Failure(ErrorCode.ValidationFailed, message);
}

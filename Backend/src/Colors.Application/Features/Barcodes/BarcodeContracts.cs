using Colors.Application.Common.Models;
using Colors.Domain.Enums;

namespace Colors.Application.Features.Barcodes;

// Shapes crossing the API for barcodes. Specification section 12.

public sealed record BarcodeDto(
    int Id,
    string Value,
    string ObjectType,
    int ObjectId,
    bool IsActive,
    DateTimeOffset CreatedAt);

/// <summary>
/// What a scan found.
///
/// <c>Found</c> is false for a code nobody issued — a label from another factory, or a
/// misread. That is a different answer from "this is a bag when you wanted a pallet",
/// and the screen says so differently.
/// </summary>
public sealed record BarcodeLookupDto(
    bool Found,
    string Value,
    string? ObjectType,
    int? ObjectId,
    bool IsActive,
    // Plain words for the worker holding the scanner.
    string Message);

/// <summary>
/// Issues barcodes and answers scans (specification section 12).
///
/// Nothing outside the server issues one: a barcode is created when a roll, a bag or
/// a pallet is created, in the same transaction, by the phase that creates it. This
/// interface is what those phases call.
/// </summary>
public interface IBarcodeService
{
    /// <summary>
    /// Issues the barcode for a newly created object. One object gets one active
    /// barcode; asking twice returns the one it already has rather than printing a
    /// second identity for the same thing.
    /// </summary>
    Task<Result<BarcodeDto>> IssueAsync(
        BarcodeObjectType objectType,
        int objectId,
        CancellationToken cancellationToken = default);

    /// <summary>Scan anything, or type it when the label is torn.</summary>
    Task<Result<BarcodeLookupDto>> LookupAsync(
        string value,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The same lookup, but refusing anything that is not the type the screen is
    /// asking for — a bag scanned into a pallet field.
    /// </summary>
    Task<Result<BarcodeLookupDto>> LookupAsync(
        string value,
        BarcodeObjectType expected,
        CancellationToken cancellationToken = default);

    /// <summary>Every barcode an object has had, newest first. For reprinting.</summary>
    Task<IReadOnlyList<BarcodeDto>> GetForObjectAsync(
        BarcodeObjectType objectType,
        int objectId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retires a barcode and issues a new one for the same object — a label so damaged
    /// it cannot be scanned. The old value is never reused and still resolves, so a bag
    /// found weeks later with the old label can still be identified.
    /// </summary>
    Task<Result<BarcodeDto>> ReplaceAsync(
        string value,
        CancellationToken cancellationToken = default);
}

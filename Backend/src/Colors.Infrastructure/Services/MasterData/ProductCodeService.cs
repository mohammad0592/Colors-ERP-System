using Colors.Application.Common.Models;
using Colors.Application.Features.MasterData;
using Colors.Domain.Entities.MasterData;
using Colors.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Colors.Infrastructure.Services.MasterData;

/// <summary>
/// The factory's list of product codes (specification section 19.25).
/// </summary>
public class ProductCodeService(ColorsDbContext db) : IProductCodeService
{
    private const int MaxCodeLength = 20;

    public async Task<IReadOnlyList<ProductCodeDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var rows = await db.ProductColourCodes
            .AsNoTracking()
            .Include(c => c.Product)
            .Include(c => c.Color)
            .ToListAsync(cancellationToken);

        // By product, then the every-colour code before the colours, then by code — the
        // order the factory's own list reads in.
        return rows
            .OrderBy(c => c.Product.Name)
            .ThenBy(c => c.ColorId is not null)
            .ThenBy(c => c.Code, StringComparer.Ordinal)
            .Select(ToDto)
            .ToList();
    }

    public async Task<Result<ProductCodeDto>> CreateAsync(
        SaveProductCodeRequest request,
        CancellationToken cancellationToken = default)
    {
        var code = Cleaned(request.Code);
        var error = await ErrorAsync(request, code, exceptId: null, cancellationToken);
        if (error is not null)
        {
            return error;
        }

        var row = new ProductColourCode
        {
            ProductId = request.ProductId,
            ColorId = request.ColorId,
            Code = code,
        };
        db.ProductColourCodes.Add(row);
        await db.SaveChangesAsync(cancellationToken);

        return Result<ProductCodeDto>.Success(await LoadAsync(row.Id, cancellationToken));
    }

    public async Task<Result<ProductCodeDto>> UpdateAsync(
        int id,
        SaveProductCodeRequest request,
        CancellationToken cancellationToken = default)
    {
        var row = await db.ProductColourCodes.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (row is null)
        {
            return Result<ProductCodeDto>.Failure(
                ErrorCode.NotFound, "This product code does not exist.", "productCode.notFound");
        }

        var code = Cleaned(request.Code);
        var error = await ErrorAsync(request, code, exceptId: id, cancellationToken);
        if (error is not null)
        {
            return error;
        }

        row.ProductId = request.ProductId;
        row.ColorId = request.ColorId;
        row.Code = code;
        await db.SaveChangesAsync(cancellationToken);

        return Result<ProductCodeDto>.Success(await LoadAsync(id, cancellationToken));
    }

    public async Task<Result<bool>> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var row = await db.ProductColourCodes.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (row is null)
        {
            return Result<bool>.Failure(
                ErrorCode.NotFound, "This product code does not exist.", "productCode.notFound");
        }

        db.ProductColourCodes.Remove(row);
        await db.SaveChangesAsync(cancellationToken);
        return Result<bool>.Success(true);
    }

    private async Task<Result<ProductCodeDto>?> ErrorAsync(
        SaveProductCodeRequest request,
        string code,
        int? exceptId,
        CancellationToken cancellationToken)
    {
        if (code.Length == 0)
        {
            return Invalid("Type the code.", "productCode.codeRequired");
        }

        if (code.Length > MaxCodeLength)
        {
            return Invalid(
                $"A code is at most {MaxCodeLength} characters.",
                "productCode.tooLong",
                MaxCodeLength.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        if (!await db.Products.AnyAsync(p => p.Id == request.ProductId, cancellationToken))
        {
            return Invalid("Choose the product.", "productCode.productRequired");
        }

        if (request.ColorId is not null
            && !await db.Colors.AnyAsync(c => c.Id == request.ColorId, cancellationToken))
        {
            return Invalid("Choose the colour.", "productCode.colourRequired");
        }

        // A code names one kind of bag. Two kinds sharing one would make the label lie
        // about whichever was printed second.
        var holder = await db.ProductColourCodes
            .Where(c => c.Code == code && (exceptId == null || c.Id != exceptId))
            .Select(c => c.Product.Name)
            .FirstOrDefaultAsync(cancellationToken);
        if (holder is not null)
        {
            return Invalid($"{code} is already the code for {holder}.", "productCode.codeTaken", code, holder);
        }

        // One code per product and colour: with two, which one a bag got would depend on
        // which row the database read first.
        var clash = await db.ProductColourCodes.AnyAsync(
            c => c.ProductId == request.ProductId
                && c.ColorId == request.ColorId
                && (exceptId == null || c.Id != exceptId),
            cancellationToken);
        return clash
            ? Invalid(
                "This product already has a code for that colour. Change that one instead.",
                "productCode.colourTaken")
            : null;
    }

    private async Task<ProductCodeDto> LoadAsync(int id, CancellationToken cancellationToken)
    {
        var row = await db.ProductColourCodes
            .AsNoTracking()
            .Include(c => c.Product)
            .Include(c => c.Color)
            .SingleAsync(c => c.Id == id, cancellationToken);
        return ToDto(row);
    }

    /// <summary>
    /// Capitals with no spaces, as the codes are printed — <c>ab500b </c> typed on a
    /// tablet is the same code as <c>AB500B</c>.
    /// </summary>
    private static string Cleaned(string? code) =>
        string.Concat((code ?? string.Empty).Where(c => !char.IsWhiteSpace(c))).ToUpperInvariant();

    private static ProductCodeDto ToDto(ProductColourCode c) =>
        new(c.Id, c.ProductId, c.Product.Name, c.ColorId, c.Color?.Name, c.Code);

    private static Result<ProductCodeDto> Invalid(string message, string code, params string[] args) =>
        Result<ProductCodeDto>.Failure(ErrorCode.ValidationFailed, message, code, args);
}

using Colors.Application.Common.Models;

namespace Colors.Application.Features.MasterData;

/// <summary>
/// One product code from the factory's list — the code printed on every bag of that
/// product in that colour (specification section 19.25). A null colour is the code for
/// every colour of the product, as the burger boxes have.
/// </summary>
public sealed record ProductCodeDto(
    int Id,
    int ProductId,
    string ProductName,
    int? ColorId,
    string? ColorName,
    string Code);

public sealed record SaveProductCodeRequest(int ProductId, int? ColorId, string Code);

/// <summary>
/// The factory's list of product codes. Read by anyone signed in; changed by the
/// administrator, like the rest of the master data.
/// </summary>
public interface IProductCodeService
{
    Task<IReadOnlyList<ProductCodeDto>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<Result<ProductCodeDto>> CreateAsync(
        SaveProductCodeRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<ProductCodeDto>> UpdateAsync(
        int id,
        SaveProductCodeRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a code for good. Nothing stores it — a bag's label reads the list each time
    /// it is printed — so there is no history for it to break.
    /// </summary>
    Task<Result<bool>> DeleteAsync(int id, CancellationToken cancellationToken = default);
}

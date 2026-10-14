using Colors.Application.Common.Models;
using Colors.Application.Features.Production;
using Colors.Domain.Entities.Production;
using Colors.Infrastructure.Persistence;
using Colors.Infrastructure.Services.Barcodes;
using Colors.Infrastructure.Services.Production;

namespace Colors.IntegrationTests.Common;

/// <summary>
/// A whole roll test, for tests that need a tested roll and are about something else.
///
/// An absorbent roll's wet weight is refused until its dry weight has been saved for ten
/// minutes (specification section 19.15), so the dry weight is saved on a clock eleven
/// minutes behind and the rest at the real time.
/// </summary>
public static class RollTesting
{
    public static async Task<Result<RollDto>> TestFullyAsync(
        ColorsDbContext db,
        int rollId,
        int userId,
        bool absorbent)
    {
        var clock = new TestClock(DateTimeOffset.UtcNow - RollTestReport.SoakTime - TimeSpan.FromMinutes(1));
        var service = new ProductionService(db, new BarcodeService(db, clock), clock);

        if (absorbent)
        {
            var dry = await service.SaveTestReportAsync(
                rollId, new SaveRollTestRequest(9m, 1.2m, 1.2m, 1.2m, 1.2m, null, 10m), userId);
            if (!dry.IsSuccess)
            {
                return dry;
            }

            clock.Now = DateTimeOffset.UtcNow;
        }

        return await service.SaveTestReportAsync(
            rollId,
            new SaveRollTestRequest(
                9m, 1.2m, 1.2m, 1.2m, 1.2m, null,
                absorbent ? 10m : null,
                absorbent ? 13.5m : null),
            userId);
    }
}

using Colors.Application.Common.Models;
using Colors.Application.Features.Dashboard;
using Colors.Application.Features.Reports;
using Colors.Domain.Enums;
using Colors.Infrastructure.Identity;
using Colors.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Colors.Infrastructure.Services.Dashboard;

/// <summary>
/// The home screen (specification section 13).
///
/// Two questions, and nothing else: <b>what is running</b>, and <b>what is waiting for
/// somebody</b>. Everything on it is read from records that already exist.
///
/// The shift's figures come from the reports service rather than being worked out again
/// here, so the home screen and the shift summary can never tell two different stories
/// about the same shift.
/// </summary>
public class DashboardService(ColorsDbContext db, IReportsService reports) : IDashboardService
{
    public async Task<Result<DashboardDto>> GetAsync(CancellationToken cancellationToken = default)
    {
        // One shift is open at a time, which is a rule the database enforces
        // (specification section 2).
        var open = await db.ShiftReports
            .Where(r => r.Status == ShiftReportStatus.Open)
            .Select(r => new
            {
                r.Id,
                r.ProductionDate,
                ShiftName = r.Shift.Name,
                r.SupervisorUserId,
                r.OpenedAt,
                LineNames = r.Lines.Select(l => l.ProductionLine.Name).ToList(),
                LineIds = r.Lines.Select(l => l.Id).ToList(),
            })
            .FirstOrDefaultAsync(cancellationToken);

        DashboardShiftDto? shift = null;
        ShiftSummaryReportDto? summary = null;

        if (open is not null)
        {
            var supervisor = open.SupervisorUserId is null
                ? null
                : await db.Set<ApplicationUser>()
                    .Where(u => u.Id == open.SupervisorUserId)
                    .Select(u => u.FullName)
                    .FirstOrDefaultAsync(cancellationToken);

            shift = new DashboardShiftDto(
                open.Id,
                open.ProductionDate,
                open.ShiftName,
                supervisor,
                open.OpenedAt,
                open.LineNames);

            var read = await reports.GetShiftSummaryAsync(open.Id, cancellationToken);
            summary = read.Value;
        }

        var lineIds = open?.LineIds ?? [];

        return Result<DashboardDto>.Success(new DashboardDto(
            shift,
            summary,
            await AlertsAsync(lineIds, cancellationToken)));
    }

    /// <summary>
    /// What is waiting for somebody. Only the things that are actually waiting come
    /// back — a row reading zero is not news, and a screen full of zeroes teaches people
    /// to stop reading it.
    /// </summary>
    private async Task<IReadOnlyList<DashboardAlertDto>> AlertsAsync(
        List<int> lineIds,
        CancellationToken cancellationToken)
    {
        var alerts = new List<DashboardAlertDto>();

        // The store, which is nobody's shift and everybody's problem.
        var low = await db.MaterialInventory
            .CountAsync(
                i => i.Material.IsActive
                     && i.Material.MinQuantity > 0
                     && i.CurrentQuantity < i.Material.MinQuantity,
                cancellationToken);

        Add("material-low", "material below its minimum", "materials below their minimum", low,
            "The store is under the figure set for it. Receiving more is the only fix.",
            false);

        // These two stop a shift closing, so they are marked (specification section 2).
        var openTickets = await db.MaterialIssueTickets
            .CountAsync(
                t => lineIds.Contains(t.ShiftLineId) && t.Status == IssueTicketStatus.Open,
                cancellationToken);

        Add("ticket-open", "issue ticket still open", "issue tickets still open", openTickets,
            "Material left the store and has not been accounted for. The shift cannot "
            + "close until the leftover is weighed back in.",
            true);

        var inThermo = await db.ThermoProductions
            .CountAsync(
                p => lineIds.Contains(p.ShiftLineId) && p.FinishedAt == null,
                cancellationToken);

        Add("roll-in-thermo", "roll still in the thermo", "rolls still in the thermo", inThermo,
            "A roll is in the machine and its run has no end time. The shift cannot "
            + "close while it is in there.",
            true);

        // Work queues: nothing is blocked, but somebody is waiting to do a job.
        var needsTest = await db.Rolls
            .CountAsync(r => r.Status == RollStatus.NeedsTest, cancellationToken);

        Add("roll-needs-test", "roll waiting to be measured", "rolls waiting to be measured", needsTest,
            "Once a roll is formed into plates there is nothing left to measure, so it "
            + "has to be done first.",
            false);

        var needsCount = await db.ThermoProductions
            .CountAsync(
                p => p.FinishedAt != null && p.TestReport == null, cancellationToken);

        Add("run-needs-count", "finished run waiting to be counted", "finished runs waiting to be counted", needsCount,
            "The bags do not exist until the run is counted, so nothing can be packed "
            + "from it yet.",
            false);

        var loose = await db.ProducedBags
            .CountAsync(b => b.Status == ProducedBagStatus.Available, cancellationToken);

        Add("bag-loose", "bag waiting for a pallet", "bags waiting for a pallet", loose,
            "Made and not yet stacked.",
            false);

        var building = await db.WoodenPallets
            .CountAsync(
                p => p.CompletedAt == null && p.ShippedAt == null && p.CancelledAt == null,
                cancellationToken);

        Add("pallet-open", "pallet being built", "pallets being built", building,
            "Started and not yet full. Each one is holding a wooden pallet from the store.",
            false);

        return alerts;

        void Add(
            string kind, string label, string plural, int count, string detail, bool blocks)
        {
            if (count > 0)
            {
                alerts.Add(new DashboardAlertDto(kind, label, plural, count, detail, blocks));
            }
        }
    }
}

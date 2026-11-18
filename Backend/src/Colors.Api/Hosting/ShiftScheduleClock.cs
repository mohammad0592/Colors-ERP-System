using Colors.Application.Features.ShiftSchedule;

namespace Colors.Api.Hosting;

/// <summary>
/// Opens and closes the scheduled shifts at their times (specification section 19.30).
///
/// Checks every half a minute, so a shift opens within thirty seconds of its start —
/// near enough for men who arrive over ten minutes, and cheap enough to cost nothing.
/// Also checks the moment the API starts, so a server restarted at 08:05 opens shift A
/// then rather than half a minute later.
///
/// The rules all live in the service; this only keeps time. It runs inside the API, so
/// the shifts open only while the API is running — on the factory server that is always,
/// because it is a Windows service that starts with the machine.
/// </summary>
public sealed class ShiftScheduleClock(
    IServiceScopeFactory scopes,
    ILogger<ShiftScheduleClock> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);

        do
        {
            try
            {
                // A scope per check: the database context is made for one unit of work,
                // and one kept for the life of the server would grow without end.
                await using var scope = scopes.CreateAsyncScope();
                var schedule = scope.ServiceProvider.GetRequiredService<IShiftScheduleService>();

                await schedule.RunDueAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                // Never let one bad check stop the clock for good. A database that is
                // down for a minute must not mean no shift opens for the rest of the day.
                logger.LogError(exception, "The shift schedule check failed. It will run again shortly.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}

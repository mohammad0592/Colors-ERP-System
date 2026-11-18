namespace Colors.Infrastructure.Services.ShiftSchedule;

/// <summary>
/// The time on the factory floor, which is what a shift's 08:00 means.
///
/// Everything else in the system keeps time in UTC and lets each screen show it in its
/// own zone. The schedule cannot: it has to know, on the server and with nobody looking,
/// that it is now 08:00 in the factory and shift A starts. On the factory server the
/// machine's own clock is the factory's, so that is the default. A cloud host runs on
/// UTC, two or three hours out, and would open every shift at the wrong time — so it is told
/// with <c>Factory:TimeZone</c>, for example <c>Asia/Hebron</c>.
/// </summary>
public sealed class FactoryClock(TimeProvider timeProvider, TimeZoneInfo zone)
{
    /// <summary>The factory's wall-clock time now.</summary>
    public DateTime Now => TimeZoneInfo.ConvertTime(timeProvider.GetUtcNow(), zone).DateTime;

    public DateOnly Today => DateOnly.FromDateTime(Now);

    /// <summary>
    /// The zone named in configuration, or the machine's own. An unknown name stops the
    /// API starting, saying which name — far better than shifts opening hours late.
    /// </summary>
    public static TimeZoneInfo ZoneFrom(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return TimeZoneInfo.Local;
        }

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(name.Trim());
        }
        catch (TimeZoneNotFoundException)
        {
            throw new InvalidOperationException(
                $"Factory:TimeZone is set to '{name}', which is not a time zone this machine "
                + "knows. Use a name such as 'Asia/Hebron', or leave it unset to use the "
                + "machine's own clock.");
        }
    }
}

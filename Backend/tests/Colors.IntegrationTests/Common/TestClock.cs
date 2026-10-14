namespace Colors.IntegrationTests.Common;

/// <summary>
/// A clock the test sets by hand, for rules that wait on time — the absorbency sample's
/// ten minutes in water (specification section 19.15) — without the suite waiting too.
/// </summary>
public sealed class TestClock(DateTimeOffset start) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = start;

    public override DateTimeOffset GetUtcNow() => Now;

    public void Advance(TimeSpan by) => Now += by;
}

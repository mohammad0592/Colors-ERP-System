using Colors.Domain.Common;

namespace Colors.IntegrationTests.Features;

/// <summary>
/// The code printed on every roll, which is its barcode too (specification section 19.12).
/// Pure arithmetic on a time, so no database is involved.
/// </summary>
public class RollCodeTests
{
    // The factory's clock, three hours ahead of UTC — the code must show its time, not
    // the server's.
    private static DateTimeOffset At(int day, int month, int year, int hour, int minute) =>
        new(year, month, day, hour, minute, 0, TimeSpan.FromHours(3));

    [Theory]
    // The factory's own three examples.
    [InlineData(4, "N", 23, 9, 2026, 8, 44, "White", "04N2309260844-White")]
    [InlineData(31, "ABS", 19, 10, 2026, 17, 14, "Green", "31ABS1910261714-Green")]
    [InlineData(61, "LN", 1, 9, 2026, 22, 21, "Black", "61LN0109262221-Black")]
    public void It_is_number_type_date_time_and_colour(
        int number, string family, int day, int month, int year, int hour, int minute, string colour, string expected)
    {
        Assert.Equal(expected, RollCode.For(number, family, At(day, month, year, hour, minute), colour));
    }

    [Fact]
    public void The_time_is_the_factory_clock_the_tablet_sent()
    {
        // 05:44 UTC is 08:44 on the factory floor.
        var outAt = new DateTimeOffset(2026, 9, 23, 5, 44, 0, TimeSpan.Zero).ToOffset(TimeSpan.FromHours(3));

        Assert.Equal("04N2309260844-White", RollCode.For(4, "N", outAt, "White"));
    }

    [Fact]
    public void The_type_is_in_capitals_whatever_master_data_says()
    {
        // The family is stored as "Abs"; the code says ABS.
        Assert.StartsWith("31ABS", RollCode.For(31, " Abs ", At(19, 10, 2026, 17, 14), "Green"), StringComparison.Ordinal);
    }

    [Fact]
    public void A_hundredth_roll_grows_a_digit_rather_than_wrapping()
    {
        // Two digits is how the factory writes it, not a limit.
        Assert.StartsWith("100N", RollCode.For(100, "N", At(1, 9, 2026, 8, 0), "White"), StringComparison.Ordinal);
    }

    [Fact]
    public void The_barcode_is_the_code_in_capitals_without_spaces()
    {
        // What a scanner or a man typing will produce, whatever case and spacing he uses.
        Assert.Equal("05LN0109262221-LIGHTGREEN", RollCode.AsBarcode("05LN0109262221-Light Green"));
        Assert.Equal(RollCode.AsBarcode("04n2309260844-white"), RollCode.AsBarcode("04N2309260844-White"));
    }
}

using Colors.Domain.Common;

namespace Colors.IntegrationTests.Features;

/// <summary>
/// The code printed on every roll and repeated on every bag made from it
/// (specification section 8). Pure arithmetic on a date, so no database is involved.
/// </summary>
public class RollCodeTests
{
    [Fact]
    public void It_reproduces_the_code_on_the_real_bag_label()
    {
        // 13BAbs240526B, photographed on a bag of black absorbent plates: roll 13 of
        // 24 May 2026, black, absorbent, shift B.
        var code = RollCode.For(13, "B", "Abs", new DateOnly(2026, 5, 24), "B");

        Assert.Equal("13BAbs240526B", code);
    }

    [Fact]
    public void The_date_is_day_month_year()
    {
        // The settling evidence is a code elsewhere reading 310526 — 31 cannot be a
        // year, so the order is DDMMYY and not YYMMDD.
        var code = RollCode.For(1, "W", "N", new DateOnly(2026, 5, 31), "A");

        Assert.Contains("310526", code, StringComparison.Ordinal);
    }

    [Fact]
    public void The_serial_is_padded_to_two_digits()
    {
        Assert.StartsWith("01", RollCode.For(1, "W", "N", new DateOnly(2026, 7, 18), "A"), StringComparison.Ordinal);
    }

    [Fact]
    public void A_hundredth_roll_grows_a_digit_rather_than_wrapping()
    {
        // Two digits is how the factory writes it, not a limit. A day that somehow
        // passes 99 rolls must not start again at 00 and collide.
        Assert.StartsWith("100", RollCode.For(100, "W", "N", new DateOnly(2026, 7, 18), "A"), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("N", "01WN180726A")]
    [InlineData("Abs", "01WAbs180726A")]
    public void The_family_part_is_not_a_fixed_length(string familyCode, string expected)
    {
        // "N" is one letter and "Abs" is three, so nothing may assume a width.
        Assert.Equal(expected, RollCode.For(1, "W", familyCode, new DateOnly(2026, 7, 18), "A"));
    }

    [Fact]
    public void The_colour_and_shift_are_upper_case_whatever_is_typed()
    {
        Assert.Equal(
            "01WN180726A",
            RollCode.For(1, " w ", "N", new DateOnly(2026, 7, 18), " a "));
    }
}

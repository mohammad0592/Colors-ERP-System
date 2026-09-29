using Colors.Domain.Entities.MasterData;

namespace Colors.UnitTests.Domain;

/// <summary>
/// Whether a roll came out at the thickness its product asks for (specification section
/// 19.1).
///
/// A verdict, never a gate: nothing here can stop a roll. What these pin down is that the
/// answer is honest — and above all that "no range" is never mistaken for "failed", since
/// most products have no range until the factory measures them.
/// </summary>
public class ProductThicknessTests
{
    private static Product Plate(decimal? min, decimal? max) =>
        new() { Name = "Big Plate", MinThickness = min, MaxThickness = max };

    [Fact]
    public void No_range_gives_no_verdict_rather_than_a_failure()
    {
        // The state every product starts in. Showing these as out of spec would make
        // every roll in the factory look bad on the day this goes live.
        Assert.Null(Plate(null, null).ThicknessInSpec(2.9m));
    }

    [Theory]
    [InlineData(2.8)]
    [InlineData(2.9)]
    [InlineData(3.0)]
    public void Inside_the_range_is_in_spec_including_both_ends(decimal average)
    {
        Assert.True(Plate(2.8m, 3.0m).ThicknessInSpec(average));
    }

    [Theory]
    [InlineData(2.799)]
    [InlineData(3.001)]
    [InlineData(4.5)]
    public void Outside_the_range_is_out_of_spec(decimal average)
    {
        Assert.False(Plate(2.8m, 3.0m).ThicknessInSpec(average));
    }

    [Fact]
    public void A_range_with_no_top_only_asks_for_the_bottom()
    {
        // "Lunch boxes more than 3 mm" -- the factory's own way of putting it.
        var box = Plate(3.0m, null);

        Assert.True(box.ThicknessInSpec(3.0m));
        Assert.True(box.ThicknessInSpec(9.0m));
        Assert.False(box.ThicknessInSpec(2.99m));
    }

    [Fact]
    public void A_range_with_no_bottom_only_asks_for_the_top()
    {
        var thin = Plate(null, 3.0m);

        Assert.True(thin.ThicknessInSpec(0.5m));
        Assert.False(thin.ThicknessInSpec(3.01m));
    }
}

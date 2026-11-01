using System.Globalization;

namespace Colors.Domain.Common;

/// <summary>
/// A bag's own code, which is also its barcode (specification section 19.25):
/// <c>B050111261758</c> — B, the bag's number that day, then the date and the 24-hour
/// time it was counted.
///
/// The product code beside it on the label says what kind of bag it is; this one says
/// which bag. Two bags can never share one: the number is unique within a day, and the
/// date in the code is that same day.
/// </summary>
public static class BagCode
{
    /// <param name="bagNumber">From the day's count — 01, 05, 37.</param>
    /// <param name="madeAt">When it was counted, in the factory's local time: the code is
    /// read by people, and 17:58 must mean the 17:58 on their clock.</param>
    public static string For(int bagNumber, DateTimeOffset madeAt)
    {
        // Two digits, as the roll number is written. Past 99 it simply grows a digit.
        var number = bagNumber.ToString("D2", CultureInfo.InvariantCulture);
        return $"B{number}{madeAt.ToString("ddMMyyHHmm", CultureInfo.InvariantCulture)}";
    }
}

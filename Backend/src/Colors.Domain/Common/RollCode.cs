using System.Globalization;

namespace Colors.Domain.Common;

/// <summary>
/// A roll's code, which is also its barcode (specification section 8):
/// <c>04N2309260844-White</c> — roll number, recipe type, the date and the 24-hour time it
/// came off the extruder, and its colour.
///
/// One code, not two. The factory reads it off the roll, types it when a label is torn,
/// and scans it; a separate machine number beside it was one more thing to get wrong.
/// </summary>
public static class RollCode
{
    /// <param name="rollNumber">From the factory's running count — 04, 31, 61.</param>
    /// <param name="recipeFamilyCode">N, ABS, LN. Written in capitals whatever the master data says.</param>
    /// <param name="outAt">The out time, in the factory's local time: the code is what
    /// the man at the machine reads, and 08:44 must mean the 08:44 on his clock.</param>
    /// <param name="colourName">White, Green — the name, not the letter.</param>
    public static string For(
        int rollNumber,
        string recipeFamilyCode,
        DateTimeOffset outAt,
        string colourName)
    {
        // Two digits, as the factory writes it — 04, 31. Past 99 it simply grows a digit
        // rather than wrapping.
        var number = rollNumber.ToString("D2", CultureInfo.InvariantCulture);
        var when = outAt.ToString("ddMMyyHHmm", CultureInfo.InvariantCulture);
        return $"{number}{recipeFamilyCode.Trim().ToUpperInvariant()}{when}-{colourName.Trim()}";
    }

    /// <summary>
    /// The code as the barcode stores it: capitals, no spaces. A scanner and a man typing
    /// both arrive in whatever case and spacing they like, and every lookup cleans what it
    /// is given the same way, so <c>04N2309260844-White</c> on the label and
    /// <c>04N2309260844-WHITE</c> in the table are one roll.
    /// </summary>
    public static string AsBarcode(string rollCode) =>
        string.Concat(rollCode.Where(c => !char.IsWhiteSpace(c))).ToUpperInvariant();
}

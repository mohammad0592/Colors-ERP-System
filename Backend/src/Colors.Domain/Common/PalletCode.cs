using System.Globalization;

namespace Colors.Domain.Common;

/// <summary>
/// A pallet's own code, which is also its barcode (specification section 19.26):
/// <c>P030111261758</c> — P, the pallet's number that day, then the date and the 24-hour
/// time it was finished, full or closed.
///
/// Made the same way as a bag's code (<see cref="BagCode"/>), with two digits for the
/// number rather than three: the factory finishes a few dozen pallets a day, not hundreds.
/// </summary>
public static class PalletCode
{
    /// <param name="palletNumber">From the day's count of finished pallets — 01, 03, 17.</param>
    /// <param name="finishedAt">When it was completed or closed, in the factory's local
    /// time, so 17:58 means the 17:58 on the clock on the wall.</param>
    public static string For(int palletNumber, DateTimeOffset finishedAt)
    {
        // Past 99 it simply grows a digit.
        var number = palletNumber.ToString("D2", CultureInfo.InvariantCulture);
        return $"P{number}{finishedAt.ToString("ddMMyyHHmm", CultureInfo.InvariantCulture)}";
    }

    /// <summary>
    /// The pallet's general code: P in front of the code of the bags on it —
    /// <c>PAB500B</c>, <c>PBB</c>. Like the bags' own, it says what kind of pallet it is,
    /// not which one.
    /// </summary>
    public static string General(string bagProductCode) => $"P{bagProductCode}";
}

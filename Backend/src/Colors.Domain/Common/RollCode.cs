using System.Globalization;

namespace Colors.Domain.Common;

/// <summary>
/// The code printed on every roll and repeated on every bag label made from it
/// (specification section 8).
///
/// <code>
/// 13   B    Abs   24 05 26   B
/// │    │    │     │  │  │     └── shift
/// │    │    │     │  │  └──────── year
/// │    │    │     │  └─────────── month
/// │    │    │     └────────────── day
/// │    │    └──────────────────── recipe family code (N, Abs)
/// │    └───────────────────────── colour letter
/// └────────────────────────────── daily serial, restarting at 1 each day
/// </code>
///
/// The date is <b>DDMMYY</b>, confirmed by the factory: the real label
/// <c>13BAbs240526B</c> reads as 24 May 2026, and <c>310526</c> elsewhere settles it —
/// 31 cannot be a year.
///
/// The family part is not a fixed length: <c>N</c> is one letter, <c>Abs</c> is three.
/// It comes from the family's own code, so a future family of any length works without
/// touching this.
/// </summary>
public static class RollCode
{
    public static string For(
        int dailySerial,
        string colourCode,
        string recipeFamilyCode,
        DateOnly productionDate,
        string shiftName)
    {
        // Two digits for the serial, as the factory writes it — 01, 13. A day that
        // somehow passes 99 rolls simply grows a digit rather than wrapping.
        var serial = dailySerial.ToString("D2", CultureInfo.InvariantCulture);
        var date = productionDate.ToString("ddMMyy", CultureInfo.InvariantCulture);

        return $"{serial}{colourCode.Trim().ToUpperInvariant()}{recipeFamilyCode.Trim()}{date}{shiftName.Trim().ToUpperInvariant()}";
    }
}

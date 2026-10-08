using System.Globalization;

namespace TaxiReceipt.Service;

/// <summary>
/// The time rules of the original app, fixed: it compared the hour with 24
/// (never true, so it never switched off) and only knew "from hour X until
/// midnight". Here the window may cross midnight, and a punch after midnight
/// belongs to the night that started the evening before.
/// </summary>
public static class NightWindow
{
    public static bool IsActive(TimeOnly time, TimeOnly from, TimeOnly until)
    {
        if (from == until) return true; // a 24-hour window
        return from < until
            ? time >= from && time < until
            : time >= from || time < until; // crosses midnight
    }

    /// <summary>The date the night started on: 02:00 on the 5th belongs to the night of the 4th.</summary>
    public static DateOnly NightOf(DateTime punch, TimeOnly from, TimeOnly until)
    {
        var date = DateOnly.FromDateTime(punch);
        var crossesMidnight = from > until;
        return crossesMidnight && TimeOnly.FromDateTime(punch) < until ? date.AddDays(-1) : date;
    }

    /// <summary>Persian year and month, e.g. 2026-10-08 -> "140507".</summary>
    public static string PersianYearMonth(DateTime date)
    {
        var pc = new PersianCalendar();
        return $"{pc.GetYear(date):0000}{pc.GetMonth(date):00}";
    }

    public static string TableName(string pattern, DateTime date) =>
        string.Format(CultureInfo.InvariantCulture, pattern, PersianYearMonth(date));
}

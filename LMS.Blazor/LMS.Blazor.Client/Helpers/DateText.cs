using System.Globalization;

namespace LMS.Blazor.Client.Helpers;

/// <summary>
/// One way to show dates and times on every page, so the same time never looks different on two pages.
///
/// Two kinds of times exist in the app:
/// - Activity times and deadlines are Swedish wall-clock time and are shown as they are (WallClock, Day, Time).
/// - Timestamps set by the server (hand-in, feedback, upload, notification) are UTC and are shown in the
///   time zone of the browser (Utc, Relative).
///
/// The year is only written when it is not the current year, for example "Thu 15 Oct, 10:00" but
/// "Thu 15 Oct 2025, 10:00" for last year.
/// </summary>
public static class DateText
{
    private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;

    // "1 Oct 2026". Used for course and module dates, which are always shown with the year.
    public static string Date(DateOnly value) =>
        value.ToString("d MMM yyyy", Culture);

    // "1 Oct". Used where the year is clear from the page, for example in a module list.
    public static string ShortDate(DateOnly value) =>
        value.ToString("d MMM", Culture);

    // "Thu 15 Oct". The day of an activity.
    public static string Day(DateTime value) =>
        value.ToString("ddd d MMM", Culture);

    // "10:00". The time of an activity.
    public static string Time(DateTime value) =>
        value.ToString("HH:mm", Culture);

    // "Thu 15 Oct, 10:00". A deadline or activity time (Swedish wall-clock time), shown as it is.
    public static string WallClock(DateTime value) =>
        value.ToString(DateTimeFormat(value), Culture);

    // "Thu 15 Oct, 10:00". A UTC time from the server, shown in the browser's own time zone.
    public static string Utc(DateTime utc) =>
        WallClock(ToLocal(utc));

    // "Just now", "5 min ago", "2 h ago", "Today 08:12", "Yesterday" or "5 Sep". For notifications.
    // The time is UTC from the server. It is converted to local time first, so it can be compared with
    // the clock of the browser.
    public static string Relative(DateTime utc)
    {
        var value = ToLocal(utc);
        var now = DateTime.Now;
        var diff = now - value;

        if (diff.TotalMinutes < 1) return "Just now";
        if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes} min ago";
        if (diff.TotalHours < 6) return $"{(int)diff.TotalHours} h ago";
        if (value.Date == now.Date) return $"Today {value:HH:mm}";
        if (value.Date == now.Date.AddDays(-1)) return "Yesterday";

        return value.ToString(value.Year == now.Year ? "d MMM" : "d MMM yyyy", Culture);
    }

    // A time from the server comes without a time zone (Kind is Unspecified) after JSON.
    // It is UTC, so say so before converting to the browser's local time.
    private static DateTime ToLocal(DateTime utc) =>
        DateTime.SpecifyKind(utc, DateTimeKind.Utc).ToLocalTime();

    // The year is added only for another year than the current one
    private static string DateTimeFormat(DateTime value) =>
        value.Year == DateTime.Now.Year ? "ddd d MMM, HH:mm" : "ddd d MMM yyyy, HH:mm";
}
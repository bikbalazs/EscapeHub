namespace EscapeHub.Web;

public static class HungarianTime
{
    private static readonly TimeZoneInfo Budapest = TimeZoneInfo.FindSystemTimeZoneById(
        OperatingSystem.IsWindows() ? "Central Europe Standard Time" : "Europe/Budapest");

    public static DateTime FromUtc(DateTime value) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(value, DateTimeKind.Utc), Budapest);
}

namespace PizzaDeli.Helpers;

/// <summary>
/// Chuyển đổi UTC ↔ Giờ Việt Nam (UTC+7).
/// Tương thích Windows ("SE Asia Standard Time") và Linux/Docker ("Asia/Ho_Chi_Minh").
/// 
/// RULE:
///   - DB luôn lưu UTC → dùng DateTime.UtcNow
///   - Hiển thị cho user → dùng TimeZoneHelper.ToVietnam(model.OrderDate)
/// 
/// Razor View:
///   @TimeZoneHelper.ToVietnam(Model.OrderDate).ToString("dd/MM/yyyy HH:mm")
/// </summary>
public static class TimeZoneHelper
{
    private static readonly TimeZoneInfo _vietnamTz = GetVietnamTimeZone();

    private static TimeZoneInfo GetVietnamTimeZone()
    {
        // Linux/Docker (Render): "Asia/Ho_Chi_Minh"
        // Windows: "SE Asia Standard Time"
        string[] ids = ["Asia/Ho_Chi_Minh", "SE Asia Standard Time"];

        foreach (var id in ids)
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
            catch { /* thử id tiếp theo */ }
        }

        // Fallback thủ công UTC+7
        return TimeZoneInfo.CreateCustomTimeZone(
            "Vietnam Standard Time",
            TimeSpan.FromHours(7),
            "Vietnam Standard Time",
            "Vietnam Standard Time");
    }

    /// <summary>Giờ Việt Nam hiện tại.</summary>
    public static DateTime NowVietnam =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _vietnamTz);

    /// <summary>
    /// Chuyển UTC → Giờ Việt Nam (dùng trong Razor View để hiển thị).
    /// Thay thế:
    ///   @TimeZoneInfo.ConvertTimeFromUtc(Model.OrderDate,
    ///       TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time"))
    /// Bằng:
    ///   @TimeZoneHelper.ToVietnam(Model.OrderDate)
    /// </summary>
    public static DateTime ToVietnam(DateTime utcTime) =>
        TimeZoneInfo.ConvertTimeFromUtc(
            DateTime.SpecifyKind(utcTime, DateTimeKind.Utc), _vietnamTz);

    /// <summary>Chuyển UTC? → Giờ Việt Nam (nullable).</summary>
    public static DateTime? ToVietnam(DateTime? utcTime) =>
        utcTime.HasValue ? ToVietnam(utcTime.Value) : null;

    /// <summary>Chuyển Giờ Việt Nam → UTC (trước khi lưu DB).</summary>
    public static DateTime ToUtc(DateTime vietnamTime) =>
        TimeZoneInfo.ConvertTimeToUtc(vietnamTime, _vietnamTz);
}

using LogLevel = MMP.Herald.Levels.LogLevel;

namespace SlotDemo.Server;

/// <summary>
/// The harness's 10-level event set. System-origin events (ASP.NET, DI, HttpClient: any
/// Microsoft.*/System.* category) log at the <c>sys.</c> variant of their level;
/// application events log at the plain variant. Ranks interleave so one minimum-level
/// threshold can keep application signal while dropping framework noise of the same
/// nominal severity. Error and fatal stay shared: a failure is a failure, whoever
/// raised it.
/// </summary>
public static class SlotDemoLevels
{
    public const string SysVerbose = "sys.verbose";
    public const string SysDebug = "sys.debug";
    public const string SysInformation = "sys.information";
    public const string SysWarning = "sys.warning";

    /// <summary>Rank order, lowest first — the single home of the ordering.</summary>
    public static readonly string[] Order =
    [
        SysVerbose, "verbose",
        SysDebug, "debug",
        SysInformation, "information",
        SysWarning, "warning",
        "error", "fatal",
    ];

    // Level instances resolve by KEY in Herald's registry — these pair with the
    // WithCustomLevel registrations in Program.cs.
    public static readonly LogLevel SysVerboseLevel = new(SysVerbose, "SysVerbose");
    public static readonly LogLevel SysDebugLevel = new(SysDebug, "SysDebug");
    public static readonly LogLevel SysInformationLevel = new(SysInformation, "SysInformation");
    public static readonly LogLevel SysWarningLevel = new(SysWarning, "SysWarning");

    /// <summary>True when a logger category is framework/system infrastructure.</summary>
    public static bool IsSystemCategory(string category) =>
        category.StartsWith("Microsoft", StringComparison.Ordinal) ||
        category.StartsWith("System", StringComparison.Ordinal);

    /// <summary>Rank of a level key in <see cref="Order"/>; -1 when unknown.</summary>
    public static int RankOf(string levelKey) =>
        Array.FindIndex(Order, k => string.Equals(k, levelKey, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Minimum-level predicate for <c>WithCustomFilter</c>. WORKAROUND, reported
    /// upstream to Herald.OSS: the engine's minimum-level filter skips events
    /// carrying custom levels (standard-level events filter correctly, and custom
    /// keys work as the floor — only custom-level EVENTS bypass the check). This
    /// predicate enforces the floor over the full 10-level order. Unknown keys
    /// pass, matching the engine's own permissive posture. Delete when Herald
    /// filters custom-level events natively.
    /// </summary>
    public static Func<MMP.Herald.Events.LogEvent, bool> AtOrAbove(string floorKey)
    {
        var floor = RankOf(floorKey);
        return logEvent =>
        {
            var rank = RankOf(logEvent.Level.Key);
            return rank < 0 || rank >= floor;
        };
    }
}

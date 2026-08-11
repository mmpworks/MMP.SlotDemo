using MMP.Herald.Events;
using MMP.Herald.Templating;
using MMP.Herald.Levels;
using MMP.Herald.Pipeline;
using LogLevel = MMP.Herald.Levels.LogLevel;
using MEL = Microsoft.Extensions.Logging;

namespace SlotDemo.Server;

/// <summary>
/// MEL provider that routes framework categories (Microsoft.*, System.*) to the
/// <c>sys.*</c> levels and application categories to the plain levels of the
/// <see cref="SlotDemoLevels"/> set, emitting into a native Herald pipeline.
/// Herald's stock MEL provider maps to the standard level set only — this is the
/// harness-owned variant that speaks the custom set.
/// </summary>
public sealed class SystemAwareHeraldProvider : MEL.ILoggerProvider
{
    private readonly StructuredLogger _logger;

    public SystemAwareHeraldProvider(StructuredLogger logger) => _logger = logger;

    public MEL.ILogger CreateLogger(string categoryName) =>
        new SystemAwareLogger(_logger, categoryName, SlotDemoLevels.IsSystemCategory(categoryName));

    public void Dispose() { /* pipeline lifetime is owned by Program.cs */ }

    private sealed class SystemAwareLogger : MEL.ILogger
    {
        private readonly StructuredLogger _logger;
        private readonly LogCategory _category;
        private readonly bool _isSystem;

        public SystemAwareLogger(StructuredLogger logger, string category, bool isSystem)
        {
            _logger = logger;
            _category = new LogCategory(category);
            _isSystem = isSystem;
        }

        public bool IsEnabled(MEL.LogLevel level) => _logger.IsEnabled(Map(level));

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public void Log<TState>(
            MEL.LogLevel level,
            MEL.EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(level)) return;

            var message = formatter(state, exception);

            IReadOnlyDictionary<string, object?>? context = exception is null
                ? null
                : new Dictionary<string, object?> { ["exception"] = exception };

            IReadOnlyList<LogProperty>? properties = null;
            if (state is IReadOnlyList<KeyValuePair<string, object?>> stateProperties)
            {
                var props = new List<LogProperty>(stateProperties.Count);
                foreach (var pair in stateProperties)
                {
                    if (pair.Key == "{OriginalFormat}") continue; // MEL's template slot
                    props.Add(new LogProperty(pair.Key, pair.Value));
                }
                if (props.Count > 0) properties = props;
            }

            _logger.Log(Map(level), _category, message, properties, context,
                eventId.Id != 0 ? new LogEventId(eventId.Id, eventId.Name) : null);
        }

        // Error and fatal are shared between system and app on purpose — see
        // SlotDemoLevels. Only the noise-prone bands get a sys. variant.
        private LogLevel Map(MEL.LogLevel level) => level switch
        {
            MEL.LogLevel.Trace => _isSystem ? SlotDemoLevels.SysVerboseLevel : KnownLogLevels.Verbose,
            MEL.LogLevel.Debug => _isSystem ? SlotDemoLevels.SysDebugLevel : KnownLogLevels.Debug,
            MEL.LogLevel.Information => _isSystem ? SlotDemoLevels.SysInformationLevel : KnownLogLevels.Information,
            MEL.LogLevel.Warning => _isSystem ? SlotDemoLevels.SysWarningLevel : KnownLogLevels.Warning,
            MEL.LogLevel.Error => KnownLogLevels.Error,
            _ => KnownLogLevels.Fatal,
        };
    }
}

# Bezoro.Logging

A flexible, high-performance logging library for .NET with optional complexity. Designed for game development and general .NET applications with Unity compatibility.

## Features

- **Conditional log calls**: `Logger.Log*` calls and their arguments are omitted at call sites compiled without `DEBUG`
- **Event-driven architecture**: Subscribe to `Logger.OnLog` to handle log output
- **Rich metadata**: Timestamps, sequence numbers, thread IDs, file locations, and caller info
- **Category system**: 50+ predefined categories for game systems, networking, UI, and more
- **Async context tracking**: Automatic flow through async/await boundaries
- **Performance timing**: Scoped elapsed-time measurement when the Bezoro.Logging assembly is compiled with `DEBUG`
- **Flexible grouping**: Group logs by caller, category, thread, time window, or async context
- **Visual styling**: Configurable colors and emoji indicators per log level
- **Unity compatible**: Targets both .NET 9.0 and .NET Standard 2.1

## Installation

Add a reference to `Bezoro.Logging` in your project:

```xml
<ProjectReference Include="path/to/Bezoro.Logging.csproj" />
```

## Quick Start

```csharp
using Bezoro.Logging;
using Bezoro.Logging.Types;

// Subscribe to log events
Logger.OnLog += payload => Console.WriteLine(payload.FormattedMessage);

// Basic logging
Logger.Log("Application started");
Logger.Log("Player joined", LogLevel.Success, LogCategory.Network);
Logger.Log("Low memory warning", LogLevel.Warning, LogCategory.Memory);
Logger.Log("Connection failed", LogLevel.Error, LogCategory.Network);

// Exception logging with automatic detail extraction
try
{
    // ... code that throws
}
catch (Exception ex)
{
    Logger.Log(ex, "Failed to load configuration", LogCategory.Configuration);
}
```

## Log Levels

| Level       | Description                               | Default Color   |
|-------------|-------------------------------------------|-----------------|
| `Info`      | General informational messages            | White           |
| `Success`   | Successful operation notifications        | Green           |
| `Warning`   | Potential issues or non-critical problems | Yellow          |
| `Error`     | Problems that don't stop execution        | Red             |
| `Exception` | Severe errors with stack trace capture    | Dark Red (Bold) |

## Categories

Bezoro.Logging includes 50+ predefined categories organized by domain:

- **Core**: `Default`, `System`, `Debug`, `Test`, `Profiling`
- **Game Systems**: `Gameplay`, `Combat`, `Inventory`, `Quest`, `Dialog`, `Ai`
- **Graphics**: `Rendering`, `Shaders`, `Particles`, `Lighting`, `PostProcessing`
- **Infrastructure**: `Network`, `Database`, `FileIo`, `Memory`, `Security`
- **UI/Input**: `Ui`, `Input`, `Camera`
- **Audio**: `Audio`
- **And more**: `Loading`, `SaveSystem`, `Resources`, `Localization`, `Authentication`

## Configuration

### Global Settings

```csharp
// Enable/disable all logging
LoggerSettings.Enabled = false;

// Set minimum log level
Logger.MinimumLevel = LogLevel.Warning;

// Mute specific categories
LoggerSettings.MutedCategories.Add(LogCategory.Debug);
LoggerSettings.MutedCategories.Add(LogCategory.Profiling);
```

### Metadata Options

```csharp
// Timestamp configuration
LoggerSettings.Timestamp = TimestampConfig.Default;           // HH:mm:ss.fff
LoggerSettings.Timestamp = TimestampConfig.Create("yyyy-MM-dd HH:mm:ss");
LoggerSettings.Timestamp = TimestampConfig.Disabled;

// Sequence numbers (for log ordering)
LoggerSettings.SequenceNumber = SequenceNumberConfig.On;
LoggerSettings.SequenceNumber = SequenceNumberConfig.Off;

// Thread ID tracking
LoggerSettings.ThreadId = ThreadIdConfig.On;
LoggerSettings.ThreadId = ThreadIdConfig.Off;

// File location in logs
LoggerSettings.FileLocation = FileLocationConfig.FullPath;
LoggerSettings.FileLocation = FileLocationConfig.FilenameOnly;
LoggerSettings.FileLocation = FileLocationConfig.Disabled;

// Frame count (for game engines)
LoggerSettings.FrameCount = FrameCountConfig.Create(() => Time.frameCount);
```

### Log Grouping

```csharp
// Group logs by various criteria
LoggerSettings.Grouping = GroupingConfig.Create(LoggerSettings.ContextGrouping.CallerType);
LoggerSettings.Grouping = GroupingConfig.Create(LoggerSettings.ContextGrouping.Category);
LoggerSettings.Grouping = GroupingConfig.Create(LoggerSettings.ContextGrouping.Thread);
LoggerSettings.Grouping = GroupingConfig.Create(LoggerSettings.ContextGrouping.AsyncContext);

// Time-window grouping
LoggerSettings.Grouping = GroupingConfig.Create(
    LoggerSettings.ContextGrouping.TimeWindow,
    timeWindowMs: 1000); // 1 second windows
```

### Visual Styling

```csharp
// Customize colors per log level
LoggerSettings.InfoStyle = new LogStyle(ConsoleColor.Cyan);
LoggerSettings.ErrorStyle = new LogStyle(ConsoleColor.Red, bold: true);
LoggerSettings.WarningStyle = new LogStyle(ConsoleColor.Yellow, italic: true);
```

## Performance Timing

Measure operation durations with a scoped timer:

```csharp
using (Logger.BeginTimer("LoadLevel", LogCategory.Loading))
{
    // ... loading code ...
}
// Output includes: LoadLevel completed in 123.45ms
```

`Logger.BeginTimer` itself is not conditional. When the Bezoro.Logging assembly is
compiled with `DEBUG`, the returned `PerformanceTimer` captures a start timestamp
and logs from `Dispose`. Otherwise, the timer stores no state and `Dispose` performs
no work. Direct `Logger.Log*` calls are conditional and are omitted when `DEBUG` is
not defined in the compilation containing each call site.

## Async Context Tracking

Track hierarchical context through async/await:

```csharp
using (LoggerSettings.BeginAsyncContext("GameLoop"))
{
    Logger.Log("Starting frame"); // Context: GameLoop

    using (LoggerSettings.BeginAsyncContext("Player-123"))
    {
        await ProcessPlayerAsync();
        Logger.Log("Player updated"); // Context: GameLoop > Player-123
    }
}
```

## Log Output Format

Depending on the enabled settings, an ordinary formatted message follows this structure:

```
🔄 [root > child] (when an async context is present)
[#sequence timestamp Fframe Tthread G:group] severity-emoji [category-emoji] Message
  └─ file :: caller
```

For example, with sequence, timestamp, and thread metadata enabled:
```
[#42 14:32:15.123 T1] ℹ️ [🌐] Player connected
  └─ GameManager.cs :: GameManager.OnPlayerJoin()
```

With async context:
```
🔄 [GameLoop > Player-123]
[#43 14:32:15.456 T1] ℹ️ [🧠] Computing path
  └─ AIController.cs :: AIController.ComputePath()
```

The detail line appears only when caller information is captured or the level is
`Warning`, `Error`, or `Exception`, and it contains only the enabled, available values.

## LogPayload

When handling log events, you receive a `LogPayload` with:

| Property | Description |
| --- | --- |
| `Timestamp` | UTC timestamp captured for the event |
| `Level` | Log severity level |
| `Category` | Optional category |
| `Message` | Raw message content |
| `FormattedMessage` | Message formatted according to the captured settings |
| `SeverityEmoji` | Emoji for the level |
| `CategoryEmoji` | Emoji for the category, when present |
| `CallerInfo` | Captured `TypeName.MethodName()` value, when requested |
| `ContextObject` | Associated object, such as a Unity object |
| `Style` | Visual styling hints captured for the level |
| `Exception` | Original exception instance, when logging an exception |
| `ExceptionType` | Exception type name |
| `InnerExceptionType` | Inner exception type name, when present |
| `InnerExceptionMessage` | Inner exception message, when present |
| `StackTrace` | Captured exception stack trace |
| `AsyncContext` | Async hierarchy joined with ` > ` |
| `AsyncContextHierarchy` | Ordered async-context names |
| `AsyncContextDepth` | Number of names in the async hierarchy |
| `GroupingContext` | Grouping identifier computed from `LoggerSettings.Grouping` |
| `Stage` | Normalized stage label, when stage tracking is enabled |

## API Reference

| API | Purpose |
| --- | --- |
| `Logger` | Emits conditional log records, exceptions, and performance timings through `OnLog`. |
| `LoggerSettings` | Configures enablement, metadata, grouping, styling, and async context. |
| `LogPayload` | Carries an init-only snapshot delivered to log subscribers. Referenced objects and collections are not guaranteed to be immutable. |
| `LogLevel` / `LogCategory` | Classifies severity and subsystem. |
| `GroupingConfig` | Selects caller, category, thread, async-context, or time-window grouping. |
| `PerformanceTimer` | Measures and logs a scoped operation duration when the Bezoro.Logging assembly is compiled with `DEBUG`. |

## Unity Integration

The library targets .NET Standard 2.1 for Unity compatibility. Connect to Unity's console:

```csharp
Logger.OnLog += payload =>
{
    switch (payload.Level)
    {
        case LogLevel.Error:
        case LogLevel.Exception:
            Debug.LogError(payload.FormattedMessage, payload.ContextObject as UnityEngine.Object);
            break;
        case LogLevel.Warning:
            Debug.LogWarning(payload.FormattedMessage, payload.ContextObject as UnityEngine.Object);
            break;
        default:
            Debug.Log(payload.FormattedMessage, payload.ContextObject as UnityEngine.Object);
            break;
    }
};
```

## Target Frameworks

- .NET 9.0
- .NET Standard 2.1 (Unity)

## Design Notes

- `Logger.Log*` calls use `Conditional("DEBUG")`; each call is controlled by the `DEBUG` symbol in the compilation containing that call site.
- `Logger.BeginTimer` remains a normal call in all builds, while its timer captures and logs only when the Bezoro.Logging assembly is compiled with `DEBUG`.
- Output is event-driven; applications own console, file, Unity, or telemetry sinks.
- Async context uses immutable linked ambient scopes so overlapping async branches push and pop independently, and disposal restores the parent scope.
- `Logger` owns filtering, stage transitions, sequence/time/thread/frame capture, and synchronous dispatch; the internal formatter deterministically maps captured input, settings, and context into `LogPayload`.
- Configuration remains static to keep call sites allocation-light and engine-independent.

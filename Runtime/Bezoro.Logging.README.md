# Bezoro.Logging

A flexible, high-performance logging library for .NET with optional complexity. Designed for game development and general .NET applications with Unity compatibility.

## Features

- **Zero-overhead in Release**: All logging is compiled away via `[Conditional("DEBUG")]`
- **Event-driven architecture**: Subscribe to `Logger.OnLog` to handle log output
- **Rich metadata**: Timestamps, sequence numbers, thread IDs, file locations, and caller info
- **Category system**: 50+ predefined categories for game systems, networking, UI, and more
- **Async context tracking**: Automatic flow through async/await boundaries
- **Performance timing**: Zero-allocation timer with automatic logging
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
- **Game Systems**: `Gameplay`, `Combat`, `Inventory`, `Quest`, `Dialog`, `AI`
- **Graphics**: `Rendering`, `Shaders`, `Particles`, `Lighting`, `PostProcessing`
- **Infrastructure**: `Network`, `Database`, `FileIO`, `Memory`, `Security`
- **UI/Input**: `UI`, `Input`, `Camera`
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
LoggerSettings.FileLocation = FileLocationConfig.FileName;
LoggerSettings.FileLocation = FileLocationConfig.Disabled;

// Frame count (for game engines)
LoggerSettings.FrameCount = FrameCountConfig.Create(() => Time.frameCount);
```

### Log Grouping

```csharp
// Group logs by various criteria
LoggerSettings.Grouping = GroupingConfig.By(LoggerSettings.ContextGrouping.CallerType);
LoggerSettings.Grouping = GroupingConfig.By(LoggerSettings.ContextGrouping.Category);
LoggerSettings.Grouping = GroupingConfig.By(LoggerSettings.ContextGrouping.Thread);
LoggerSettings.Grouping = GroupingConfig.By(LoggerSettings.ContextGrouping.AsyncContext);

// Time-window grouping
LoggerSettings.Grouping = GroupingConfig.ByTimeWindow(1000); // 1 second windows
```

### Visual Styling

```csharp
// Customize colors per log level
LoggerSettings.InfoStyle = new LogStyle(ConsoleColor.Cyan);
LoggerSettings.ErrorStyle = new LogStyle(ConsoleColor.Red, bold: true);
LoggerSettings.WarningStyle = new LogStyle(ConsoleColor.Yellow, italic: true);
```

## Performance Timing

Measure operation durations with zero-allocation timing:

```csharp
using (Logger.BeginTimer("LoadLevel", LogCategory.Loading))
{
    // ... loading code ...
}
// Output: [Info] [Loading] LoadLevel completed in 123.45ms
```

The timer is fully compiled away in release builds.

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

The formatted message follows a hierarchical structure:

```
[#seq][timestamp][thread] severity [category] Message
  └─ file :: caller
```

Example output:
```
[#42][14:32:15.123][T:1] Info [Network] Player connected
  └─ GameManager.cs :: OnPlayerJoin()
```

With async context:
```
🔄 [GameLoop > Player-123]
[#42][14:32:15.123][T:1] Info [AI] Computing path
  └─ AIController.cs :: ComputePath()
```

## LogPayload

When handling log events, you receive a `LogPayload` with:

| Property           | Description                            |
|--------------------|----------------------------------------|
| `Timestamp`        | UTC timestamp                          |
| `Level`            | Log severity level                     |
| `Category`         | Optional category                      |
| `Message`          | Raw message content                    |
| `FormattedMessage` | Ready-to-display message               |
| `SeverityEmoji`    | Emoji for the level                    |
| `CategoryEmoji`    | Emoji for the category                 |
| `CallerInfo`       | `TypeName.MethodName()` format         |
| `ContextObject`    | Associated object (e.g., Unity Object) |
| `Style`            | Visual styling hints                   |
| `AsyncContext`     | Formatted async context string         |
| `StackTrace`       | Stack trace (exceptions only)          |
| `ExceptionType`    | Exception type name                    |
| `GroupingContext`  | Computed grouping identifier           |

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

## Dependencies

- [Bezoro.Core](../Bezoro.Core/README.md)

## Target Frameworks

- .NET 9.0
- .NET Standard 2.1 (Unity)

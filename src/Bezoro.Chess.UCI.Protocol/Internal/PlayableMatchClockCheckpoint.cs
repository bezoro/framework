namespace Bezoro.Chess.UCI.Protocol.Internal;

internal readonly record struct PlayableMatchClockCheckpoint(
    TimeSpan WhiteRemaining,
    TimeSpan BlackRemaining,
    char ActiveColor,
    int WhiteMovesCompleted,
    int BlackMovesCompleted,
    DateTimeOffset TurnStartedAtUtc,
    DateTimeOffset? PausedAtUtc
);

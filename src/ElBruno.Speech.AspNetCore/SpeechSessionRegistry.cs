using System.Collections.Concurrent;

namespace ElBruno.Speech.AspNetCore;

/// <summary>
/// Tracks active <see cref="SpeechWebSocketSession"/> instances.
/// Register as a singleton in DI.
/// </summary>
public sealed class SpeechSessionRegistry
{
    private readonly ConcurrentDictionary<string, SpeechWebSocketSession> _sessions = new();

    /// <summary>Number of currently active sessions.</summary>
    public int ActiveSessionCount => _sessions.Count;

    internal void Register(SpeechWebSocketSession session) =>
        _sessions[session.SessionId] = session;

    internal void Unregister(SpeechWebSocketSession session) =>
        _sessions.TryRemove(session.SessionId, out _);

    /// <summary>Returns a snapshot of all active session IDs.</summary>
    public IReadOnlyList<string> GetSessionIds() => [.. _sessions.Keys];
}

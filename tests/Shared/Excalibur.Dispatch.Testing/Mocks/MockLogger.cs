// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// Licensed under the Excalibur License 1.0 - see LICENSE files for details.

namespace Excalibur.Dispatch.Testing.Mocks;

/// <summary>
/// A test logger that captures log entries for assertions.
/// </summary>
/// <typeparam name="T">The category type for the logger.</typeparam>
public class MockLogger<T> : ILogger<T>
{
	private readonly List<LogEntry> _entries = new();

	public IReadOnlyList<LogEntry> Entries => _entries.AsReadOnly();

	public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

	public bool IsEnabled(LogLevel logLevel) => true;

	public void Log<TState>(
		LogLevel logLevel,
		EventId eventId,
		TState state,
		Exception? exception,
		Func<TState, Exception?, string> formatter)
	{
		_entries.Add(new LogEntry(
			logLevel,
			eventId,
			formatter(state, exception),
			exception));
	}

	public void Clear() => _entries.Clear();

	public bool HasLoggedError() => _entries.Any(e => e.Level >= LogLevel.Error);

	public bool HasLogged(LogLevel level, string messageContains) =>
		_entries.Any(e => e.Level == level && e.Message.Contains(messageContains));
}

public record LogEntry(LogLevel Level, EventId EventId, string Message, Exception? Exception);

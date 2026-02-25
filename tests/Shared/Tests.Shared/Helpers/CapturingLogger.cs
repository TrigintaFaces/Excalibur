// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Tests.Shared.Helpers;

/// <summary>
/// A test logger that captures log entries for assertions.
/// Use this when you need to verify that specific log messages were written.
/// </summary>
/// <typeparam name="T">The category type for the logger.</typeparam>
public sealed class CapturingLogger<T> : ILogger<T>
{
	private readonly object _lock = new();
	private readonly List<CapturedLogEntry> _entries = [];

	/// <summary>
	/// Gets a snapshot of captured log entries.
	/// </summary>
	public IReadOnlyList<CapturedLogEntry> Entries
	{
		get
		{
			lock (_lock)
			{
				return [.. _entries];
			}
		}
	}

	/// <summary>
	/// Gets the number of captured entries.
	/// </summary>
	public int Count
	{
		get
		{
			lock (_lock)
			{
				return _entries.Count;
			}
		}
	}

	/// <inheritdoc/>
	public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

	/// <inheritdoc/>
	public bool IsEnabled(LogLevel logLevel) => true;

	/// <inheritdoc/>
	public void Log<TState>(
		LogLevel logLevel,
		EventId eventId,
		TState state,
		Exception? exception,
		Func<TState, Exception?, string> formatter)
	{
		lock (_lock)
		{
			_entries.Add(new CapturedLogEntry(
				logLevel,
				eventId,
				formatter(state, exception),
				exception,
				typeof(T).Name));
		}
	}

	/// <summary>
	/// Clears all captured entries.
	/// </summary>
	public void Clear()
	{
		lock (_lock)
		{
			_entries.Clear();
		}
	}

	/// <summary>
	/// Returns <see langword="true"/> if any entry was logged at <see cref="LogLevel.Error"/> or above.
	/// </summary>
	public bool HasLoggedError()
	{
		lock (_lock)
		{
			return _entries.Any(e => e.Level >= LogLevel.Error);
		}
	}

	/// <summary>
	/// Returns <see langword="true"/> if any entry at <paramref name="level"/> contains
	/// <paramref name="messageContains"/>.
	/// </summary>
	public bool HasLogged(LogLevel level, string messageContains)
	{
		lock (_lock)
		{
			return _entries.Any(e =>
				e.Level == level &&
				e.Message.Contains(messageContains, StringComparison.Ordinal));
		}
	}
}

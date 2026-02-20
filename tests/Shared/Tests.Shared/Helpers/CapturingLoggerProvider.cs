// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Tests.Shared.Helpers;

/// <summary>
/// An <see cref="ILoggerProvider"/> that captures all log entries across categories
/// for test assertions. Thread-safe.
/// </summary>
public sealed class CapturingLoggerProvider : ILoggerProvider
{
	private readonly object _lock = new();
	private readonly List<CapturedLogEntry> _entries = [];

	/// <summary>
	/// Gets a snapshot of all captured log entries across all loggers.
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
	public ILogger CreateLogger(string categoryName) =>
		new CapturingLogger(categoryName, _lock, _entries);

	/// <inheritdoc/>
	public void Dispose()
	{
		// Nothing to dispose.
	}

	private sealed class CapturingLogger(string categoryName, object lockObj, List<CapturedLogEntry> entries) : ILogger
	{
		public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

		public bool IsEnabled(LogLevel logLevel) => true;

		public void Log<TState>(
			LogLevel logLevel,
			EventId eventId,
			TState state,
			Exception? exception,
			Func<TState, Exception?, string> formatter)
		{
			ArgumentNullException.ThrowIfNull(formatter);

			lock (lockObj)
			{
				entries.Add(new CapturedLogEntry(
					logLevel,
					eventId,
					formatter(state, exception),
					exception,
					categoryName));
			}
		}
	}
}

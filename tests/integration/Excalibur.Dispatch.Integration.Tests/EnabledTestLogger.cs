// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Integration.Tests;

internal static class EnabledTestLogger
{
	public static ILogger<T> Create<T>() => new NoOpLogger<T>();

	private sealed class NoOpLogger<T> : ILogger<T>
	{
		public IDisposable BeginScope<TState>(TState state) where TState : notnull => NoOpScope.Instance;

		public bool IsEnabled(LogLevel logLevel) => true;

		public void Log<TState>(
			LogLevel logLevel,
			EventId eventId,
			TState state,
			Exception? exception,
			Func<TState, Exception?, string> formatter)
		{
		}
	}

	private sealed class NoOpScope : IDisposable
	{
		internal static readonly NoOpScope Instance = new();

		public void Dispose()
		{
		}
	}
}

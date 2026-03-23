// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Hosting.AspNetCore;
using Excalibur.Dispatch.Options.Configuration;

using Microsoft.AspNetCore.Builder;

namespace Excalibur.Dispatch.Hosting.AspNetCore.Tests;

/// <summary>
/// Unit tests for <see cref="DispatchStartupFilter"/>.
/// </summary>
/// <remarks>
/// Sprint 698 T.3 (t2hyt): Tests for the internal startup filter that validates keyed service
/// registrations, detects collisions, and validates observability configuration.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class DispatchStartupFilterShould
{
	#region Configure Tests

	[Fact]
	public void ReturnNextAction()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddSingleton(A.Fake<IDispatcher>());
		services.AddSingleton(A.Fake<IDispatchMiddleware>());
		services.AddSingleton<System.Diagnostics.Metrics.IMeterFactory>(new TestMeterFactory());
		var sp = services.BuildServiceProvider();
		var filter = new DispatchStartupFilter(sp, NullLogger<DispatchStartupFilter>.Instance);
		var nextCalled = false;
		Action<IApplicationBuilder> next = _ => nextCalled = true;

		// Act
		var result = filter.Configure(next);

		// Assert
		result.ShouldNotBeNull();
		result(A.Fake<IApplicationBuilder>());
		nextCalled.ShouldBeTrue();
	}

	#endregion

	#region Missing IDispatcher Tests

	[Fact]
	public void LogErrorWhenIDispatcherMissing()
	{
		// Arrange
		var services = new ServiceCollection();
		// No IDispatcher registered
		var sp = services.BuildServiceProvider();
		var logger = new FakeLogger<DispatchStartupFilter>();
		var filter = new DispatchStartupFilter(sp, logger);

		// Act
		filter.Configure(_ => { });

		// Assert - should log error about missing IDispatcher
		logger.LogEntries.ShouldContain(e =>
			e.LogLevel == LogLevel.Error &&
			e.Message.Contains("IDispatcher"));
	}

	#endregion

	#region Empty Pipeline Tests

	[Fact]
	public void LogWarningWhenPipelineEmpty()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddSingleton(A.Fake<IDispatcher>());
		// No middleware registered
		services.AddSingleton<System.Diagnostics.Metrics.IMeterFactory>(new TestMeterFactory());
		var sp = services.BuildServiceProvider();
		var logger = new FakeLogger<DispatchStartupFilter>();
		var filter = new DispatchStartupFilter(sp, logger);

		// Act
		filter.Configure(_ => { });

		// Assert
		logger.LogEntries.ShouldContain(e =>
			e.LogLevel == LogLevel.Warning &&
			e.Message.Contains("pipeline"));
	}

	#endregion

	#region Outbox Without Store Tests

	[Fact]
	public void LogWarningWhenOutboxEnabledWithoutStore()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddSingleton(A.Fake<IDispatcher>());
		services.AddSingleton(A.Fake<IDispatchMiddleware>());
		services.AddSingleton<System.Diagnostics.Metrics.IMeterFactory>(new TestMeterFactory());
		services.Configure<OutboxConfigurationOptions>(o => o.Enabled = true);
		// No IOutboxStore registered
		var sp = services.BuildServiceProvider();
		var logger = new FakeLogger<DispatchStartupFilter>();
		var filter = new DispatchStartupFilter(sp, logger);

		// Act
		filter.Configure(_ => { });

		// Assert
		logger.LogEntries.ShouldContain(e =>
			e.LogLevel == LogLevel.Warning &&
			e.Message.Contains("IOutboxStore"));
	}

	#endregion

	#region Observability Tests (Event 2605)

	[Fact]
	public void LogInfoWhenNoIMeterFactoryRegistered()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddSingleton(A.Fake<IDispatcher>());
		services.AddSingleton(A.Fake<IDispatchMiddleware>());
		// No IMeterFactory registered
		var sp = services.BuildServiceProvider();
		var logger = new FakeLogger<DispatchStartupFilter>();
		var filter = new DispatchStartupFilter(sp, logger);

		// Act
		filter.Configure(_ => { });

		// Assert
		logger.LogEntries.ShouldContain(e =>
			e.LogLevel == LogLevel.Information &&
			e.Message.Contains("IMeterFactory"));
	}

	[Fact]
	public void NotLogObservabilityWarningWhenIMeterFactoryRegistered()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddSingleton(A.Fake<IDispatcher>());
		services.AddSingleton(A.Fake<IDispatchMiddleware>());
		services.AddSingleton<System.Diagnostics.Metrics.IMeterFactory>(new TestMeterFactory());
		var sp = services.BuildServiceProvider();
		var logger = new FakeLogger<DispatchStartupFilter>();
		var filter = new DispatchStartupFilter(sp, logger);

		// Act
		filter.Configure(_ => { });

		// Assert
		logger.LogEntries.ShouldNotContain(e =>
			e.Message.Contains("IMeterFactory"));
	}

	#endregion

	#region Keyed Service Collision Detection (Event 2604)

	[Fact]
	public void LogDebugWhenKeyedDefaultResolves()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddSingleton(A.Fake<IDispatcher>());
		services.AddSingleton(A.Fake<IDispatchMiddleware>());
		services.AddSingleton<System.Diagnostics.Metrics.IMeterFactory>(new TestMeterFactory());
		services.AddKeyedSingleton<IOutboxStore>("default", A.Fake<IOutboxStore>());
		services.AddKeyedSingleton<IInboxStore>("default", A.Fake<IInboxStore>());
		var sp = services.BuildServiceProvider();
		var logger = new FakeLogger<DispatchStartupFilter>();
		var filter = new DispatchStartupFilter(sp, logger);

		// Act
		filter.Configure(_ => { });

		// Assert
		logger.LogEntries.ShouldContain(e =>
			e.LogLevel == LogLevel.Debug &&
			e.Message.Contains("IOutboxStore") &&
			e.Message.Contains("default"));
	}

	[Fact]
	public void NotLogCollisionWhenNoKeyedServicesRegistered()
	{
		// Arrange - no keyed outbox/inbox registered at all
		var services = new ServiceCollection();
		services.AddSingleton(A.Fake<IDispatcher>());
		services.AddSingleton(A.Fake<IDispatchMiddleware>());
		services.AddSingleton<System.Diagnostics.Metrics.IMeterFactory>(new TestMeterFactory());
		var sp = services.BuildServiceProvider();
		var logger = new FakeLogger<DispatchStartupFilter>();
		var filter = new DispatchStartupFilter(sp, logger);

		// Act
		filter.Configure(_ => { });

		// Assert - no collision warning
		logger.LogEntries.ShouldNotContain(e =>
			e.LogLevel == LogLevel.Warning &&
			e.Message.Contains("collision"));
	}

	#endregion

	#region Helpers

	/// <summary>
	/// Simple fake logger that captures log entries for assertion.
	/// </summary>
	private sealed class FakeLogger<T> : ILogger<T>
	{
		public List<LogEntry> LogEntries { get; } = [];

		public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

		public bool IsEnabled(LogLevel logLevel) => true;

		public void Log<TState>(
			LogLevel logLevel,
			EventId eventId,
			TState state,
			Exception? exception,
			Func<TState, Exception?, string> formatter)
		{
			LogEntries.Add(new LogEntry(logLevel, eventId, formatter(state, exception)));
		}
	}

	internal sealed record LogEntry(LogLevel LogLevel, EventId EventId, string Message);

	/// <summary>
	/// Minimal IMeterFactory for tests.
	/// </summary>
	private sealed class TestMeterFactory : System.Diagnostics.Metrics.IMeterFactory
	{
		private readonly List<System.Diagnostics.Metrics.Meter> _meters = [];

		public System.Diagnostics.Metrics.Meter Create(System.Diagnostics.Metrics.MeterOptions options)
		{
			var meter = new System.Diagnostics.Metrics.Meter(options.Name, options.Version);
			_meters.Add(meter);
			return meter;
		}

		public void Dispose()
		{
			foreach (var meter in _meters)
			{
				meter.Dispose();
			}
		}
	}

	#endregion
}

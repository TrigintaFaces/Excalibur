// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Dispatch.Hosting.AspNetCore;
using Excalibur.Dispatch.Options.Configuration;

using Microsoft.AspNetCore.Builder;

namespace Excalibur.Dispatch.Hosting.AspNetCore.Tests;

/// <summary>
/// Unit tests for <see cref="DispatchStartupFilter"/>.
/// </summary>
/// <remarks>
/// Sprint 698 T.3 (t2hyt): Tests for the internal startup filter. Required services (dispatcher, and an
/// outbox store when the outbox is enabled) throw; advisory configuration is logged. Each throwing arm is
/// paired with a liveness arm so a guard that rejected everything could not pass.
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
	public void ThrowWhenIDispatcherMissing()
	{
		// Arrange
		var services = new ServiceCollection();
		// No IDispatcher registered
		var sp = services.BuildServiceProvider();
		var logger = new FakeLogger<DispatchStartupFilter>();
		var filter = new DispatchStartupFilter(sp, logger);

		// Act + Assert - a host with no dispatcher must not start
		var ex = Should.Throw<InvalidOperationException>(() => filter.Configure(_ => { }));
		ex.Message.ShouldContain("IDispatcher");
		ex.Message.ShouldContain("AddDispatch()");
	}

	[Fact]
	public void NotThrowWhenIDispatcherRegistered()
	{
		// Liveness arm: a valid configuration must still start. Without this, a guard that rejected
		// every configuration would satisfy the safety arm above.
		var services = new ServiceCollection();
		services.AddSingleton(A.Fake<IDispatcher>());
		services.AddSingleton(A.Fake<IDispatchMiddleware>());
		services.AddSingleton<System.Diagnostics.Metrics.IMeterFactory>(new TestMeterFactory());
		var sp = services.BuildServiceProvider();
		var filter = new DispatchStartupFilter(sp, new FakeLogger<DispatchStartupFilter>());

		Should.NotThrow(() => filter.Configure(_ => { }));
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
	public void ThrowWhenOutboxEnabledWithoutStore()
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

		// Act + Assert - an enabled outbox with no store loses every staged message silently, so the
		// host must refuse to start rather than accept writes it cannot persist.
		var ex = Should.Throw<InvalidOperationException>(() => filter.Configure(_ => { }));
		ex.Message.ShouldContain("IOutboxStore");
	}

	[Fact]
	public void NotThrowWhenOutboxEnabledWithStore()
	{
		// Liveness arm: a correctly configured outbox must still start.
		var services = new ServiceCollection();
		services.AddSingleton(A.Fake<IDispatcher>());
		services.AddSingleton(A.Fake<IDispatchMiddleware>());
		services.AddSingleton<System.Diagnostics.Metrics.IMeterFactory>(new TestMeterFactory());
		services.Configure<OutboxConfigurationOptions>(o => o.Enabled = true);
		services.AddKeyedSingleton<IOutboxStore>("default", A.Fake<IOutboxStore>());
		var sp = services.BuildServiceProvider();
		var filter = new DispatchStartupFilter(sp, new FakeLogger<DispatchStartupFilter>());

		Should.NotThrow(() => filter.Configure(_ => { }));
	}

	[Fact]
	public void NotThrowWhenOutboxDisabledWithoutStore()
	{
		// Liveness arm: the outbox is optional. A host that never enabled it must not be blocked by a
		// missing outbox store.
		var services = new ServiceCollection();
		services.AddSingleton(A.Fake<IDispatcher>());
		services.AddSingleton(A.Fake<IDispatchMiddleware>());
		services.AddSingleton<System.Diagnostics.Metrics.IMeterFactory>(new TestMeterFactory());
		services.Configure<OutboxConfigurationOptions>(o => o.Enabled = false);
		var sp = services.BuildServiceProvider();
		var filter = new DispatchStartupFilter(sp, new FakeLogger<DispatchStartupFilter>());

		Should.NotThrow(() => filter.Configure(_ => { }));
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

	#region Keyed Default Resolution

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
	public void NotThrowWhenNoKeyedServicesRegistered()
	{
		// Arrange - no keyed outbox/inbox registered at all. Neither is required, so an absent keyed
		// registration is a legal configuration and must start.
		var services = new ServiceCollection();
		services.AddSingleton(A.Fake<IDispatcher>());
		services.AddSingleton(A.Fake<IDispatchMiddleware>());
		services.AddSingleton<System.Diagnostics.Metrics.IMeterFactory>(new TestMeterFactory());
		var sp = services.BuildServiceProvider();
		var filter = new DispatchStartupFilter(sp, new FakeLogger<DispatchStartupFilter>());

		// Act + Assert
		Should.NotThrow(() => filter.Configure(_ => { }));
	}

	[Fact]
	public void ThrowWhenKeyedDefaultAliasCannotResolve()
	{
		// A "default" alias delegating to a provider key nothing registered would fail at first use.
		var services = new ServiceCollection();
		services.AddSingleton(A.Fake<IDispatcher>());
		services.AddSingleton(A.Fake<IDispatchMiddleware>());
		services.AddSingleton<System.Diagnostics.Metrics.IMeterFactory>(new TestMeterFactory());
		services.AddKeyedSingleton<IOutboxStore>(
			"default",
			(sp, _) => sp.GetRequiredKeyedService<IOutboxStore>("never-registered"));
		var sp = services.BuildServiceProvider();
		var filter = new DispatchStartupFilter(sp, new FakeLogger<DispatchStartupFilter>());

		var ex = Should.Throw<InvalidOperationException>(() => filter.Configure(_ => { }));
		ex.Message.ShouldContain("IOutboxStore");
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

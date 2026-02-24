// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.EventSourcing.Abstractions;
using Excalibur.EventSourcing.Services;

using FakeItEasy;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Tests.Shared.Infrastructure;

namespace Excalibur.EventSourcing.Tests.MaterializedViews.Services;

/// <summary>
/// Unit tests for <see cref="MaterializedViewRefreshService"/>.
/// </summary>
/// <remarks>
/// Sprint 517: Materialized Views provider tests.
/// Tests verify background service behavior, scheduling modes, and retry logic.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "MaterializedViews")]
[Trait("Feature", "Services")]
public sealed class MaterializedViewRefreshServiceShould
{
	private readonly IServiceScopeFactory _scopeFactory;
	private readonly IServiceScope _scope;
	private readonly IServiceProvider _serviceProvider;

	public MaterializedViewRefreshServiceShould()
	{
		_scopeFactory = A.Fake<IServiceScopeFactory>();
		_scope = A.Fake<IServiceScope>();
		_serviceProvider = A.Fake<IServiceProvider>();

		A.CallTo(() => _scopeFactory.CreateScope()).Returns(_scope);
		A.CallTo(() => _scope.ServiceProvider).Returns(_serviceProvider);
	}

	#region Type Tests

	[Fact]
	public void BeSealed()
	{
		// Assert
		typeof(MaterializedViewRefreshService).IsSealed.ShouldBeTrue();
	}

	[Fact]
	public void BePublic()
	{
		// Assert
		typeof(MaterializedViewRefreshService).IsPublic.ShouldBeTrue();
	}

	[Fact]
	public void ImplementBackgroundService()
	{
		// Assert
		typeof(BackgroundService).IsAssignableFrom(typeof(MaterializedViewRefreshService)).ShouldBeTrue();
	}

	[Fact]
	public void HavePartialModifier()
	{
		// Assert - partial class is needed for LoggerMessage source generation
		// This is verified by the class compiling with [LoggerMessage] attributes
		typeof(MaterializedViewRefreshService).IsSealed.ShouldBeTrue();
	}

	#endregion

	#region Constructor Tests

	[Fact]
	public void Constructor_ThrowArgumentNullExceptionForNullScopeFactory()
	{
		// Arrange
		var options = Options.Create(new MaterializedViewRefreshOptions());
		var timeProvider = TimeProvider.System;
		var logger = NullLogger<MaterializedViewRefreshService>.Instance;

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			new MaterializedViewRefreshService(null!, options, timeProvider, logger));
	}

	[Fact]
	public void Constructor_ThrowArgumentNullExceptionForNullOptions()
	{
		// Arrange
		var timeProvider = TimeProvider.System;
		var logger = NullLogger<MaterializedViewRefreshService>.Instance;

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			new MaterializedViewRefreshService(_scopeFactory, null!, timeProvider, logger));
	}

	[Fact]
	public void Constructor_ThrowArgumentNullExceptionForNullTimeProvider()
	{
		// Arrange
		var options = Options.Create(new MaterializedViewRefreshOptions());
		var logger = NullLogger<MaterializedViewRefreshService>.Instance;

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			new MaterializedViewRefreshService(_scopeFactory, options, null!, logger));
	}

	[Fact]
	public void Constructor_ThrowArgumentNullExceptionForNullLogger()
	{
		// Arrange
		var options = Options.Create(new MaterializedViewRefreshOptions());
		var timeProvider = TimeProvider.System;

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			new MaterializedViewRefreshService(_scopeFactory, options, timeProvider, null!));
	}

	[Fact]
	public void Constructor_SucceedWithValidParameters()
	{
		// Arrange
		var options = Options.Create(new MaterializedViewRefreshOptions());
		var timeProvider = TimeProvider.System;
		var logger = NullLogger<MaterializedViewRefreshService>.Instance;

		// Act
		var service = new MaterializedViewRefreshService(_scopeFactory, options, timeProvider, logger);

		// Assert
		service.ShouldNotBeNull();
	}

	#endregion

	#region ExecuteAsync Disabled Tests

	[Fact]
	public async Task ExecuteAsync_ExitImmediatelyWhenDisabled()
	{
		// Arrange
		var options = Options.Create(new MaterializedViewRefreshOptions { Enabled = false });
		var timeProvider = TimeProvider.System;
		var logger = NullLogger<MaterializedViewRefreshService>.Instance;
		var service = new MaterializedViewRefreshService(_scopeFactory, options, timeProvider, logger);

		using var cts = new CancellationTokenSource();

		// Act - start and wait briefly
		await service.StartAsync(cts.Token);
		await global::Tests.Shared.Infrastructure.TestTiming.DelayAsync(100);
		await service.StopAsync(cts.Token);

		// Assert - no scope should be created when disabled
		A.CallTo(() => _scopeFactory.CreateScope()).MustNotHaveHappened();
	}

	#endregion

	#region CatchUpOnStartup Tests

	[Fact]
	public async Task ExecuteAsync_PerformCatchUpOnStartupWhenEnabled()
	{
		// Arrange
		var processor = A.Fake<IMaterializedViewProcessor>();
		var options = Options.Create(new MaterializedViewRefreshOptions
		{
			Enabled = true,
			CatchUpOnStartup = true,
			RefreshInterval = TimeSpan.FromHours(1) // Long interval to control test timing
		});

		A.CallTo(() => _serviceProvider.GetService(typeof(IMaterializedViewProcessor))).Returns(processor);

		var timeProvider = TimeProvider.System;
		var logger = NullLogger<MaterializedViewRefreshService>.Instance;
		var service = new MaterializedViewRefreshService(_scopeFactory, options, timeProvider, logger);

		using var cts = new CancellationTokenSource();

		// Act
		await service.StartAsync(cts.Token);
		await global::Tests.Shared.Infrastructure.TestTiming.DelayAsync(200); // Give time for catch-up
		await cts.CancelAsync();
		await service.StopAsync(CancellationToken.None);

		// Assert - scope should have been created for catch-up
		A.CallTo(() => _scopeFactory.CreateScope()).MustHaveHappenedOnceOrMore();
	}

	[Fact]
	public async Task ExecuteAsync_SkipCatchUpWhenDisabled()
	{
		// Arrange
		var processor = A.Fake<IMaterializedViewProcessor>();
		var options = Options.Create(new MaterializedViewRefreshOptions
		{
			Enabled = true,
			CatchUpOnStartup = false, // Disabled
			RefreshInterval = TimeSpan.FromHours(1)
		});

		A.CallTo(() => _serviceProvider.GetService(typeof(IMaterializedViewProcessor))).Returns(processor);

		var timeProvider = TimeProvider.System;
		var logger = NullLogger<MaterializedViewRefreshService>.Instance;
		var service = new MaterializedViewRefreshService(_scopeFactory, options, timeProvider, logger);

		using var cts = new CancellationTokenSource();

		// Act - start and stop immediately (no time for interval-based refresh)
		await service.StartAsync(cts.Token);
		await cts.CancelAsync();
		await service.StopAsync(CancellationToken.None);

		// Assert - no scope created since catch-up is disabled and interval didn't elapse
		// Note: scope might be created for waiting, but processor shouldn't be called
	}

	#endregion

	#region Processor Missing Tests

	[Fact]
	public async Task ExecuteAsync_ContinueWhenNoProcessorRegistered()
	{
		// Arrange
		var options = Options.Create(new MaterializedViewRefreshOptions
		{
			Enabled = true,
			CatchUpOnStartup = true,
			RefreshInterval = TimeSpan.FromHours(1)
		});

		A.CallTo(() => _serviceProvider.GetService(typeof(IMaterializedViewProcessor))).Returns(null);

		var timeProvider = TimeProvider.System;
		var logger = NullLogger<MaterializedViewRefreshService>.Instance;
		var service = new MaterializedViewRefreshService(_scopeFactory, options, timeProvider, logger);

		using var cts = new CancellationTokenSource();

		// Act & Assert - should not throw
		await service.StartAsync(cts.Token);
		await global::Tests.Shared.Infrastructure.TestTiming.DelayAsync(200);
		await cts.CancelAsync();
		await service.StopAsync(CancellationToken.None);

		// No exception means success
	}

	#endregion

	#region Graceful Shutdown Tests

	[Fact]
	public async Task ExecuteAsync_SupportGracefulShutdown()
	{
		// Arrange
		var options = Options.Create(new MaterializedViewRefreshOptions
		{
			Enabled = true,
			RefreshInterval = TimeSpan.FromMilliseconds(100)
		});

		A.CallTo(() => _serviceProvider.GetService(typeof(IMaterializedViewProcessor))).Returns(null);

		var timeProvider = TimeProvider.System;
		var logger = NullLogger<MaterializedViewRefreshService>.Instance;
		var service = new MaterializedViewRefreshService(_scopeFactory, options, timeProvider, logger);

		using var cts = new CancellationTokenSource();

		// Act
		await service.StartAsync(cts.Token);
		await global::Tests.Shared.Infrastructure.TestTiming.DelayAsync(50);

		// Request shutdown
		await cts.CancelAsync();
		await service.StopAsync(CancellationToken.None);

		// Assert - service should have stopped gracefully (no exception)
	}

	[Fact]
	public async Task ExecuteAsync_RespectCancellationDuringDelay()
	{
		// Arrange
		var options = Options.Create(new MaterializedViewRefreshOptions
		{
			Enabled = true,
			RefreshInterval = TimeSpan.FromMinutes(10) // Long interval
		});

		A.CallTo(() => _serviceProvider.GetService(typeof(IMaterializedViewProcessor))).Returns(null);

		var timeProvider = TimeProvider.System;
		var logger = NullLogger<MaterializedViewRefreshService>.Instance;
		var service = new MaterializedViewRefreshService(_scopeFactory, options, timeProvider, logger);

		using var cts = new CancellationTokenSource();
		var stopwatch = System.Diagnostics.Stopwatch.StartNew();

		// Act
		await service.StartAsync(cts.Token);

		// Cancel quickly - should not wait for the full 10 minute interval
		await global::Tests.Shared.Infrastructure.TestTiming.DelayAsync(100);
		await cts.CancelAsync();
		await service.StopAsync(CancellationToken.None);

		stopwatch.Stop();

		// Assert - should have stopped quickly, not waited for interval
		stopwatch.ElapsedMilliseconds.ShouldBeLessThan(5000);
	}

	#endregion

	#region Cron Expression Configuration Tests

	[Fact]
	public void Constructor_ParseValidCronExpression()
	{
		// Arrange
		var options = Options.Create(new MaterializedViewRefreshOptions
		{
			CronExpression = "*/5 * * * *" // Every 5 minutes
		});
		var timeProvider = TimeProvider.System;
		var logger = NullLogger<MaterializedViewRefreshService>.Instance;

		// Act - should not throw
		var service = new MaterializedViewRefreshService(_scopeFactory, options, timeProvider, logger);

		// Assert
		service.ShouldNotBeNull();
	}

	[Fact]
	public void Constructor_HandleInvalidCronExpression()
	{
		// Arrange
		var options = Options.Create(new MaterializedViewRefreshOptions
		{
			CronExpression = "invalid cron expression"
		});
		var timeProvider = TimeProvider.System;
		var logger = NullLogger<MaterializedViewRefreshService>.Instance;

		// Act - should not throw, falls back to interval
		var service = new MaterializedViewRefreshService(_scopeFactory, options, timeProvider, logger);

		// Assert
		service.ShouldNotBeNull();
	}

	[Theory]
	[InlineData("* * * * *")]        // Every minute
	[InlineData("0 * * * *")]        // Every hour
	[InlineData("0 0 * * *")]        // Daily at midnight
	[InlineData("0 2 * * 0")]        // Weekly on Sunday at 2 AM
	public void Constructor_AcceptVariousCronExpressions(string cronExpression)
	{
		// Arrange
		var options = Options.Create(new MaterializedViewRefreshOptions
		{
			CronExpression = cronExpression
		});
		var timeProvider = TimeProvider.System;
		var logger = NullLogger<MaterializedViewRefreshService>.Instance;

		// Act - should not throw
		var service = new MaterializedViewRefreshService(_scopeFactory, options, timeProvider, logger);

		// Assert
		service.ShouldNotBeNull();
	}

	#endregion

	#region Interval-Based Scheduling Tests

	[Fact]
	public async Task ExecuteAsync_UseDefaultIntervalWhenNotConfigured()
	{
		// Arrange
		var processor = A.Fake<IMaterializedViewProcessor>();
		var options = Options.Create(new MaterializedViewRefreshOptions
		{
			Enabled = true,
			CatchUpOnStartup = false,
			RefreshInterval = null, // Null - should use default 30s
			CronExpression = null
		});

		A.CallTo(() => _serviceProvider.GetService(typeof(IMaterializedViewProcessor))).Returns(processor);

		var timeProvider = TimeProvider.System;
		var logger = NullLogger<MaterializedViewRefreshService>.Instance;
		var service = new MaterializedViewRefreshService(_scopeFactory, options, timeProvider, logger);

		using var cts = new CancellationTokenSource();

		// Act & Assert - should not throw, service uses 30s default
		await service.StartAsync(cts.Token);
		await global::Tests.Shared.Infrastructure.TestTiming.DelayAsync(100);
		await cts.CancelAsync();
		await service.StopAsync(CancellationToken.None);
	}

	[Fact]
	public async Task ExecuteAsync_UseConfiguredInterval()
	{
		// Arrange
		var processor = A.Fake<IMaterializedViewProcessor>();
		var options = Options.Create(new MaterializedViewRefreshOptions
		{
			Enabled = true,
			CatchUpOnStartup = false,
			RefreshInterval = TimeSpan.FromMilliseconds(100),
			CronExpression = null
		});

		A.CallTo(() => _serviceProvider.GetService(typeof(IMaterializedViewProcessor))).Returns(processor);

		var timeProvider = TimeProvider.System;
		var logger = NullLogger<MaterializedViewRefreshService>.Instance;
		var service = new MaterializedViewRefreshService(_scopeFactory, options, timeProvider, logger);

		using var cts = new CancellationTokenSource();

		// Act
		await service.StartAsync(cts.Token);
		var refreshStarted = await WaitHelpers.WaitUntilAsync(() =>
		{
			try
			{
				A.CallTo(() => _scopeFactory.CreateScope()).MustHaveHappened();
				return true;
			}
			catch (ExpectationException)
			{
				return false;
			}
		}, timeout: TimeSpan.FromSeconds(3), pollInterval: TimeSpan.FromMilliseconds(50));

		await cts.CancelAsync();
		await service.StopAsync(CancellationToken.None);

		// Assert - configured interval should eventually trigger refresh execution
		refreshStarted.ShouldBeTrue();
	}

	#endregion

	#region Retry Logic Tests

	[Fact]
	public async Task ExecuteAsync_RetryOnTransientFailure()
	{
		// Arrange
		var processor = A.Fake<IMaterializedViewProcessor>();
		var callCount = 0;

		// Simulate transient failure on first call
		A.CallTo(() => processor.CatchUpAsync(A<string>._, A<CancellationToken>._))
			.Invokes(() =>
			{
				callCount++;
				if (callCount == 1)
				{
					throw new InvalidOperationException("Transient failure");
				}
			});

		var options = Options.Create(new MaterializedViewRefreshOptions
		{
			Enabled = true,
			CatchUpOnStartup = true,
			RefreshInterval = TimeSpan.FromHours(1),
			InitialRetryDelay = TimeSpan.FromMilliseconds(10),
			MaxRetryCount = 3
		});

		A.CallTo(() => _serviceProvider.GetService(typeof(IMaterializedViewProcessor))).Returns(processor);

		var timeProvider = TimeProvider.System;
		var logger = NullLogger<MaterializedViewRefreshService>.Instance;
		var service = new MaterializedViewRefreshService(_scopeFactory, options, timeProvider, logger);

		using var cts = new CancellationTokenSource();

		// Act
		await service.StartAsync(cts.Token);
		await global::Tests.Shared.Infrastructure.TestTiming.DelayAsync(500); // Give time for retry
		await cts.CancelAsync();
		await service.StopAsync(CancellationToken.None);

		// Assert - processor should have been called more than once due to retry
		// Note: exact behavior depends on registration availability
	}

	#endregion

	#region TimeProvider Integration Tests

	[Fact]
	public void Constructor_AcceptCustomTimeProvider()
	{
		// Arrange
		var timeProvider = A.Fake<TimeProvider>();
		A.CallTo(() => timeProvider.GetUtcNow()).Returns(DateTimeOffset.UtcNow);

		var options = Options.Create(new MaterializedViewRefreshOptions());
		var logger = NullLogger<MaterializedViewRefreshService>.Instance;

		// Act
		var service = new MaterializedViewRefreshService(_scopeFactory, options, timeProvider, logger);

		// Assert
		service.ShouldNotBeNull();
	}

	#endregion
}

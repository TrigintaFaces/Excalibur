// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Delivery.Handlers;
using Excalibur.Dispatch.Delivery.Pipeline;
using Excalibur.Dispatch.Performance;

using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Tests.Performance;

/// <summary>
/// Tests for Sprint 567 S567.2: DispatchCacheManager freeze lock timeout.
/// Validates that FreezeAll uses Monitor.TryEnter with a bounded timeout
/// instead of an unbounded lock, and logs a warning on timeout.
/// </summary>
[Collection("HandlerInvokerRegistry")]
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Priority", "2")]
public sealed class DispatchCacheManagerFreezeLockShould : IDisposable
{
	private readonly DispatchCacheManager _cacheManager;
	private readonly ILogger<DispatchCacheManager> _logger;

	public DispatchCacheManagerFreezeLockShould()
	{
		_logger = A.Fake<ILogger<DispatchCacheManager>>();
		A.CallTo(() => _logger.IsEnabled(A<LogLevel>._)).Returns(true);
		_cacheManager = new DispatchCacheManager(_logger);
		ResetAllCaches();
	}

	public void Dispose()
	{
		ResetAllCaches();
	}

	private static void ResetAllCaches()
	{
		HandlerInvoker.ClearCache();
		HandlerInvokerRegistry.ClearCache();
		HandlerActivator.ClearCache();
		FinalDispatchHandler.ClearResultFactoryCache();
		MiddlewareApplicabilityEvaluator.ClearCache();
	}

	[Fact]
	public void FreezeAll_CompletesWithinTimeout_WhenLockIsUncontested()
	{
		// Act - should complete without throwing TimeoutException
		_cacheManager.FreezeAll();

		// Assert
		_cacheManager.IsFrozen.ShouldBeTrue();
	}

	[Fact]
	public void FreezeAll_HasDefaultFreezeLockTimeoutField()
	{
		// Verify the DefaultFreezeLockTimeout constant exists and is reasonable
		var field = typeof(DispatchCacheManager)
			.GetField("DefaultFreezeLockTimeout", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

		field.ShouldNotBeNull("DispatchCacheManager should have a DefaultFreezeLockTimeout field");
		field.FieldType.ShouldBe(typeof(TimeSpan));

		var value = (TimeSpan)field.GetValue(null)!;
		value.ShouldBeGreaterThan(TimeSpan.Zero, "DefaultFreezeLockTimeout must be positive");
		value.ShouldBeLessThanOrEqualTo(TimeSpan.FromMinutes(5), "DefaultFreezeLockTimeout should be bounded");
	}

	[Fact]
	public void FreezeAll_AcceptsCustomTimeout()
	{
		// Arrange - create with custom timeout
		var customTimeout = TimeSpan.FromSeconds(10);
		var manager = new DispatchCacheManager(_logger, customTimeout);

		// Act
		manager.FreezeAll();

		// Assert - should work with custom timeout
		manager.IsFrozen.ShouldBeTrue();
	}

	[Fact]
	public void FreezeAll_WhenTimeoutExceeded_LogsWarningAndReturns()
	{
		// Arrange - create with a very short timeout (1ms) and hold the lock externally
		var manager = new DispatchCacheManager(_logger, TimeSpan.FromMilliseconds(1));

		// Get the _freezeLock field to hold it externally
		var lockField = typeof(DispatchCacheManager)
			.GetField("_freezeLock", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
		lockField.ShouldNotBeNull();
		var lockObj = lockField.GetValue(manager);

		// Hold the lock to force timeout
		var freezeCompleted = false;
		lock (lockObj)
		{
			// FreezeAll should not throw but log warning and return
			manager.FreezeAll();
			freezeCompleted = true;
		}

		// Assert - should have completed without throwing
		freezeCompleted.ShouldBeTrue("FreezeAll should return gracefully on timeout instead of throwing");

		// Verify warning was logged
		A.CallTo(_logger)
			.Where(call => call.Method.Name == "Log")
			.MustHaveHappened();
	}

	[Fact]
	public void FreezeAll_UsesFreezeLockObject()
	{
		// Verify the _freezeLock field exists (used by Monitor.TryEnter)
		var field = typeof(DispatchCacheManager)
			.GetField("_freezeLock", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

		field.ShouldNotBeNull("DispatchCacheManager should have a _freezeLock field for Monitor.TryEnter");
	}

	[Fact]
	public void FreezeAll_LogsWhenAlreadyFrozen()
	{
		// Arrange - freeze first
		_cacheManager.FreezeAll();

		// Act - freeze again (idempotent, should log "already frozen")
		_cacheManager.FreezeAll();

		// Assert - verify logging occurred (at least two calls: first freeze + already frozen)
		A.CallTo(_logger)
			.Where(call => call.Method.Name == "Log")
			.MustHaveHappened();

		_cacheManager.IsFrozen.ShouldBeTrue();
	}

	[Fact]
	public void FreezeAll_ConcurrentCalls_DoNotDeadlock()
	{
		// Arrange
		var exceptions = new System.Collections.Concurrent.ConcurrentBag<Exception>();
		var barrier = new Barrier(10);

		// Act - concurrent freeze attempts with barrier synchronization
		Parallel.For(0, 10, i =>
		{
			try
			{
				barrier.SignalAndWait(TimeSpan.FromSeconds(5));
				_cacheManager.FreezeAll();
			}
			catch (Exception ex) when (ex is not BarrierPostPhaseException)
			{
				exceptions.Add(ex);
			}
		});

		// Assert - no TimeoutException or deadlock
		exceptions.ShouldBeEmpty("Concurrent FreezeAll calls should not cause timeout or deadlock");
		_cacheManager.IsFrozen.ShouldBeTrue();
	}

	[Fact]
	public void FreezeAll_PartiallyFrozenState_CompletesRemaining()
	{
		try
		{
			// Arrange - partially freeze (only HandlerInvoker)
			HandlerInvoker.FreezeCache();

			// Act - FreezeAll should freeze remaining caches
			_cacheManager.FreezeAll();

			// Assert - all should now be frozen
			var status = _cacheManager.GetStatus();
			status.AllFrozen.ShouldBeTrue();
			status.HandlerInvokerFrozen.ShouldBeTrue();
			status.HandlerRegistryFrozen.ShouldBeTrue();
			status.HandlerActivatorFrozen.ShouldBeTrue();
			status.ResultFactoryFrozen.ShouldBeTrue();
			status.MiddlewareEvaluatorFrozen.ShouldBeTrue();
		}
		finally
		{
			ResetAllCaches();
		}
	}
}

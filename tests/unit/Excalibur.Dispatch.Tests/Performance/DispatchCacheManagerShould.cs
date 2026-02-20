// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Delivery.Handlers;
using Excalibur.Dispatch.Delivery.Pipeline;
using Excalibur.Dispatch.Performance;

using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Tests.Performance;

/// <summary>
/// Tests for <see cref="DispatchCacheManager"/> centralized cache coordination.
/// Validates freeze operations, status reporting, and idempotency.
/// </summary>
/// <remarks>
/// Sprint 455 - S455.5: Unit tests for auto-freeze functionality.
/// Tests the centralized cache manager that coordinates freezing across all Dispatch caches.
/// </remarks>
[Collection("HandlerInvokerRegistry")]
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Priority", "0")]
public sealed class DispatchCacheManagerShould : IDisposable
{
	private readonly DispatchCacheManager _cacheManager;
	private readonly ILogger<DispatchCacheManager> _logger;

	public DispatchCacheManagerShould()
	{
		_logger = A.Fake<ILogger<DispatchCacheManager>>();
		_cacheManager = new DispatchCacheManager(_logger);

		// Reset all caches to unfrozen state before each test
		ResetAllCaches();
	}

	public void Dispose()
	{
		// Clean up after tests
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

	#region Initial State Tests (2 tests)

	[Fact]
	public void IsFrozen_Initially_ReturnsFalse()
	{
		// Assert - caches start unfrozen
		_cacheManager.IsFrozen.ShouldBeFalse();
	}

	[Fact]
	public void GetStatus_Initially_ReturnsAllUnfrozen()
	{
		// Act
		var status = _cacheManager.GetStatus();

		// Assert - all caches start unfrozen
		status.HandlerInvokerFrozen.ShouldBeFalse();
		status.HandlerRegistryFrozen.ShouldBeFalse();
		status.HandlerActivatorFrozen.ShouldBeFalse();
		status.ResultFactoryFrozen.ShouldBeFalse();
		status.MiddlewareEvaluatorFrozen.ShouldBeFalse();
		status.FrozenAt.ShouldBeNull();
		status.AllFrozen.ShouldBeFalse();
	}

	#endregion

	#region FreezeAll Tests (4 tests)

	[Fact]
	public void FreezeAll_FreezesAllCaches()
	{
		// Act
		_cacheManager.FreezeAll();

		// Assert - all caches are frozen
		var status = _cacheManager.GetStatus();
		status.HandlerInvokerFrozen.ShouldBeTrue();
		status.HandlerRegistryFrozen.ShouldBeTrue();
		status.HandlerActivatorFrozen.ShouldBeTrue();
		status.ResultFactoryFrozen.ShouldBeTrue();
		status.MiddlewareEvaluatorFrozen.ShouldBeTrue();
		status.AllFrozen.ShouldBeTrue();
	}

	[Fact]
	public void FreezeAll_SetsIsFrozenToTrue()
	{
		// Act
		_cacheManager.FreezeAll();

		// Assert
		_cacheManager.IsFrozen.ShouldBeTrue();
	}

	[Fact]
	public void FreezeAll_SetsFrozenAtTimestamp()
	{
		// Arrange
		var before = DateTimeOffset.UtcNow;

		// Act
		_cacheManager.FreezeAll();

		// Assert
		var after = DateTimeOffset.UtcNow;
		var status = _cacheManager.GetStatus();
		_ = status.FrozenAt.ShouldNotBeNull();
		status.FrozenAt.Value.ShouldBeGreaterThanOrEqualTo(before);
		status.FrozenAt.Value.ShouldBeLessThanOrEqualTo(after);
	}

	[Fact]
	public void FreezeAll_IsIdempotent()
	{
		// Act - freeze twice
		_cacheManager.FreezeAll();
		var firstStatus = _cacheManager.GetStatus();
		var firstFrozenAt = firstStatus.FrozenAt;

		_cacheManager.FreezeAll();
		var secondStatus = _cacheManager.GetStatus();

		// Assert - timestamp unchanged (no re-freeze)
		secondStatus.FrozenAt.ShouldBe(firstFrozenAt);
		secondStatus.AllFrozen.ShouldBeTrue();
	}

	#endregion

	#region Thread Safety Tests (1 test)

	[Fact]
	public void FreezeAll_ThreadSafe_UnderConcurrentAccess()
	{
		// Arrange
		var exceptions = new List<Exception>();

		// Act - concurrent freeze attempts
		_ = Parallel.For(0, 100, i =>
		{
			try
			{
				_cacheManager.FreezeAll();
			}
			catch (Exception ex)
			{
				lock (exceptions)
				{
					exceptions.Add(ex);
				}
			}
		});

		// Assert - no exceptions, cache is frozen
		exceptions.ShouldBeEmpty();
		_cacheManager.IsFrozen.ShouldBeTrue();
	}

	#endregion

	#region CacheFreezeStatus Tests (3 tests)

	[Fact]
	public void CacheFreezeStatus_AllFrozen_TrueOnlyWhenAllCachesFrozen()
	{
		try
		{
			// Arrange - freeze only HandlerInvoker
			HandlerInvoker.FreezeCache();

			// Act
			var status = _cacheManager.GetStatus();

			// Assert - AllFrozen is false because not all are frozen
			status.HandlerInvokerFrozen.ShouldBeTrue();
			status.AllFrozen.ShouldBeFalse();
		}
		finally
		{
			// Cleanup - reset caches after partial freeze test
			ResetAllCaches();
		}
	}

	[Fact]
	public void CacheFreezeStatus_Unfrozen_ReturnsDefaultUnfrozenState()
	{
		// Act
		var unfrozen = CacheFreezeStatus.Unfrozen;

		// Assert
		unfrozen.HandlerInvokerFrozen.ShouldBeFalse();
		unfrozen.HandlerRegistryFrozen.ShouldBeFalse();
		unfrozen.HandlerActivatorFrozen.ShouldBeFalse();
		unfrozen.ResultFactoryFrozen.ShouldBeFalse();
		unfrozen.MiddlewareEvaluatorFrozen.ShouldBeFalse();
		unfrozen.FrozenAt.ShouldBeNull();
		unfrozen.AllFrozen.ShouldBeFalse();
	}

	[Fact]
	public void GetStatus_ReflectsIndividualCacheState()
	{
		try
		{
			// Arrange - freeze caches one by one and verify status
			_cacheManager.GetStatus().HandlerInvokerFrozen.ShouldBeFalse();

			HandlerInvoker.FreezeCache();
			_cacheManager.GetStatus().HandlerInvokerFrozen.ShouldBeTrue();
			_cacheManager.GetStatus().HandlerRegistryFrozen.ShouldBeFalse();

			HandlerInvokerRegistry.FreezeCache();
			_cacheManager.GetStatus().HandlerRegistryFrozen.ShouldBeTrue();
			_cacheManager.GetStatus().HandlerActivatorFrozen.ShouldBeFalse();

			HandlerActivator.FreezeCache();
			_cacheManager.GetStatus().HandlerActivatorFrozen.ShouldBeTrue();
			_cacheManager.GetStatus().ResultFactoryFrozen.ShouldBeFalse();

			FinalDispatchHandler.FreezeResultFactoryCache();
			_cacheManager.GetStatus().ResultFactoryFrozen.ShouldBeTrue();
			_cacheManager.GetStatus().MiddlewareEvaluatorFrozen.ShouldBeFalse();

			MiddlewareApplicabilityEvaluator.FreezeCache();
			_cacheManager.GetStatus().MiddlewareEvaluatorFrozen.ShouldBeTrue();
			_cacheManager.GetStatus().AllFrozen.ShouldBeTrue();
		}
		finally
		{
			// Cleanup - reset all caches after partial freeze test
			ResetAllCaches();
		}
	}

	#endregion

	#region Logger Tests (1 test)

	[Fact]
	public void FreezeAll_LogsInformation()
	{
		// Arrange - enable logging so [LoggerMessage] source-gen emits Log calls
		_ = A.CallTo(() => _logger.IsEnabled(A<LogLevel>._)).Returns(true);

		// Act
		_cacheManager.FreezeAll();

		// Assert - verify logging occurred (at least Information level)
		_ = A.CallTo(_logger)
			.Where(call => call.Method.Name == "Log")
			.MustHaveHappened();
	}

	#endregion
}

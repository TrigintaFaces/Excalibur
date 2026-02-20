// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Caching;

namespace Excalibur.Dispatch.Caching.Tests;

/// <summary>
/// Unit tests for <see cref="CacheResilienceOptions"/> class.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Caching")]
[Trait("Feature", "Resilience")]
public sealed class CacheResilienceOptionsShould : UnitTestBase
{
	[Fact]
	public void HaveCircuitBreakerEnabled_ByDefault()
	{
		// Arrange & Act
		var options = new CacheResilienceOptions();

		// Assert
		options.CircuitBreaker.Enabled.ShouldBeTrue();
	}

	[Fact]
	public void HaveFailureThresholdOf5_ByDefault()
	{
		// Arrange & Act
		var options = new CacheResilienceOptions();

		// Assert
		options.CircuitBreaker.FailureThreshold.ShouldBe(5);
	}

	[Fact]
	public void HaveOneMinuteFailureWindow_ByDefault()
	{
		// Arrange & Act
		var options = new CacheResilienceOptions();

		// Assert
		options.CircuitBreaker.FailureWindow.ShouldBe(TimeSpan.FromMinutes(1));
	}

	[Fact]
	public void HaveThirtySecondOpenDuration_ByDefault()
	{
		// Arrange & Act
		var options = new CacheResilienceOptions();

		// Assert
		options.CircuitBreaker.OpenDuration.ShouldBe(TimeSpan.FromSeconds(30));
	}

	[Fact]
	public void HaveHalfOpenTestLimitOf3_ByDefault()
	{
		// Arrange & Act
		var options = new CacheResilienceOptions();

		// Assert
		options.CircuitBreaker.HalfOpenTestLimit.ShouldBe(3);
	}

	[Fact]
	public void HaveHalfOpenSuccessThresholdOf2_ByDefault()
	{
		// Arrange & Act
		var options = new CacheResilienceOptions();

		// Assert
		options.CircuitBreaker.HalfOpenSuccessThreshold.ShouldBe(2);
	}

	[Fact]
	public void HaveMaxTypeNameCacheSizeOf10000_ByDefault()
	{
		// Arrange & Act
		var options = new CacheResilienceOptions();

		// Assert
		options.TypeNameCache.MaxCacheSize.ShouldBe(10_000);
	}

	[Fact]
	public void HaveOneHourTypeNameCacheTtl_ByDefault()
	{
		// Arrange & Act
		var options = new CacheResilienceOptions();

		// Assert
		options.TypeNameCache.CacheTtl.ShouldBe(TimeSpan.FromHours(1));
	}

	[Fact]
	public void HaveFallbackEnabled_ByDefault()
	{
		// Arrange & Act
		var options = new CacheResilienceOptions();

		// Assert
		options.EnableFallback.ShouldBeTrue();
	}

	[Fact]
	public void HaveLogMetricsOnDisposalEnabled_ByDefault()
	{
		// Arrange & Act
		var options = new CacheResilienceOptions();

		// Assert
		options.LogMetricsOnDisposal.ShouldBeTrue();
	}

	[Fact]
	public void AllowSettingCircuitBreakerEnabled()
	{
		// Act
		var options = new CacheResilienceOptions();
		options.CircuitBreaker.Enabled = false;

		// Assert
		options.CircuitBreaker.Enabled.ShouldBeFalse();
	}

	[Fact]
	public void AllowSettingFailureThreshold()
	{
		// Act
		var options = new CacheResilienceOptions();
		options.CircuitBreaker.FailureThreshold = 10;

		// Assert
		options.CircuitBreaker.FailureThreshold.ShouldBe(10);
	}

	[Fact]
	public void AllowSettingFailureWindow()
	{
		// Arrange
		var window = TimeSpan.FromMinutes(5);

		// Act
		var options = new CacheResilienceOptions();
		options.CircuitBreaker.FailureWindow = window;

		// Assert
		options.CircuitBreaker.FailureWindow.ShouldBe(window);
	}

	[Fact]
	public void AllowSettingOpenDuration()
	{
		// Arrange
		var duration = TimeSpan.FromMinutes(1);

		// Act
		var options = new CacheResilienceOptions();
		options.CircuitBreaker.OpenDuration = duration;

		// Assert
		options.CircuitBreaker.OpenDuration.ShouldBe(duration);
	}
}

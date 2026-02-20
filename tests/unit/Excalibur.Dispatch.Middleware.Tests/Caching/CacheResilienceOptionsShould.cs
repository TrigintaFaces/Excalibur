// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Caching;

namespace Excalibur.Dispatch.Middleware.Tests.Caching;

/// <summary>
/// Unit tests for <see cref="CacheResilienceOptions"/> and related options.
/// </summary>
[Trait("Category", "Unit")]
public sealed class CacheResilienceOptionsShould : UnitTestBase
{
	[Fact]
	public void Create_WithDefaults_HasExpectedValues()
	{
		// Arrange & Act
		var options = new CacheResilienceOptions();

		// Assert
		options.EnableFallback.ShouldBeTrue();
		options.LogMetricsOnDisposal.ShouldBeTrue();
		options.CircuitBreaker.ShouldNotBeNull();
		options.TypeNameCache.ShouldNotBeNull();
	}

	[Fact]
	public void CircuitBreakerOptions_HasExpectedDefaults()
	{
		// Arrange & Act
		var options = new CacheCircuitBreakerOptions();

		// Assert
		options.Enabled.ShouldBeTrue();
		options.FailureThreshold.ShouldBe(5);
		options.FailureWindow.ShouldBe(TimeSpan.FromMinutes(1));
		options.OpenDuration.ShouldBe(TimeSpan.FromSeconds(30));
		options.HalfOpenTestLimit.ShouldBe(3);
		options.HalfOpenSuccessThreshold.ShouldBe(2);
	}

	[Fact]
	public void TypeNameCacheOptions_HasExpectedDefaults()
	{
		// Arrange & Act
		var options = new CacheTypeNameOptions();

		// Assert
		options.MaxCacheSize.ShouldBe(10_000);
		options.CacheTtl.ShouldBe(TimeSpan.FromHours(1));
	}

	[Fact]
	public void EnableFallback_CanBeDisabled()
	{
		// Arrange
		var options = new CacheResilienceOptions { EnableFallback = false };

		// Assert
		options.EnableFallback.ShouldBeFalse();
	}

	[Fact]
	public void LogMetricsOnDisposal_CanBeDisabled()
	{
		// Arrange
		var options = new CacheResilienceOptions { LogMetricsOnDisposal = false };

		// Assert
		options.LogMetricsOnDisposal.ShouldBeFalse();
	}

	[Fact]
	public void CircuitBreaker_CanBeConfigured()
	{
		// Arrange
		var options = new CacheCircuitBreakerOptions
		{
			Enabled = false,
			FailureThreshold = 10,
			FailureWindow = TimeSpan.FromMinutes(5),
			OpenDuration = TimeSpan.FromMinutes(1),
			HalfOpenTestLimit = 5,
			HalfOpenSuccessThreshold = 3,
		};

		// Assert
		options.Enabled.ShouldBeFalse();
		options.FailureThreshold.ShouldBe(10);
		options.FailureWindow.ShouldBe(TimeSpan.FromMinutes(5));
		options.OpenDuration.ShouldBe(TimeSpan.FromMinutes(1));
		options.HalfOpenTestLimit.ShouldBe(5);
		options.HalfOpenSuccessThreshold.ShouldBe(3);
	}

	[Fact]
	public void TypeNameCache_CanBeConfigured()
	{
		// Arrange
		var options = new CacheTypeNameOptions
		{
			MaxCacheSize = 50_000,
			CacheTtl = TimeSpan.FromHours(2),
		};

		// Assert
		options.MaxCacheSize.ShouldBe(50_000);
		options.CacheTtl.ShouldBe(TimeSpan.FromHours(2));
	}
}

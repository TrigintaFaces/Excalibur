// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Options.Configuration;

namespace Excalibur.Dispatch.Tests.Options.Configuration;

/// <summary>
/// Unit tests for <see cref="ResilienceOptions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Dispatch")]
public sealed class ResilienceOptionsShould
{
	[Fact]
	public void HaveDefaultValues()
	{
		// Arrange & Act
		var options = new ResilienceOptions();

		// Assert
		options.DefaultRetryCount.ShouldBe(3);
		options.EnableCircuitBreaker.ShouldBeFalse();
		options.EnableTimeout.ShouldBeFalse();
		options.EnableBulkhead.ShouldBeFalse();
	}

	[Theory]
	[InlineData(0)]
	[InlineData(1)]
	[InlineData(5)]
	[InlineData(10)]
	public void AllowSettingRetryCount(int retryCount)
	{
		// Arrange
		var options = new ResilienceOptions();

		// Act
		options.DefaultRetryCount = retryCount;

		// Assert
		options.DefaultRetryCount.ShouldBe(retryCount);
	}

	[Fact]
	public void AllowNegativeRetryCount()
	{
		// Arrange
		var options = new ResilienceOptions();

		// Act - No validation at options level
		options.DefaultRetryCount = -1;

		// Assert
		options.DefaultRetryCount.ShouldBe(-1);
	}

	[Fact]
	public void AllowEnablingCircuitBreaker()
	{
		// Arrange
		var options = new ResilienceOptions();

		// Act
		options.EnableCircuitBreaker = true;

		// Assert
		options.EnableCircuitBreaker.ShouldBeTrue();
	}

	[Fact]
	public void AllowEnablingTimeout()
	{
		// Arrange
		var options = new ResilienceOptions();

		// Act
		options.EnableTimeout = true;

		// Assert
		options.EnableTimeout.ShouldBeTrue();
	}

	[Fact]
	public void AllowEnablingBulkhead()
	{
		// Arrange
		var options = new ResilienceOptions();

		// Act
		options.EnableBulkhead = true;

		// Assert
		options.EnableBulkhead.ShouldBeTrue();
	}

	[Theory]
	[InlineData(1, true, true, true)]
	[InlineData(5, false, true, false)]
	[InlineData(0, true, false, true)]
	[InlineData(10, false, false, false)]
	public void SupportVariousConfigurations(int retryCount, bool circuitBreaker, bool timeout, bool bulkhead)
	{
		// Arrange & Act
		var options = new ResilienceOptions
		{
			DefaultRetryCount = retryCount,
			EnableCircuitBreaker = circuitBreaker,
			EnableTimeout = timeout,
			EnableBulkhead = bulkhead
		};

		// Assert
		options.DefaultRetryCount.ShouldBe(retryCount);
		options.EnableCircuitBreaker.ShouldBe(circuitBreaker);
		options.EnableTimeout.ShouldBe(timeout);
		options.EnableBulkhead.ShouldBe(bulkhead);
	}

	[Fact]
	public void AllowSelectiveFeatureEnabling()
	{
		// Arrange - Start with defaults
		var options = new ResilienceOptions();

		// Act - Enable only circuit breaker
		options.EnableCircuitBreaker = true;

		// Assert - Everything else stays disabled
		options.DefaultRetryCount.ShouldBe(3);
		options.EnableCircuitBreaker.ShouldBeTrue();
		options.EnableTimeout.ShouldBeFalse();
		options.EnableBulkhead.ShouldBeFalse();
	}
}

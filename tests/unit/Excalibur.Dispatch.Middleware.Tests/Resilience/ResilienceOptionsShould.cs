// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Resilience.Polly;

namespace Excalibur.Dispatch.Middleware.Tests.Resilience;

/// <summary>
/// Unit tests for <see cref="ResilienceOptions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Resilience")]
public sealed class ResilienceOptionsShould : UnitTestBase
{
	[Fact]
	public void HaveCorrectDefaultValues()
	{
		// Act
		var options = new ResilienceOptions();

		// Assert
		options.Enabled.ShouldBeTrue();
		options.DefaultRetryCount.ShouldBe(3);
		options.DefaultTimeoutSeconds.ShouldBe(30);
		options.EnableCircuitBreaker.ShouldBeTrue();
	}

	[Fact]
	public void AllowSettingEnabled()
	{
		// Arrange
		var options = new ResilienceOptions();

		// Act
		options.Enabled = false;

		// Assert
		options.Enabled.ShouldBeFalse();
	}

	[Fact]
	public void AllowSettingDefaultRetryCount()
	{
		// Arrange
		var options = new ResilienceOptions();

		// Act
		options.DefaultRetryCount = 5;

		// Assert
		options.DefaultRetryCount.ShouldBe(5);
	}

	[Fact]
	public void AllowSettingDefaultTimeoutSeconds()
	{
		// Arrange
		var options = new ResilienceOptions();

		// Act
		options.DefaultTimeoutSeconds = 60;

		// Assert
		options.DefaultTimeoutSeconds.ShouldBe(60);
	}

	[Fact]
	public void AllowSettingEnableCircuitBreaker()
	{
		// Arrange
		var options = new ResilienceOptions();

		// Act
		options.EnableCircuitBreaker = false;

		// Assert
		options.EnableCircuitBreaker.ShouldBeFalse();
	}

	[Theory]
	[InlineData(0)]
	[InlineData(1)]
	[InlineData(10)]
	[InlineData(100)]
	public void AcceptVariousRetryCountValues(int retryCount)
	{
		// Arrange
		var options = new ResilienceOptions();

		// Act
		options.DefaultRetryCount = retryCount;

		// Assert
		options.DefaultRetryCount.ShouldBe(retryCount);
	}

	[Theory]
	[InlineData(1)]
	[InlineData(30)]
	[InlineData(60)]
	[InlineData(300)]
	public void AcceptVariousTimeoutValues(int timeoutSeconds)
	{
		// Arrange
		var options = new ResilienceOptions();

		// Act
		options.DefaultTimeoutSeconds = timeoutSeconds;

		// Assert
		options.DefaultTimeoutSeconds.ShouldBe(timeoutSeconds);
	}
}

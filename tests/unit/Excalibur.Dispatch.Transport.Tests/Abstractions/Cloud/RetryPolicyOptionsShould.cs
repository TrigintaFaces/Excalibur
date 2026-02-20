// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport;

namespace Excalibur.Dispatch.Transport.Tests.Abstractions.Cloud;

/// <summary>
/// Unit tests for <see cref="RetryPolicyOptions"/> class.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Transport.Abstractions")]
public sealed class RetryPolicyOptionsShould
{
	[Fact]
	public void HaveThreeMaxRetryAttempts_ByDefault()
	{
		// Arrange & Act
		var options = new RetryPolicyOptions();

		// Assert
		options.MaxRetryAttempts.ShouldBe(3);
	}

	[Fact]
	public void Have1000MsBaseDelay_ByDefault()
	{
		// Arrange & Act
		var options = new RetryPolicyOptions();

		// Assert
		options.BaseDelayMs.ShouldBe(1000);
	}

	[Fact]
	public void Have30000MsMaxDelay_ByDefault()
	{
		// Arrange & Act
		var options = new RetryPolicyOptions();

		// Assert
		options.MaxDelayMs.ShouldBe(30000);
	}

	[Fact]
	public void HaveExponentialBackoffEnabled_ByDefault()
	{
		// Arrange & Act
		var options = new RetryPolicyOptions();

		// Assert
		options.UseExponentialBackoff.ShouldBeTrue();
	}

	[Fact]
	public void AllowSettingMaxRetryAttempts()
	{
		// Arrange
		var options = new RetryPolicyOptions();

		// Act
		options.MaxRetryAttempts = 5;

		// Assert
		options.MaxRetryAttempts.ShouldBe(5);
	}

	[Fact]
	public void AllowSettingBaseDelayMs()
	{
		// Arrange
		var options = new RetryPolicyOptions();

		// Act
		options.BaseDelayMs = 500;

		// Assert
		options.BaseDelayMs.ShouldBe(500);
	}

	[Fact]
	public void AllowSettingMaxDelayMs()
	{
		// Arrange
		var options = new RetryPolicyOptions();

		// Act
		options.MaxDelayMs = 60000;

		// Assert
		options.MaxDelayMs.ShouldBe(60000);
	}

	[Fact]
	public void AllowDisablingExponentialBackoff()
	{
		// Arrange
		var options = new RetryPolicyOptions();

		// Act
		options.UseExponentialBackoff = false;

		// Assert
		options.UseExponentialBackoff.ShouldBeFalse();
	}

	[Fact]
	public void AllowZeroMaxRetryAttempts()
	{
		// Arrange
		var options = new RetryPolicyOptions();

		// Act
		options.MaxRetryAttempts = 0;

		// Assert
		options.MaxRetryAttempts.ShouldBe(0);
	}

	[Fact]
	public void AllowZeroBaseDelay()
	{
		// Arrange
		var options = new RetryPolicyOptions();

		// Act
		options.BaseDelayMs = 0;

		// Assert
		options.BaseDelayMs.ShouldBe(0);
	}

	[Fact]
	public void HaveBaseDelayLessThanOrEqualToMaxDelay_ByDefault()
	{
		// Arrange & Act
		var options = new RetryPolicyOptions();

		// Assert
		options.BaseDelayMs.ShouldBeLessThanOrEqualTo(options.MaxDelayMs);
	}
}

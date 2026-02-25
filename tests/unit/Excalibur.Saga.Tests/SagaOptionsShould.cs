// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Saga.Tests;

/// <summary>
/// Unit tests for <see cref="SagaOptions"/>.
/// </summary>
[Trait("Category", "Unit")]
public sealed class SagaOptionsShould : UnitTestBase
{
	[Fact]
	public void HaveCorrectDefaultMaxConcurrency()
	{
		// Arrange & Act
		var options = new SagaOptions();

		// Assert
		options.MaxConcurrency.ShouldBe(10);
	}

	[Fact]
	public void HaveCorrectDefaultTimeout()
	{
		// Arrange & Act
		var options = new SagaOptions();

		// Assert
		options.DefaultTimeout.ShouldBe(TimeSpan.FromMinutes(30));
	}

	[Fact]
	public void HaveCorrectDefaultSagaRetentionPeriod()
	{
		// Arrange & Act
		var options = new SagaOptions();

		// Assert
		options.SagaRetentionPeriod.ShouldBe(TimeSpan.FromDays(30));
	}

	[Fact]
	public void HaveCorrectDefaultEnableAutomaticCleanup()
	{
		// Arrange & Act
		var options = new SagaOptions();

		// Assert
		options.EnableAutomaticCleanup.ShouldBeTrue();
	}

	[Fact]
	public void HaveCorrectDefaultCleanupInterval()
	{
		// Arrange & Act
		var options = new SagaOptions();

		// Assert
		options.CleanupInterval.ShouldBe(TimeSpan.FromHours(1));
	}

	[Fact]
	public void HaveCorrectDefaultMaxRetryAttempts()
	{
		// Arrange & Act
		var options = new SagaOptions();

		// Assert
		options.MaxRetryAttempts.ShouldBe(3);
	}

	[Fact]
	public void HaveCorrectDefaultRetryDelay()
	{
		// Arrange & Act
		var options = new SagaOptions();

		// Assert
		options.RetryDelay.ShouldBe(TimeSpan.FromMinutes(1));
	}

	[Fact]
	public void HaveCorrectDefaultEnableOptimisticConcurrency()
	{
		// Arrange & Act
		var options = new SagaOptions();

		// Assert
		options.EnableOptimisticConcurrency.ShouldBeTrue();
	}

	[Fact]
	public void AllowCustomMaxConcurrency()
	{
		// Arrange
		var options = new SagaOptions();

		// Act
		options.MaxConcurrency = 50;

		// Assert
		options.MaxConcurrency.ShouldBe(50);
	}

	[Fact]
	public void AllowCustomDefaultTimeout()
	{
		// Arrange
		var options = new SagaOptions();

		// Act
		options.DefaultTimeout = TimeSpan.FromHours(1);

		// Assert
		options.DefaultTimeout.ShouldBe(TimeSpan.FromHours(1));
	}

	[Fact]
	public void AllowCustomSagaRetentionPeriod()
	{
		// Arrange
		var options = new SagaOptions();

		// Act
		options.SagaRetentionPeriod = TimeSpan.FromDays(90);

		// Assert
		options.SagaRetentionPeriod.ShouldBe(TimeSpan.FromDays(90));
	}

	[Fact]
	public void AllowDisablingAutomaticCleanup()
	{
		// Arrange
		var options = new SagaOptions();

		// Act
		options.EnableAutomaticCleanup = false;

		// Assert
		options.EnableAutomaticCleanup.ShouldBeFalse();
	}

	[Fact]
	public void AllowCustomCleanupInterval()
	{
		// Arrange
		var options = new SagaOptions();

		// Act
		options.CleanupInterval = TimeSpan.FromMinutes(30);

		// Assert
		options.CleanupInterval.ShouldBe(TimeSpan.FromMinutes(30));
	}

	[Fact]
	public void AllowCustomMaxRetryAttempts()
	{
		// Arrange
		var options = new SagaOptions();

		// Act
		options.MaxRetryAttempts = 5;

		// Assert
		options.MaxRetryAttempts.ShouldBe(5);
	}

	[Fact]
	public void AllowCustomRetryDelay()
	{
		// Arrange
		var options = new SagaOptions();

		// Act
		options.RetryDelay = TimeSpan.FromSeconds(30);

		// Assert
		options.RetryDelay.ShouldBe(TimeSpan.FromSeconds(30));
	}

	[Fact]
	public void AllowDisablingOptimisticConcurrency()
	{
		// Arrange
		var options = new SagaOptions();

		// Act
		options.EnableOptimisticConcurrency = false;

		// Assert
		options.EnableOptimisticConcurrency.ShouldBeFalse();
	}
}

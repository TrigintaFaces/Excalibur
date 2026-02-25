// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Saga.Tests.Core;

/// <summary>
/// Unit tests for <see cref="AdvancedSagaOptions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Saga")]
public sealed class AdvancedSagaOptionsShould
{
	#region Default Values Tests

	[Fact]
	public void HaveDefaultTimeout()
	{
		// Arrange & Act
		var options = new AdvancedSagaOptions();

		// Assert
		options.DefaultTimeout.ShouldBe(TimeSpan.FromMinutes(30));
	}

	[Fact]
	public void HaveDefaultStepTimeout()
	{
		// Arrange & Act
		var options = new AdvancedSagaOptions();

		// Assert
		options.DefaultStepTimeout.ShouldBe(TimeSpan.FromMinutes(5));
	}

	[Fact]
	public void HaveDefaultMaxRetryAttempts()
	{
		// Arrange & Act
		var options = new AdvancedSagaOptions();

		// Assert
		options.MaxRetryAttempts.ShouldBe(3);
	}

	[Fact]
	public void HaveDefaultRetryBaseDelay()
	{
		// Arrange & Act
		var options = new AdvancedSagaOptions();

		// Assert
		options.RetryBaseDelay.ShouldBe(TimeSpan.FromSeconds(1));
	}

	[Fact]
	public void HaveDefaultEnableAutoCompensation()
	{
		// Arrange & Act
		var options = new AdvancedSagaOptions();

		// Assert
		options.EnableAutoCompensation.ShouldBeTrue();
	}

	[Fact]
	public void HaveDefaultEnableStatePersistence()
	{
		// Arrange & Act
		var options = new AdvancedSagaOptions();

		// Assert
		options.EnableStatePersistence.ShouldBeTrue();
	}

	[Fact]
	public void HaveDefaultMaxDegreeOfParallelism()
	{
		// Arrange & Act
		var options = new AdvancedSagaOptions();

		// Assert
		options.MaxDegreeOfParallelism.ShouldBe(10);
	}

	[Fact]
	public void HaveDefaultEnableMetrics()
	{
		// Arrange & Act
		var options = new AdvancedSagaOptions();

		// Assert
		options.EnableMetrics.ShouldBeTrue();
	}

	[Fact]
	public void HaveDefaultCleanupInterval()
	{
		// Arrange & Act
		var options = new AdvancedSagaOptions();

		// Assert
		options.CleanupInterval.ShouldBe(TimeSpan.FromHours(1));
	}

	[Fact]
	public void HaveDefaultCompletedSagaRetention()
	{
		// Arrange & Act
		var options = new AdvancedSagaOptions();

		// Assert
		options.CompletedSagaRetention.ShouldBe(TimeSpan.FromDays(7));
	}

	#endregion Default Values Tests

	#region Property Setting Tests

	[Fact]
	public void AllowDefaultTimeoutToBeSet()
	{
		// Arrange & Act
		var options = new AdvancedSagaOptions { DefaultTimeout = TimeSpan.FromHours(1) };

		// Assert
		options.DefaultTimeout.ShouldBe(TimeSpan.FromHours(1));
	}

	[Fact]
	public void AllowDefaultStepTimeoutToBeSet()
	{
		// Arrange & Act
		var options = new AdvancedSagaOptions { DefaultStepTimeout = TimeSpan.FromMinutes(10) };

		// Assert
		options.DefaultStepTimeout.ShouldBe(TimeSpan.FromMinutes(10));
	}

	[Fact]
	public void AllowMaxRetryAttemptsToBeSet()
	{
		// Arrange & Act
		var options = new AdvancedSagaOptions { MaxRetryAttempts = 5 };

		// Assert
		options.MaxRetryAttempts.ShouldBe(5);
	}

	[Fact]
	public void AllowRetryBaseDelayToBeSet()
	{
		// Arrange & Act
		var options = new AdvancedSagaOptions { RetryBaseDelay = TimeSpan.FromMilliseconds(500) };

		// Assert
		options.RetryBaseDelay.ShouldBe(TimeSpan.FromMilliseconds(500));
	}

	[Fact]
	public void AllowEnableAutoCompensationToBeSet()
	{
		// Arrange & Act
		var options = new AdvancedSagaOptions { EnableAutoCompensation = false };

		// Assert
		options.EnableAutoCompensation.ShouldBeFalse();
	}

	[Fact]
	public void AllowEnableStatePersistenceToBeSet()
	{
		// Arrange & Act
		var options = new AdvancedSagaOptions { EnableStatePersistence = false };

		// Assert
		options.EnableStatePersistence.ShouldBeFalse();
	}

	[Fact]
	public void AllowMaxDegreeOfParallelismToBeSet()
	{
		// Arrange & Act
		var options = new AdvancedSagaOptions { MaxDegreeOfParallelism = 50 };

		// Assert
		options.MaxDegreeOfParallelism.ShouldBe(50);
	}

	[Fact]
	public void AllowEnableMetricsToBeSet()
	{
		// Arrange & Act
		var options = new AdvancedSagaOptions { EnableMetrics = false };

		// Assert
		options.EnableMetrics.ShouldBeFalse();
	}

	[Fact]
	public void AllowCleanupIntervalToBeSet()
	{
		// Arrange & Act
		var options = new AdvancedSagaOptions { CleanupInterval = TimeSpan.FromMinutes(30) };

		// Assert
		options.CleanupInterval.ShouldBe(TimeSpan.FromMinutes(30));
	}

	[Fact]
	public void AllowCompletedSagaRetentionToBeSet()
	{
		// Arrange & Act
		var options = new AdvancedSagaOptions { CompletedSagaRetention = TimeSpan.FromDays(30) };

		// Assert
		options.CompletedSagaRetention.ShouldBe(TimeSpan.FromDays(30));
	}

	#endregion Property Setting Tests

	#region Comprehensive Configuration Tests

	[Fact]
	public void CreateHighThroughputConfiguration()
	{
		// Arrange & Act
		var options = new AdvancedSagaOptions
		{
			DefaultTimeout = TimeSpan.FromMinutes(10),
			DefaultStepTimeout = TimeSpan.FromMinutes(2),
			MaxRetryAttempts = 5,
			RetryBaseDelay = TimeSpan.FromMilliseconds(100),
			MaxDegreeOfParallelism = 50,
			EnableMetrics = true,
		};

		// Assert
		options.MaxDegreeOfParallelism.ShouldBe(50);
		options.RetryBaseDelay.ShouldBeLessThan(TimeSpan.FromSeconds(1));
	}

	[Fact]
	public void CreateReliabilityFocusedConfiguration()
	{
		// Arrange & Act
		var options = new AdvancedSagaOptions
		{
			DefaultTimeout = TimeSpan.FromHours(2),
			DefaultStepTimeout = TimeSpan.FromMinutes(15),
			MaxRetryAttempts = 10,
			RetryBaseDelay = TimeSpan.FromSeconds(5),
			EnableAutoCompensation = true,
			EnableStatePersistence = true,
			CompletedSagaRetention = TimeSpan.FromDays(30),
		};

		// Assert
		options.EnableAutoCompensation.ShouldBeTrue();
		options.EnableStatePersistence.ShouldBeTrue();
		options.MaxRetryAttempts.ShouldBe(10);
	}

	[Fact]
	public void CreateMinimalConfiguration()
	{
		// Arrange & Act
		var options = new AdvancedSagaOptions
		{
			MaxRetryAttempts = 0,
			EnableAutoCompensation = false,
			EnableStatePersistence = false,
			EnableMetrics = false,
			MaxDegreeOfParallelism = 1,
		};

		// Assert
		options.MaxRetryAttempts.ShouldBe(0);
		options.EnableAutoCompensation.ShouldBeFalse();
		options.EnableStatePersistence.ShouldBeFalse();
		options.EnableMetrics.ShouldBeFalse();
		options.MaxDegreeOfParallelism.ShouldBe(1);
	}

	#endregion Comprehensive Configuration Tests
}

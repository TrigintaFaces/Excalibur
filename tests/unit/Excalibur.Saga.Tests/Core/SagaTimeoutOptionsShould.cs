// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Saga.Tests.Core;

/// <summary>
/// Unit tests for <see cref="SagaTimeoutOptions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Saga")]
public sealed class SagaTimeoutOptionsShould
{
	#region Default Values Tests

	[Fact]
	public void HaveDefaultPollInterval()
	{
		// Arrange & Act
		var options = new SagaTimeoutOptions();

		// Assert
		options.PollInterval.ShouldBe(TimeSpan.FromSeconds(1));
	}

	[Fact]
	public void HaveDefaultBatchSize()
	{
		// Arrange & Act
		var options = new SagaTimeoutOptions();

		// Assert
		options.BatchSize.ShouldBe(100);
	}

	[Fact]
	public void HaveDefaultShutdownTimeout()
	{
		// Arrange & Act
		var options = new SagaTimeoutOptions();

		// Assert
		options.ShutdownTimeout.ShouldBe(TimeSpan.FromSeconds(30));
	}

	[Fact]
	public void HaveDefaultEnableVerboseLogging()
	{
		// Arrange & Act
		var options = new SagaTimeoutOptions();

		// Assert
		options.EnableVerboseLogging.ShouldBeTrue();
	}

	#endregion Default Values Tests

	#region Property Setting Tests

	[Fact]
	public void AllowPollIntervalToBeSet()
	{
		// Arrange & Act
		var options = new SagaTimeoutOptions { PollInterval = TimeSpan.FromMilliseconds(500) };

		// Assert
		options.PollInterval.ShouldBe(TimeSpan.FromMilliseconds(500));
	}

	[Fact]
	public void AllowBatchSizeToBeSet()
	{
		// Arrange & Act
		var options = new SagaTimeoutOptions { BatchSize = 50 };

		// Assert
		options.BatchSize.ShouldBe(50);
	}

	[Fact]
	public void AllowShutdownTimeoutToBeSet()
	{
		// Arrange & Act
		var options = new SagaTimeoutOptions { ShutdownTimeout = TimeSpan.FromMinutes(1) };

		// Assert
		options.ShutdownTimeout.ShouldBe(TimeSpan.FromMinutes(1));
	}

	[Fact]
	public void AllowEnableVerboseLoggingToBeSet()
	{
		// Arrange & Act
		var options = new SagaTimeoutOptions { EnableVerboseLogging = false };

		// Assert
		options.EnableVerboseLogging.ShouldBeFalse();
	}

	#endregion Property Setting Tests

	#region Configuration Scenario Tests

	[Fact]
	public void CreateHighFrequencyPollingConfiguration()
	{
		// Arrange & Act
		var options = new SagaTimeoutOptions
		{
			PollInterval = TimeSpan.FromMilliseconds(100),
			BatchSize = 50,
		};

		// Assert
		options.PollInterval.ShouldBe(TimeSpan.FromMilliseconds(100));
		options.BatchSize.ShouldBe(50);
	}

	[Fact]
	public void CreateLowFrequencyPollingConfiguration()
	{
		// Arrange & Act
		var options = new SagaTimeoutOptions
		{
			PollInterval = TimeSpan.FromSeconds(10),
			BatchSize = 200,
		};

		// Assert
		options.PollInterval.ShouldBe(TimeSpan.FromSeconds(10));
		options.BatchSize.ShouldBe(200);
	}

	[Fact]
	public void CreateQuietConfiguration()
	{
		// Arrange & Act
		var options = new SagaTimeoutOptions
		{
			EnableVerboseLogging = false,
		};

		// Assert
		options.EnableVerboseLogging.ShouldBeFalse();
	}

	[Fact]
	public void CreateFastShutdownConfiguration()
	{
		// Arrange & Act
		var options = new SagaTimeoutOptions
		{
			ShutdownTimeout = TimeSpan.FromSeconds(5),
		};

		// Assert
		options.ShutdownTimeout.ShouldBe(TimeSpan.FromSeconds(5));
	}

	#endregion Configuration Scenario Tests
}

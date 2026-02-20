// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Cdc.Processing;

namespace Excalibur.Tests.Cdc.Processing;

/// <summary>
/// Unit tests for <see cref="CdcProcessingOptions"/>.
/// Tests default values, property setters, and computed properties.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Cdc")]
[Trait("Priority", "0")]
public sealed class CdcProcessingOptionsShould : UnitTestBase
{
	#region Default Values Tests

	[Fact]
	public void HaveCorrectDefaults()
	{
		// Arrange & Act
		var options = new CdcProcessingOptions();

		// Assert
		options.PollingInterval.ShouldBe(TimeSpan.FromSeconds(5));
		options.Enabled.ShouldBeTrue();
		options.DrainTimeoutSeconds.ShouldBe(30);
	}

	[Fact]
	public void ReturnCorrectDrainTimeoutFromSeconds()
	{
		// Arrange
		var options = new CdcProcessingOptions();

		// Act
		var drainTimeout = options.DrainTimeout;

		// Assert
		drainTimeout.ShouldBe(TimeSpan.FromSeconds(30));
	}

	#endregion

	#region PollingInterval Property Tests

	[Fact]
	public void AllowSettingPollingInterval()
	{
		// Arrange
		var options = new CdcProcessingOptions();

		// Act
		options.PollingInterval = TimeSpan.FromSeconds(10);

		// Assert
		options.PollingInterval.ShouldBe(TimeSpan.FromSeconds(10));
	}

	[Fact]
	public void AllowSettingPollingIntervalToZero()
	{
		// Arrange
		var options = new CdcProcessingOptions();

		// Act
		options.PollingInterval = TimeSpan.Zero;

		// Assert
		options.PollingInterval.ShouldBe(TimeSpan.Zero);
	}

	[Fact]
	public void AllowSettingPollingIntervalToLargeValue()
	{
		// Arrange
		var options = new CdcProcessingOptions();

		// Act
		options.PollingInterval = TimeSpan.FromMinutes(30);

		// Assert
		options.PollingInterval.ShouldBe(TimeSpan.FromMinutes(30));
	}

	#endregion

	#region Enabled Property Tests

	[Fact]
	public void AllowDisabling()
	{
		// Arrange
		var options = new CdcProcessingOptions();

		// Act
		options.Enabled = false;

		// Assert
		options.Enabled.ShouldBeFalse();
	}

	[Fact]
	public void AllowEnabling()
	{
		// Arrange
		var options = new CdcProcessingOptions { Enabled = false };

		// Act
		options.Enabled = true;

		// Assert
		options.Enabled.ShouldBeTrue();
	}

	#endregion

	#region DrainTimeoutSeconds Property Tests

	[Fact]
	public void AllowSettingDrainTimeoutSeconds()
	{
		// Arrange
		var options = new CdcProcessingOptions();

		// Act
		options.DrainTimeoutSeconds = 60;

		// Assert
		options.DrainTimeoutSeconds.ShouldBe(60);
	}

	[Fact]
	public void AllowSettingDrainTimeoutSecondsToZero()
	{
		// Arrange
		var options = new CdcProcessingOptions();

		// Act
		options.DrainTimeoutSeconds = 0;

		// Assert
		options.DrainTimeoutSeconds.ShouldBe(0);
		options.DrainTimeout.ShouldBe(TimeSpan.Zero);
	}

	[Fact]
	public void AllowSettingDrainTimeoutSecondsToLargeValue()
	{
		// Arrange
		var options = new CdcProcessingOptions();

		// Act
		options.DrainTimeoutSeconds = 3600; // 1 hour

		// Assert
		options.DrainTimeoutSeconds.ShouldBe(3600);
		options.DrainTimeout.ShouldBe(TimeSpan.FromHours(1));
	}

	#endregion

	#region DrainTimeout Computed Property Tests

	[Fact]
	public void ComputeDrainTimeoutFromSeconds()
	{
		// Arrange
		var options = new CdcProcessingOptions { DrainTimeoutSeconds = 45 };

		// Act
		var drainTimeout = options.DrainTimeout;

		// Assert
		drainTimeout.ShouldBe(TimeSpan.FromSeconds(45));
	}

	[Fact]
	public void ReflectChangesToDrainTimeoutSeconds()
	{
		// Arrange
		var options = new CdcProcessingOptions();

		// Act
		options.DrainTimeoutSeconds = 15;

		// Assert
		options.DrainTimeout.ShouldBe(TimeSpan.FromSeconds(15));

		// Act again
		options.DrainTimeoutSeconds = 90;

		// Assert again
		options.DrainTimeout.ShouldBe(TimeSpan.FromSeconds(90));
	}

	[Theory]
	[InlineData(1)]
	[InlineData(10)]
	[InlineData(30)]
	[InlineData(60)]
	[InlineData(120)]
	[InlineData(300)]
	public void ComputeCorrectDrainTimeoutForVariousValues(int seconds)
	{
		// Arrange
		var options = new CdcProcessingOptions { DrainTimeoutSeconds = seconds };

		// Act & Assert
		options.DrainTimeout.ShouldBe(TimeSpan.FromSeconds(seconds));
	}

	#endregion

	#region Integration Tests

	[Fact]
	public void SupportConfigurationScenario_HighThroughput()
	{
		// Arrange & Act
		var options = new CdcProcessingOptions
		{
			PollingInterval = TimeSpan.FromMilliseconds(100),
			Enabled = true,
			DrainTimeoutSeconds = 10
		};

		// Assert
		options.PollingInterval.ShouldBe(TimeSpan.FromMilliseconds(100));
		options.Enabled.ShouldBeTrue();
		options.DrainTimeout.ShouldBe(TimeSpan.FromSeconds(10));
	}

	[Fact]
	public void SupportConfigurationScenario_LowResourceUsage()
	{
		// Arrange & Act
		var options = new CdcProcessingOptions
		{
			PollingInterval = TimeSpan.FromMinutes(1),
			Enabled = true,
			DrainTimeoutSeconds = 120
		};

		// Assert
		options.PollingInterval.ShouldBe(TimeSpan.FromMinutes(1));
		options.Enabled.ShouldBeTrue();
		options.DrainTimeout.ShouldBe(TimeSpan.FromMinutes(2));
	}

	[Fact]
	public void SupportConfigurationScenario_Disabled()
	{
		// Arrange & Act
		var options = new CdcProcessingOptions
		{
			Enabled = false
		};

		// Assert - other settings should retain defaults
		options.Enabled.ShouldBeFalse();
		options.PollingInterval.ShouldBe(TimeSpan.FromSeconds(5));
		options.DrainTimeoutSeconds.ShouldBe(30);
	}

	#endregion
}

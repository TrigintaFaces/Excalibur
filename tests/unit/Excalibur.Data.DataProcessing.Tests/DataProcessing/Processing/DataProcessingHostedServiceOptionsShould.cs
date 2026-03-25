// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.DataProcessing.Processing;

namespace Excalibur.Data.Tests.DataProcessing.Processing;

/// <summary>
/// Unit tests for <see cref="DataProcessingHostedServiceOptions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class DataProcessingHostedServiceOptionsShould : UnitTestBase
{
	[Fact]
	public void HaveExpectedDefaults()
	{
		// Arrange & Act
		var options = new DataProcessingHostedServiceOptions();

		// Assert
		options.PollingInterval.ShouldBe(TimeSpan.FromSeconds(5));
		options.Enabled.ShouldBeTrue();
		options.DrainTimeoutSeconds.ShouldBe(30);
		options.UnhealthyThreshold.ShouldBe(3);
	}

	[Fact]
	public void ComputeDrainTimeout_FromDrainTimeoutSeconds()
	{
		// Arrange
		var options = new DataProcessingHostedServiceOptions { DrainTimeoutSeconds = 60 };

		// Act & Assert
		options.DrainTimeout.ShouldBe(TimeSpan.FromSeconds(60));
	}

	[Fact]
	public void AllowCustomPollingInterval()
	{
		// Arrange & Act
		var options = new DataProcessingHostedServiceOptions
		{
			PollingInterval = TimeSpan.FromSeconds(15),
		};

		// Assert
		options.PollingInterval.ShouldBe(TimeSpan.FromSeconds(15));
	}

	[Fact]
	public void AllowDisabling()
	{
		// Arrange & Act
		var options = new DataProcessingHostedServiceOptions { Enabled = false };

		// Assert
		options.Enabled.ShouldBeFalse();
	}

	[Fact]
	public void AllowCustomUnhealthyThreshold()
	{
		// Arrange & Act
		var options = new DataProcessingHostedServiceOptions { UnhealthyThreshold = 10 };

		// Assert
		options.UnhealthyThreshold.ShouldBe(10);
	}
}

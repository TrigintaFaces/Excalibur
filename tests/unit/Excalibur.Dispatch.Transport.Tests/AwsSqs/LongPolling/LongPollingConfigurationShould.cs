// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Aws;

namespace Excalibur.Dispatch.Transport.Tests.AwsSqs.LongPolling;

[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait("Component", "Platform")]
public sealed class LongPollingConfigurationShould
{
	[Fact]
	public void ComputeMaxWaitTimeFromSeconds()
	{
		// Arrange
		var config = new LongPollingOptions();
		config.Polling.MaxWaitTimeSeconds = 15;

		// Act & Assert
		config.Polling.MaxWaitTime.ShouldBe(TimeSpan.FromSeconds(15));
	}

	[Fact]
	public void ComputeMinWaitTimeFromSeconds()
	{
		// Arrange
		var config = new LongPollingOptions();
		config.Polling.MinWaitTimeSeconds = 3;

		// Act & Assert
		config.Polling.MinWaitTime.ShouldBe(TimeSpan.FromSeconds(3));
	}

	[Fact]
	public void AllowSettingAdaptiveOptions()
	{
		// Arrange
		var config = new LongPollingOptions();

		// Act
		config.Adaptive.Enabled = false;
		config.Adaptive.SmoothingFactor = 0.5;
		config.Adaptive.HighLoadThreshold = 0.9;
		config.Adaptive.LowLoadThreshold = 0.1;

		// Assert
		config.Adaptive.Enabled.ShouldBeFalse();
		config.Adaptive.SmoothingFactor.ShouldBe(0.5);
		config.Adaptive.HighLoadThreshold.ShouldBe(0.9);
		config.Adaptive.LowLoadThreshold.ShouldBe(0.1);
	}

}

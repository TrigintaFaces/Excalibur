// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Google;

namespace Excalibur.Dispatch.Transport.Tests.GooglePubSub.FlowControl;

[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class PubSubFlowControlOptionsShould
{
	[Fact]
	public void HaveCorrectDefaults()
	{
		// Arrange & Act
		var options = new PubSubFlowControlOptions();

		// Assert
		options.MaxOutstandingElementCount.ShouldBe(1000);
		options.MaxOutstandingByteCount.ShouldBe(100_000_000);
		options.EnableAdaptiveFlowControl.ShouldBeTrue();
		options.AdaptationInterval.ShouldBe(TimeSpan.FromSeconds(5));
		options.MinOutstandingElementCount.ShouldBe(100);
		options.MinOutstandingByteCount.ShouldBe(10_000_000);
		options.ScaleUpFactor.ShouldBe(1.5);
		options.ScaleDownFactor.ShouldBe(0.8);
		options.TargetUtilizationPercentage.ShouldBe(80.0);
		options.MemoryPressureThreshold.ShouldBe(75.0);
	}

	[Fact]
	public void AllowSettingAllProperties()
	{
		// Arrange & Act
		var options = new PubSubFlowControlOptions
		{
			MaxOutstandingElementCount = 2000,
			MaxOutstandingByteCount = 200_000_000,
			EnableAdaptiveFlowControl = false,
			AdaptationInterval = TimeSpan.FromSeconds(10),
			MinOutstandingElementCount = 200,
			MinOutstandingByteCount = 20_000_000,
			ScaleUpFactor = 2.0,
			ScaleDownFactor = 0.5,
			TargetUtilizationPercentage = 90.0,
			MemoryPressureThreshold = 85.0,
		};

		// Assert
		options.MaxOutstandingElementCount.ShouldBe(2000);
		options.MaxOutstandingByteCount.ShouldBe(200_000_000);
		options.EnableAdaptiveFlowControl.ShouldBeFalse();
		options.AdaptationInterval.ShouldBe(TimeSpan.FromSeconds(10));
		options.MinOutstandingElementCount.ShouldBe(200);
		options.MinOutstandingByteCount.ShouldBe(20_000_000);
		options.ScaleUpFactor.ShouldBe(2.0);
		options.ScaleDownFactor.ShouldBe(0.5);
		options.TargetUtilizationPercentage.ShouldBe(90.0);
		options.MemoryPressureThreshold.ShouldBe(85.0);
	}

	[Fact]
	public void ValidateThrowWhenMaxElementCountZero()
	{
		// Arrange
		var options = new PubSubFlowControlOptions { MaxOutstandingElementCount = 0 };

		// Act & Assert
		Should.Throw<ArgumentException>(() => options.Validate())
			.Message.ShouldContain("MaxOutstandingElementCount");
	}

	[Fact]
	public void ValidateThrowWhenMaxByteCountZero()
	{
		// Arrange
		var options = new PubSubFlowControlOptions { MaxOutstandingByteCount = 0 };

		// Act & Assert
		Should.Throw<ArgumentException>(() => options.Validate())
			.Message.ShouldContain("MaxOutstandingByteCount");
	}

	[Fact]
	public void ValidateThrowWhenMinElementCountExceedsMax()
	{
		// Arrange
		var options = new PubSubFlowControlOptions
		{
			MaxOutstandingElementCount = 100,
			MinOutstandingElementCount = 200,
		};

		// Act & Assert
		Should.Throw<ArgumentException>(() => options.Validate())
			.Message.ShouldContain("MinOutstandingElementCount");
	}

	[Fact]
	public void ValidateThrowWhenMinByteCountExceedsMax()
	{
		// Arrange
		var options = new PubSubFlowControlOptions
		{
			MaxOutstandingByteCount = 1000,
			MinOutstandingByteCount = 2000,
		};

		// Act & Assert
		Should.Throw<ArgumentException>(() => options.Validate())
			.Message.ShouldContain("MinOutstandingByteCount");
	}

	[Fact]
	public void ValidateThrowWhenAdaptationIntervalZero()
	{
		// Arrange
		var options = new PubSubFlowControlOptions { AdaptationInterval = TimeSpan.Zero };

		// Act & Assert
		Should.Throw<ArgumentException>(() => options.Validate())
			.Message.ShouldContain("AdaptationInterval");
	}

	[Fact]
	public void ValidateThrowWhenScaleUpFactorTooLow()
	{
		// Arrange
		var options = new PubSubFlowControlOptions { ScaleUpFactor = 1.0 };

		// Act & Assert
		Should.Throw<ArgumentException>(() => options.Validate())
			.Message.ShouldContain("ScaleUpFactor");
	}

	[Fact]
	public void ValidateThrowWhenScaleDownFactorTooHigh()
	{
		// Arrange
		var options = new PubSubFlowControlOptions { ScaleDownFactor = 1.0 };

		// Act & Assert
		Should.Throw<ArgumentException>(() => options.Validate())
			.Message.ShouldContain("ScaleDownFactor");
	}

	[Fact]
	public void ValidateThrowWhenTargetUtilizationTooHigh()
	{
		// Arrange
		var options = new PubSubFlowControlOptions { TargetUtilizationPercentage = 101.0 };

		// Act & Assert
		Should.Throw<ArgumentException>(() => options.Validate())
			.Message.ShouldContain("TargetUtilizationPercentage");
	}

	[Fact]
	public void ValidateThrowWhenMemoryPressureThresholdTooHigh()
	{
		// Arrange
		var options = new PubSubFlowControlOptions { MemoryPressureThreshold = 101.0 };

		// Act & Assert
		Should.Throw<ArgumentException>(() => options.Validate())
			.Message.ShouldContain("MemoryPressureThreshold");
	}

	[Fact]
	public void ValidateSucceedWithValidConfig()
	{
		// Arrange
		var options = new PubSubFlowControlOptions();

		// Act & Assert — should not throw
		options.Validate();
	}
}

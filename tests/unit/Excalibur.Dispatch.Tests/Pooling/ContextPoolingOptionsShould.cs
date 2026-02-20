// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Pooling;

namespace Excalibur.Dispatch.Tests.Pooling;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class ContextPoolingOptionsShould
{
	[Fact]
	public void DefaultValues_AreCorrect()
	{
		// Act
		var options = new ContextPoolingOptions();

		// Assert
		options.Enabled.ShouldBeFalse();
		options.MaxPoolSize.ShouldBe(Environment.ProcessorCount * 4);
		options.PreWarmCount.ShouldBe(0);
		options.TrackMetrics.ShouldBeFalse();
	}

	[Fact]
	public void AllProperties_AreSettable()
	{
		// Act
		var options = new ContextPoolingOptions
		{
			Enabled = true,
			MaxPoolSize = 64,
			PreWarmCount = 8,
			TrackMetrics = true,
		};

		// Assert
		options.Enabled.ShouldBeTrue();
		options.MaxPoolSize.ShouldBe(64);
		options.PreWarmCount.ShouldBe(8);
		options.TrackMetrics.ShouldBeTrue();
	}

	[Fact]
	public void MaxPoolSize_DefaultIsProcessorCountTimes4()
	{
		// Arrange
		var expected = Environment.ProcessorCount * 4;

		// Act
		var options = new ContextPoolingOptions();

		// Assert
		options.MaxPoolSize.ShouldBe(expected);
		options.MaxPoolSize.ShouldBeGreaterThan(0);
	}
}

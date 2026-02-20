// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Observability.Sampling;

namespace Excalibur.Dispatch.Middleware.Tests.Observability.Sampling;

/// <summary>
/// Unit tests for <see cref="TraceSamplerOptions"/>.
/// </summary>
[Trait("Category", "Unit")]
public sealed class TraceSamplerOptionsShould : UnitTestBase
{
	[Fact]
	public void Create_WithDefaults_HasExpectedValues()
	{
		// Arrange & Act
		var options = new TraceSamplerOptions();

		// Assert
		options.SamplingRatio.ShouldBe(1.0);
		options.Strategy.ShouldBe(SamplingStrategy.AlwaysOn);
	}

	[Fact]
	public void SamplingRatio_CanBeSet()
	{
		// Arrange
		var options = new TraceSamplerOptions { SamplingRatio = 0.5 };

		// Assert
		options.SamplingRatio.ShouldBe(0.5);
	}

	[Fact]
	public void Strategy_CanBeSet()
	{
		// Arrange
		var options = new TraceSamplerOptions { Strategy = SamplingStrategy.RatioBased };

		// Assert
		options.Strategy.ShouldBe(SamplingStrategy.RatioBased);
	}

	[Theory]
	[InlineData(SamplingStrategy.AlwaysOn)]
	[InlineData(SamplingStrategy.AlwaysOff)]
	[InlineData(SamplingStrategy.RatioBased)]
	[InlineData(SamplingStrategy.ParentBased)]
	public void Strategy_SupportsAllValues(SamplingStrategy strategy)
	{
		// Arrange
		var options = new TraceSamplerOptions { Strategy = strategy };

		// Assert
		options.Strategy.ShouldBe(strategy);
	}
}

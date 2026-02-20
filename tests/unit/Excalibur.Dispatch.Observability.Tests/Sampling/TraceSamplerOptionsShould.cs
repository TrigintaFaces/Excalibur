// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Observability.Sampling;

namespace Excalibur.Dispatch.Observability.Tests.Sampling;

/// <summary>
/// Unit tests for <see cref="TraceSamplerOptions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Observability")]
[Trait("Feature", "Sampling")]
public sealed class TraceSamplerOptionsShould
{
	[Fact]
	public void HaveDefaultSamplingRatioOfOne()
	{
		var options = new TraceSamplerOptions();
		options.SamplingRatio.ShouldBe(1.0);
	}

	[Fact]
	public void HaveDefaultStrategyOfAlwaysOn()
	{
		var options = new TraceSamplerOptions();
		options.Strategy.ShouldBe(SamplingStrategy.AlwaysOn);
	}

	[Fact]
	public void AllowSettingSamplingRatio()
	{
		var options = new TraceSamplerOptions { SamplingRatio = 0.5 };
		options.SamplingRatio.ShouldBe(0.5);
	}

	[Fact]
	public void AllowSettingStrategy()
	{
		var options = new TraceSamplerOptions { Strategy = SamplingStrategy.RatioBased };
		options.Strategy.ShouldBe(SamplingStrategy.RatioBased);
	}
}

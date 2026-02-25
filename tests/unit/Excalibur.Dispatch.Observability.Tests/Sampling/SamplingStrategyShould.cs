// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Observability.Sampling;

namespace Excalibur.Dispatch.Observability.Tests.Sampling;

/// <summary>
/// Unit tests for <see cref="SamplingStrategy"/> enum.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Observability")]
[Trait("Feature", "Sampling")]
public sealed class SamplingStrategyShould
{
	[Fact]
	public void HaveFourValues()
	{
		var values = Enum.GetValues<SamplingStrategy>();
		values.Length.ShouldBe(4);
	}

	[Fact]
	public void HaveExpectedValues()
	{
		Enum.IsDefined(SamplingStrategy.AlwaysOn).ShouldBeTrue();
		Enum.IsDefined(SamplingStrategy.AlwaysOff).ShouldBeTrue();
		Enum.IsDefined(SamplingStrategy.RatioBased).ShouldBeTrue();
		Enum.IsDefined(SamplingStrategy.ParentBased).ShouldBeTrue();
	}

	[Fact]
	public void HaveExpectedNumericValues()
	{
		((int)SamplingStrategy.AlwaysOn).ShouldBe(0);
		((int)SamplingStrategy.AlwaysOff).ShouldBe(1);
		((int)SamplingStrategy.RatioBased).ShouldBe(2);
		((int)SamplingStrategy.ParentBased).ShouldBe(3);
	}
}

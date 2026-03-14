// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Observability.Sampling;

namespace Excalibur.Dispatch.Middleware.Tests.Observability.Sampling;

[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class SamplingStrategyShould
{
	[Fact]
	public void HaveExpectedValues()
	{
		((int)SamplingStrategy.AlwaysOn).ShouldBe(0);
		((int)SamplingStrategy.AlwaysOff).ShouldBe(1);
		((int)SamplingStrategy.RatioBased).ShouldBe(2);
		((int)SamplingStrategy.ParentBased).ShouldBe(3);
	}

	[Fact]
	public void HaveExactlyFourMembers()
	{
		Enum.GetValues<SamplingStrategy>().Length.ShouldBe(4);
	}

	[Fact]
	public void DefaultToAlwaysOn()
	{
		default(SamplingStrategy).ShouldBe(SamplingStrategy.AlwaysOn);
	}
}

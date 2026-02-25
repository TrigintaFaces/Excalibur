// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Saga.Tests.Core;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class CompensationStrategyShould
{
	[Fact]
	public void DefineDefaultAsZero()
	{
		((int)CompensationStrategy.Default).ShouldBe(0);
	}

	[Fact]
	public void DefineRetryAsOne()
	{
		((int)CompensationStrategy.Retry).ShouldBe(1);
	}

	[Fact]
	public void DefineSkipAsTwo()
	{
		((int)CompensationStrategy.Skip).ShouldBe(2);
	}

	[Fact]
	public void DefineManualInterventionAsThree()
	{
		((int)CompensationStrategy.ManualIntervention).ShouldBe(3);
	}

	[Fact]
	public void HaveExactlyFourValues()
	{
		Enum.GetValues<CompensationStrategy>().Length.ShouldBe(4);
	}
}

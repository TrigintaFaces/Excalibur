// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Resilience.Polly;

namespace Excalibur.Dispatch.Tests.Resilience;

/// <summary>
/// Unit tests for resilience enum types: <see cref="DegradationLevel"/> and <see cref="JitterStrategy"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Resilience")]
public sealed class ResilienceEnumsShould : UnitTestBase
{
	#region DegradationLevel

	[Fact]
	public void DegradationLevel_Normal_HasValueZero()
	{
		((int)DegradationLevel.Normal).ShouldBe(0);
	}

	[Fact]
	public void DegradationLevel_Minor_HasValueOne()
	{
		((int)DegradationLevel.Minor).ShouldBe(1);
	}

	[Fact]
	public void DegradationLevel_Moderate_HasValueTwo()
	{
		((int)DegradationLevel.Moderate).ShouldBe(2);
	}

	[Fact]
	public void DegradationLevel_Major_HasValueThree()
	{
		((int)DegradationLevel.Major).ShouldBe(3);
	}

	[Fact]
	public void DegradationLevel_Severe_HasValueFour()
	{
		((int)DegradationLevel.Severe).ShouldBe(4);
	}

	[Fact]
	public void DegradationLevel_Emergency_HasValueFive()
	{
		((int)DegradationLevel.Emergency).ShouldBe(5);
	}

	[Fact]
	public void DegradationLevel_HasSixValues()
	{
		Enum.GetValues<DegradationLevel>().Length.ShouldBe(6);
	}

	[Fact]
	public void DegradationLevel_DefaultsToNormal()
	{
		default(DegradationLevel).ShouldBe(DegradationLevel.Normal);
	}

	[Fact]
	public void DegradationLevel_Ordering_NormalIsLessThanEmergency()
	{
		((int)DegradationLevel.Normal).ShouldBeLessThan((int)DegradationLevel.Emergency);
	}

	#endregion

	#region JitterStrategy

	[Fact]
	public void JitterStrategy_None_HasValueZero()
	{
		((int)JitterStrategy.None).ShouldBe(0);
	}

	[Fact]
	public void JitterStrategy_Full_HasValueOne()
	{
		((int)JitterStrategy.Full).ShouldBe(1);
	}

	[Fact]
	public void JitterStrategy_Equal_HasValueTwo()
	{
		((int)JitterStrategy.Equal).ShouldBe(2);
	}

	[Fact]
	public void JitterStrategy_Decorrelated_HasValueThree()
	{
		((int)JitterStrategy.Decorrelated).ShouldBe(3);
	}

	[Fact]
	public void JitterStrategy_Exponential_HasValueFour()
	{
		((int)JitterStrategy.Exponential).ShouldBe(4);
	}

	[Fact]
	public void JitterStrategy_HasFiveValues()
	{
		Enum.GetValues<JitterStrategy>().Length.ShouldBe(5);
	}

	[Fact]
	public void JitterStrategy_DefaultsToNone()
	{
		default(JitterStrategy).ShouldBe(JitterStrategy.None);
	}

	#endregion
}

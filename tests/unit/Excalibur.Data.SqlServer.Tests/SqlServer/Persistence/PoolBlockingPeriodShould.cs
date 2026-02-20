// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.SqlServer.Persistence;

namespace Excalibur.Data.Tests.SqlServer.Persistence;

/// <summary>
/// Unit tests for <see cref="PoolBlockingPeriod"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Data.SqlServer")]
[Trait("Feature", "Persistence")]
public sealed class PoolBlockingPeriodShould : UnitTestBase
{
	[Fact]
	public void HaveAutoAsDefaultValue()
	{
		// Assert
		((int)PoolBlockingPeriod.Auto).ShouldBe(0);
	}

	[Fact]
	public void HaveAlwaysBlockValue()
	{
		// Assert
		((int)PoolBlockingPeriod.AlwaysBlock).ShouldBe(1);
	}

	[Fact]
	public void HaveNeverBlockValue()
	{
		// Assert
		((int)PoolBlockingPeriod.NeverBlock).ShouldBe(2);
	}

	[Fact]
	public void HaveExpectedMemberCount()
	{
		// Assert
		Enum.GetValues<PoolBlockingPeriod>().Length.ShouldBe(3);
	}

	[Theory]
	[InlineData("Auto", PoolBlockingPeriod.Auto)]
	[InlineData("AlwaysBlock", PoolBlockingPeriod.AlwaysBlock)]
	[InlineData("NeverBlock", PoolBlockingPeriod.NeverBlock)]
	public void ParseFromString(string name, PoolBlockingPeriod expected)
	{
		// Act
		var result = Enum.Parse<PoolBlockingPeriod>(name);

		// Assert
		result.ShouldBe(expected);
	}

	[Fact]
	public void BeDefinedForAllValues()
	{
		// Assert
		Enum.IsDefined(PoolBlockingPeriod.Auto).ShouldBeTrue();
		Enum.IsDefined(PoolBlockingPeriod.AlwaysBlock).ShouldBeTrue();
		Enum.IsDefined(PoolBlockingPeriod.NeverBlock).ShouldBeTrue();
	}

	[Fact]
	public void DefaultToAuto()
	{
		// Arrange & Act
		var defaultValue = default(PoolBlockingPeriod);

		// Assert
		defaultValue.ShouldBe(PoolBlockingPeriod.Auto);
	}
}

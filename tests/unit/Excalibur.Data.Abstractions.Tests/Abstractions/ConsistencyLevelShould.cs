// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Abstractions.CloudNative;

namespace Excalibur.Data.Tests.Abstractions;

/// <summary>
/// Unit tests for <see cref="ConsistencyLevel"/> enum.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Data")]
[Trait("Feature", "Abstractions")]
public sealed class ConsistencyLevelShould : UnitTestBase
{
	[Fact]
	public void HaveFiveLevels()
	{
		// Act
		var values = Enum.GetValues<ConsistencyLevel>();

		// Assert
		values.Length.ShouldBe(5);
	}

	[Fact]
	public void HaveDefaultAsDefault()
	{
		// Assert
		ConsistencyLevel defaultValue = default;
		defaultValue.ShouldBe(ConsistencyLevel.Default);
	}

	[Theory]
	[InlineData(ConsistencyLevel.Default, 0)]
	[InlineData(ConsistencyLevel.Strong, 1)]
	[InlineData(ConsistencyLevel.Session, 2)]
	[InlineData(ConsistencyLevel.BoundedStaleness, 3)]
	[InlineData(ConsistencyLevel.Eventual, 4)]
	public void HaveCorrectUnderlyingValues(ConsistencyLevel level, int expectedValue)
	{
		// Assert
		((int)level).ShouldBe(expectedValue);
	}

	[Theory]
	[InlineData("Default", ConsistencyLevel.Default)]
	[InlineData("Strong", ConsistencyLevel.Strong)]
	[InlineData("Session", ConsistencyLevel.Session)]
	[InlineData("BoundedStaleness", ConsistencyLevel.BoundedStaleness)]
	[InlineData("Eventual", ConsistencyLevel.Eventual)]
	public void ParseFromString(string input, ConsistencyLevel expected)
	{
		// Act
		var result = Enum.Parse<ConsistencyLevel>(input);

		// Assert
		result.ShouldBe(expected);
	}

	[Fact]
	public void BeDefinedForAllValues()
	{
		// Act & Assert
		foreach (var level in Enum.GetValues<ConsistencyLevel>())
		{
			Enum.IsDefined(level).ShouldBeTrue();
		}
	}

	[Theory]
	[InlineData(ConsistencyLevel.Default, ConsistencyLevel.Strong)]
	[InlineData(ConsistencyLevel.Strong, ConsistencyLevel.Session)]
	[InlineData(ConsistencyLevel.Session, ConsistencyLevel.BoundedStaleness)]
	[InlineData(ConsistencyLevel.BoundedStaleness, ConsistencyLevel.Eventual)]
	public void SupportComparisonForOrdering(ConsistencyLevel weaker, ConsistencyLevel stronger)
	{
		// Assert - lower ordinal values come first
		((int)weaker).ShouldBeLessThan((int)stronger);
	}
}

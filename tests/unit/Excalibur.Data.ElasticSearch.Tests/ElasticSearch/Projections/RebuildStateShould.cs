// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.ElasticSearch.Projections;

namespace Excalibur.Data.Tests.ElasticSearch.Projections;

/// <summary>
/// Unit tests for the <see cref="RebuildState"/> enum.
/// </summary>
/// <remarks>
/// Sprint 513 (S513.2): Elasticsearch Phase 2 unit tests.
/// Tests verify enum values for projection rebuild states.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Elasticsearch")]
[Trait("Feature", "Projections")]
public sealed class RebuildStateShould
{
	#region Enum Value Tests

	[Fact]
	public void DefineQueuedAsZero()
	{
		// Assert
		((int)RebuildState.Queued).ShouldBe(0);
	}

	[Fact]
	public void DefineInProgressAsOne()
	{
		// Assert
		((int)RebuildState.InProgress).ShouldBe(1);
	}

	[Fact]
	public void DefinePausedAsTwo()
	{
		// Assert
		((int)RebuildState.Paused).ShouldBe(2);
	}

	[Fact]
	public void DefineCompletedAsThree()
	{
		// Assert
		((int)RebuildState.Completed).ShouldBe(3);
	}

	[Fact]
	public void DefineFailedAsFour()
	{
		// Assert
		((int)RebuildState.Failed).ShouldBe(4);
	}

	[Fact]
	public void DefineCancelledAsFive()
	{
		// Assert
		((int)RebuildState.Cancelled).ShouldBe(5);
	}

	#endregion

	#region Enum Count Tests

	[Fact]
	public void HaveSixDefinedValues()
	{
		// Act
		var values = Enum.GetValues<RebuildState>();

		// Assert
		values.Length.ShouldBe(6);
	}

	[Fact]
	public void ContainAllExpectedStates()
	{
		// Act
		var values = Enum.GetValues<RebuildState>();

		// Assert
		values.ShouldContain(RebuildState.Queued);
		values.ShouldContain(RebuildState.InProgress);
		values.ShouldContain(RebuildState.Paused);
		values.ShouldContain(RebuildState.Completed);
		values.ShouldContain(RebuildState.Failed);
		values.ShouldContain(RebuildState.Cancelled);
	}

	#endregion

	#region Enum Parse Tests

	[Theory]
	[InlineData("Queued", RebuildState.Queued)]
	[InlineData("InProgress", RebuildState.InProgress)]
	[InlineData("Paused", RebuildState.Paused)]
	[InlineData("Completed", RebuildState.Completed)]
	[InlineData("Failed", RebuildState.Failed)]
	[InlineData("Cancelled", RebuildState.Cancelled)]
	public void ParseFromString_WithValidName(string name, RebuildState expected)
	{
		// Act
		var result = Enum.Parse<RebuildState>(name);

		// Assert
		result.ShouldBe(expected);
	}

	[Theory]
	[InlineData("queued", RebuildState.Queued)]
	[InlineData("INPROGRESS", RebuildState.InProgress)]
	[InlineData("completed", RebuildState.Completed)]
	public void ParseFromString_WithCaseInsensitiveMatch(string name, RebuildState expected)
	{
		// Act
		var result = Enum.Parse<RebuildState>(name, ignoreCase: true);

		// Assert
		result.ShouldBe(expected);
	}

	#endregion

	#region State Category Tests

	[Theory]
	[InlineData(RebuildState.Queued)]
	[InlineData(RebuildState.InProgress)]
	[InlineData(RebuildState.Paused)]
	public void BeAnActiveState_WhenNotTerminal(RebuildState state)
	{
		// Assert - Non-terminal states are less than Completed
		((int)state).ShouldBeLessThan((int)RebuildState.Completed);
	}

	[Theory]
	[InlineData(RebuildState.Completed)]
	[InlineData(RebuildState.Failed)]
	[InlineData(RebuildState.Cancelled)]
	public void BeATerminalState_WhenFinal(RebuildState state)
	{
		// Assert - Terminal states are Completed or higher
		((int)state).ShouldBeGreaterThanOrEqualTo((int)RebuildState.Completed);
	}

	#endregion
}

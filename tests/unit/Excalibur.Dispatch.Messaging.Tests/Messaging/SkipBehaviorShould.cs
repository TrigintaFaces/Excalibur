// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Messaging;

namespace Excalibur.Dispatch.Tests.Messaging;

/// <summary>
/// Unit tests for <see cref="SkipBehavior"/> enum.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Dispatch")]
public sealed class SkipBehaviorShould
{
	[Fact]
	public void HaveThreeDistinctValues()
	{
		// Arrange
		var values = Enum.GetValues<SkipBehavior>();

		// Assert
		values.Length.ShouldBe(3);
		values.ShouldContain(SkipBehavior.Silent);
		values.ShouldContain(SkipBehavior.LogOnly);
		values.ShouldContain(SkipBehavior.ReturnSkippedResult);
	}

	[Fact]
	public void Silent_HasExpectedValue()
	{
		// Assert
		((int)SkipBehavior.Silent).ShouldBe(0);
	}

	[Fact]
	public void LogOnly_HasExpectedValue()
	{
		// Assert
		((int)SkipBehavior.LogOnly).ShouldBe(1);
	}

	[Fact]
	public void ReturnSkippedResult_HasExpectedValue()
	{
		// Assert
		((int)SkipBehavior.ReturnSkippedResult).ShouldBe(2);
	}

	[Fact]
	public void Silent_IsDefaultValue()
	{
		// Arrange
		SkipBehavior defaultBehavior = default;

		// Assert
		defaultBehavior.ShouldBe(SkipBehavior.Silent);
	}

	[Theory]
	[InlineData(SkipBehavior.Silent)]
	[InlineData(SkipBehavior.LogOnly)]
	[InlineData(SkipBehavior.ReturnSkippedResult)]
	public void BeDefinedForAllValues(SkipBehavior behavior)
	{
		// Assert
		Enum.IsDefined(behavior).ShouldBeTrue();
	}

	[Theory]
	[InlineData(0, SkipBehavior.Silent)]
	[InlineData(1, SkipBehavior.LogOnly)]
	[InlineData(2, SkipBehavior.ReturnSkippedResult)]
	public void CastFromInt_ReturnsCorrectValue(int value, SkipBehavior expected)
	{
		// Act
		var behavior = (SkipBehavior)value;

		// Assert
		behavior.ShouldBe(expected);
	}

	[Fact]
	public void HaveBehaviorsOrderedByVerbosity()
	{
		// Assert - Values should be ordered from least to most verbose
		(SkipBehavior.Silent < SkipBehavior.LogOnly).ShouldBeTrue();
		(SkipBehavior.LogOnly < SkipBehavior.ReturnSkippedResult).ShouldBeTrue();
	}
}

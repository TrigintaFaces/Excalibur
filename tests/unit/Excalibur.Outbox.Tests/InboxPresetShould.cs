// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Outbox.Tests;

/// <summary>
/// Unit tests for <see cref="InboxPreset"/> enum.
/// </summary>
[Trait("Category", "Unit")]
public sealed class InboxPresetShould : UnitTestBase
{
	[Fact]
	public void HighThroughput_HasValueZero()
	{
		((int)InboxPreset.HighThroughput).ShouldBe(0);
	}

	[Fact]
	public void Balanced_HasValueOne()
	{
		((int)InboxPreset.Balanced).ShouldBe(1);
	}

	[Fact]
	public void HighReliability_HasValueTwo()
	{
		((int)InboxPreset.HighReliability).ShouldBe(2);
	}

	[Fact]
	public void Custom_HasValueThree()
	{
		((int)InboxPreset.Custom).ShouldBe(3);
	}

	[Fact]
	public void DefaultValue_IsHighThroughput()
	{
		var preset = default(InboxPreset);
		preset.ShouldBe(InboxPreset.HighThroughput);
	}

	[Fact]
	public void Enum_HasExactlyFourMembers()
	{
		var values = Enum.GetValues<InboxPreset>();
		values.Length.ShouldBe(4);
	}

	[Theory]
	[InlineData("Custom", InboxPreset.Custom)]
	[InlineData("Balanced", InboxPreset.Balanced)]
	[InlineData("HighThroughput", InboxPreset.HighThroughput)]
	[InlineData("HighReliability", InboxPreset.HighReliability)]
	public void Parse_ValidString_ReturnsCorrectValue(string value, InboxPreset expected)
	{
		var result = Enum.Parse<InboxPreset>(value);
		result.ShouldBe(expected);
	}
}

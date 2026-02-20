// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Outbox.Tests;

/// <summary>
/// Unit tests for <see cref="OutboxPreset"/> enum.
/// </summary>
[Trait("Category", "Unit")]
public sealed class OutboxPresetShould : UnitTestBase
{
	[Fact]
	public void HighThroughput_HasValueZero()
	{
		((int)OutboxPreset.HighThroughput).ShouldBe(0);
	}

	[Fact]
	public void Balanced_HasValueOne()
	{
		((int)OutboxPreset.Balanced).ShouldBe(1);
	}

	[Fact]
	public void HighReliability_HasValueTwo()
	{
		((int)OutboxPreset.HighReliability).ShouldBe(2);
	}

	[Fact]
	public void Custom_HasValueThree()
	{
		((int)OutboxPreset.Custom).ShouldBe(3);
	}

	[Fact]
	public void DefaultValue_IsHighThroughput()
	{
		var preset = default(OutboxPreset);
		preset.ShouldBe(OutboxPreset.HighThroughput);
	}

	[Fact]
	public void Enum_HasExactlyFourMembers()
	{
		var values = Enum.GetValues<OutboxPreset>();
		values.Length.ShouldBe(4);
	}

	[Theory]
	[InlineData("Custom", OutboxPreset.Custom)]
	[InlineData("Balanced", OutboxPreset.Balanced)]
	[InlineData("HighThroughput", OutboxPreset.HighThroughput)]
	[InlineData("HighReliability", OutboxPreset.HighReliability)]
	public void Parse_ValidString_ReturnsCorrectValue(string value, OutboxPreset expected)
	{
		var result = Enum.Parse<OutboxPreset>(value);
		result.ShouldBe(expected);
	}
}

// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.RabbitMQ;

using Tests.Shared.Categories;

namespace Excalibur.Dispatch.Transport.Tests.RabbitMQ.Consumer;

/// <summary>
/// Unit tests for <see cref="AckMode"/> enum values.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Transport")]
[Trait("Pattern", "TRANSPORT")]
public sealed class AckModeShould : UnitTestBase
{
	[Fact]
	public void HaveAutoAsDefault()
	{
		var mode = default(AckMode);

		mode.ShouldBe(AckMode.Auto);
		((int)mode).ShouldBe(0);
	}

	[Fact]
	public void HaveExpectedEnumValues()
	{
		((int)AckMode.Auto).ShouldBe(0);
		((int)AckMode.Manual).ShouldBe(1);
		((int)AckMode.Batch).ShouldBe(2);
	}

	[Fact]
	public void SupportAllThreeModes()
	{
		var values = Enum.GetValues<AckMode>();

		values.Length.ShouldBe(3);
		values.ShouldContain(AckMode.Auto);
		values.ShouldContain(AckMode.Manual);
		values.ShouldContain(AckMode.Batch);
	}

	[Theory]
	[InlineData("Auto", AckMode.Auto)]
	[InlineData("Manual", AckMode.Manual)]
	[InlineData("Batch", AckMode.Batch)]
	public void ParseFromString(string name, AckMode expected)
	{
		var parsed = Enum.Parse<AckMode>(name);
		parsed.ShouldBe(expected);
	}
}

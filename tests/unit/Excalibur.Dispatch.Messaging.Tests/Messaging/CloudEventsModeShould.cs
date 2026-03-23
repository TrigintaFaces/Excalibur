// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

// Sprint 682 T.8: CloudEventsMode (plural) deleted. Tests now target canonical CloudEventMode (singular).
using Excalibur.Dispatch.CloudEvents;

namespace Excalibur.Dispatch.Tests.Messaging;

[Trait("Category", "Unit")]
[Trait("Component", "Messaging")]
[Trait("Priority", "0")]
public sealed class CloudEventsModeShould
{
	[Theory]
	[InlineData(CloudEventMode.Structured, 0)]
	[InlineData(CloudEventMode.Binary, 1)]
	public void HaveExpectedValues(CloudEventMode mode, int expected)
	{
		((int)mode).ShouldBe(expected);
	}

	[Fact]
	public void HaveTwoDefinedValues()
	{
		Enum.GetValues<CloudEventMode>().Length.ShouldBe(2);
	}

	[Fact]
	public void BePublicEnum()
	{
		typeof(CloudEventMode).IsPublic.ShouldBeTrue();
	}

	[Theory]
	[InlineData("Structured", CloudEventMode.Structured)]
	[InlineData("Binary", CloudEventMode.Binary)]
	public void ParseFromString(string input, CloudEventMode expected)
	{
		var result = Enum.Parse<CloudEventMode>(input);
		result.ShouldBe(expected);
	}

	[Fact]
	public void DefaultValueIsStructured()
	{
		CloudEventMode defaultValue = default;
		defaultValue.ShouldBe(CloudEventMode.Structured);
	}
}

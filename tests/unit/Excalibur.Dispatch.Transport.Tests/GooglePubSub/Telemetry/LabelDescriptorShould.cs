// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Google;

namespace Excalibur.Dispatch.Transport.Tests.GooglePubSub.Telemetry;

[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class LabelDescriptorShould
{
	[Fact]
	public void HaveCorrectDefaults()
	{
		// Arrange & Act
		var descriptor = new LabelDescriptor();

		// Assert
		descriptor.Key.ShouldBe(string.Empty);
		descriptor.ValueType.ShouldBe(LabelDescriptor.LabelValueTypes.ValueType.String);
		descriptor.Description.ShouldBe(string.Empty);
	}

	[Fact]
	public void AllowSettingAllProperties()
	{
		// Arrange & Act
		var descriptor = new LabelDescriptor
		{
			Key = "region",
			ValueType = LabelDescriptor.LabelValueTypes.ValueType.String,
			Description = "Cloud region",
		};

		// Assert
		descriptor.Key.ShouldBe("region");
		descriptor.ValueType.ShouldBe(LabelDescriptor.LabelValueTypes.ValueType.String);
		descriptor.Description.ShouldBe("Cloud region");
	}

	[Theory]
	[InlineData(LabelDescriptor.LabelValueTypes.ValueType.String, 0)]
	[InlineData(LabelDescriptor.LabelValueTypes.ValueType.Bool, 1)]
	[InlineData(LabelDescriptor.LabelValueTypes.ValueType.Int64, 2)]
	public void HaveCorrectLabelValueTypeValues(LabelDescriptor.LabelValueTypes.ValueType type, int expected)
	{
		((int)type).ShouldBe(expected);
	}

	[Fact]
	public void HaveAllLabelValueTypeMembers()
	{
		Enum.GetValues<LabelDescriptor.LabelValueTypes.ValueType>().Length.ShouldBe(3);
	}
}

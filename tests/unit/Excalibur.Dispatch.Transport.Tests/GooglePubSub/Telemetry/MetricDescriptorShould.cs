// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Google;

namespace Excalibur.Dispatch.Transport.Tests.GooglePubSub.Telemetry;

[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class MetricDescriptorShould
{
	[Fact]
	public void HaveCorrectDefaults()
	{
		// Arrange & Act
		var descriptor = new MetricDescriptor();

		// Assert
		descriptor.Type.ShouldBe(string.Empty);
		descriptor.DisplayName.ShouldBe(string.Empty);
		descriptor.Description.ShouldBe(string.Empty);
		descriptor.MetricKind.ShouldBe(MetricDescriptor.MetricDescriptorTypes.MetricKind.Unspecified);
		descriptor.ValueType.ShouldBe(MetricDescriptor.MetricDescriptorTypes.ValueType.Unspecified);
		descriptor.Unit.ShouldBe(string.Empty);
		descriptor.Labels.ShouldBeEmpty();
	}

	[Fact]
	public void AllowSettingAllProperties()
	{
		// Arrange & Act
		var descriptor = new MetricDescriptor
		{
			Type = "custom.googleapis.com/pubsub/messages_sent",
			DisplayName = "Messages Sent",
			Description = "Total number of messages sent",
			MetricKind = MetricDescriptor.MetricDescriptorTypes.MetricKind.Cumulative,
			ValueType = MetricDescriptor.MetricDescriptorTypes.ValueType.Int64,
			Unit = "1",
		};
		descriptor.Labels.Add(new LabelDescriptor { Key = "topic", Description = "Topic name" });

		// Assert
		descriptor.Type.ShouldBe("custom.googleapis.com/pubsub/messages_sent");
		descriptor.DisplayName.ShouldBe("Messages Sent");
		descriptor.Description.ShouldBe("Total number of messages sent");
		descriptor.MetricKind.ShouldBe(MetricDescriptor.MetricDescriptorTypes.MetricKind.Cumulative);
		descriptor.ValueType.ShouldBe(MetricDescriptor.MetricDescriptorTypes.ValueType.Int64);
		descriptor.Unit.ShouldBe("1");
		descriptor.Labels.Count.ShouldBe(1);
		descriptor.Labels[0].Key.ShouldBe("topic");
	}

	[Theory]
	[InlineData(MetricDescriptor.MetricDescriptorTypes.MetricKind.Unspecified, 0)]
	[InlineData(MetricDescriptor.MetricDescriptorTypes.MetricKind.Gauge, 1)]
	[InlineData(MetricDescriptor.MetricDescriptorTypes.MetricKind.Delta, 2)]
	[InlineData(MetricDescriptor.MetricDescriptorTypes.MetricKind.Cumulative, 3)]
	public void HaveCorrectMetricKindValues(MetricDescriptor.MetricDescriptorTypes.MetricKind kind, int expected)
	{
		((int)kind).ShouldBe(expected);
	}

	[Theory]
	[InlineData(MetricDescriptor.MetricDescriptorTypes.ValueType.Unspecified, 0)]
	[InlineData(MetricDescriptor.MetricDescriptorTypes.ValueType.Bool, 1)]
	[InlineData(MetricDescriptor.MetricDescriptorTypes.ValueType.Int64, 2)]
	[InlineData(MetricDescriptor.MetricDescriptorTypes.ValueType.Double, 3)]
	[InlineData(MetricDescriptor.MetricDescriptorTypes.ValueType.String, 4)]
	[InlineData(MetricDescriptor.MetricDescriptorTypes.ValueType.Distribution, 5)]
	[InlineData(MetricDescriptor.MetricDescriptorTypes.ValueType.Money, 6)]
	public void HaveCorrectValueTypeValues(MetricDescriptor.MetricDescriptorTypes.ValueType valueType, int expected)
	{
		((int)valueType).ShouldBe(expected);
	}
}

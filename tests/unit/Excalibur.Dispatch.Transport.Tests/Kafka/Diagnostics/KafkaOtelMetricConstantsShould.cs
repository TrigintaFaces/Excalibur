// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Kafka;

namespace Excalibur.Dispatch.Transport.Tests.Kafka.Diagnostics;

[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class KafkaOtelMetricConstantsShould
{
	[Fact]
	public void HaveCorrectInstrumentNames()
	{
		KafkaOtelMetricConstants.Instruments.MessagesProduced.ShouldBe("dispatch.kafka.messages.produced");
		KafkaOtelMetricConstants.Instruments.MessagesConsumed.ShouldBe("dispatch.kafka.messages.consumed");
		KafkaOtelMetricConstants.Instruments.ConsumerLag.ShouldBe("dispatch.kafka.consumer.lag");
		KafkaOtelMetricConstants.Instruments.PartitionCount.ShouldBe("dispatch.kafka.partition.count");
	}

	[Fact]
	public void HaveCorrectTagNames()
	{
		KafkaOtelMetricConstants.Tags.Topic.ShouldBe("kafka.topic");
		KafkaOtelMetricConstants.Tags.ConsumerGroup.ShouldBe("kafka.consumer_group");
		KafkaOtelMetricConstants.Tags.Partition.ShouldBe("kafka.partition");
	}
}

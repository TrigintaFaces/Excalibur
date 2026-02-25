// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Kafka;

namespace Excalibur.Dispatch.Transport.Tests.Kafka.Admin;

[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class TopicModelsShould
{
	[Fact]
	public void CreateTopicSpecificationWithRequiredProperties()
	{
		// Arrange & Act
		var spec = new TopicSpecification("my-topic", 6, 3);

		// Assert
		spec.Name.ShouldBe("my-topic");
		spec.Partitions.ShouldBe(6);
		spec.ReplicationFactor.ShouldBe((short)3);
		spec.Config.ShouldBeNull();
	}

	[Fact]
	public void CreateTopicSpecificationWithConfig()
	{
		// Arrange
		var config = new Dictionary<string, string> { ["retention.ms"] = "86400000" };

		// Act
		var spec = new TopicSpecification("my-topic", 3, 1, config);

		// Assert
		spec.Config.ShouldNotBeNull();
		spec.Config["retention.ms"].ShouldBe("86400000");
	}

	[Fact]
	public void CreateTopicDescription()
	{
		// Arrange
		var config = new Dictionary<string, string> { ["cleanup.policy"] = "compact" };

		// Act
		var desc = new TopicDescription("events", 12, 3, config, false);

		// Assert
		desc.Name.ShouldBe("events");
		desc.Partitions.ShouldBe(12);
		desc.ReplicationFactor.ShouldBe((short)3);
		desc.Config["cleanup.policy"].ShouldBe("compact");
		desc.IsInternal.ShouldBeFalse();
	}

	[Fact]
	public void CreateInternalTopicDescription()
	{
		// Arrange
		var config = new Dictionary<string, string>();

		// Act
		var desc = new TopicDescription("__consumer_offsets", 50, 3, config, true);

		// Assert
		desc.IsInternal.ShouldBeTrue();
	}

	[Fact]
	public void SupportRecordEquality()
	{
		// Arrange
		var spec1 = new TopicSpecification("topic-a", 3, 1);
		var spec2 = new TopicSpecification("topic-a", 3, 1);
		var spec3 = new TopicSpecification("topic-b", 3, 1);

		// Assert
		spec1.ShouldBe(spec2);
		spec1.ShouldNotBe(spec3);
	}
}

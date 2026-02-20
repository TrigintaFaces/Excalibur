// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Kafka;

namespace Excalibur.Dispatch.Transport.Tests.Kafka.CloudEvents;

[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class KafkaEnumsShould
{
	[Theory]
	[InlineData(KafkaAckLevel.None, 0)]
	[InlineData(KafkaAckLevel.Leader, 1)]
	[InlineData(KafkaAckLevel.All, 2)]
	public void HaveCorrectAckLevelValues(KafkaAckLevel level, int expected)
	{
		((int)level).ShouldBe(expected);
	}

	[Fact]
	public void HaveAllAckLevelMembers()
	{
		Enum.GetValues<KafkaAckLevel>().Length.ShouldBe(3);
	}

	[Theory]
	[InlineData(KafkaCompressionType.None, 0)]
	[InlineData(KafkaCompressionType.Gzip, 1)]
	[InlineData(KafkaCompressionType.Snappy, 2)]
	[InlineData(KafkaCompressionType.Lz4, 3)]
	[InlineData(KafkaCompressionType.Zstd, 4)]
	public void HaveCorrectCompressionTypeValues(KafkaCompressionType type, int expected)
	{
		((int)type).ShouldBe(expected);
	}

	[Fact]
	public void HaveAllCompressionTypeMembers()
	{
		Enum.GetValues<KafkaCompressionType>().Length.ShouldBe(5);
	}

	[Theory]
	[InlineData(KafkaOffsetReset.Earliest, 0)]
	[InlineData(KafkaOffsetReset.Latest, 1)]
	public void HaveCorrectOffsetResetValues(KafkaOffsetReset reset, int expected)
	{
		((int)reset).ShouldBe(expected);
	}

	[Fact]
	public void HaveAllOffsetResetMembers()
	{
		Enum.GetValues<KafkaOffsetReset>().Length.ShouldBe(2);
	}

	[Theory]
	[InlineData(KafkaPartitioningStrategy.CorrelationId, 0)]
	[InlineData(KafkaPartitioningStrategy.TenantId, 1)]
	[InlineData(KafkaPartitioningStrategy.UserId, 2)]
	[InlineData(KafkaPartitioningStrategy.Source, 3)]
	[InlineData(KafkaPartitioningStrategy.Type, 4)]
	[InlineData(KafkaPartitioningStrategy.EventId, 5)]
	[InlineData(KafkaPartitioningStrategy.RoundRobin, 6)]
	[InlineData(KafkaPartitioningStrategy.Custom, 7)]
	public void HaveCorrectPartitioningStrategyValues(KafkaPartitioningStrategy strategy, int expected)
	{
		((int)strategy).ShouldBe(expected);
	}

	[Fact]
	public void HaveAllPartitioningStrategyMembers()
	{
		Enum.GetValues<KafkaPartitioningStrategy>().Length.ShouldBe(8);
	}
}

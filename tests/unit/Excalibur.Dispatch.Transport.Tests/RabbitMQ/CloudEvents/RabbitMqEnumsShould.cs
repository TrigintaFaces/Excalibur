// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.RabbitMQ;

namespace Excalibur.Dispatch.Transport.Tests.RabbitMQ.CloudEvents;

[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class RabbitMqEnumsShould
{
	[Theory]
	[InlineData(RabbitMqExchangeType.Direct, 0)]
	[InlineData(RabbitMqExchangeType.Topic, 1)]
	[InlineData(RabbitMqExchangeType.Fanout, 2)]
	[InlineData(RabbitMqExchangeType.Headers, 3)]
	public void HaveCorrectExchangeTypeValues(RabbitMqExchangeType type, int expected)
	{
		((int)type).ShouldBe(expected);
	}

	[Fact]
	public void HaveAllExchangeTypeMembers()
	{
		Enum.GetValues<RabbitMqExchangeType>().Length.ShouldBe(4);
	}

	[Theory]
	[InlineData(RabbitMqPersistence.Transient, 0)]
	[InlineData(RabbitMqPersistence.Persistent, 1)]
	public void HaveCorrectPersistenceValues(RabbitMqPersistence persistence, int expected)
	{
		((int)persistence).ShouldBe(expected);
	}

	[Fact]
	public void HaveAllPersistenceMembers()
	{
		Enum.GetValues<RabbitMqPersistence>().Length.ShouldBe(2);
	}

	[Theory]
	[InlineData(RabbitMqRoutingStrategy.EventType, 0)]
	[InlineData(RabbitMqRoutingStrategy.Source, 1)]
	[InlineData(RabbitMqRoutingStrategy.Subject, 2)]
	[InlineData(RabbitMqRoutingStrategy.CorrelationId, 3)]
	[InlineData(RabbitMqRoutingStrategy.TenantId, 4)]
	[InlineData(RabbitMqRoutingStrategy.TypeAndSource, 5)]
	[InlineData(RabbitMqRoutingStrategy.Custom, 6)]
	[InlineData(RabbitMqRoutingStrategy.Static, 7)]
	public void HaveCorrectRoutingStrategyValues(RabbitMqRoutingStrategy strategy, int expected)
	{
		((int)strategy).ShouldBe(expected);
	}

	[Fact]
	public void HaveAllRoutingStrategyMembers()
	{
		Enum.GetValues<RabbitMqRoutingStrategy>().Length.ShouldBe(8);
	}
}

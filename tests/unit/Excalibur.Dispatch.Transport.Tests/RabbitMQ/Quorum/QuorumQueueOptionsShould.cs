// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.RabbitMQ;

using RabbitMqDeadLetterStrategy = Excalibur.Dispatch.Transport.RabbitMQ.DeadLetterStrategy;

namespace Excalibur.Dispatch.Transport.Tests.RabbitMQ.Quorum;

[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class QuorumQueueOptionsShould
{
	[Fact]
	public void HaveCorrectDefaults()
	{
		// Arrange & Act
		var options = new QuorumQueueOptions();

		// Assert
		options.DeliveryLimit.ShouldBeNull();
		options.DeadLetterStrategy.ShouldBe(RabbitMqDeadLetterStrategy.AtMostOnce);
		options.QuorumSize.ShouldBeNull();
	}

	[Fact]
	public void AllowSettingAllProperties()
	{
		// Arrange & Act
		var options = new QuorumQueueOptions
		{
			DeliveryLimit = 5,
			DeadLetterStrategy = RabbitMqDeadLetterStrategy.AtLeastOnce,
			QuorumSize = 3,
		};

		// Assert
		options.DeliveryLimit.ShouldBe(5);
		options.DeadLetterStrategy.ShouldBe(RabbitMqDeadLetterStrategy.AtLeastOnce);
		options.QuorumSize.ShouldBe(3);
	}

	[Theory]
	[InlineData(RabbitMqDeadLetterStrategy.AtMostOnce, 0)]
	[InlineData(RabbitMqDeadLetterStrategy.AtLeastOnce, 1)]
	public void HaveCorrectDeadLetterStrategyValues(RabbitMqDeadLetterStrategy strategy, int expected)
	{
		((int)strategy).ShouldBe(expected);
	}

	[Fact]
	public void HaveAllDeadLetterStrategyMembers()
	{
		Enum.GetValues<RabbitMqDeadLetterStrategy>().Length.ShouldBe(2);
	}
}

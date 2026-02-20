// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.RabbitMQ;

namespace Excalibur.Dispatch.Transport.Tests.RabbitMQ.CloudEvents;

[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class RabbitMqCloudEventOptionsShould
{
	[Fact]
	public void HaveCorrectDefaults()
	{
		// Arrange & Act
		var options = new RabbitMqCloudEventOptions();

		// Assert
		options.Consumer.ShouldNotBeNull();
		options.Publisher.ShouldNotBeNull();
		options.DefaultExchange.ShouldBe("cloudevents");
		options.ExchangeType.ShouldBe(RabbitMqExchangeType.Topic);
		options.RoutingStrategy.ShouldBe(RabbitMqRoutingStrategy.EventType);
		options.DefaultQueue.ShouldBeNull();
		options.DurableQueues.ShouldBeTrue();
		options.DurableExchanges.ShouldBeTrue();
		options.EnablePublisherConfirms.ShouldBeTrue();
		options.MandatoryPublishing.ShouldBeTrue();
		options.Persistence.ShouldBe(RabbitMqPersistence.Persistent);
		options.MessageTtl.ShouldBe(TimeSpan.FromDays(7));
		options.MaxMessageSizeBytes.ShouldBe(128 * 1024 * 1024);
		options.EnableDeadLetterExchange.ShouldBeTrue();
		options.DeadLetterExchange.ShouldBe("cloudevents.dlx");
		options.MaxRetryAttempts.ShouldBe(3);
		options.RetryDelay.ShouldBe(TimeSpan.FromSeconds(30));
		options.UseQuorumQueues.ShouldBeFalse();
		options.PrefetchCount.ShouldBe((ushort)10);
		options.EnableConsumerAcks.ShouldBeTrue();
		options.AutomaticRecoveryEnabled.ShouldBeTrue();
		options.NetworkRecoveryInterval.ShouldBe(TimeSpan.FromSeconds(5));
	}

	[Fact]
	public void AllowSettingAllProperties()
	{
		// Arrange & Act
		var options = new RabbitMqCloudEventOptions
		{
			DefaultExchange = "my-exchange",
			ExchangeType = RabbitMqExchangeType.Fanout,
			RoutingStrategy = RabbitMqRoutingStrategy.Subject,
			DefaultQueue = "my-queue",
			DurableQueues = false,
			DurableExchanges = false,
			EnablePublisherConfirms = false,
			MandatoryPublishing = false,
			Persistence = RabbitMqPersistence.Transient,
			MessageTtl = TimeSpan.FromHours(1),
			MaxMessageSizeBytes = 64 * 1024 * 1024,
			EnableDeadLetterExchange = false,
			DeadLetterExchange = "custom-dlx",
			MaxRetryAttempts = 5,
			RetryDelay = TimeSpan.FromMinutes(1),
			UseQuorumQueues = true,
			PrefetchCount = 50,
			EnableConsumerAcks = false,
			AutomaticRecoveryEnabled = false,
			NetworkRecoveryInterval = TimeSpan.FromSeconds(10),
		};

		// Assert
		options.DefaultExchange.ShouldBe("my-exchange");
		options.ExchangeType.ShouldBe(RabbitMqExchangeType.Fanout);
		options.RoutingStrategy.ShouldBe(RabbitMqRoutingStrategy.Subject);
		options.DefaultQueue.ShouldBe("my-queue");
		options.DurableQueues.ShouldBeFalse();
		options.DurableExchanges.ShouldBeFalse();
		options.EnablePublisherConfirms.ShouldBeFalse();
		options.MandatoryPublishing.ShouldBeFalse();
		options.Persistence.ShouldBe(RabbitMqPersistence.Transient);
		options.MessageTtl.ShouldBe(TimeSpan.FromHours(1));
		options.MaxMessageSizeBytes.ShouldBe(64 * 1024 * 1024);
		options.EnableDeadLetterExchange.ShouldBeFalse();
		options.DeadLetterExchange.ShouldBe("custom-dlx");
		options.MaxRetryAttempts.ShouldBe(5);
		options.RetryDelay.ShouldBe(TimeSpan.FromMinutes(1));
		options.UseQuorumQueues.ShouldBeTrue();
		options.PrefetchCount.ShouldBe((ushort)50);
		options.EnableConsumerAcks.ShouldBeFalse();
		options.AutomaticRecoveryEnabled.ShouldBeFalse();
		options.NetworkRecoveryInterval.ShouldBe(TimeSpan.FromSeconds(10));
	}

	[Fact]
	public void ExposeConsumerAndPublisherSubOptions()
	{
		// Arrange & Act
		var options = new RabbitMqCloudEventOptions();

		// Assert
		options.Consumer.ShouldBeOfType<RabbitMqConsumerOptions>();
		options.Publisher.ShouldBeOfType<RabbitMqPublisherOptions>();
	}
}

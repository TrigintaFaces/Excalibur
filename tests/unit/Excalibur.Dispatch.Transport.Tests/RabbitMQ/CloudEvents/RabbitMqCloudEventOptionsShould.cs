// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.RabbitMQ;

namespace Excalibur.Dispatch.Transport.Tests.RabbitMQ.CloudEvents;

[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Transport)]
public sealed class RabbitMqCloudEventOptionsShould
{
	[Fact]
	public void HaveCorrectDefaults()
	{
		// Arrange & Act
		var options = new RabbitMqCloudEventOptions();

		// Assert - Sub-option references
		options.Consumer.ShouldNotBeNull();
		options.Publisher.ShouldNotBeNull();
		options.Exchange.ShouldNotBeNull();
		options.DeadLetter.ShouldNotBeNull();
		options.Recovery.ShouldNotBeNull();

		// Assert - Root properties
		options.DefaultQueue.ShouldBeNull();
		options.DurableQueues.ShouldBeTrue();
		options.UseQuorumQueues.ShouldBeFalse();
		options.PrefetchCount.ShouldBe((ushort)10);
		options.EnableConsumerAcks.ShouldBeTrue();

		// Assert - Exchange sub-options
		options.Exchange.DefaultExchange.ShouldBe("cloudevents");
		options.Exchange.ExchangeType.ShouldBe(RabbitMQExchangeType.Topic);
		options.Exchange.RoutingStrategy.ShouldBe(RabbitMqRoutingStrategy.EventType);
		options.Exchange.DurableExchanges.ShouldBeTrue();
		options.Exchange.EnablePublisherConfirms.ShouldBeTrue();
		options.Exchange.MandatoryPublishing.ShouldBeTrue();
		options.Exchange.Persistence.ShouldBe(RabbitMqPersistence.Persistent);
		options.Exchange.MessageTtl.ShouldBe(TimeSpan.FromDays(7));
		options.Exchange.MaxMessageSizeBytes.ShouldBe(128 * 1024 * 1024);

		// Assert - DeadLetter sub-options
		options.DeadLetter.EnableDeadLetterExchange.ShouldBeTrue();
		options.DeadLetter.DeadLetterExchange.ShouldBe("cloudevents.dlx");
		options.DeadLetter.MaxRetryAttempts.ShouldBe(3);
		options.DeadLetter.RetryDelay.ShouldBe(TimeSpan.FromSeconds(30));

		// Assert - Recovery sub-options
		options.Recovery.AutomaticRecoveryEnabled.ShouldBeTrue();
		options.Recovery.NetworkRecoveryInterval.ShouldBe(TimeSpan.FromSeconds(5));
	}

	[Fact]
	public void AllowSettingAllProperties()
	{
		// Arrange & Act
		var options = new RabbitMqCloudEventOptions
		{
			DefaultQueue = "my-queue",
			DurableQueues = false,
			UseQuorumQueues = true,
			PrefetchCount = 50,
			EnableConsumerAcks = false,
			Exchange =
			{
				DefaultExchange = "my-exchange",
				ExchangeType = RabbitMQExchangeType.Fanout,
				RoutingStrategy = RabbitMqRoutingStrategy.Subject,
				DurableExchanges = false,
				EnablePublisherConfirms = false,
				MandatoryPublishing = false,
				Persistence = RabbitMqPersistence.Transient,
				MessageTtl = TimeSpan.FromHours(1),
				MaxMessageSizeBytes = 64 * 1024 * 1024,
			},
			DeadLetter =
			{
				EnableDeadLetterExchange = false,
				DeadLetterExchange = "custom-dlx",
				MaxRetryAttempts = 5,
				RetryDelay = TimeSpan.FromMinutes(1),
			},
			Recovery =
			{
				AutomaticRecoveryEnabled = false,
				NetworkRecoveryInterval = TimeSpan.FromSeconds(10),
			},
		};

		// Assert - Root properties
		options.DefaultQueue.ShouldBe("my-queue");
		options.DurableQueues.ShouldBeFalse();
		options.UseQuorumQueues.ShouldBeTrue();
		options.PrefetchCount.ShouldBe((ushort)50);
		options.EnableConsumerAcks.ShouldBeFalse();

		// Assert - Exchange sub-options
		options.Exchange.DefaultExchange.ShouldBe("my-exchange");
		options.Exchange.ExchangeType.ShouldBe(RabbitMQExchangeType.Fanout);
		options.Exchange.RoutingStrategy.ShouldBe(RabbitMqRoutingStrategy.Subject);
		options.Exchange.DurableExchanges.ShouldBeFalse();
		options.Exchange.EnablePublisherConfirms.ShouldBeFalse();
		options.Exchange.MandatoryPublishing.ShouldBeFalse();
		options.Exchange.Persistence.ShouldBe(RabbitMqPersistence.Transient);
		options.Exchange.MessageTtl.ShouldBe(TimeSpan.FromHours(1));
		options.Exchange.MaxMessageSizeBytes.ShouldBe(64 * 1024 * 1024);

		// Assert - DeadLetter sub-options
		options.DeadLetter.EnableDeadLetterExchange.ShouldBeFalse();
		options.DeadLetter.DeadLetterExchange.ShouldBe("custom-dlx");
		options.DeadLetter.MaxRetryAttempts.ShouldBe(5);
		options.DeadLetter.RetryDelay.ShouldBe(TimeSpan.FromMinutes(1));

		// Assert - Recovery sub-options
		options.Recovery.AutomaticRecoveryEnabled.ShouldBeFalse();
		options.Recovery.NetworkRecoveryInterval.ShouldBe(TimeSpan.FromSeconds(10));
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

	[Fact]
	public void ExposeNewSubOptionTypes()
	{
		// Arrange & Act
		var options = new RabbitMqCloudEventOptions();

		// Assert
		options.Exchange.ShouldBeOfType<RabbitMqCloudEventExchangeOptions>();
		options.DeadLetter.ShouldBeOfType<RabbitMqCloudEventDeadLetterOptions>();
		options.Recovery.ShouldBeOfType<RabbitMqCloudEventRecoveryOptions>();
	}
}

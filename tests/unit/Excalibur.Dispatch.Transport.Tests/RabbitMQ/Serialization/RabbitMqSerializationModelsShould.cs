// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.RabbitMQ;

namespace Excalibur.Dispatch.Transport.Tests.RabbitMQ.Serialization;

[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class RabbitMqSerializationModelsShould
{
	[Fact]
	public void CreateExchangeConfiguration()
	{
		// Arrange & Act
		var config = new ExchangeConfiguration("my-exchange", "topic", true, false);

		// Assert
		config.Name.ShouldBe("my-exchange");
		config.Type.ShouldBe("topic");
		config.Durable.ShouldBeTrue();
		config.AutoDelete.ShouldBeFalse();
		config.Arguments.ShouldBeNull();
	}

	[Fact]
	public void CreateExchangeConfigurationWithArguments()
	{
		// Arrange
		var args = new Dictionary<string, object> { ["alternate-exchange"] = "alt" };

		// Act
		var config = new ExchangeConfiguration("my-exchange", "direct", true, false, args);

		// Assert
		config.Arguments.ShouldNotBeNull();
		config.Arguments!["alternate-exchange"].ShouldBe("alt");
	}

	[Fact]
	public void SupportExchangeConfigurationRecordEquality()
	{
		var c1 = new ExchangeConfiguration("ex", "topic", true, false);
		var c2 = new ExchangeConfiguration("ex", "topic", true, false);
		c1.ShouldBe(c2);
	}

	[Fact]
	public void CreateQueueConfiguration()
	{
		// Arrange & Act
		var config = new QueueConfiguration("my-queue", true, false, false);

		// Assert
		config.Name.ShouldBe("my-queue");
		config.Durable.ShouldBeTrue();
		config.Exclusive.ShouldBeFalse();
		config.AutoDelete.ShouldBeFalse();
		config.Arguments.ShouldBeNull();
		config.MaxLength.ShouldBeNull();
		config.MaxLengthBytes.ShouldBeNull();
		config.DeadLetterExchange.ShouldBeNull();
		config.DeadLetterRoutingKey.ShouldBeNull();
		config.MessageTtl.ShouldBeNull();
		config.MaxPriority.ShouldBeNull();
	}

	[Fact]
	public void CreateQueueConfigurationWithAllOptions()
	{
		// Arrange & Act
		var config = new QueueConfiguration(
			"my-queue", true, false, false,
			MaxLength: 10000,
			MaxLengthBytes: 104857600,
			DeadLetterExchange: "dlx",
			DeadLetterRoutingKey: "dlq",
			MessageTtl: 60000,
			MaxPriority: 10);

		// Assert
		config.MaxLength.ShouldBe(10000);
		config.MaxLengthBytes.ShouldBe(104857600);
		config.DeadLetterExchange.ShouldBe("dlx");
		config.DeadLetterRoutingKey.ShouldBe("dlq");
		config.MessageTtl.ShouldBe(60000);
		config.MaxPriority.ShouldBe(10);
	}

	[Fact]
	public void CreateBindingConfiguration()
	{
		// Arrange & Act
		var binding = new BindingConfiguration("my-queue", "my-exchange", "order.*");

		// Assert
		binding.Queue.ShouldBe("my-queue");
		binding.Exchange.ShouldBe("my-exchange");
		binding.RoutingKey.ShouldBe("order.*");
		binding.Arguments.ShouldBeNull();
	}

	[Fact]
	public void CreateBindingConfigurationWithArguments()
	{
		// Arrange
		var args = new Dictionary<string, object> { ["x-match"] = "all" };

		// Act
		var binding = new BindingConfiguration("q", "ex", "#", args);

		// Assert
		binding.Arguments.ShouldNotBeNull();
		binding.Arguments!["x-match"].ShouldBe("all");
	}

	[Fact]
	public void CreateChannelState()
	{
		// Arrange & Act
		var state = new ChannelState(1, true, 10, 42UL, ["consumer-1", "consumer-2"]);

		// Assert
		state.ChannelNumber.ShouldBe(1);
		state.IsOpen.ShouldBeTrue();
		state.PrefetchCount.ShouldBe(10);
		state.NextPublishSeqNo.ShouldBe(42UL);
		state.ConsumerTags.Count.ShouldBe(2);
	}

	[Fact]
	public void CreateConnectionState()
	{
		// Arrange
		var now = DateTimeOffset.UtcNow;

		// Act
		var state = new ConnectionState("localhost:5672", true, "MyApp", 3, now);

		// Assert
		state.Endpoint.ShouldBe("localhost:5672");
		state.IsOpen.ShouldBeTrue();
		state.ClientProvidedName.ShouldBe("MyApp");
		state.ChannelCount.ShouldBe(3);
		state.ConnectedAt.ShouldBe(now);
	}

	[Fact]
	public void CreateConsumerState()
	{
		// Arrange & Act
		var state = new ConsumerState("tag-1", "my-queue", false, false, null, 20, true);

		// Assert
		state.ConsumerTag.ShouldBe("tag-1");
		state.QueueName.ShouldBe("my-queue");
		state.AutoAck.ShouldBeFalse();
		state.Exclusive.ShouldBeFalse();
		state.Arguments.ShouldBeNull();
		state.PrefetchCount.ShouldBe(20);
		state.IsActive.ShouldBeTrue();
	}

	[Fact]
	public void CreateDeadLetterConfiguration()
	{
		// Arrange & Act
		var config = new DeadLetterConfiguration("dlx", "dlq-routing", 3, TimeSpan.FromSeconds(30));

		// Assert
		config.Exchange.ShouldBe("dlx");
		config.RoutingKey.ShouldBe("dlq-routing");
		config.MaxRetries.ShouldBe(3);
		config.RetryInterval.ShouldBe(TimeSpan.FromSeconds(30));
	}

	[Fact]
	public void CreateRetryAttempt()
	{
		// Arrange
		var now = DateTimeOffset.UtcNow;

		// Act
		var attempt = new RetryAttempt(2, now, "Connection refused", TimeSpan.FromSeconds(10));

		// Assert
		attempt.AttemptNumber.ShouldBe(2);
		attempt.AttemptTime.ShouldBe(now);
		attempt.Error.ShouldBe("Connection refused");
		attempt.NextDelay.ShouldBe(TimeSpan.FromSeconds(10));
	}

	[Fact]
	public void CreateRetryAttemptWithNullError()
	{
		// Arrange & Act
		var attempt = new RetryAttempt(1, DateTimeOffset.UtcNow, null, TimeSpan.FromSeconds(5));

		// Assert
		attempt.Error.ShouldBeNull();
	}

	[Fact]
	public void CreateRetryConfiguration()
	{
		// Arrange & Act
		var config = new RetryConfiguration(5, TimeSpan.FromSeconds(1), TimeSpan.FromMinutes(1), 2.0);

		// Assert
		config.MaxAttempts.ShouldBe(5);
		config.InitialInterval.ShouldBe(TimeSpan.FromSeconds(1));
		config.MaxInterval.ShouldBe(TimeSpan.FromMinutes(1));
		config.Multiplier.ShouldBe(2.0);
		config.ExponentialBackoff.ShouldBeTrue(); // default
	}

	[Fact]
	public void CreateRetryConfigurationWithoutExponentialBackoff()
	{
		// Arrange & Act
		var config = new RetryConfiguration(3, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(15), 1.0, false);

		// Assert
		config.ExponentialBackoff.ShouldBeFalse();
	}

	[Fact]
	public void CreateRoutingInfo()
	{
		// Arrange & Act
		var info = new RoutingInfo("my-exchange", "order.created", true);

		// Assert
		info.Exchange.ShouldBe("my-exchange");
		info.RoutingKey.ShouldBe("order.created");
		info.Mandatory.ShouldBeTrue();
		info.Priority.ShouldBeNull();
		info.AlternateExchange.ShouldBeNull();
	}

	[Fact]
	public void CreateRoutingInfoWithAllOptions()
	{
		// Arrange & Act
		var info = new RoutingInfo("ex", "rk", false, 5, "alt-exchange");

		// Assert
		info.Priority.ShouldBe((byte)5);
		info.AlternateExchange.ShouldBe("alt-exchange");
	}
}

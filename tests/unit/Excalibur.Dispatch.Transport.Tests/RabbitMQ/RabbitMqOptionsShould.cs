using Excalibur.Dispatch.Transport.RabbitMQ;

namespace Excalibur.Dispatch.Transport.Tests.RabbitMQ;

/// <summary>
/// Unit tests for RabbitMqOptions configuration.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
public sealed class RabbitMqOptionsShould : UnitTestBase
{
	[Fact]
	public void Create_WithDefaults_HasExpectedDefaultValues()
	{
		// Arrange & Act
		var options = new RabbitMqOptions();

		// Assert - Root properties
		options.Exchange.ShouldBe(string.Empty);
		options.RoutingKey.ShouldBe(string.Empty);
		options.EnableEncryption.ShouldBeFalse();

		// Assert - Sub-option objects are initialized
		_ = options.Connection.ShouldNotBeNull();
		_ = options.Queue.ShouldNotBeNull();
		_ = options.DeadLetter.ShouldNotBeNull();
		_ = options.Consumption.ShouldNotBeNull();

		// Assert - Connection defaults
		options.Connection.ConnectionString.ShouldBe(string.Empty);
		options.Connection.ConnectionTimeoutSeconds.ShouldBe(30);
		options.Connection.AutomaticRecoveryEnabled.ShouldBeTrue();
		options.Connection.NetworkRecoveryIntervalSeconds.ShouldBe(10);

		// Assert - Queue defaults
		options.Queue.QueueName.ShouldBe(string.Empty);
		options.Queue.QueueDurable.ShouldBeTrue();
		options.Queue.QueueExclusive.ShouldBeFalse();
		options.Queue.QueueAutoDelete.ShouldBeFalse();
		_ = options.Queue.QueueArguments.ShouldNotBeNull();
		options.Queue.QueueArguments.ShouldBeEmpty();

		// Assert - DeadLetter defaults
		options.DeadLetter.EnableDeadLetterExchange.ShouldBeFalse();
		options.DeadLetter.DeadLetterExchange.ShouldBeNull();
		options.DeadLetter.DeadLetterRoutingKey.ShouldBeNull();
		options.DeadLetter.RequeueOnReject.ShouldBeFalse();

		// Assert - Consumption defaults
		options.Consumption.PrefetchCount.ShouldBe((ushort)100);
		options.Consumption.PrefetchGlobal.ShouldBeFalse();
		options.Consumption.AutoAck.ShouldBeFalse();
		options.Consumption.MaxBatchSize.ShouldBe(50);
		options.Consumption.MaxBatchWaitMs.ShouldBe(500);
		options.Consumption.ConsumerTag.ShouldBe("dispatch-consumer");
	}

	[Fact]
	public void ConnectionString_CanBeCustomized()
	{
		// Arrange
		var options = new RabbitMqOptions();

		// Act
		options.Connection.ConnectionString = "amqp://user:pass@localhost:5672";

		// Assert
		options.Connection.ConnectionString.ShouldBe("amqp://user:pass@localhost:5672");
	}

	[Fact]
	public void PrefetchCount_CanBeCustomized()
	{
		// Arrange
		var options = new RabbitMqOptions();

		// Act
		options.Consumption.PrefetchCount = 200;

		// Assert
		options.Consumption.PrefetchCount.ShouldBe((ushort)200);
	}

	[Fact]
	public void QueueDurable_CanBeDisabled()
	{
		// Arrange
		var options = new RabbitMqOptions();

		// Act
		options.Queue.QueueDurable = false;

		// Assert
		options.Queue.QueueDurable.ShouldBeFalse();
	}

	[Fact]
	public void AutoAck_CanBeEnabled()
	{
		// Arrange
		var options = new RabbitMqOptions();

		// Act
		options.Consumption.AutoAck = true;

		// Assert
		options.Consumption.AutoAck.ShouldBeTrue();
	}

	[Fact]
	public void MaxBatchSize_CanBeCustomized()
	{
		// Arrange
		var options = new RabbitMqOptions();

		// Act
		options.Consumption.MaxBatchSize = 100;

		// Assert
		options.Consumption.MaxBatchSize.ShouldBe(100);
	}

	[Fact]
	public void EnableDeadLetterExchange_CanBeEnabled()
	{
		// Arrange
		var options = new RabbitMqOptions();

		// Act
		options.DeadLetter.EnableDeadLetterExchange = true;
		options.DeadLetter.DeadLetterExchange = "dlx";
		options.DeadLetter.DeadLetterRoutingKey = "dlx-routing";

		// Assert
		options.DeadLetter.EnableDeadLetterExchange.ShouldBeTrue();
		options.DeadLetter.DeadLetterExchange.ShouldBe("dlx");
		options.DeadLetter.DeadLetterRoutingKey.ShouldBe("dlx-routing");
	}

	[Fact]
	public void AutomaticRecoveryEnabled_CanBeDisabled()
	{
		// Arrange
		var options = new RabbitMqOptions();

		// Act
		options.Connection.AutomaticRecoveryEnabled = false;

		// Assert
		options.Connection.AutomaticRecoveryEnabled.ShouldBeFalse();
	}

	[Fact]
	public void QueueArguments_CanAddArguments()
	{
		// Arrange
		var options = new RabbitMqOptions();

		// Act
		options.Queue.QueueArguments["x-message-ttl"] = 60000;

		// Assert
		options.Queue.QueueArguments.ShouldContainKey("x-message-ttl");
		options.Queue.QueueArguments["x-message-ttl"].ShouldBe(60000);
	}
}

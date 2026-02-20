using Excalibur.Dispatch.Transport.RabbitMQ;

namespace Excalibur.Dispatch.Transport.Tests.RabbitMQ;

/// <summary>
/// Unit tests for RabbitMqOptions configuration.
/// </summary>
[Trait("Category", "Unit")]
public sealed class RabbitMqOptionsShould : UnitTestBase
{
	[Fact]
	public void Create_WithDefaults_HasExpectedDefaultValues()
	{
		// Arrange & Act
		var options = new RabbitMqOptions();

		// Assert
		options.ConnectionString.ShouldBe(string.Empty);
		options.Exchange.ShouldBe(string.Empty);
		options.RoutingKey.ShouldBe(string.Empty);
		options.QueueName.ShouldBe(string.Empty);
		options.EnableEncryption.ShouldBeFalse();
		options.PrefetchCount.ShouldBe((ushort)100);
		options.PrefetchGlobal.ShouldBeFalse();
		options.QueueDurable.ShouldBeTrue();
		options.QueueExclusive.ShouldBeFalse();
		options.QueueAutoDelete.ShouldBeFalse();
		_ = options.QueueArguments.ShouldNotBeNull();
		options.QueueArguments.ShouldBeEmpty();
		options.AutoAck.ShouldBeFalse();
		options.MaxBatchSize.ShouldBe(50);
		options.MaxBatchWaitMs.ShouldBe(500);
		options.ConsumerTag.ShouldBe("dispatch-consumer");
		options.EnableDeadLetterExchange.ShouldBeFalse();
		options.DeadLetterExchange.ShouldBeNull();
		options.DeadLetterRoutingKey.ShouldBeNull();
		options.RequeueOnReject.ShouldBeFalse();
		options.ConnectionTimeoutSeconds.ShouldBe(30);
		options.AutomaticRecoveryEnabled.ShouldBeTrue();
		options.NetworkRecoveryIntervalSeconds.ShouldBe(10);
	}

	[Fact]
	public void ConnectionString_CanBeCustomized()
	{
		// Arrange
		var options = new RabbitMqOptions();

		// Act
		options.ConnectionString = "amqp://user:pass@localhost:5672";

		// Assert
		options.ConnectionString.ShouldBe("amqp://user:pass@localhost:5672");
	}

	[Fact]
	public void PrefetchCount_CanBeCustomized()
	{
		// Arrange
		var options = new RabbitMqOptions();

		// Act
		options.PrefetchCount = 200;

		// Assert
		options.PrefetchCount.ShouldBe((ushort)200);
	}

	[Fact]
	public void QueueDurable_CanBeDisabled()
	{
		// Arrange
		var options = new RabbitMqOptions();

		// Act
		options.QueueDurable = false;

		// Assert
		options.QueueDurable.ShouldBeFalse();
	}

	[Fact]
	public void AutoAck_CanBeEnabled()
	{
		// Arrange
		var options = new RabbitMqOptions();

		// Act
		options.AutoAck = true;

		// Assert
		options.AutoAck.ShouldBeTrue();
	}

	[Fact]
	public void MaxBatchSize_CanBeCustomized()
	{
		// Arrange
		var options = new RabbitMqOptions();

		// Act
		options.MaxBatchSize = 100;

		// Assert
		options.MaxBatchSize.ShouldBe(100);
	}

	[Fact]
	public void EnableDeadLetterExchange_CanBeEnabled()
	{
		// Arrange
		var options = new RabbitMqOptions();

		// Act
		options.EnableDeadLetterExchange = true;
		options.DeadLetterExchange = "dlx";
		options.DeadLetterRoutingKey = "dlx-routing";

		// Assert
		options.EnableDeadLetterExchange.ShouldBeTrue();
		options.DeadLetterExchange.ShouldBe("dlx");
		options.DeadLetterRoutingKey.ShouldBe("dlx-routing");
	}

	[Fact]
	public void AutomaticRecoveryEnabled_CanBeDisabled()
	{
		// Arrange
		var options = new RabbitMqOptions();

		// Act
		options.AutomaticRecoveryEnabled = false;

		// Assert
		options.AutomaticRecoveryEnabled.ShouldBeFalse();
	}

	[Fact]
	public void QueueArguments_CanAddArguments()
	{
		// Arrange
		var options = new RabbitMqOptions();

		// Act
		options.QueueArguments["x-message-ttl"] = 60000;

		// Assert
		options.QueueArguments.ShouldContainKey("x-message-ttl");
		options.QueueArguments["x-message-ttl"].ShouldBe(60000);
	}
}

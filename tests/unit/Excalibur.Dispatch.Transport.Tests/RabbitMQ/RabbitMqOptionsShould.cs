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

using Excalibur.Dispatch.Transport.RabbitMQ;

using FakeItEasy;

using RabbitMQ.Client;

using Tests.Shared.Categories;

namespace Excalibur.Dispatch.Transport.Tests.RabbitMQ;

/// <summary>
/// Unit tests for RabbitMqTopologyInitializer option wiring.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Transport")]
[Trait("Pattern", "TRANSPORT")]
public sealed class RabbitMqTopologyInitializerShould : UnitTestBase
{
	[Fact]
	public async Task EnsureInitializedAsync_AddsQuorumAndDeadLetterArguments()
	{
		var options = new RabbitMqOptions
		{
			Exchange = "dispatch.exchange",
			QueueName = "dispatch.queue",
			DeadLetterRoutingKey = "dlq-route",
		};
		var cloudEventOptions = new RabbitMqCloudEventOptions
		{
			DefaultExchange = "cloud.exchange",
			DefaultQueue = "cloud.queue",
			UseQuorumQueues = true,
			EnableDeadLetterExchange = true,
			DeadLetterExchange = "dispatch.dlx",
		};

		var channel = A.Fake<IChannel>();
		IDictionary<string, object?>? capturedArguments = null;

		_ = A.CallTo(() => channel.ExchangeDeclareAsync(
						A<string>._,
						A<string>._,
						A<bool>._,
						A<bool>._,
						A<IDictionary<string, object?>>._,
						A<bool>._,
						A<bool>._,
						A<CancellationToken>._))
				.Returns(Task.CompletedTask);

		_ = A.CallTo(() => channel.QueueDeclareAsync(
						A<string>._,
						A<bool>._,
						A<bool>._,
						A<bool>._,
						A<IDictionary<string, object?>>._,
						A<bool>._,
						A<bool>._,
						A<CancellationToken>._))
				.Invokes((
						string queueName,
						bool durable,
						bool exclusive,
						bool autoDelete,
						IDictionary<string, object?> arguments,
						bool passive,
						bool noWait,
						CancellationToken cancellationToken) =>
				{
					capturedArguments = arguments;
				})
				.Returns(Task.FromResult(new QueueDeclareOk(options.QueueName, 0, 0)));

		_ = A.CallTo(() => channel.QueueBindAsync(
						A<string>._,
						A<string>._,
						A<string>._,
						A<IDictionary<string, object?>>._,
						A<bool>._,
						A<CancellationToken>._))
				.Returns(Task.CompletedTask);

		var initializer = new RabbitMqTopologyInitializer(options, cloudEventOptions);

		await initializer.EnsureInitializedAsync(channel, CancellationToken.None).ConfigureAwait(false);

		_ = capturedArguments.ShouldNotBeNull();
		capturedArguments.ShouldContainKey("x-queue-type");
		capturedArguments["x-queue-type"].ShouldBe("quorum");
		capturedArguments.ShouldContainKey("x-dead-letter-exchange");
		capturedArguments["x-dead-letter-exchange"].ShouldBe("dispatch.dlx");
		capturedArguments.ShouldContainKey("x-dead-letter-routing-key");
		capturedArguments["x-dead-letter-routing-key"].ShouldBe("dlq-route");

		_ = A.CallTo(() => channel.ExchangeDeclareAsync(
						"dispatch.dlx",
						ExchangeType.Direct,
						A<bool>._,
						false,
						A<IDictionary<string, object?>>._,
						A<bool>._,
						A<bool>._,
						A<CancellationToken>._))
				.MustHaveHappenedOnceExactly();
	}
}

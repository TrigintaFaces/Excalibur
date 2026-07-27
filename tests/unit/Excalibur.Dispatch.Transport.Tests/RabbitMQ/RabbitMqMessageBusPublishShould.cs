using Excalibur.Dispatch;
using Excalibur.Dispatch.Serialization;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Transport.RabbitMQ;

using FakeItEasy;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

using RabbitMQ.Client;
using RabbitMQ.Client.Events;

using Tests.Shared.Categories;

using RabbitMqBasicProperties = RabbitMQ.Client.BasicProperties;

namespace Excalibur.Dispatch.Transport.Tests.RabbitMQ;

/// <summary>
/// Unit tests for RabbitMqMessageBus publish confirmation behavior.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Transport)]
[Trait("Pattern", "TRANSPORT")]
public sealed class RabbitMqMessageBusPublishShould : UnitTestBase
{
	[Fact]
	public async Task PublishAsync_Throws_WhenMandatoryPublishIsReturned()
	{
		var channel = A.Fake<IChannel>();
		var serializer = A.Fake<IPayloadSerializer>();
		var logger = NullLogger<RabbitMqMessageBus>.Instance;
		var options = new RabbitMqOptions { Exchange = "ex", RoutingKey = "rk" };
		var cloudOptions = new RabbitMqCloudEventOptions
		{
			Exchange = new RabbitMqCloudEventExchangeOptions
			{
				EnablePublisherConfirms = true,
				MandatoryPublishing = true,
			},
		};

		AsyncEventHandler<BasicReturnEventArgs>? returnHandler = null;
		_ = A.CallTo(channel)
				.Where(call => call.Method.Name == "add_BasicReturnAsync")
				.Invokes(call => returnHandler = call.Arguments[0] as AsyncEventHandler<BasicReturnEventArgs>);

		var action = A.Fake<IDispatchAction>();
		var context = new MessageContext(action, CreateServiceProvider());

		_ = A.CallTo(() => serializer.SerializeObject(A<object>._, A<Type>._))
				.Returns(new byte[] { 0x1 });

		_ = A.CallTo(() => channel.GetNextPublishSequenceNumberAsync(A<CancellationToken>._))
				.Returns(new ValueTask<ulong>(1));

		_ = A.CallTo(() => channel.BasicPublishAsync(
						A<string>._,
						A<string>._,
						A<bool>._,
						A<RabbitMqBasicProperties>._,
						A<ReadOnlyMemory<byte>>._,
						A<CancellationToken>._))
				.ReturnsLazily(call =>
				{
					var props = call.Arguments.Get<RabbitMqBasicProperties>(3);
					var body = call.Arguments.Get<ReadOnlyMemory<byte>>(4);

					var args = new BasicReturnEventArgs(
									replyCode: 312,
									replyText: "NO_ROUTE",
									exchange: options.Exchange,
									routingKey: options.RoutingKey,
									basicProperties: props,
									body: body,
									cancellationToken: CancellationToken.None);

					// Simulate the broker firing BasicReturn during publish and honor the async handler as
					// part of the publish's own completion. Awaiting it (rather than blocking on it via
					// GetAwaiter().GetResult()) is what lets the single-threaded test config drain the
					// handler's continuation instead of self-deadlocking the bus's publish (jetk8q).
					return returnHandler is null
						? ValueTask.CompletedTask
						: new ValueTask(returnHandler.Invoke(channel, args));
				});

		var bus = new RabbitMqMessageBus(
				channel,
				serializer,
				options,
				logger,
				cloudEventBridge: null,
				cloudEventMapper: null,
				cloudEventOptions: cloudOptions,
				topologyInitializer: null);

		var exception = await Should.ThrowAsync<InvalidOperationException>(
				() => bus.PublishAsync(action, context, CancellationToken.None));

		exception.Message.ShouldContain("NO_ROUTE");
	}

	private static IServiceProvider CreateServiceProvider()
	{
		var services = new ServiceCollection();
		return services.BuildServiceProvider();
	}
}

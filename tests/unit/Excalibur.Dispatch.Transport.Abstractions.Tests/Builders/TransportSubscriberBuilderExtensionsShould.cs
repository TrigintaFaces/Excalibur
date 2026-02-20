using System.Diagnostics;
using System.Diagnostics.Metrics;

using Excalibur.Dispatch.Transport;
using Excalibur.Dispatch.Transport.Builders;
using Excalibur.Dispatch.Transport.Decorators;

namespace Excalibur.Dispatch.Transport.Abstractions.Tests.Builders;

[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class TransportSubscriberBuilderExtensionsShould : IDisposable
{
	private readonly Meter _meter = new("test.transport.subscriber.extensions");
	private readonly ActivitySource _activitySource = new("test.transport.subscriber.extensions");

	public void Dispose()
	{
		_activitySource.Dispose();
		_meter.Dispose();
		GC.SuppressFinalize(this);
	}

	[Fact]
	public void UseTelemetry_AddsTelemetryDecorator()
	{
		var inner = A.Fake<ITransportSubscriber>();
		var builder = new TransportSubscriberBuilder(inner);

		var built = builder
			.UseTelemetry("Kafka", _meter, _activitySource)
			.Build();

		built.ShouldBeOfType<TelemetryTransportSubscriber>();
	}

	[Fact]
	public void UseDeadLetterQueue_AddsDeadLetterDecorator()
	{
		var inner = A.Fake<ITransportSubscriber>();
		var builder = new TransportSubscriberBuilder(inner);
		Func<TransportReceivedMessage, string?, CancellationToken, Task> deadLetterHandler =
			(_, _, _) => Task.CompletedTask;

		var built = builder
			.UseDeadLetterQueue("Kafka", deadLetterHandler, _meter)
			.Build();

		built.ShouldBeOfType<DeadLetterTransportSubscriber>();
	}
}

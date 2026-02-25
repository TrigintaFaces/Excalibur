using System.Diagnostics;
using System.Diagnostics.Metrics;

using Excalibur.Dispatch.Transport;
using Excalibur.Dispatch.Transport.Builders;
using Excalibur.Dispatch.Transport.Decorators;

namespace Excalibur.Dispatch.Transport.Abstractions.Tests.Builders;

[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class TransportReceiverBuilderExtensionsShould : IDisposable
{
	private readonly Meter _meter = new("test.transport.receiver.extensions");
	private readonly ActivitySource _activitySource = new("test.transport.receiver.extensions");

	public void Dispose()
	{
		_activitySource.Dispose();
		_meter.Dispose();
		GC.SuppressFinalize(this);
	}

	[Fact]
	public void UseTelemetry_AddsTelemetryDecorator()
	{
		var inner = A.Fake<ITransportReceiver>();
		var builder = new TransportReceiverBuilder(inner);

		var built = builder
			.UseTelemetry("Kafka", _meter, _activitySource)
			.Build();

		built.ShouldBeOfType<TelemetryTransportReceiver>();
	}

	[Fact]
	public void UseDeadLetterQueue_AddsDeadLetterDecorator()
	{
		var inner = A.Fake<ITransportReceiver>();
		var builder = new TransportReceiverBuilder(inner);
		Func<TransportReceivedMessage, string?, CancellationToken, Task> deadLetterHandler =
			(_, _, _) => Task.CompletedTask;

		var built = builder
			.UseDeadLetterQueue("Kafka", deadLetterHandler, _meter)
			.Build();

		built.ShouldBeOfType<DeadLetterTransportReceiver>();
	}

	[Fact]
	public void UseCloudEvents_AddsCloudEventsDecorator()
	{
		var inner = A.Fake<ITransportReceiver>();
		var mapper = A.Fake<ICloudEventMapper<TransportReceivedMessage>>();
		var builder = new TransportReceiverBuilder(inner);

		var built = builder
			.UseCloudEvents(mapper, m => m)
			.Build();

		built.ShouldBeOfType<CloudEventsTransportReceiver>();
	}
}

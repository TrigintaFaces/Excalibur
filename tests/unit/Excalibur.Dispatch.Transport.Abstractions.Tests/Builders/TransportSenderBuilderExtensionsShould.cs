using System.Diagnostics;
using System.Diagnostics.Metrics;

using CloudNative.CloudEvents;

using Excalibur.Dispatch.Transport;
using Excalibur.Dispatch.Transport.Builders;
using Excalibur.Dispatch.Transport.Decorators;

namespace Excalibur.Dispatch.Transport.Abstractions.Tests.Builders;

[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class TransportSenderBuilderExtensionsShould : IDisposable
{
	private readonly Meter _meter = new("test.transport.sender.extensions");
	private readonly ActivitySource _activitySource = new("test.transport.sender.extensions");

	public void Dispose()
	{
		_activitySource.Dispose();
		_meter.Dispose();
		GC.SuppressFinalize(this);
	}

	[Fact]
	public void UseTelemetry_AddsTelemetryDecorator()
	{
		var inner = A.Fake<ITransportSender>();
		var builder = new TransportSenderBuilder(inner);

		var built = builder
			.UseTelemetry("Kafka", _meter, _activitySource)
			.Build();

		built.ShouldBeOfType<TelemetryTransportSender>();
	}

	[Fact]
	public void UseOrdering_AddsOrderingDecorator()
	{
		var inner = A.Fake<ITransportSender>();
		var builder = new TransportSenderBuilder(inner);

		var built = builder
			.UseOrdering(m => m.Id)
			.Build();

		built.ShouldBeOfType<OrderingTransportSender>();
	}

	[Fact]
	public void UseDeduplication_AddsDeduplicationDecorator()
	{
		var inner = A.Fake<ITransportSender>();
		var builder = new TransportSenderBuilder(inner);

		var built = builder
			.UseDeduplication(m => m.Id)
			.Build();

		built.ShouldBeOfType<DeduplicationTransportSender>();
	}

	[Fact]
	public void UseScheduling_AddsSchedulingDecorator()
	{
		var inner = A.Fake<ITransportSender>();
		var builder = new TransportSenderBuilder(inner);

		var built = builder
			.UseScheduling(_ => DateTimeOffset.UtcNow.AddMinutes(1))
			.Build();

		built.ShouldBeOfType<SchedulingTransportSender>();
	}

	[Fact]
	public void UseCloudEvents_AddsCloudEventsDecorator()
	{
		var inner = A.Fake<ITransportSender>();
		var mapper = A.Fake<ICloudEventMapper<TransportMessage>>();
		var builder = new TransportSenderBuilder(inner);

		var built = builder
			.UseCloudEvents(
				mapper,
				_ => new CloudEvent
				{
					Type = "dispatch.test",
					Source = new Uri("urn:test"),
				})
			.Build();

		built.ShouldBeOfType<CloudEventsTransportSender>();
	}
}

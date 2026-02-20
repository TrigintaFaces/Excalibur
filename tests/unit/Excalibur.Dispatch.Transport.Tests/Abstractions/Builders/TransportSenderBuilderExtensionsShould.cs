// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;
using System.Diagnostics.Metrics;

using CloudNative.CloudEvents;

using Excalibur.Dispatch.Transport;
using Excalibur.Dispatch.Transport.Builders;
using Excalibur.Dispatch.Transport.Decorators;

namespace Excalibur.Dispatch.Transport.Tests.Abstractions.Builders;

/// <summary>
/// Tests for <see cref="TransportSenderBuilderExtensions"/>.
/// Verifies UseTelemetry(), UseOrdering(), UseDeduplication(), UseScheduling(), and UseCloudEvents().
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class TransportSenderBuilderExtensionsShould : IDisposable
{
	private readonly ITransportSender _innerSender = A.Fake<ITransportSender>();
	private readonly Meter _meter = new("Excalibur.Dispatch.Transport.SenderExtTest", "1.0.0");
	private readonly ActivitySource _activitySource = new("Excalibur.Dispatch.Transport.SenderExtTest");
	private bool _disposed;

	[Fact]
	public void UseTelemetry_Wraps_With_TelemetryTransportSender()
	{
		var result = new TransportSenderBuilder(_innerSender)
			.UseTelemetry("Kafka", _meter, _activitySource)
			.Build();

		result.ShouldBeOfType<TelemetryTransportSender>();
	}

	[Fact]
	public void UseOrdering_Wraps_With_OrderingTransportSender()
	{
		var result = new TransportSenderBuilder(_innerSender)
			.UseOrdering(_ => "key")
			.Build();

		result.ShouldBeOfType<OrderingTransportSender>();
	}

	[Fact]
	public void UseDeduplication_Wraps_With_DeduplicationTransportSender()
	{
		var result = new TransportSenderBuilder(_innerSender)
			.UseDeduplication(_ => "dedup-id")
			.Build();

		result.ShouldBeOfType<DeduplicationTransportSender>();
	}

	[Fact]
	public void UseScheduling_Wraps_With_SchedulingTransportSender()
	{
		var result = new TransportSenderBuilder(_innerSender)
			.UseScheduling(_ => DateTimeOffset.UtcNow.AddMinutes(5))
			.Build();

		result.ShouldBeOfType<SchedulingTransportSender>();
	}

	[Fact]
	public void UseCloudEvents_Wraps_With_CloudEventsTransportSender()
	{
		var mapper = A.Fake<ICloudEventMapper<TransportMessage>>();
		Func<TransportMessage, CloudEvent> factory = _ => new CloudEvent(CloudEventsSpecVersion.V1_0)
		{
			Id = "ce-1",
			Type = "test.event",
			Source = new Uri("urn:test"),
		};

		var result = new TransportSenderBuilder(_innerSender)
			.UseCloudEvents(mapper, factory)
			.Build();

		result.ShouldBeOfType<CloudEventsTransportSender>();
	}

	[Fact]
	public void Stacking_Order_Preserved()
	{
		var mapper = A.Fake<ICloudEventMapper<TransportMessage>>();
		Func<TransportMessage, CloudEvent> factory = _ => new CloudEvent(CloudEventsSpecVersion.V1_0)
		{
			Id = "ce-1",
			Type = "test.event",
			Source = new Uri("urn:test"),
		};

		var result = new TransportSenderBuilder(_innerSender)
			.UseTelemetry("Kafka", _meter, _activitySource)
			.UseOrdering(_ => "key")
			.UseCloudEvents(mapper, factory)
			.Build();

		// Outermost should be CloudEvents (last registered)
		result.ShouldBeOfType<CloudEventsTransportSender>();
	}

	[Fact]
	public void Returns_Builder_For_Chaining()
	{
		var builder = new TransportSenderBuilder(_innerSender);

		builder.UseTelemetry("Kafka", _meter, _activitySource).ShouldBeSameAs(builder);
		builder.UseOrdering(_ => "key").ShouldBeSameAs(builder);
		builder.UseDeduplication(_ => "id").ShouldBeSameAs(builder);
		builder.UseScheduling(_ => null).ShouldBeSameAs(builder);
	}

	public void Dispose()
	{
		if (_disposed) return;
		_disposed = true;
		_meter.Dispose();
		_activitySource.Dispose();
	}
}

// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;
using System.Diagnostics.Metrics;

using Excalibur.Dispatch.Transport;
using Excalibur.Dispatch.Transport.Builders;
using Excalibur.Dispatch.Transport.Decorators;

namespace Excalibur.Dispatch.Transport.Tests.Abstractions.Builders;

/// <summary>
/// Tests for <see cref="TransportReceiverBuilderExtensions"/>.
/// Verifies UseTelemetry(), UseDeadLetterQueue(), and UseCloudEvents() extension methods.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class TransportReceiverBuilderExtensionsShould : IDisposable
{
	private readonly ITransportReceiver _innerReceiver = A.Fake<ITransportReceiver>();
	private readonly Meter _meter = new("Excalibur.Dispatch.Transport.ReceiverExtTest", "1.0.0");
	private readonly ActivitySource _activitySource = new("Excalibur.Dispatch.Transport.ReceiverExtTest");
	private bool _disposed;

	[Fact]
	public void UseTelemetry_Wraps_With_TelemetryTransportReceiver()
	{
		var result = new TransportReceiverBuilder(_innerReceiver)
			.UseTelemetry("Kafka", _meter, _activitySource)
			.Build();

		result.ShouldBeOfType<TelemetryTransportReceiver>();
	}

	[Fact]
	public void UseDeadLetterQueue_Wraps_With_DeadLetterTransportReceiver()
	{
		var result = new TransportReceiverBuilder(_innerReceiver)
			.UseDeadLetterQueue("test-transport", (_, _, _) => Task.CompletedTask)
			.Build();

		result.ShouldBeOfType<DeadLetterTransportReceiver>();
	}

	[Fact]
	public void UseDeadLetterQueue_With_Meter_Wraps_With_DeadLetterTransportReceiver()
	{
		var result = new TransportReceiverBuilder(_innerReceiver)
			.UseDeadLetterQueue("test-transport", (_, _, _) => Task.CompletedTask, _meter)
			.Build();

		result.ShouldBeOfType<DeadLetterTransportReceiver>();
	}

	[Fact]
	public void UseCloudEvents_Wraps_With_CloudEventsTransportReceiver()
	{
		var mapper = A.Fake<ICloudEventMapper<TransportReceivedMessage>>();
		var result = new TransportReceiverBuilder(_innerReceiver)
			.UseCloudEvents(mapper)
			.Build();

		result.ShouldBeOfType<CloudEventsTransportReceiver>();
	}

	[Fact]
	public void Stacking_Order_Preserved_Telemetry_Then_DLQ_Then_CloudEvents()
	{
		var mapper = A.Fake<ICloudEventMapper<TransportReceivedMessage>>();
		var result = new TransportReceiverBuilder(_innerReceiver)
			.UseTelemetry("Kafka", _meter, _activitySource)
			.UseDeadLetterQueue("test-transport", (_, _, _) => Task.CompletedTask)
			.UseCloudEvents(mapper)
			.Build();

		// Outermost should be CloudEvents (last registered)
		result.ShouldBeOfType<CloudEventsTransportReceiver>();
	}

	[Fact]
	public void UseTelemetry_Returns_Builder_For_Chaining()
	{
		var builder = new TransportReceiverBuilder(_innerReceiver);
		var returned = builder.UseTelemetry("Kafka", _meter, _activitySource);

		returned.ShouldBeSameAs(builder);
	}

	[Fact]
	public void UseDeadLetterQueue_Returns_Builder_For_Chaining()
	{
		var builder = new TransportReceiverBuilder(_innerReceiver);
		var returned = builder.UseDeadLetterQueue("test-transport", (_, _, _) => Task.CompletedTask);

		returned.ShouldBeSameAs(builder);
	}

	public void Dispose()
	{
		if (_disposed) return;
		_disposed = true;
		_meter.Dispose();
		_activitySource.Dispose();
	}
}

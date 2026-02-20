// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;
using System.Diagnostics.Metrics;

using Excalibur.Dispatch.Transport;
using Excalibur.Dispatch.Transport.Builders;
using Excalibur.Dispatch.Transport.Decorators;

namespace Excalibur.Dispatch.Transport.Tests.Abstractions.Builders;

/// <summary>
/// Tests for <see cref="TransportSubscriberBuilderExtensions"/>.
/// Verifies UseTelemetry() and UseDeadLetterQueue() extension methods.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class TransportSubscriberBuilderExtensionsShould : IDisposable
{
	private readonly ITransportSubscriber _innerSubscriber = A.Fake<ITransportSubscriber>();
	private readonly Meter _meter = new("Excalibur.Dispatch.Transport.ExtTest", "1.0.0");
	private readonly ActivitySource _activitySource = new("Excalibur.Dispatch.Transport.ExtTest");
	private bool _disposed;

	[Fact]
	public void UseTelemetry_Wraps_With_TelemetryTransportSubscriber()
	{
		var result = new TransportSubscriberBuilder(_innerSubscriber)
			.UseTelemetry("Kafka", _meter, _activitySource)
			.Build();

		result.ShouldBeOfType<TelemetryTransportSubscriber>();
	}

	[Fact]
	public void UseDeadLetterQueue_Wraps_With_DeadLetterTransportSubscriber()
	{
		var result = new TransportSubscriberBuilder(_innerSubscriber)
			.UseDeadLetterQueue("test-transport", (_, _, _) => Task.CompletedTask)
			.Build();

		result.ShouldBeOfType<DeadLetterTransportSubscriber>();
	}

	[Fact]
	public void UseDeadLetterQueue_With_Meter_Wraps_With_DeadLetterTransportSubscriber()
	{
		var result = new TransportSubscriberBuilder(_innerSubscriber)
			.UseDeadLetterQueue("test-transport", (_, _, _) => Task.CompletedTask, _meter)
			.Build();

		result.ShouldBeOfType<DeadLetterTransportSubscriber>();
	}

	[Fact]
	public void UseTelemetry_Then_UseDeadLetterQueue_Stacks_Correctly()
	{
		var result = new TransportSubscriberBuilder(_innerSubscriber)
			.UseTelemetry("Kafka", _meter, _activitySource)
			.UseDeadLetterQueue("test-transport", (_, _, _) => Task.CompletedTask)
			.Build();

		// Outermost should be DLQ (last registered)
		result.ShouldBeOfType<DeadLetterTransportSubscriber>();
	}

	[Fact]
	public void UseTelemetry_Returns_Builder_For_Chaining()
	{
		var builder = new TransportSubscriberBuilder(_innerSubscriber);
		var returned = builder.UseTelemetry("Kafka", _meter, _activitySource);

		returned.ShouldBeSameAs(builder);
	}

	[Fact]
	public void UseDeadLetterQueue_Returns_Builder_For_Chaining()
	{
		var builder = new TransportSubscriberBuilder(_innerSubscriber);
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

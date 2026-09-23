// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using CloudNative.CloudEvents;

using Excalibur.Dispatch.CloudEvents;
using Excalibur.Dispatch.Testing.Transport;
using Excalibur.Dispatch.Transport;
using Excalibur.Dispatch.Transport.RabbitMQ;

using Microsoft.Extensions.DependencyInjection;

using RabbitMQ.Client;

using RabbitMqBasicProperties = RabbitMQ.Client.BasicProperties;

namespace Excalibur.Dispatch.Transport.Tests.CloudEvents;

/// <summary>
/// Runs the CloudEvents transport conformance suite against the RabbitMQ transport.
/// </summary>
/// <remarks>
/// <para>
/// Every assertion lives in the kit. This class supplies only the three RabbitMQ-specific things: the
/// receiver a consumer resolves, a way to put a message in front of it, and this transport's own
/// encoding.
/// </para>
/// <para>
/// <b>No infrastructure.</b> The channel is faked and the registration is otherwise exactly what a
/// consumer gets — the receiver is built by the transport's own DI factory and carries the decoding
/// decoration that factory applies, so what is under test is the path a consumer runs rather than a
/// receiver assembled by this fixture.
/// </para>
/// <para>
/// <b>The encoder is RESOLVED, not constructed</b>, for the reason the sibling suites give: constructing
/// an encoder by hand tests an encoder nobody runs. CloudEvents is opt-in here, so the fixture makes
/// both calls a CloudEvents consumer makes — the transport registration and the CloudEvents binding.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class RabbitMqCloudEventConformanceTests : CloudEventTransportConformanceTests, IAsyncDisposable
{
	private const string TransportName = "rabbitmq-conformance";

	private readonly List<BasicGetResult> _inbound = [];

	private ServiceProvider? _provider;

	/// <inheritdoc />
	protected override Task<ITransportReceiver> CreateReceiverAsync() =>
		Task.FromResult(BuildProvider().GetRequiredKeyedService<ITransportReceiver>(TransportName));

	/// <inheritdoc />
	protected override Task SeedMessagesAsync(
		ITransportReceiver receiver, IReadOnlyList<TransportReceivedMessage> messages)
	{
		_inbound.Clear();

		var tag = 1UL;

		foreach (var message in messages)
		{
			var headers = new Dictionary<string, object?>(StringComparer.Ordinal);

			foreach (var property in message.Properties)
			{
				// RabbitMQ carries header values as byte arrays on the wire; the receiver decodes them
				// back to strings, so seeding them any other way would test a shape no broker produces.
				headers[property.Key] = System.Text.Encoding.UTF8.GetBytes(property.Value?.ToString() ?? string.Empty);
			}

			var properties = new RabbitMqBasicProperties
			{
				MessageId = message.Id,
				ContentType = message.ContentType,
				Headers = headers,
			};

			_inbound.Add(new BasicGetResult(
				deliveryTag: tag++,
				redelivered: false,
				exchange: string.Empty,
				routingKey: TransportName,
				messageCount: 0,
				basicProperties: properties,
				body: message.Body.ToArray()));
		}

		return Task.CompletedTask;
	}

	/// <inheritdoc />
	protected override async Task<TransportReceivedMessage> EncodeAsync(
		CloudEvent cloudEvent, CloudEventMode mode)
	{
		var encoder = BuildProvider()
			.GetRequiredService<ICloudEventEncoder<(IBasicProperties Properties, ReadOnlyMemory<byte> Body)>>();

		var (properties, body) = await encoder.ToTransportMessageAsync(cloudEvent, mode, CancellationToken.None);

		var decoded = new Dictionary<string, object>(StringComparer.Ordinal);

		foreach (var header in properties.Headers ?? new Dictionary<string, object?>(StringComparer.Ordinal))
		{
			decoded[header.Key] = header.Value is byte[] raw
				? System.Text.Encoding.UTF8.GetString(raw)
				: header.Value ?? string.Empty;
		}

		return new TransportReceivedMessage
		{
			Id = "rabbitmq-wire-1",
			Body = body,
			// Structured mode is identified by its MEDIA TYPE, which this transport carries on the message
			// envelope rather than among the headers. Dropping it here produces a false RED that reads
			// exactly like a decode failure.
			ContentType = properties.ContentType,
			EnqueuedAt = DateTimeOffset.UtcNow,
			Properties = decoded,
		};
	}

	private ServiceProvider BuildProvider()
	{
		if (_provider is not null)
		{
			return _provider;
		}

		var channel = A.Fake<IChannel>();

		_ = A.CallTo(() => channel.BasicGetAsync(A<string>._, A<bool>._, A<CancellationToken>._))
			.ReturnsLazily(() =>
			{
				if (_inbound.Count == 0)
				{
					// The receiver drains until the broker has nothing ready; a null result is how it
					// learns that, exactly as BasicGetAsync reports an empty queue.
					return Task.FromResult<BasicGetResult?>(null);
				}

				var next = _inbound[0];
				_inbound.RemoveAt(0);
				return Task.FromResult<BasicGetResult?>(next);
			});

		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddSingleton(channel);
		// A connection string is REQUIRED by this transport's options validator, and supplying one is part
		// of testing the consumer's path rather than a way around it: a consumer cannot register this
		// transport without one either, and the validator rejects the guest/guest default. Nothing
		// connects — the channel above is a fake and no broker is contacted.
		_ = services.AddRabbitMQTransport(TransportName, rabbit =>
			_ = rabbit.ConnectionString("amqp://conformance:conformance@localhost:5672/"));
		_ = services.AddCloudEventsForRabbitMq();

		_provider = services.BuildServiceProvider();

		return _provider;
	}

	/// <summary>
	/// LIVENESS. Binary-mode round trip through this transport's own send and receive paths.
	/// </summary>
	[Fact]
	public Task Round_trip_a_binary_mode_event() =>
		VerifyBinaryModeRoundTripsThroughItsOwnReceiver();

	/// <summary>
	/// LIVENESS. Structured mode is a different wire shape and a different decode path.
	/// </summary>
	[Fact]
	public Task Round_trip_a_structured_mode_event() =>
		VerifyStructuredModeRoundTripsThroughItsOwnReceiver();

	/// <summary>
	/// SAFETY. Ordinary RabbitMQ traffic is not reported as a CloudEvent.
	/// </summary>
	[Fact]
	public Task Leave_ordinary_traffic_alone() =>
		VerifyOrdinaryTrafficIsNotReportedAsCloudEvent();

	/// <summary>
	/// SAFETY. A mode this transport declines is not decoded anyway.
	/// </summary>
	[Fact]
	public Task Decline_no_mode_it_cannot_decode() =>
		VerifyDeclinedModesDoNotDecode();

	/// <inheritdoc />
	/// <remarks>
	/// ASYNC disposal: the container resolves a receiver decorator implementing only
	/// <see cref="IAsyncDisposable"/>, so a synchronous Dispose throws in teardown.
	/// </remarks>
	public async ValueTask DisposeAsync()
	{
		if (_provider is not null)
		{
			await _provider.DisposeAsync();
		}

		GC.SuppressFinalize(this);
	}
}

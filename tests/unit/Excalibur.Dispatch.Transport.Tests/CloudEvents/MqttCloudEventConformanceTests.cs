// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Buffers;

using CloudNative.CloudEvents;

using Excalibur.Dispatch.CloudEvents;
using Excalibur.Dispatch.Testing.Transport;
using Excalibur.Dispatch.Transport;
using Excalibur.Dispatch.Transport.Mqtt;

using Microsoft.Extensions.DependencyInjection;

using MQTTnet;
using MQTTnet.Packets;

namespace Excalibur.Dispatch.Transport.Tests.CloudEvents;

/// <summary>
/// Runs the CloudEvents transport conformance suite against the MQTT transport.
/// </summary>
/// <remarks>
/// <para>
/// Every assertion lives in the kit. This class supplies only the three MQTT-specific things: the
/// receiver a consumer resolves, a way to put a message in front of it, and this transport's own
/// encoding.
/// </para>
/// <para>
/// <b>No infrastructure.</b> The connection provider is faked and registered under the transport's key
/// first, which the transport's own <c>TryAddKeyedSingleton</c> honours — so every other part of the
/// wiring is the consumer's, including the decode decoration the receiver factory applies.
/// </para>
/// <para>
/// <b>Seeding rides the subscribe call rather than a separate seam.</b> This receiver pulls from an
/// internal buffer that only its own message handler writes, and it attaches that handler and then
/// subscribes on the first receive — so a message published before the subscribe is lost, and one
/// published after it would need the receive to already be blocked. Raising the client's
/// received-message event from inside the faked <c>SubscribeAsync</c> puts the message in front of the
/// receiver at the one moment both are true.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class MqttCloudEventConformanceTests : CloudEventTransportConformanceTests, IAsyncDisposable
{
	private const string TransportName = "mqtt-conformance";

	private readonly List<MqttApplicationMessage> _inbound = [];

	private ServiceProvider? _provider;

	/// <inheritdoc />
	protected override Task<ITransportReceiver> CreateReceiverAsync() =>
		Task.FromResult(BuildProvider().GetRequiredKeyedService<ITransportReceiver>(TransportName));

	/// <inheritdoc />
	protected override Task SeedMessagesAsync(
		ITransportReceiver receiver, IReadOnlyList<TransportReceivedMessage> messages)
	{
		_inbound.Clear();

		foreach (var message in messages)
		{
			var wire = new MqttApplicationMessage
			{
				Topic = TransportName,
				ContentType = message.ContentType,
				Payload = new ReadOnlySequence<byte>(message.Body.ToArray()),
			};

			// MQTT v5 carries the binary-mode ce-* attributes as user properties, which is where this
			// transport's receiver reads them from; seeding them anywhere else would test a shape no
			// broker produces.
			foreach (var property in message.Properties)
			{
				wire.UserProperties ??= [];
				wire.UserProperties.Add(
					new MqttUserProperty(property.Key, property.Value?.ToString() ?? string.Empty));
			}

			_inbound.Add(wire);
		}

		return Task.CompletedTask;
	}

	/// <inheritdoc />
	protected override async Task<TransportReceivedMessage> EncodeAsync(
		CloudEvent cloudEvent, CloudEventMode mode)
	{
		var encoder = BuildProvider().GetRequiredService<ICloudEventEncoder<MqttApplicationMessage>>();

		var encoded = await encoder.ToTransportMessageAsync(cloudEvent, mode, CancellationToken.None);

		var properties = new Dictionary<string, object>(StringComparer.Ordinal);

		foreach (var property in encoded.UserProperties ?? [])
		{
			properties[property.Name] = property.Value;
		}

		return new TransportReceivedMessage
		{
			Id = "mqtt-wire-1",
			Body = encoded.Payload.ToArray(),
			// Structured mode is identified by its MEDIA TYPE, which this transport carries on the message
			// envelope rather than among the user properties. Dropping it here produces a false RED that
			// reads exactly like a decode failure.
			ContentType = encoded.ContentType,
			EnqueuedAt = DateTimeOffset.UtcNow,
			Properties = properties,
		};
	}

	private ServiceProvider BuildProvider()
	{
		if (_provider is not null)
		{
			return _provider;
		}

		var client = A.Fake<IMqttClient>();

		_ = A.CallTo(() => client.SubscribeAsync(A<MqttClientSubscribeOptions>._, A<CancellationToken>._))
			.Invokes(() =>
			{
				// The handler is attached immediately before this call, so this is the first instant a
				// published message can reach the receiver's buffer.
				foreach (var message in _inbound)
				{
					var args = new MqttApplicationMessageReceivedEventArgs(
						"mqtt-conformance-client",
						message,
						new MqttPublishPacket(),
						static (_, _) => Task.CompletedTask);

					client.ApplicationMessageReceivedAsync +=
						Raise.FreeForm<Func<MqttApplicationMessageReceivedEventArgs, Task>>.With(args);
				}

				_inbound.Clear();
			});

		var connections = A.Fake<IMqttConnectionProvider>();
		_ = A.CallTo(() => connections.CreateClient()).Returns(client);

		var services = new ServiceCollection();
		_ = services.AddLogging();
		// Registered FIRST so the transport's own TryAddKeyedSingleton stands down; nothing else about the
		// registration is substituted, and no broker is contacted.
		_ = services.AddKeyedSingleton(TransportName, connections);

		_ = services.AddMqttTransport(TransportName, mqtt =>
		{
			// The options validator rejects each of these when empty, so supplying them is part of
			// testing the consumer's path rather than a way around it.
			mqtt.Host = "localhost";
			mqtt.ClientId = "conformance";
			mqtt.Topic = TransportName;
			mqtt.RequireTls = false;
		});

		_ = services.AddCloudEventsForMqtt();

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
	/// SAFETY. Ordinary MQTT traffic is not reported as a CloudEvent.
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

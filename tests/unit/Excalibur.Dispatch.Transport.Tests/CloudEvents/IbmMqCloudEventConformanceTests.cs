// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using CloudNative.CloudEvents;

using Excalibur.Dispatch.CloudEvents;
using Excalibur.Dispatch.Testing.Transport;
using Excalibur.Dispatch.Transport;
using Excalibur.Dispatch.Transport.IbmMq;

using IBM.WMQ;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Transport.Tests.CloudEvents;

/// <summary>
/// Runs the CloudEvents transport conformance suite against the IBM MQ transport.
/// </summary>
/// <remarks>
/// <para>
/// <b>No broker.</b> The queue-manager connection is faked and registered under the transport's key first,
/// which the transport's own <c>TryAddKeyedSingleton</c> honours — so every other part of the wiring is the
/// consumer's, including the decode decoration the receiver factory applies. Until the connection seam
/// existed this transport could not be enrolled at all: the provider handed back a concrete queue-manager
/// type whose constructor connects, so there was nothing to substitute and its decode path had never been
/// observed.
/// </para>
/// <para>
/// <b>Seeding goes through a real message object and the sender's own property-setting path.</b> The
/// message is a data carrier constructed in process, so using the real one costs nothing and keeps the
/// platform's own rules in play — including the one that decides whether this transport can carry the
/// attribute names its registered binding probes for. A fixture that stored properties in a dictionary of
/// its own would answer a question about the dictionary.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class IbmMqCloudEventConformanceTests : CloudEventTransportConformanceTests, IAsyncDisposable
{
	private const string TransportName = "ibmmq-conformance";

	private readonly Queue<TransportReceivedMessage> _inbound = new();

	private ServiceProvider? _provider;

	/// <inheritdoc />
	/// <remarks>
	/// Both modes, because the registration binds both: the sender carries the shared structured encoder
	/// and the receiver is decorated with a binding that names a binary attribute spelling. Narrowing this
	/// to structured would describe the encoder and stay silent about the decoder, which is the half that
	/// decides what a third-party publisher can send this transport.
	/// </remarks>
	protected override IReadOnlyCollection<CloudEventMode> SupportedModes =>
		[CloudEventMode.Binary, CloudEventMode.Structured];

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
			_inbound.Enqueue(message);
		}

		return Task.CompletedTask;
	}

	/// <inheritdoc />
	protected override async Task<TransportReceivedMessage> EncodeAsync(
		CloudEvent cloudEvent, CloudEventMode mode) =>
		mode == CloudEventMode.Structured
			? await EncodeStructuredAsync(cloudEvent)
			: EncodeBinary(cloudEvent);

	/// <summary>
	/// Structured mode, produced by the shared encoding decorator this transport's sender factory applies.
	/// </summary>
	private async Task<TransportReceivedMessage> EncodeStructuredAsync(CloudEvent cloudEvent)
	{
		TransportMessage? captured = null;

		var inner = A.Fake<ITransportSender>();
		_ = A.CallTo(() => inner.SendAsync(A<TransportMessage>._, A<CancellationToken>._))
			.Invokes((TransportMessage sent, CancellationToken _) => captured = sent)
			.Returns(Task.FromResult(SendResult.Success("ibmmq-conformance-1")));

		var encoding = inner.WithCloudEventEncoding();

		var outbound = new TransportMessage
		{
			Body = Payload,
			Properties = new Dictionary<string, object>(StringComparer.Ordinal)
			{
				["cloudevent"] = cloudEvent,
			},
		};

		_ = await encoding.SendAsync(outbound, CancellationToken.None);

		var wire = captured ?? outbound;

		return new TransportReceivedMessage
		{
			Id = "ibmmq-wire-1",
			Body = wire.Body,
			// Structured mode is identified by its MEDIA TYPE, which this transport carries in a message
			// property rather than on the envelope. Dropping it here produces a false RED that reads exactly
			// like a decode failure.
			ContentType = wire.ContentType,
			EnqueuedAt = DateTimeOffset.UtcNow,
			Properties = wire.Properties
				.Where(static pair => pair.Value is not CloudEvent)
				.ToDictionary(static pair => pair.Key, static pair => pair.Value, StringComparer.Ordinal),
		};
	}

	/// <summary>
	/// Binary mode, spelled the way this transport's REGISTERED BINDING says binary attributes are named.
	/// </summary>
	/// <remarks>
	/// <para>
	/// This transport ships no binary encoder of its own, so the wire shape it must be able to read is the
	/// one its registration declares it reads — nothing else can arrive and be recognised. Taking the prefix
	/// from the binding rather than writing a literal is what stops this fixture and the decoder drifting
	/// into agreement with each other instead of with the transport.
	/// </para>
	/// <para>
	/// The body is left as the plain payload: binary mode puts the event's data in the body and each
	/// attribute in its own metadata entry.
	/// </para>
	/// </remarks>
	private TransportReceivedMessage EncodeBinary(CloudEvent cloudEvent)
	{
		var prefix = IbmMqRegisteredBinding.AttributePrefixes[0];

		var properties = new Dictionary<string, object>(StringComparer.Ordinal)
		{
			[prefix + "specversion"] = cloudEvent.SpecVersion.VersionId,
			[prefix + "id"] = cloudEvent.Id!,
			[prefix + "type"] = cloudEvent.Type!,
			[prefix + "source"] = cloudEvent.Source!.ToString(),
		};

		if (cloudEvent.DataContentType is { } dataContentType)
		{
			properties[prefix + "datacontenttype"] = dataContentType;
		}

		return new TransportReceivedMessage
		{
			Id = "ibmmq-wire-1",
			Body = Payload,
			EnqueuedAt = DateTimeOffset.UtcNow,
			Properties = properties,
		};
	}

	/// <summary>
	/// The binding the transport's own registration hands its decoding decorator.
	/// </summary>
	/// <remarks>
	/// <b>This restates the registration by hand, so it must be changed with it.</b> The arms encode using
	/// this prefix and the receiver decodes using whatever the registration actually passes; if the two
	/// drift apart the binary arm fails, which is the correct outcome and names the wrong cause. It reads
	/// <see cref="CloudEventBinding.IbmMq"/> rather than the house convention because IBM MQ refuses a
	/// hyphenated property name outright — the seeding below drops it exactly as a real queue does, so the
	/// house spelling produced a message carrying no attributes at all.
	/// </remarks>
	private static CloudEventBinding IbmMqRegisteredBinding => CloudEventBinding.IbmMq;

	private ServiceProvider BuildProvider()
	{
		if (_provider is not null)
		{
			return _provider;
		}

		var queue = A.Fake<IIbmMqQueue>();
		_ = A.CallTo(() => queue.Get(A<MQMessage>._, A<MQGetMessageOptions>._))
			.Invokes((MQMessage message, MQGetMessageOptions _) => Deliver(message));

		var queueManager = A.Fake<IIbmMqQueueManager>();
		_ = A.CallTo(() => queueManager.AccessQueue(A<string>._, A<int>._)).Returns(queue);

		var connections = A.Fake<IIbmMqConnectionProvider>();
		_ = A.CallTo(() => connections.CreateQueueManager()).Returns(queueManager);

		var services = new ServiceCollection();
		_ = services.AddLogging();
		// Registered FIRST so the transport's own TryAddKeyedSingleton stands down; nothing else about the
		// registration is substituted, and no queue manager is contacted.
		_ = services.AddKeyedSingleton(TransportName, connections);

		_ = services.AddIbmMqTransport(TransportName, mq =>
		{
			// The options validator rejects each of these when empty, so supplying them is part of testing
			// the consumer's path rather than a way around it.
			mq.QueueManager = "QM1";
			mq.Host = "localhost";
			mq.Port = 1414;
			mq.Channel = "DEV.APP.SVRCONN";
			mq.QueueName = TransportName;
			mq.RequireTls = false;
		});

		_provider = services.BuildServiceProvider();

		return _provider;
	}

	/// <summary>
	/// Fills the receiver's own message object with the next seeded message, or reports an empty queue.
	/// </summary>
	/// <remarks>
	/// <b>Properties are set through the platform's own API, and a refusal is dropped exactly as the sender
	/// drops one.</b> IBM MQ validates property names, so a name it will not accept never reaches a real
	/// queue either — reproducing that here is the difference between measuring this transport and measuring
	/// a dictionary.
	/// </remarks>
	private void Deliver(MQMessage message)
	{
		if (_inbound.Count == 0)
		{
			throw new MQException(MQC.MQCC_FAILED, MQC.MQRC_NO_MSG_AVAILABLE);
		}

		var seeded = _inbound.Dequeue();

		message.Format = MQC.MQFMT_NONE;
		message.CharacterSet = 1208;

		if (seeded.ContentType is { Length: > 0 } contentType)
		{
			SetProperty(message, IbmMqTransportSender.ContentTypePropertyName, contentType);
		}

		foreach (var (name, value) in seeded.Properties)
		{
			if (value is not null)
			{
				SetProperty(message, name, value as string ?? value.ToString()!);
			}
		}

		message.Write(seeded.Body.ToArray());

		// A real get leaves the data pointer at the start of the payload; the receiver reads from wherever
		// it is, so without this it would read past the end of what was just written.
		message.Seek(0);
	}

	private static void SetProperty(MQMessage message, string name, string value)
	{
		try
		{
			message.SetStringProperty(name, value);
		}
		catch (MQException)
		{
			// The queue manager refused the name. The sender does the same thing — logs and carries on — so
			// the property is absent on the wire in production too, and the arms see what a consumer sees.
		}
	}

	/// <summary>
	/// LIVENESS. Binary-mode round trip through this transport's own receive path.
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
	/// SAFETY. Ordinary IBM MQ traffic is not reported as a CloudEvent.
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
	public async ValueTask DisposeAsync()
	{
		if (_provider is not null)
		{
			await _provider.DisposeAsync();
		}

		GC.SuppressFinalize(this);
	}
}

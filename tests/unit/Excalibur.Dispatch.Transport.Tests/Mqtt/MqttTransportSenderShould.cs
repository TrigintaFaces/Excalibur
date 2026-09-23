// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.Transport;
using Excalibur.Dispatch.Transport.Mqtt;

using Microsoft.Extensions.Logging.Abstractions;

using MQTTnet;

namespace Excalibur.Dispatch.Transport.Tests.Mqtt;

/// <summary>
/// Unit tests for <see cref="MqttTransportSender"/>.
/// Validates constructor validation, destination exposure, and <c>GetService(Type)</c>.
/// </summary>
/// <remarks>
/// This class had no dedicated unit test file at all before this lock (bd-s1cprr) -- one of the three
/// transports named directly for thin failure-path coverage. Unlike the other transports, MQTT connects
/// lazily on first send/receive, so its native <see cref="MQTTnet.IMqttClient"/> handle is not available
/// via <c>GetService</c> until a connection has been established -- <c>null</c> before that point is the
/// documented, intended behavior (see the type's remarks), not a silent capability decline. Driving a
/// full fake connect+publish round trip to exercise the populated branch belongs with the broader
/// failure-path coverage this transport still lacks (bd-swm3hz), not this narrow GetService lock.
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Transport)]
public sealed class MqttTransportSenderShould : IAsyncDisposable
{
	private readonly IMqttConnectionProvider _fakeConnectionProvider = A.Fake<IMqttConnectionProvider>();
	private readonly MqttOptions _options = new() { Host = "broker.example.com", ClientId = "test-client", Topic = "orders/topic" };
	private readonly MqttTransportSender _sut;

	public MqttTransportSenderShould()
	{
		_sut = new MqttTransportSender(_fakeConnectionProvider, _options, NullLogger<MqttTransportSender>.Instance);
	}

	public ValueTask DisposeAsync() => _sut.DisposeAsync();

	/// <summary>
	/// SAFETY. The media type and the application properties reach the wire.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The builder set topic, payload, QoS, correlation and response-topic, and silently discarded
	/// <c>ContentType</c> and <c>Properties</c>. Nothing failed: the publish succeeded, the broker accepted
	/// it, and the receiver — which reads both fields — reported every message this sender published as
	/// having no content type and no properties. Both halves were self-consistent and the data was simply
	/// not there.
	/// </para>
	/// <para>
	/// The concrete casualty is <b>binary-mode CloudEvents</b>, whose attributes are carried as MQTT v5 user
	/// properties: a CloudEvent published through this sender arrived at a consumer of this framework as
	/// ordinary traffic, indistinguishable from a message that never carried the markers.
	/// </para>
	/// <para>
	/// The arm asserts what was handed to the client, because that is the wire. The publish RESULT is
	/// irrelevant here and the fake supplies whatever it supplies — so the capture is read even if the call
	/// then throws, rather than letting an unrelated dummy-construction failure mask the property.
	/// </para>
	/// </remarks>
	[Fact]
	public async Task Put_the_content_type_and_user_properties_on_the_wire()
	{
		MqttApplicationMessage? published = null;

		var client = A.Fake<IMqttClient>();
		_ = A.CallTo(() => client.PublishAsync(A<MqttApplicationMessage>._, A<CancellationToken>._))
			.Invokes((MqttApplicationMessage m, CancellationToken _) => published = m);

		_ = A.CallTo(() => _fakeConnectionProvider.CreateClient()).Returns(client);
		_ = A.CallTo(() => _fakeConnectionProvider.BuildClientOptions(A<string>._))
			.Returns(new MqttClientOptions { ClientId = "pub" });

		try
		{
			_ = await _sut.SendAsync(
				new TransportMessage
				{
					Body = "{}"u8.ToArray(),
					ContentType = "application/cloudevents+json",
					Properties = new Dictionary<string, object>(StringComparer.Ordinal)
					{
						["ce-type"] = "com.excalibur.test.v1",
						["ce-id"] = "evt-1",
					},
				},
                CancellationToken.None);
		}
		catch (NullReferenceException)
		{
			// The fake's publish result is not the subject; the captured message is.
		}

		_ = published.ShouldNotBeNull("nothing was handed to the client, so this arm proves nothing about "
			+ "what reaches the wire.");

		published.ContentType.ShouldBe("application/cloudevents+json");

		var userProperties = published.UserProperties ?? [];
		userProperties.ShouldContain(p => p.Name == "ce-type" && p.Value == "com.excalibur.test.v1");
		userProperties.ShouldContain(p => p.Name == "ce-id" && p.Value == "evt-1");
	}

	[Fact]
	public void Expose_destination_from_options_topic()
	{
		_sut.Destination.ShouldBe(_options.Topic);
	}

	[Fact]
	public void Throw_when_connection_provider_is_null()
	{
		Should.Throw<ArgumentNullException>(() =>
			new MqttTransportSender(null!, _options, NullLogger<MqttTransportSender>.Instance));
	}

	[Fact]
	public void Throw_when_options_is_null()
	{
		Should.Throw<ArgumentNullException>(() =>
			new MqttTransportSender(_fakeConnectionProvider, null!, NullLogger<MqttTransportSender>.Instance));
	}

	[Fact]
	public void Throw_when_logger_is_null()
	{
		Should.Throw<ArgumentNullException>(() =>
			new MqttTransportSender(_fakeConnectionProvider, _options, null!));
	}

	[Fact]
	public void Return_null_before_connecting()
	{
		// The client connects lazily on first send; before that, no capability has been established yet.
		var result = _sut.GetService(typeof(MQTTnet.IMqttClient));
		result.ShouldBeNull();
	}

	[Fact]
	public void Return_null_for_unknown_service_type()
	{
		var result = _sut.GetService(typeof(string));
		result.ShouldBeNull();
	}

	[Fact]
	public void Throw_when_GetService_type_is_null()
	{
		Should.Throw<ArgumentNullException>(() => _sut.GetService(null!));
	}

	/// <summary>
	/// SAFETY. A cancelled batch still reports one result per input, so the caller can tell which messages
	/// are unaccounted for.
	/// </summary>
	/// <remarks>
	/// This used to throw from inside the per-message loop, so a batch cancelled part way returned no
	/// result at all after having already sent some of its messages. The caller was told only "cancelled"
	/// and had no way to learn which inputs had been delivered — leaving resend-everything as the only
	/// safe move, which on an at-least-once transport duplicates every message that already arrived.
	/// </remarks>
	[Fact]
	public async Task Report_every_input_when_the_batch_is_cancelled()
	{
		using var cancelled = new CancellationTokenSource();
		await cancelled.CancelAsync();

		var messages = new List<TransportMessage>
		{
			TransportMessage.FromString("a"),
			TransportMessage.FromString("b"),
			TransportMessage.FromString("c"),
		};

		var result = await _sut.SendBatchAsync(messages, cancelled.Token);

		result.Results.Count.ShouldBe(3, "a cancelled batch must still account for every input.");
		result.TotalMessages.ShouldBe(3);
		(result.SuccessCount + result.FailureCount).ShouldBe(3);
		result.Results.ShouldAllBe(r => !r.IsSuccess && r.Error!.IsRetryable);
	}

	/// <summary>
	/// LIVENESS. An uncancelled batch still sends every message and reports it.
	/// </summary>
	/// <remarks>
	/// Without this, the arm above is satisfied by a sender that reports every input as a cancelled
	/// failure and never publishes anything — safe, complete, and useless.
	/// </remarks>
	[Fact]
	public async Task Publish_every_message_when_the_batch_is_not_cancelled()
	{
		var client = A.Fake<IMqttClient>();
		_ = A.CallTo(() => _fakeConnectionProvider.CreateClient()).Returns(client);
		_ = A.CallTo(() => _fakeConnectionProvider.BuildClientOptions(A<string>._))
			.Returns(new MqttClientOptions { ClientId = "pub" });
		_ = A.CallTo(() => client.PublishAsync(A<MqttApplicationMessage>._, A<CancellationToken>._))
			.Returns(new MqttClientPublishResult(0, MqttClientPublishReasonCode.Success, null, []));

		var messages = new List<TransportMessage>
		{
			TransportMessage.FromString("a"),
			TransportMessage.FromString("b"),
		};

		var result = await _sut.SendBatchAsync(messages, CancellationToken.None);

		result.Results.Count.ShouldBe(2);
		result.SuccessCount.ShouldBe(2);
		A.CallTo(() => client.PublishAsync(A<MqttApplicationMessage>._, A<CancellationToken>._))
			.MustHaveHappenedTwiceExactly();
	}
}

// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;

using Excalibur.Dispatch.Transport;
using Excalibur.Dispatch.Transport.Mqtt;

using Microsoft.Extensions.Logging.Abstractions;

using MQTTnet;
using MQTTnet.Packets;

namespace Excalibur.Dispatch.Transport.Tests.Mqtt;

/// <summary>
/// Unit tests for <see cref="MqttTransportReceiver"/>.
/// Validates constructor validation, source exposure, and <c>GetService(Type)</c>.
/// </summary>
/// <remarks>
/// This class had no dedicated unit test file at all before this lock (bd-s1cprr). See
/// <see cref="MqttTransportSenderShould"/> for why the pre-connect <c>null</c> is intended behavior,
/// not a silent capability decline.
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Transport)]
public sealed class MqttTransportReceiverShould : IAsyncDisposable
{
	private readonly IMqttConnectionProvider _fakeConnectionProvider = A.Fake<IMqttConnectionProvider>();
	private readonly MqttOptions _options = new() { Host = "broker.example.com", ClientId = "test-client", Topic = "orders/topic" };
	private readonly MqttTransportReceiver _sut;

	public MqttTransportReceiverShould()
	{
		_sut = new MqttTransportReceiver(_fakeConnectionProvider, _options, NullLogger<MqttTransportReceiver>.Instance);
	}

	public ValueTask DisposeAsync() => _sut.DisposeAsync();

	[Fact]
	public void Expose_source_from_options_topic()
	{
		_sut.Source.ShouldBe(_options.Topic);
	}

	[Fact]
	public void Throw_when_connection_provider_is_null()
	{
		Should.Throw<ArgumentNullException>(() =>
			new MqttTransportReceiver(null!, _options, NullLogger<MqttTransportReceiver>.Instance));
	}

	[Fact]
	public void Throw_when_options_is_null()
	{
		Should.Throw<ArgumentNullException>(() =>
			new MqttTransportReceiver(_fakeConnectionProvider, null!, NullLogger<MqttTransportReceiver>.Instance));
	}

	[Fact]
	public void Throw_when_logger_is_null()
	{
		Should.Throw<ArgumentNullException>(() =>
			new MqttTransportReceiver(_fakeConnectionProvider, _options, null!));
	}

	[Fact]
	public void Return_null_before_connecting()
	{
		// The client connects lazily on first receive; before that, no capability has been established yet.
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

	[Fact]
	public async Task PopulateContentTypeAndProperties_FromTheWireMessage()
	{
		// esue64: the receiver used to discard ContentType and UserProperties entirely, making
		// binary-mode CloudEvents (which live in MQTT v5 user properties) structurally undetectable.
		var message = new MqttApplicationMessage
		{
			Topic = _options.Topic,
			ContentType = "application/cloudevents+json",
			UserProperties = [new MqttUserProperty("ce-type", "com.excalibur.test.v1")],
		};
		var args = new MqttApplicationMessageReceivedEventArgs(
			"client-1",
			message,
			new MqttPublishPacket(),
			static (_, _) => Task.CompletedTask);

		var handler = typeof(MqttTransportReceiver).GetMethod(
			"OnMessageReceivedAsync",
			BindingFlags.NonPublic | BindingFlags.Instance)!;
		await (Task)handler.Invoke(_sut, [args])!;

		var received = await _sut.ReceiveAsync(maxMessages: 1, CancellationToken.None);

		received.Count.ShouldBe(1);
		received[0].ContentType.ShouldBe("application/cloudevents+json");
		received[0].Properties.ShouldContainKey("ce-type");
		received[0].Properties["ce-type"].ShouldBe("com.excalibur.test.v1");
	}
}

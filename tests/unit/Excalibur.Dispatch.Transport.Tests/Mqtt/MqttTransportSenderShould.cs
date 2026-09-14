// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport;
using Excalibur.Dispatch.Transport.Mqtt;

using Microsoft.Extensions.Logging.Abstractions;

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
}

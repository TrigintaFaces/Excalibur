// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;

using Excalibur.Dispatch.Transport;
using Excalibur.Dispatch.Transport.IbmMq;

using IBM.WMQ;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Transport.Tests.IbmMq;

/// <summary>
/// Unit tests for <see cref="IbmMqTransportReceiver"/>.
/// Validates constructor validation, source exposure, and <c>GetService(Type)</c>.
/// </summary>
/// <remarks>
/// This class had no dedicated unit test file at all before this lock (bd-s1cprr). See
/// <see cref="IbmMqTransportSenderShould"/> for the GetService rationale.
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Transport)]
public sealed class IbmMqTransportReceiverShould : IAsyncDisposable
{
	private const string TestSource = "ORDERS.QUEUE";
	private readonly IIbmMqConnectionProvider _fakeConnectionProvider;
	private readonly IbmMqTransportReceiver _sut;

	public IbmMqTransportReceiverShould()
	{
		_fakeConnectionProvider = A.Fake<IIbmMqConnectionProvider>();
		_sut = new IbmMqTransportReceiver(
			_fakeConnectionProvider,
			TestSource,
			new IbmMqReceiveTuningOptions(),
			NullLogger<IbmMqTransportReceiver>.Instance);
	}

	public ValueTask DisposeAsync() => _sut.DisposeAsync();

	[Fact]
	public void Expose_source_from_constructor()
	{
		_sut.Source.ShouldBe(TestSource);
	}

	[Fact]
	public void Throw_when_connection_provider_is_null()
	{
		Should.Throw<ArgumentNullException>(() =>
			new IbmMqTransportReceiver(null!, TestSource, new IbmMqReceiveTuningOptions(), NullLogger<IbmMqTransportReceiver>.Instance));
	}

	[Fact]
	public void Throw_when_source_is_null()
	{
		Should.Throw<ArgumentNullException>(() =>
			new IbmMqTransportReceiver(A.Fake<IIbmMqConnectionProvider>(), null!, new IbmMqReceiveTuningOptions(), NullLogger<IbmMqTransportReceiver>.Instance));
	}

	[Fact]
	public void Throw_when_receive_options_is_null()
	{
		Should.Throw<ArgumentNullException>(() =>
			new IbmMqTransportReceiver(A.Fake<IIbmMqConnectionProvider>(), TestSource, null!, NullLogger<IbmMqTransportReceiver>.Instance));
	}

	[Fact]
	public void Throw_when_logger_is_null()
	{
		Should.Throw<ArgumentNullException>(() =>
			new IbmMqTransportReceiver(A.Fake<IIbmMqConnectionProvider>(), TestSource, new IbmMqReceiveTuningOptions(), null!));
	}

	[Fact]
	public void Return_connection_provider_via_GetService()
	{
		var result = _sut.GetService(typeof(IIbmMqConnectionProvider));
		result.ShouldBe(_fakeConnectionProvider);
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
	public void PopulateContentTypeAndProperties_FromTheWireMessage()
	{
		// esue64: BuildReceived used to discard Format and message properties entirely, making
		// binary-mode CloudEvents (which live in IBM MQ RFH2 properties) structurally undetectable.
		var mqMessage = new MQMessage();
		mqMessage.WriteString("{}");
		mqMessage.Format = MQC.MQFMT_STRING;
		// IBM MQ validates property names as Java identifiers -- a hyphenated "ce-type" is rejected
		// outright (MQRC_PROPERTY_NAME_ERROR), unlike Kafka/RabbitMQ/AWS's "ce-"/"ce_" header names.
		// A future IbmMq CloudEvents mapper will need its own identifier-safe naming convention; this
		// test only proves the receiver round-trips whatever property name a producer sets.
		mqMessage.SetStringProperty("ce_type", "com.excalibur.test.v1");

		var buildReceived = typeof(IbmMqTransportReceiver).GetMethod(
			"BuildReceived",
			BindingFlags.NonPublic | BindingFlags.Instance)!;
		var received = (TransportReceivedMessage)buildReceived.Invoke(_sut, ["msg-1", mqMessage])!;

		// FLIPPED to the new contract. This used to assert ContentType == MQC.MQFMT_STRING, which
		// certified a defect: the MQMD tag describes how the queue manager should CONVERT the payload, not
		// what the payload IS, so reporting it in a MIME field handed every caller a value that can never
		// parse as a media type -- on every message, because the tag is always present.
		received.ContentType.ShouldBeNull(
			"this producer declared no content type, and the wire-format tag is not one: null is the "
			+ "honest answer and it is what the rest of the framework already expects");

		received.Properties[IbmMqTransportReceiver.MqFormatPropertyName].ShouldBe(
			MQC.MQFMT_STRING,
			"the tag is not discarded - it stays available under a name that says what it is, so the "
			+ "deployments that switch on it keep it without it impersonating a media type");
		received.Properties.ShouldContainKey("ce_type");
		received.Properties["ce_type"].ShouldBe("com.excalibur.test.v1");
	}
}

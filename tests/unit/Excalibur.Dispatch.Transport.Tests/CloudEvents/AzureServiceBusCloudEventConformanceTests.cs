// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Azure.Messaging.ServiceBus;

using CloudNative.CloudEvents;

using Excalibur.Dispatch.CloudEvents;
using Excalibur.Dispatch.Testing.Transport;
using Excalibur.Dispatch.Transport;
using Excalibur.Dispatch.Transport.AzureServiceBus.Internal;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Transport.Tests.CloudEvents;

/// <summary>
/// Runs the CloudEvents transport conformance suite against the Azure Service Bus transport.
/// </summary>
/// <remarks>
/// <para>
/// Every assertion lives in the kit. This class supplies only what is Service Bus-specific: the receiver
/// a consumer resolves, a way to put a message in front of it, and this transport's own encoding.
/// </para>
/// <para>
/// <b>Why this transport needed its own suite, stated as the reason rather than as coverage.</b> This
/// transport shipped a receive path that decoded CloudEvents only when the consumer had opted into
/// sessions. The session flag has no initialiser, so the branch that did NOT decode was the DEFAULT
/// one: a consumer publishing and consuming over Service Bus with stock options got CloudEvents
/// delivered as ordinary traffic, indistinguishable from a message that never carried the markers.
/// </para>
/// <para>
/// Nothing caught it, and the reason is the point. A census of decoding call sites in the registration
/// reported this transport as wired — one call site is one call site, whether it sits on the default
/// path or behind a branch almost nobody takes. A structural check over constructor shapes could not
/// see it either. The defect is only visible to a suite that RESOLVES the receiver a consumer actually
/// gets and puts a real CloudEvent in front of it, which is what this class does: the receiver here
/// comes out of the transport's own registration under default options, so the path under test is the
/// path a consumer runs.
/// </para>
/// <para>
/// <b>The encoder is RESOLVED, not constructed</b>, for the reason the Kafka suite gives: constructing
/// an encoder by hand tests an encoder nobody runs, while resolving one tests the encoder a consumer
/// gets. CloudEvents is opt-in on this transport, so the fixture makes both calls a CloudEvents
/// consumer makes — the transport registration and the CloudEvents binding.
/// </para>
/// <para>
/// <b>No infrastructure, and no vendor type is faked.</b> What is substituted is this transport's own
/// <see cref="IServiceBusReceiverSeam"/> — the seam the session receive path already routed through.
/// Faking the SDK's concrete <c>ServiceBusClient</c> and <c>ServiceBusReceiver</c> instead, as this
/// fixture first did, is forbidden: those types carry non-virtual overloads that make a fake's behaviour
/// diverge from the real client's, so the governance boundary requires the seam and treats a concrete
/// fake as a missing one rather than as a site to exempt.
/// </para>
/// <para>
/// Supplying the seam is also what keeps this fixture free of a connection string. The default receive
/// path used to build its receiver from the vendor client inline, so there was nothing to substitute
/// below the client itself; it now consults the seam first and constructs a client only when none is
/// present. Everything else in the wiring is the consumer's, including the decode decoration.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class AzureServiceBusCloudEventConformanceTests : CloudEventTransportConformanceTests, IAsyncDisposable
{
	private const string TransportName = "servicebus-conformance";

	private readonly List<ServiceBusReceivedMessage> _inbound = [];

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
			var properties = new Dictionary<string, object>(StringComparer.Ordinal);

			foreach (var property in message.Properties)
			{
				properties[property.Key] = property.Value ?? string.Empty;
			}

			_inbound.Add(ServiceBusModelFactory.ServiceBusReceivedMessage(
				body: new BinaryData(message.Body.ToArray()),
				messageId: message.Id,
				contentType: message.ContentType,
				properties: properties,
				lockTokenGuid: Guid.NewGuid()));
		}

		return Task.CompletedTask;
	}

	/// <inheritdoc />
	protected override async Task<TransportReceivedMessage> EncodeAsync(
		CloudEvent cloudEvent, CloudEventMode mode)
	{
		var encoder = BuildProvider().GetRequiredService<ICloudEventEncoder<ServiceBusMessage>>();

		var encoded = await encoder.ToTransportMessageAsync(cloudEvent, mode, CancellationToken.None);

		var properties = new Dictionary<string, object>(StringComparer.Ordinal);

		foreach (var property in encoded.ApplicationProperties)
		{
			properties[property.Key] = property.Value ?? string.Empty;
		}

		return new TransportReceivedMessage
		{
			Id = "servicebus-wire-1",
			Body = encoded.Body.ToMemory(),
			// Structured mode is identified by its MEDIA TYPE, which this transport carries on the
			// message envelope rather than among the application properties. Dropping it here produces a
			// false RED that reads exactly like a decode failure.
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

		var seam = A.Fake<IServiceBusReceiverSeam>();

		_ = A.CallTo(() => seam.ReceiveMessagesAsync(A<int>._, A<CancellationToken>._))
			.ReturnsLazily(() => Task.FromResult<IReadOnlyList<ServiceBusReceivedMessage>>([.. _inbound]));

		var services = new ServiceCollection();
		_ = services.AddLogging();
		// Registered FIRST under the transport's key, which the receiver factory consults before
		// building its own adapter over the vendor client.
		_ = services.AddKeyedSingleton(TransportName, seam);
		_ = services.AddAzureServiceBusTransport(TransportName, _ => { });
		_ = services.AddCloudEventsForServiceBus();

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
	/// SAFETY. Ordinary Service Bus traffic is not reported as a CloudEvent.
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
	/// ASYNC disposal, and not by preference: the container resolves a receiver decorator that implements
	/// only <see cref="IAsyncDisposable"/>, so a synchronous <c>Dispose</c> throws rather than cleaning
	/// up — which surfaced here as three arms failing in teardown while the assertions themselves passed.
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

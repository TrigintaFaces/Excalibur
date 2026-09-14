// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Buffers.Binary;
using System.Net;
using System.Net.Http.Headers;

using CloudNative.CloudEvents;

using Excalibur.Dispatch.CloudEvents;
using Excalibur.Dispatch.Testing.Transport;
using Excalibur.Dispatch.Transport;
using Excalibur.Dispatch.Transport.Grpc;

using global::Grpc.Net.Client;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Transport.Tests.CloudEvents;

/// <summary>
/// Runs the CloudEvents transport conformance suite against the gRPC transport.
/// </summary>
/// <remarks>
/// <para>
/// <b>This transport is STRUCTURED-ONLY, and that is a contract rather than a gap.</b> Its registration
/// binds structured-only decoding and its sender applies the shared encoding decorator, which publishes
/// structured JSON — encode and decode agreeing. Declaring the narrower set here is therefore honest,
/// and it is not free: the kit requires a declined mode to actually fail to decode, so a declaration
/// made to dodge a red arm produces a different red instead.
/// </para>
/// <para>
/// <b>No infrastructure, and no server.</b> Unlike every sibling transport, this one has no client
/// interface to substitute: the receiver takes a sealed <see cref="GrpcChannel"/> and builds its own
/// call invoker. The seam that remains is the channel's HTTP handler, so the fixture answers the
/// receiver's unary call with a real gRPC response frame — a length-prefixed body and a
/// <c>grpc-status</c> trailer — carrying a payload written by the transport's OWN response marshaller.
/// Nothing listens on a port, and nothing about the encoding is this fixture's invention.
/// </para>
/// <para>
/// The channel is registered under the transport's key first, which the registration's
/// <c>TryAddKeyedSingleton</c> honours; the receiver itself is still built by the transport's own
/// factory, carrying the decode decoration that factory applies.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class GrpcCloudEventConformanceTests : CloudEventTransportConformanceTests, IAsyncDisposable
{
	private const string TransportName = "grpc-conformance";

	private readonly List<TransportReceivedMessage> _inbound = [];

	private ServiceProvider? _provider;

	/// <inheritdoc />
	/// <remarks>
	/// Structured only. See the class remarks: this is the transport's registered contract, and the
	/// declined-mode arm holds the declaration to it.
	/// </remarks>
	protected override IReadOnlyCollection<CloudEventMode> SupportedModes => [CloudEventMode.Structured];

	/// <inheritdoc />
	protected override Task<ITransportReceiver> CreateReceiverAsync() =>
		Task.FromResult(BuildProvider().GetRequiredKeyedService<ITransportReceiver>(TransportName));

	/// <inheritdoc />
	protected override Task SeedMessagesAsync(
		ITransportReceiver receiver, IReadOnlyList<TransportReceivedMessage> messages)
	{
		_inbound.Clear();
		_inbound.AddRange(messages);

		return Task.CompletedTask;
	}

	/// <inheritdoc />
	protected override async Task<TransportReceivedMessage> EncodeAsync(
		CloudEvent cloudEvent, CloudEventMode mode)
	{
		if (mode != CloudEventMode.Structured)
		{
			// This transport carries the shared structured encoding decorator and nothing else, so it
			// cannot emit any other mode - which is why its structured-only receiver is not an
			// emit-without-decode gap.
			throw new NotSupportedException($"gRPC encodes structured mode only; asked for {mode}.");
		}

		TransportMessage? captured = null;

		var inner = A.Fake<ITransportSender>();
		_ = A.CallTo(() => inner.SendAsync(A<TransportMessage>._, A<CancellationToken>._))
			.Invokes((TransportMessage sent, CancellationToken _) => captured = sent)
			.Returns(Task.FromResult(SendResult.Success("grpc-conformance-1")));

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
			Id = "grpc-wire-1",
			Body = wire.Body,
			// Structured mode is identified by its MEDIA TYPE, which this transport carries on the
			// message envelope rather than among the properties. Dropping it here produces a false RED
			// that reads exactly like a decode failure.
			ContentType = wire.ContentType,
			EnqueuedAt = DateTimeOffset.UtcNow,
			Properties = wire.Properties
				.Where(static pair => pair.Value is not CloudEvent)
				.ToDictionary(static pair => pair.Key, static pair => pair.Value, StringComparer.Ordinal),
		};
	}

	private ServiceProvider BuildProvider()
	{
		if (_provider is not null)
		{
			return _provider;
		}

		var channel = GrpcChannel.ForAddress(
			"https://localhost:5001",
			new GrpcChannelOptions { HttpHandler = new ReceiveResponseHandler(DrainOne) });

		var services = new ServiceCollection();
		_ = services.AddLogging();
		// Registered FIRST so the transport's own TryAddKeyedSingleton stands down; the receiver is still
		// the one its factory builds.
		_ = services.AddKeyedSingleton(TransportName, channel);

		_ = services.AddGrpcTransport(TransportName, grpc =>
		{
			// An absolute https address is REQUIRED by this transport's options validator, and the
			// channel above never opens a connection to it.
			grpc.ServerAddress = "https://localhost:5001";
			grpc.Destination = TransportName;
		});

		_provider = services.BuildServiceProvider();

		return _provider;
	}

	/// <summary>
	/// Hands the receiver the next seeded message, or an empty batch once drained.
	/// </summary>
	/// <returns>The response payload the transport's own marshaller would produce.</returns>
	private byte[] DrainOne()
	{
		var response = new GrpcReceiveResponse();

		if (_inbound.Count > 0)
		{
			var message = _inbound[0];
			_inbound.RemoveAt(0);

			var wire = new GrpcReceivedMessage
			{
				Id = message.Id,
				// This transport carries the body base64-encoded inside its JSON envelope, so seeding raw
				// bytes here would produce a decode failure belonging to the fixture rather than the
				// transport.
				Body = Convert.ToBase64String(message.Body.ToArray()),
				ContentType = message.ContentType,
				DeliveryCount = 1,
				Source = TransportName,
			};

			foreach (var property in message.Properties)
			{
				wire.Properties[property.Key] = property.Value?.ToString() ?? string.Empty;
			}

			response.Messages.Add(wire);
		}

		return GrpcTransportMarshaller.ReceiveResponseMarshaller.Serializer(response);
	}

	/// <summary>
	/// LIVENESS. Structured mode round trip through this transport's own send and receive paths.
	/// </summary>
	[Fact]
	public Task Round_trip_a_structured_mode_event() =>
		VerifyStructuredModeRoundTripsThroughItsOwnReceiver();

	/// <summary>
	/// LIVENESS. Binary mode, which this transport declines; the kit skips it and the arm below holds
	/// the declaration honest.
	/// </summary>
	[Fact]
	public Task Round_trip_a_binary_mode_event() =>
		VerifyBinaryModeRoundTripsThroughItsOwnReceiver();

	/// <summary>
	/// SAFETY. Ordinary gRPC traffic is not reported as a CloudEvent.
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

	/// <summary>
	/// Answers the receiver's unary call with a real gRPC response frame.
	/// </summary>
	/// <param name="payload">Produces the marshalled response body for each call.</param>
	private sealed class ReceiveResponseHandler(Func<byte[]> payload) : HttpMessageHandler
	{
		/// <inheritdoc />
		protected override Task<HttpResponseMessage> SendAsync(
			HttpRequestMessage request, CancellationToken cancellationToken)
		{
			var body = payload();

			// A gRPC message is a one-byte compression flag, a four-byte big-endian length, then the
			// payload. Writing this by hand is what lets the call complete with no server.
			var framed = new byte[5 + body.Length];
			framed[0] = 0;
			BinaryPrimitives.WriteUInt32BigEndian(framed.AsSpan(1, 4), (uint)body.Length);
			body.CopyTo(framed, 5);

			var response = new HttpResponseMessage(HttpStatusCode.OK)
			{
				Version = new Version(2, 0),
				Content = new ByteArrayContent(framed),
			};

			response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/grpc");

			// Without an OK status in the trailers the client raises RpcException regardless of the body.
			response.TrailingHeaders.Add("grpc-status", "0");

			return Task.FromResult(response);
		}
	}
}

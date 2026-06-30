// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using CloudNative.CloudEvents;

namespace Excalibur.Dispatch.Tests.Conformance.Transport;

/// <summary>
/// Optional transport-conformance capabilities a transport may support beyond the body-only
/// <see cref="IChannelSender" />/<see cref="IChannelReceiver" /> surface.
/// </summary>
/// <remarks>
/// The production <see cref="IChannelReceiver" /> is intentionally minimal (body-only
/// <c>ReceiveAsync&lt;T&gt;</c>) — transport-carrier concerns (Kafka <c>Headers</c>, RabbitMQ
/// <c>BasicProperties</c>, CloudEvents protocol binding, ack/nack) are <b>conformance-harness</b>
/// concerns, surfaced here so a deriving transport can opt in and the conformance suite can make a
/// <b>real, RED-able</b> assertion (no false conformance). A transport that does not advertise a
/// capability simply skips the capability-gated facts; a transport that advertises one but implements
/// it incorrectly FAILS the corresponding assertion (the htcbgu vacuity this seam exists to prevent).
/// </remarks>
[Flags]
public enum TransportCapability
{
	/// <summary>No advanced capabilities — body-only send/receive.</summary>
	None = 0,

	/// <summary>
	/// The transport surfaces carrier headers (Kafka <c>Headers</c> / RabbitMQ <c>BasicProperties</c>)
	/// on receive so a deriver can assert metadata survived on the carrier, not just the body (bd-liyait).
	/// </summary>
	HeaderSurfacing = 1 << 0,

	/// <summary>
	/// The transport binds CloudEvents (structured content-mode and binary <c>ce-</c> attribute mapping)
	/// so a deriver can assert real CloudEvents semantic-equality round-trip (bd-jj4hx4).
	/// </summary>
	CloudEventsBinding = 1 << 1,

	/// <summary>
	/// The transport exposes an ack/nack-capable receive so a deriver can assert that a nack'd message is
	/// redelivered (at-least-once), which a single send/receive cannot prove (bd-5dox7c).
	/// </summary>
	AckNackRedelivery = 1 << 2,

	/// <summary>
	/// The transport supports server-side message filtering (Azure Service Bus / AWS SQS) so a deriver can
	/// assert a non-matching message is not received and a matching one is (bd-1rbj0a).
	/// </summary>
	Filtering = 1 << 3,
}

/// <summary>
/// CloudEvents protocol binding modes a transport may support.
/// </summary>
public enum CloudEventBinding
{
	/// <summary>Structured content-mode: the whole CloudEvent is the message body (application/cloudevents+json).</summary>
	Structured,

	/// <summary>Binary content-mode: CloudEvent attributes map to transport carrier headers (<c>ce-*</c>), data is the body.</summary>
	Binary,
}

/// <summary>
/// The result of a capability-aware receive: the deserialized body, the transport-carrier headers that
/// were surfaced, and an ack/nack handle (when the transport supports <see cref="TransportCapability.AckNackRedelivery" />).
/// </summary>
/// <typeparam name="T">The body type.</typeparam>
public sealed class ConformanceReceiveResult<T>
{
	private static readonly IReadOnlyDictionary<string, string> EmptyHeaders =
		new Dictionary<string, string>(StringComparer.Ordinal);

	private readonly Func<CancellationToken, Task>? _acknowledge;
	private readonly Func<CancellationToken, Task>? _reject;

	/// <summary>Initializes a new capability-aware receive result.</summary>
	/// <param name="body">The deserialized message body.</param>
	/// <param name="headers">The transport-carrier headers surfaced on receive (never null).</param>
	/// <param name="acknowledge">Acknowledge (settle) the message, or null if ack/nack is unsupported.</param>
	/// <param name="reject">Reject (nack) the message so it is redelivered, or null if unsupported.</param>
	public ConformanceReceiveResult(
		T? body,
		IReadOnlyDictionary<string, string>? headers,
		Func<CancellationToken, Task>? acknowledge,
		Func<CancellationToken, Task>? reject)
	{
		Body = body;
		Headers = headers ?? EmptyHeaders;
		_acknowledge = acknowledge;
		_reject = reject;
	}

	/// <summary>Gets the deserialized message body.</summary>
	public T? Body { get; }

	/// <summary>Gets the transport-carrier headers surfaced on receive (empty if none).</summary>
	public IReadOnlyDictionary<string, string> Headers { get; }

	/// <summary>Acknowledges (settles) the message so it is not redelivered.</summary>
	public Task AcknowledgeAsync(CancellationToken cancellationToken) =>
		_acknowledge?.Invoke(cancellationToken) ?? Task.CompletedTask;

	/// <summary>Rejects (nacks) the message so the transport redelivers it (at-least-once).</summary>
	public Task RejectAsync(CancellationToken cancellationToken) =>
		_reject?.Invoke(cancellationToken) ?? Task.CompletedTask;
}

/// <summary>
/// The capability seam a transport-conformance deriver exposes when it supports capabilities beyond the
/// body-only send/receive surface. Implementations advertise <see cref="Capabilities" /> and implement only
/// the operations for the flags they set; the conformance suite gates each real assertion on the flag and
/// asserts the operation's <b>behavior</b> (header round-trip, CE semantic equality, nack→redelivery,
/// filter match) so a non-conforming transport FAILS rather than silently passing.
/// </summary>
public interface ITransportConformanceCapabilities
{
	/// <summary>Gets the set of advanced capabilities this transport advertises.</summary>
	TransportCapability Capabilities { get; }

	/// <summary>
	/// Sends <paramref name="body" /> with the supplied transport-carrier headers
	/// (<see cref="TransportCapability.HeaderSurfacing" />).
	/// </summary>
	Task SendWithHeadersAsync<T>(
		T body,
		IReadOnlyDictionary<string, string> headers,
		CancellationToken cancellationToken);

	/// <summary>
	/// Receives the next message surfacing its carrier headers and (if supported) an ack/nack handle
	/// (<see cref="TransportCapability.HeaderSurfacing" /> / <see cref="TransportCapability.AckNackRedelivery" />).
	/// </summary>
	Task<ConformanceReceiveResult<T>?> ReceiveWithContextAsync<T>(CancellationToken cancellationToken);

	/// <summary>
	/// Sends a CloudEvent using the requested protocol binding
	/// (<see cref="TransportCapability.CloudEventsBinding" />).
	/// </summary>
	Task SendCloudEventAsync(CloudEvent cloudEvent, CloudEventBinding binding, CancellationToken cancellationToken);

	/// <summary>
	/// Receives the next message decoded as a CloudEvent, or null if it could not be bound as one
	/// (<see cref="TransportCapability.CloudEventsBinding" />).
	/// </summary>
	Task<CloudEvent?> ReceiveCloudEventAsync(CloudEventBinding binding, CancellationToken cancellationToken);

	/// <summary>
	/// Sends <paramref name="body" /> tagged with the supplied filter attributes
	/// (<see cref="TransportCapability.Filtering" />).
	/// </summary>
	Task SendFilterableAsync<T>(
		T body,
		IReadOnlyDictionary<string, string> attributes,
		CancellationToken cancellationToken);

	/// <summary>
	/// Receives the next message matching <paramref name="filter" />; messages not matching the filter MUST NOT
	/// be returned (<see cref="TransportCapability.Filtering" />).
	/// </summary>
	Task<ConformanceReceiveResult<T>?> ReceiveMatchingAsync<T>(
		IReadOnlyDictionary<string, string> filter,
		CancellationToken cancellationToken);
}

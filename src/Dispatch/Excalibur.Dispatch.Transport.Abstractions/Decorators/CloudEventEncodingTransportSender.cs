// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using CloudNative.CloudEvents;

namespace Excalibur.Dispatch.Transport.Decorators;

/// <summary>
/// Decorates an <see cref="ITransportSender"/> so that a message carrying a <see cref="CloudEvent"/> is
/// published in the CloudEvents structured JSON mode.
/// </summary>
/// <remarks>
/// <para>
/// The send counterpart of the inbound decoding decorator, sitting on the same shared contract:
/// <see cref="ITransportSender"/> is defined in terms of <see cref="TransportMessage"/>, so one decorator
/// serves every transport.
/// </para>
/// <para>
/// <b>Opt-in per message, and this is the one place the inbound pattern must NOT be copied.</b> Decoding is
/// applied unconditionally because it is <i>total</i> — a message carrying no CloudEvents markers is
/// returned untouched, so a consumer who never uses CloudEvents sees no change. Encoding has no such
/// property: rewriting the body of a message nobody asked to encode would corrupt ordinary business
/// traffic. A message therefore reaches the wire byte-identical unless it explicitly carries a
/// <see cref="CloudEvent"/> under <see cref="CloudEventPropertyKey"/>.
/// </para>
/// <para>
/// <b>The marker is removed once consumed.</b> It is an instruction to this decorator, not payload, and
/// leaving it in <see cref="TransportMessage.Properties"/> would push a non-string object into every
/// provider's metadata mapping — where it would at best be stringified onto the wire and at worst be
/// refused by the broker.
/// </para>
/// </remarks>
internal sealed class CloudEventEncodingTransportSender : DelegatingTransportSender
{
	/// <summary>
	/// The <see cref="TransportMessage.Properties"/> key under which a caller supplies the
	/// <see cref="CloudEvent"/> to publish.
	/// </summary>
	/// <remarks>
	/// Deliberately the same key the inbound decorator publishes a decoded event under, so a message
	/// received as a CloudEvent and forwarded on is symmetric: read from one side, written to the other.
	/// </remarks>
	public const string CloudEventPropertyKey = "cloudevent";

	/// <summary>
	/// Initializes a new instance of the <see cref="CloudEventEncodingTransportSender"/> class.
	/// </summary>
	/// <param name="innerSender">The sender to decorate.</param>
	public CloudEventEncodingTransportSender(ITransportSender innerSender)
		: base(innerSender)
	{
	}

	/// <inheritdoc />
	public override Task<SendResult> SendAsync(TransportMessage message, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(message);

		return base.SendAsync(Encode(message), cancellationToken);
	}

	/// <inheritdoc />
	public override Task<BatchSendResult> SendBatchAsync(
		IReadOnlyList<TransportMessage> messages,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(messages);

		var encoded = new TransportMessage[messages.Count];
		for (var i = 0; i < messages.Count; i++)
		{
			encoded[i] = Encode(messages[i]);
		}

		return base.SendBatchAsync(encoded, cancellationToken);
	}

	/// <summary>
	/// Rewrites the message as a structured CloudEvent when one is attached; otherwise returns it unchanged.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Only the body and content type change. Structured mode replaces the <i>body</i> — the event's own
	/// payload travels inside the envelope — but the transport still has to route, correlate and expire the
	/// message, so a caller that set a partition key or a time-to-live does not forfeit it by choosing
	/// CloudEvents.
	/// </para>
	/// <para>
	/// <b>The rewrite is in place, on the caller's own message.</b> Copying instead would mean restating
	/// every field of <see cref="TransportMessage"/> here, which silently drops any field added to that type
	/// later — a failure that would surface as one lost header on one transport, long after the change that
	/// caused it. Mutating is also idempotent: the marker is consumed, so a retry or a second send finds no
	/// event to encode and forwards the already-encoded body unchanged.
	/// </para>
	/// </remarks>
	private static TransportMessage Encode(TransportMessage message)
	{
		if (!message.HasProperties
			|| !message.Properties.TryGetValue(CloudEventPropertyKey, out var marker)
			|| marker is not CloudEvent cloudEvent)
		{
			return message;
		}

		message.Body = TransportMessageCloudEventEncoder.Encode(cloudEvent);
		message.ContentType = TransportMessageCloudEventEncoder.StructuredModeMediaType;
		_ = message.Properties.Remove(CloudEventPropertyKey);

		return message;
	}
}

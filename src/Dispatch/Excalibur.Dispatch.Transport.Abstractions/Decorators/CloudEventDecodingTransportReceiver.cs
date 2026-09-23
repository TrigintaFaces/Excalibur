// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Text.Json;

using CloudNative.CloudEvents;

namespace Excalibur.Dispatch.Transport.Decorators;

/// <summary>
/// Decorates an <see cref="ITransportReceiver"/> so that inbound CloudEvents are decoded on the receive
/// path, making a message that arrived as a CloudEvent observable as one to the consumer.
/// </summary>
/// <remarks>
/// <para>
/// This is the receive counterpart of the encode path. It sits here, rather than in a transport adapter,
/// because the decoder consumes <see cref="TransportReceivedMessage"/> — the shape
/// <see cref="ITransportReceiver"/> is defined in terms of — so one decorator over the receive contract
/// serves every transport instead of one call site per provider.
/// </para>
/// <para>
/// <b>The decoded event is attached, not substituted.</b> The message continues to flow as a
/// <see cref="TransportReceivedMessage"/> with its body and properties untouched; the
/// <see cref="CloudEvent"/> is placed in <see cref="TransportReceivedMessage.ProviderData"/> under
/// <see cref="CloudEventProviderDataKey"/>. Replacing the message would break every consumer that reads
/// the raw body, and acknowledgement still has to address the original message.
/// </para>
/// <para>
/// <b>A message carrying no CloudEvents markers passes through untouched</b>, which is why the decoder is
/// a total primitive: "not a CloudEvent" is a normal, expected outcome on a shared transport, not an
/// error.
/// </para>
/// </remarks>
internal sealed class CloudEventDecodingTransportReceiver : DelegatingTransportReceiver
{
	/// <summary>
	/// The <see cref="TransportReceivedMessage.ProviderData"/> key under which a decoded
	/// <see cref="CloudEvent"/> is published.
	/// </summary>
	public const string CloudEventProviderDataKey = "cloudevent";

	/// <summary>
	/// The <see cref="TransportReceivedMessage.ProviderData"/> key under which the reason a message
	/// carrying CloudEvents markers could not be decoded is published.
	/// </summary>
	/// <remarks>
	/// A message with this key set is one the transport believes to be a CloudEvent and this decorator
	/// could not read. The value is the caught exception, so a consumer, a health check or a dead-letter
	/// policy can see both that decoding was attempted and why it failed.
	/// <para>
	/// With <see cref="CloudEventProviderDataKey"/> this gives three mutually exclusive, directly
	/// observable states: <b>decoded</b> (event present, error absent), <b>pass-through</b> (neither
	/// present) and <b>malformed</b> (error present, event absent). The failure is deliberately NOT
	/// carried under the event's own key — handing a consumer an exception where it expects a
	/// <see cref="CloudEvent"/> would be a cast trap worse than the defect being fixed.
	/// </para>
	/// </remarks>
	public const string CloudEventErrorProviderDataKey = "cloudevent.error";

	private readonly ICloudEventDecoder<TransportReceivedMessage> _decoder;

	/// <summary>
	/// Initializes a new instance of the <see cref="CloudEventDecodingTransportReceiver"/> class.
	/// </summary>
	/// <param name="innerReceiver">The receiver to decorate.</param>
	/// <param name="decoder">The decoder applied to each received message.</param>
	public CloudEventDecodingTransportReceiver(
		ITransportReceiver innerReceiver,
		ICloudEventDecoder<TransportReceivedMessage> decoder)
		: base(innerReceiver) =>
		_decoder = decoder ?? throw new ArgumentNullException(nameof(decoder));

	/// <inheritdoc />
	/// <remarks>
	/// <para>
	/// <b>Every message in the batch is delivered, including a malformed one.</b> A message whose
	/// CloudEvent cannot be decoded is marked and handed to the caller like any other — it is never
	/// dropped, and decoding it is what fails, not receiving it. One corrupt message must not fail
	/// delivery of the well-formed messages beside it: on a shared transport those may belong to
	/// entirely unrelated consumers, so failing the batch turns one bad publisher into an outage for
	/// everyone reading that queue.
	/// </para>
	/// <para>
	/// <b>Undecodable is not the same as absent, and the difference is carried in the message.</b> A
	/// malformed event does not simply arrive undecoded — it arrives with
	/// <see cref="CloudEventErrorProviderDataKey"/> set. Without that a consumer could not tell a
	/// corrupt CloudEvent from an ordinary non-CloudEvent message, which is the silent downgrade this
	/// decorator exists to prevent. The decoder remains total and still throws; this decorator is where
	/// the throw becomes an observable per-message outcome.
	/// </para>
	/// <para>
	/// <b>Do not describe this as "skipping" a message.</b> That word was used here once and its plain
	/// reading — that the message is not delivered — was copied into consumer-facing documentation while
	/// the qualification a paragraph later was not. The behaviour is delivery-with-an-annotation, and the
	/// wording has to say so on its own, because the sentence travels further than the paragraph.
	/// </para>
	/// </remarks>
	public override async Task<IReadOnlyList<TransportReceivedMessage>> ReceiveAsync(
		int maxMessages,
		CancellationToken cancellationToken)
	{
		var received = await base.ReceiveAsync(maxMessages, cancellationToken).ConfigureAwait(false);

		foreach (var message in received)
		{
			CloudEvent? cloudEvent;

			try
			{
				cloudEvent = await _decoder.TryDecodeAsync(message, cancellationToken).ConfigureAwait(false);
			}
			catch (Exception ex) when (ex is InvalidOperationException or JsonException or FormatException)
			{
				// Batch-isolated: this message is marked and the rest of the batch is delivered.
				//
				// CAUGHT BY ENUMERATION, NOT BY BREADTH. These three are the decoder's malformed-input
				// outcomes and nothing else: InvalidOperationException from its own four refusals, and
				// JsonException / FormatException from JsonDocument.Parse and the base64 payload decode --
				// a structured body that is not valid JSON is the canonical malformed CloudEvent, so
				// letting it escape would reintroduce exactly the batch failure this catch exists to
				// prevent. A broad catch is the wrong fix: it would convert a transport fault, an OOM or a
				// cancellation into a per-message annotation and hide it.
				message.ProviderData[CloudEventErrorProviderDataKey] = ex;
				continue;
			}

			if (cloudEvent is not null)
			{
				message.ProviderData[CloudEventProviderDataKey] = cloudEvent;
			}
		}

		return received;
	}
}

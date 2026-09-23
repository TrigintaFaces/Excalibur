// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.Transport.Decorators;

namespace Excalibur.Dispatch.Transport;

/// <summary>
/// Applies inbound CloudEvents decoding to a transport receiver.
/// </summary>
public static class CloudEventDecodingReceiverExtensions
{
	/// <summary>
	/// Wraps a receiver so that inbound CloudEvents are decoded and published on each received message.
	/// </summary>
	/// <param name="receiver">The receiver to wrap.</param>
	/// <param name="binding">
	/// The CloudEvents protocol binding this transport speaks, which decides how binary-mode attributes
	/// are named. Pass <see cref="CloudEventBinding.StructuredOnly"/> where the specification assigns the
	/// transport no binding.
	/// </param>
	/// <returns>A receiver that decodes inbound CloudEvents before returning messages to the caller.</returns>
	/// <remarks>
	/// <para>
	/// <b>Applied unconditionally, and that is safe because the decoder is total.</b> A message carrying no
	/// CloudEvents markers is returned untouched — "not a CloudEvent" is a defined outcome rather than an
	/// error — so a consumer who never uses CloudEvents sees no behavioural change. Making the wrap
	/// conditional on a separate opt-in is what would leave the receive path asymmetric with send, where
	/// encoding is applied automatically once CloudEvents is configured.
	/// </para>
	/// <para>
	/// The decoded event is attached to
	/// <see cref="TransportReceivedMessage.ProviderData"/>; the message itself is not replaced, so callers
	/// that read the raw body are unaffected and acknowledgement still addresses the original message.
	/// </para>
	/// </remarks>
	public static ITransportReceiver WithCloudEventDecoding(
		this ITransportReceiver receiver,
		CloudEventBinding binding)
	{
		ArgumentNullException.ThrowIfNull(receiver);
		ArgumentNullException.ThrowIfNull(binding);

		// Constructed here rather than resolved from the container, which is what lets the wrap stay
		// unconditional: there is no registration that can be missing, so a receiver cannot be
		// decorated-but-inert. It is per-binding rather than shared because the binding IS its state.
		return new CloudEventDecodingTransportReceiver(
			receiver,
			new TransportReceivedMessageCloudEventDecoder(binding));
	}
}

// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using CloudNative.CloudEvents;

namespace Excalibur.Dispatch.Transport;

/// <summary>
/// Attempts to decode a <see cref="CloudEvent" /> from a provider-specific inbound transport message.
/// </summary>
/// <typeparam name="TInbound"> The transport message type consumed on receive. </typeparam>
/// <remarks>
/// Split from the former <c>ICloudEventMapper&lt;T&gt;</c>. This is a TOTAL
/// primitive, unlike the removed <c>FromTransportMessageAsync</c> it replaces: every inbound message,
/// CloudEvent-shaped or not, has a defined outcome.
/// <list type="bullet">
///   <item><c>null</c> -- the message carries none of the CloudEvents markers; the caller passes the
///   original message through untouched.</item>
///   <item>a value -- the message decoded as a CloudEvent.</item>
///   <item>a thrown exception -- the message carries CloudEvents markers but is malformed.</item>
/// </list>
/// The framework ships exactly one implementation, over <see cref="TransportReceivedMessage"/>, in this
/// package -- <c>TransportReceivedMessage</c> is transport-neutral (every <see cref="ITransportReceiver"/>
/// normalizes into it before anything CloudEvents-aware sees the message), so one decoder serves every
/// provider instead of nine hand-written, divergent ones. <typeparamref name="TInbound"/> stays generic as
/// a seam, not a signal that more implementations are expected: only build a transport-specific decoder if
/// a provider proves it genuinely cannot be decoded from <see cref="TransportReceivedMessage"/>.
/// </remarks>
// INTERNAL UNTIL AN IMPLEMENTOR EXISTS. A public interface that nothing implements is an
// advertised capability a consumer cannot obtain: it appears in IntelliSense and in the API
// baseline, and the only thing a consumer can do with it is write their own implementation of a
// contract the framework does not yet honour on any transport. It becomes public in the same
// change that ships the first decoding transport, so the declaration and the capability arrive
// together.
internal interface ICloudEventDecoder<TInbound>
{
	/// <summary>
	/// Attempts to decode a <see cref="CloudEvent" /> from an inbound transport message.
	/// </summary>
	/// <param name="transportMessage"> The transport message to inspect. </param>
	/// <param name="cancellationToken"> Cancellation token for the async operation. </param>
	/// <returns>
	/// The decoded <see cref="CloudEvent" />, or <see langword="null" /> when the message carries none of
	/// the CloudEvents markers.
	/// </returns>
	Task<CloudEvent?> TryDecodeAsync(
		TInbound transportMessage,
		CancellationToken cancellationToken);
}

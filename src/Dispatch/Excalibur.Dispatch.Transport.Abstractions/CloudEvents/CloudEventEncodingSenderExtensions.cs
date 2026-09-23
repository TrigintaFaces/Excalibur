// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using CloudNative.CloudEvents;

using Excalibur.Dispatch.Transport.Decorators;

namespace Excalibur.Dispatch.Transport;

/// <summary>
/// Applies outbound CloudEvents encoding to a transport sender.
/// </summary>
public static class CloudEventEncodingSenderExtensions
{
	/// <summary>
	/// Wraps a sender so that a message carrying a <see cref="CloudEvent"/> is published in the CloudEvents
	/// structured JSON mode.
	/// </summary>
	/// <param name="sender">The sender to wrap.</param>
	/// <returns>A sender that encodes attached CloudEvents before handing the message to the transport.</returns>
	/// <remarks>
	/// <para>
	/// <b>Applied unconditionally, and that is safe because the encoding is opt-in per message.</b> A
	/// message reaches the wire byte-identical unless it explicitly carries a <see cref="CloudEvent"/>, so
	/// wrapping a sender never alters ordinary business traffic. This differs from the inbound decorator,
	/// which is safe because decoding is <i>total</i>; here the safety comes from the caller having to ask.
	/// </para>
	/// <para>
	/// Encoding uses structured mode, which carries the whole event as JSON in the body and identifies it
	/// by the content type alone. <b>That makes the content type load-bearing, and it is why this wrapper
	/// is applied per transport rather than everywhere.</b> A transport whose send path cannot carry a
	/// media type end to end — one whose native header for it is a fixed-width wire-format tag, for
	/// instance — will emit a body a conformant receiver cannot recognise as a CloudEvent, so it is not
	/// wrapped. Transports shipping a binary-mode encoder for their own protocol binding continue to use
	/// it; this wrapper is what gives a transport with neither a conformant send path at all.
	/// </para>
	/// </remarks>
	public static ITransportSender WithCloudEventEncoding(this ITransportSender sender)
	{
		ArgumentNullException.ThrowIfNull(sender);

		return new CloudEventEncodingTransportSender(sender);
	}
}

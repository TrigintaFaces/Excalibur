// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using CloudNative.CloudEvents;

using Excalibur.Dispatch.Transport.Decorators;

namespace Excalibur.Dispatch.Transport;

/// <summary>
/// Attaches a <see cref="CloudEvent"/> to an outbound message so the transport publishes it as a
/// CloudEvent.
/// </summary>
public static class CloudEventTransportMessageExtensions
{
	/// <summary>
	/// Marks the message to be published as a CloudEvent.
	/// </summary>
	/// <param name="message">The message to publish.</param>
	/// <param name="cloudEvent">The event to publish it as.</param>
	/// <returns>The same message, so the call can be chained onto construction.</returns>
	/// <remarks>
	/// <para>
	/// This is the only supported way to ask for CloudEvents encoding on the send path, and it exists
	/// because the alternative did not work. The encoder reads a marker from
	/// <see cref="TransportMessage.Properties"/>; without a typed method the caller had to know the
	/// marker's key, which lives on an internal type and so could only be supplied as a hand-written
	/// string copied out of framework source. <b>A capability reachable only by a literal nobody can
	/// discover is not opt-in, it is unreachable</b> — so the door is a method on the type the caller
	/// already holds, where it appears in completion beside everything else they can do to a message.
	/// </para>
	/// <para>
	/// Sending is unchanged for any message this is not called on. The encoding decorator rewrites only
	/// messages carrying the marker, so ordinary traffic reaches the wire byte-identical whether or not a
	/// transport is wrapped.
	/// </para>
	/// <para>
	/// The event is encoded in CloudEvents structured mode: the whole event becomes a JSON document in
	/// the body under the <c>application/cloudevents+json</c> content type. <b>That content type is the
	/// only thing identifying the message as a CloudEvent</b>, so encoding is enabled per transport rather
	/// than everywhere — a transport whose send path cannot carry a media type end to end would emit a
	/// body no conformant receiver could recognise. Transports shipping their own protocol-binding encoder
	/// continue to use it.
	/// </para>
	/// </remarks>
	public static TransportMessage WithCloudEvent(this TransportMessage message, CloudEvent cloudEvent)
	{
		ArgumentNullException.ThrowIfNull(message);
		ArgumentNullException.ThrowIfNull(cloudEvent);

		message.Properties[CloudEventEncodingTransportSender.CloudEventPropertyKey] = cloudEvent;

		return message;
	}
}

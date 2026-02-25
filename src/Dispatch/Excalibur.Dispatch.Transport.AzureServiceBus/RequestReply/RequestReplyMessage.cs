// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Transport.Azure;

/// <summary>
/// Represents a message in the request/reply pattern for Azure Service Bus.
/// </summary>
/// <remarks>
/// <para>
/// Contains the message body, correlation metadata, and session information used for
/// request/reply correlation via Azure Service Bus sessions.
/// </para>
/// </remarks>
public sealed class RequestReplyMessage
{
	/// <summary>
	/// Gets or sets the message identifier.
	/// </summary>
	/// <value>The unique message ID. Auto-generated if not set.</value>
	public string MessageId { get; set; } = Guid.NewGuid().ToString("N");

	/// <summary>
	/// Gets or sets the message body as a byte array.
	/// </summary>
	/// <value>The message body.</value>
	public byte[] Body { get; set; } = [];

	/// <summary>
	/// Gets or sets the correlation ID for linking requests and replies.
	/// </summary>
	/// <value>The correlation ID.</value>
	public string? CorrelationId { get; set; }

	/// <summary>
	/// Gets or sets the session ID for session-based correlation.
	/// </summary>
	/// <remarks>
	/// When sending a request, this is set to the reply session ID so the responder
	/// knows where to direct the reply. When receiving a reply, this contains the
	/// session ID the message was received on.
	/// </remarks>
	/// <value>The session ID.</value>
	public string? SessionId { get; set; }

	/// <summary>
	/// Gets or sets the content type of the message body.
	/// </summary>
	/// <value>The content type (e.g., "application/json").</value>
	public string? ContentType { get; set; }

	/// <summary>
	/// Gets or sets the subject (label) of the message.
	/// </summary>
	/// <value>The message subject.</value>
	public string? Subject { get; set; }

	/// <summary>
	/// Gets the application-specific properties for the message.
	/// </summary>
	/// <value>The application properties dictionary.</value>
	public Dictionary<string, object> Properties { get; } = new(StringComparer.Ordinal);

	/// <summary>
	/// Gets or sets the reply-to entity path for routing replies.
	/// </summary>
	/// <value>The reply-to queue or topic name.</value>
	public string? ReplyTo { get; set; }

	/// <summary>
	/// Gets or sets the reply-to session ID for session-based reply correlation.
	/// </summary>
	/// <value>The reply-to session ID.</value>
	public string? ReplyToSessionId { get; set; }

	/// <summary>
	/// Gets or sets the message time-to-live.
	/// </summary>
	/// <value>The time-to-live. Default is <c>null</c> (uses queue/topic default).</value>
	public TimeSpan? TimeToLive { get; set; }
}

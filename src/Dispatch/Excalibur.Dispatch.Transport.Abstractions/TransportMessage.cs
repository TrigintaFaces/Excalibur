// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text;

namespace Excalibur.Dispatch.Transport;

/// <summary>
/// Represents a message to be sent via a transport.
/// This is the slim replacement for <c>CloudMessage</c>, following the Microsoft.Extensions.AI minimal-type pattern.
/// </summary>
/// <remarks>
/// Transport-specific hints (ordering key, partition key, deduplication ID, scheduled time) flow via
/// <see cref="Properties"/> with well-known keys from <see cref="Diagnostics.TransportTelemetryConstants.PropertyKeys"/>.
/// Each transport reads these keys and maps them to native SDK concepts.
/// </remarks>
public sealed class TransportMessage
{
	/// <summary>
	/// Gets or sets the unique identifier of the message.
	/// </summary>
	/// <value>The unique identifier of the message.</value>
	public string Id { get; set; } = Guid.NewGuid().ToString();

	/// <summary>
	/// Gets or sets the message body content.
	/// </summary>
	/// <value>The message body as a byte buffer.</value>
	public ReadOnlyMemory<byte> Body { get; set; }

	/// <summary>
	/// Gets or sets the content type of the message body (e.g., "application/json").
	/// </summary>
	/// <value>The MIME content type of the body.</value>
	public string? ContentType { get; set; }

	/// <summary>
	/// Gets or sets the message type identifier for routing and processing.
	/// </summary>
	/// <value>The message type identifier.</value>
	public string? MessageType { get; set; }

	/// <summary>
	/// Gets or sets the correlation identifier for message tracking across services.
	/// </summary>
	/// <value>The correlation identifier.</value>
	public string? CorrelationId { get; set; }

	/// <summary>
	/// Gets or sets the subject/label of the message.
	/// </summary>
	/// <value>The message subject.</value>
	public string? Subject { get; set; }

	/// <summary>
	/// Gets or sets the time-to-live for the message.
	/// </summary>
	/// <value>The time-to-live duration, or <see langword="null"/> if unlimited.</value>
	public TimeSpan? TimeToLive { get; set; }

	/// <summary>
	/// Gets or sets the timestamp when the message was created.
	/// </summary>
	/// <value>The creation timestamp.</value>
	public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

	private Dictionary<string, object>? _properties;

	/// <summary>
	/// Gets custom message properties/attributes.
	/// </summary>
	/// <remarks>
	/// Used by decorators to pass transport-specific hints (ordering key, partition key, etc.)
	/// to the underlying transport implementation via well-known keys.
	/// The dictionary is lazily initialized on first access to avoid allocation when no properties are set.
	/// </remarks>
	/// <value>The message properties dictionary.</value>
	public Dictionary<string, object> Properties
	{
		get => _properties ??= [];
		init => _properties = value;
	}

	/// <summary>
	/// Gets a value indicating whether the <see cref="Properties"/> dictionary has been allocated.
	/// </summary>
	/// <value><see langword="true"/> if properties have been set; otherwise, <see langword="false"/>.</value>
	public bool HasProperties => _properties is { Count: > 0 };

	/// <summary>
	/// Creates a new <see cref="TransportMessage"/> from a byte array body.
	/// </summary>
	/// <param name="body">The message body as a byte array.</param>
	/// <returns>A new <see cref="TransportMessage"/> instance.</returns>
	public static TransportMessage FromBytes(byte[] body) => new() { Body = body };

	/// <summary>
	/// Creates a new <see cref="TransportMessage"/> from a string body using UTF-8 encoding.
	/// </summary>
	/// <param name="body">The message body as a string.</param>
	/// <returns>A new <see cref="TransportMessage"/> instance with content type set to "text/plain".</returns>
	public static TransportMessage FromString(string body) =>
		new() { Body = Encoding.UTF8.GetBytes(body), ContentType = "text/plain" };
}

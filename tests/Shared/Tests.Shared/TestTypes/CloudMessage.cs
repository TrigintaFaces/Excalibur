// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Tests.Shared.TestTypes;

/// <summary>
/// Test cloud message for production cloud message testing scenarios.
/// </summary>
public class CloudMessage
{
	/// <summary>
	/// Gets or sets the message ID.
	/// </summary>
	public string MessageId { get; set; } = Guid.NewGuid().ToString();

	/// <summary>
	/// Gets or sets the message body as bytes.
	/// </summary>
	public byte[]? Body { get; set; }

	/// <summary>
	/// Gets or sets the message content type.
	/// </summary>
	public string ContentType { get; set; } = "application/json";

	/// <summary>
	/// Gets or sets the correlation ID for message tracing.
	/// </summary>
	public string? CorrelationId { get; set; }

	/// <summary>
	/// Gets or sets the session ID for session-based messaging.
	/// </summary>
	public string? SessionId { get; set; }

	/// <summary>
	/// Gets or sets the message properties/headers.
	/// </summary>
	public IDictionary<string, object?> Properties { get; set; } = new Dictionary<string, object?>();

	/// <summary>
	/// Gets or sets the timestamp when the message was created.
	/// </summary>
	public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

	/// <summary>
	/// Gets or sets the message label/subject.
	/// </summary>
	public string? Label { get; set; }
}

/// <summary>
/// Interface for cloud message adapters used in testing.
/// </summary>
public interface ICloudMessageAdapter
{
	/// <summary>
	/// Converts a cloud message to a message envelope.
	/// </summary>
	/// <param name="cloudMessage">The cloud message to convert.</param>
	/// <returns>The message envelope.</returns>
	object ToEnvelope(CloudMessage cloudMessage);

	/// <summary>
	/// Converts a message envelope to a cloud message.
	/// </summary>
	/// <param name="envelope">The message envelope to convert.</param>
	/// <returns>The cloud message.</returns>
	CloudMessage FromEnvelope(object envelope);
}

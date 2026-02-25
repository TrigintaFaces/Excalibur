// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions.Validation;

/// <summary>
/// Provides context for message validation operations.
/// </summary>
/// <remarks> Initializes a new instance of the <see cref="MessageValidationContext" /> class. </remarks>
/// <param name="messageId"> The message identifier. </param>
/// <param name="messageType"> The message type. </param>
public sealed class MessageValidationContext(string messageId, Type messageType)
{
	/// <summary>
	/// Gets the message identifier.
	/// </summary>
	/// <value> The identifier associated with the message under validation. </value>
	public string MessageId { get; } = messageId ?? throw new ArgumentNullException(nameof(messageId));

	/// <summary>
	/// Gets the message type.
	/// </summary>
	/// <value> The runtime type of the message. </value>
	public Type MessageType { get; } = messageType ?? throw new ArgumentNullException(nameof(messageType));

	/// <summary>
	/// Gets or sets the correlation identifier.
	/// </summary>
	/// <value> The optional correlation identifier. </value>
	public string? CorrelationId { get; set; }

	/// <summary>
	/// Gets or sets the tenant identifier.
	/// </summary>
	/// <value> The optional tenant identifier. </value>
	public string? TenantId { get; set; }

	/// <summary>
	/// Gets additional properties for the validation context.
	/// </summary>
	/// <value> A property bag available to validators. </value>
	public IDictionary<string, object> Properties { get; init; } = new Dictionary<string, object>(StringComparer.Ordinal);

	/// <summary>
	/// Gets or sets the timestamp when validation occurred.
	/// </summary>
	/// <value> The timestamp captured for the validation operation. </value>
	public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
}

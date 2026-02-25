// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Transport.Kafka;

/// <summary>
/// Represents the result of resolving a schema ID to a .NET type.
/// </summary>
/// <remarks>
/// <para>
/// This type is immutable and can be safely cached. Resolution is performed once
/// per schema ID and reused for subsequent messages with the same schema.
/// </para>
/// </remarks>
public sealed class SchemaTypeResolution
{
	private SchemaTypeResolution(
		int schemaId,
		Type messageType,
		string messageTypeName,
		string schemaJson,
		bool isSuccess,
		string? failureReason)
	{
		SchemaId = schemaId;
		MessageType = messageType;
		MessageTypeName = messageTypeName;
		SchemaJson = schemaJson;
		IsSuccess = isSuccess;
		FailureReason = failureReason;
	}

	/// <summary>
	/// Gets the Schema Registry ID.
	/// </summary>
	/// <value>The schema ID from the wire format header.</value>
	public int SchemaId { get; }

	/// <summary>
	/// Gets the resolved .NET type.
	/// </summary>
	/// <value>The .NET type to deserialize to.</value>
	public Type MessageType { get; }

	/// <summary>
	/// Gets the message type name from the JSON Schema <c>title</c> property.
	/// </summary>
	/// <value>The message type name (e.g., "OrderCreated").</value>
	public string MessageTypeName { get; }

	/// <summary>
	/// Gets the JSON Schema content from the Schema Registry.
	/// </summary>
	/// <value>The raw JSON Schema string.</value>
	public string SchemaJson { get; }

	/// <summary>
	/// Gets a value indicating whether the resolution was successful.
	/// </summary>
	/// <value><see langword="true"/> if successful; otherwise, <see langword="false"/>.</value>
	public bool IsSuccess { get; }

	/// <summary>
	/// Gets the failure reason if resolution failed.
	/// </summary>
	/// <value>The failure reason, or <see langword="null"/> if successful.</value>
	public string? FailureReason { get; }

	/// <summary>
	/// Creates a successful type resolution.
	/// </summary>
	/// <param name="schemaId">The Schema Registry ID.</param>
	/// <param name="messageType">The resolved .NET type.</param>
	/// <param name="messageTypeName">The message type name from the schema.</param>
	/// <param name="schemaJson">The JSON Schema content.</param>
	/// <returns>A successful resolution instance.</returns>
	public static SchemaTypeResolution Success(int schemaId, Type messageType, string messageTypeName, string schemaJson)
	{
		ArgumentNullException.ThrowIfNull(messageType);
		ArgumentNullException.ThrowIfNull(messageTypeName);
		ArgumentNullException.ThrowIfNull(schemaJson);

		return new SchemaTypeResolution(schemaId, messageType, messageTypeName, schemaJson, true, null);
	}

	/// <summary>
	/// Creates a failed resolution result.
	/// </summary>
	/// <param name="schemaId">The schema ID that failed to resolve.</param>
	/// <param name="schemaJson">The JSON Schema content (if available).</param>
	/// <param name="reason">The reason for failure.</param>
	/// <returns>A failed resolution instance.</returns>
	public static SchemaTypeResolution Failed(int schemaId, string? schemaJson, string reason)
	{
		return new SchemaTypeResolution(schemaId, typeof(object), string.Empty, schemaJson ?? string.Empty, false, reason);
	}
}

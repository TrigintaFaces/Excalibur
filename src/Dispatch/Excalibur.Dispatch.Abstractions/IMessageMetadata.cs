// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions;

/// <summary>
/// Represents metadata associated with a message for serialization and transport.
/// </summary>
/// <remarks>
/// This interface defines the standard metadata properties that flow with messages across service boundaries. Implementations typically
/// serialize this metadata as message headers or properties in the underlying transport mechanism. All properties use init-only setters to
/// ensure immutability after creation.
/// </remarks>
public interface ITransportMessageMetadata
{
	/// <summary>
	/// Gets the correlation identifier that groups related messages together.
	/// </summary>
	/// <remarks>
	/// This ID flows through all messages in a business transaction or workflow, enabling end-to-end tracing across service boundaries.
	/// </remarks>
	/// <value> The unique correlation identifier. </value>
	string CorrelationId { get; init; }

	/// <summary>
	/// Gets the identifier of the message that caused this message to be created.
	/// </summary>
	/// <remarks> Forms a causality chain for debugging. May be null for root messages that were not caused by another message. </remarks>
	/// <value> The causation message identifier or <see langword="null" />. </value>
	string? CausationId { get; init; }

	/// <summary>
	/// Gets the W3C trace context header for distributed tracing integration.
	/// </summary>
	/// <remarks>
	/// Contains trace-id and span-id in W3C format. Integrates with OpenTelemetry and other distributed tracing systems for observability.
	/// </remarks>
	/// <value> The W3C trace context header or <see langword="null" />. </value>
	string? TraceParent { get; init; }

	/// <summary>
	/// Gets the tenant identifier for multi-tenant message routing and isolation.
	/// </summary>
	/// <remarks>
	/// Used to ensure messages are processed in the correct tenant context and to enforce data isolation in multi-tenant systems.
	/// </remarks>
	/// <value> The tenant identifier or <see langword="null" />. </value>
	string? TenantId { get; init; }

	/// <summary>
	/// Gets the identifier of the user who initiated this message.
	/// </summary>
	/// <remarks> Used for audit trails and authorization. May be null for system-initiated messages or anonymous operations. </remarks>
	/// <value> The initiating user identifier or <see langword="null" />. </value>
	string? UserId { get; init; }

	/// <summary>
	/// Gets the MIME type of the message payload serialization format.
	/// </summary>
	/// <remarks>
	/// Common values include "application/json", "application/x-msgpack", or "application/octet-stream". Used to select the appropriate deserializer.
	/// </remarks>
	/// <value> The payload MIME type. </value>
	string ContentType { get; init; }

	/// <summary>
	/// Gets the version of the serializer used to encode the message.
	/// </summary>
	/// <remarks>
	/// Enables backward compatibility when serialization libraries are upgraded. Format is typically "major.minor" (e.g., "1.0", "2.1").
	/// </remarks>
	/// <value> The serializer version. </value>
	string SerializerVersion { get; init; }

	/// <summary>
	/// Gets the schema version of the message payload structure.
	/// </summary>
	/// <remarks> Indicates the version of the message contract. Used by versioning middleware to apply migrations between versions. </remarks>
	/// <value> The payload schema version. </value>
	string MessageVersion { get; init; }

	/// <summary>
	/// Gets the overall API contract version this message adheres to.
	/// </summary>
	/// <remarks>
	/// Represents a higher-level versioning scheme that may encompass multiple message versions within a service or bounded context.
	/// </remarks>
	/// <value> The API contract version. </value>
	string ContractVersion { get; init; }
}

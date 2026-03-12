// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Abstractions;

/// <summary>
/// Builder interface for creating immutable message metadata instances. Provides a minimal fluent API
/// for constructing message metadata with core envelope properties and extensibility via <see cref="WithProperty"/>.
/// </summary>
/// <remarks>
/// <para>
/// This interface follows the Microsoft design guideline of keeping interfaces small (max 5 methods + properties bag).
/// Domain-specific builder methods (identity, routing, temporal, transport, event sourcing) are available as
/// extension methods in <c>Excalibur.Dispatch.Abstractions</c>:
/// </para>
/// <list type="bullet">
/// <item><description><c>MetadataBuilderIdentityExtensions</c> -- WithExternalId, WithUserId, WithTenantId, WithTraceParent, WithTraceState, WithBaggage, WithRoles, WithClaims, AddRole</description></item>
/// <item><description><c>MetadataBuilderVersioningExtensions</c> -- WithContentEncoding, WithMessageVersion, WithSerializerVersion, WithContractVersion</description></item>
/// <item><description><c>MetadataBuilderRoutingExtensions</c> -- WithSource, WithDestination, WithReplyTo, WithSessionId, WithPartitionKey, WithRoutingKey, WithGroupId, WithGroupSequence, WithMessageType, WithContentType</description></item>
/// <item><description><c>MetadataBuilderTemporalExtensions</c> -- WithCreatedTimestampUtc, WithSentTimestampUtc, WithReceivedTimestampUtc, WithScheduledEnqueueTimeUtc, WithTimeToLive, WithExpiresAtUtc, WithTiming</description></item>
/// <item><description><c>MetadataBuilderTransportExtensions</c> -- WithDeliveryCount, WithMaxDeliveryCount, WithLastDeliveryError, WithDeadLetter*, WithPriority, WithDurable, WithDuplicateDetection</description></item>
/// <item><description><c>MetadataBuilderEventSourcingExtensions</c> -- WithEventSourcing, WithGlobalPosition, WithEventType, WithEventVersion</description></item>
/// <item><description><c>MetadataBuilderCollectionExtensions</c> -- AddHeaders, AddAttributes, AddProperties, AddItems</description></item>
/// </list>
/// </remarks>
public interface IMessageMetadataBuilder
{
	/// <summary>
	/// Gets an optional marker type for tooling.
	/// </summary>
	/// <value> The optional marker type for tooling. </value>
	Type? MarkerType { get; }

	/// <summary>
	/// Sets the message identifier and optionally the correlation identifier if not already set.
	/// </summary>
	/// <param name="messageId"> The unique message identifier. </param>
	/// <returns> The builder instance for method chaining. </returns>
	IMessageMetadataBuilder WithMessageId(string messageId);

	/// <summary>
	/// Sets the correlation identifier for message tracing.
	/// </summary>
	/// <param name="correlationId"> The correlation identifier to track related messages. </param>
	/// <returns> The builder instance for method chaining. </returns>
	IMessageMetadataBuilder WithCorrelationId(string correlationId);

	/// <summary>
	/// Sets the causation identifier linking this message to its immediate cause.
	/// </summary>
	/// <param name="causationId"> The identifier of the message that caused this message. </param>
	/// <returns> The builder instance for method chaining. </returns>
	IMessageMetadataBuilder WithCausationId(string? causationId);

	/// <summary>
	/// Sets a named property on the metadata builder. Well-known property keys from
	/// <see cref="MetadataPropertyKeys"/> are dispatched to typed internal fields by the
	/// concrete builder; unknown keys are stored in the general-purpose properties dictionary.
	/// </summary>
	/// <param name="key"> The property key. </param>
	/// <param name="value"> The property value, or null to remove. </param>
	/// <returns> The builder instance for method chaining. </returns>
	IMessageMetadataBuilder WithProperty(string key, object? value);

	/// <summary>
	/// Adds a single header to the message metadata.
	/// </summary>
	/// <param name="key"> The header key. </param>
	/// <param name="value"> The header value. </param>
	/// <returns> The builder instance for method chaining. </returns>
	IMessageMetadataBuilder AddHeader(string key, string value);

	/// <summary>
	/// Builds the immutable message metadata instance from the configured values.
	/// </summary>
	/// <returns> An immutable <see cref="IMessageMetadata" /> instance. </returns>
	IMessageMetadata Build();
}

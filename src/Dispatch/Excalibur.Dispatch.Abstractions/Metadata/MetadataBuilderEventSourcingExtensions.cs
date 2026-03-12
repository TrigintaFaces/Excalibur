// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Abstractions;

/// <summary>
/// Extension methods for setting event sourcing properties on <see cref="IMessageMetadataBuilder"/>.
/// </summary>
public static class MetadataBuilderEventSourcingExtensions
{
	/// <summary>
	/// Sets event sourcing metadata in a single call.
	/// </summary>
	/// <param name="builder"> The builder instance. </param>
	/// <param name="aggregateId"> The aggregate identifier. </param>
	/// <param name="aggregateType"> The aggregate type name. </param>
	/// <param name="aggregateVersion"> The aggregate version. </param>
	/// <param name="streamName"> The event stream name. </param>
	/// <param name="streamPosition"> The position within the event stream. </param>
	/// <returns> The builder instance for method chaining. </returns>
	public static IMessageMetadataBuilder WithEventSourcing(
		this IMessageMetadataBuilder builder,
		string? aggregateId,
		string? aggregateType = null,
		long? aggregateVersion = null,
		string? streamName = null,
		long? streamPosition = null)
	{
		return builder
			.WithProperty(MetadataPropertyKeys.AggregateId, aggregateId)
			.WithProperty(MetadataPropertyKeys.AggregateType, aggregateType)
			.WithProperty(MetadataPropertyKeys.AggregateVersion, aggregateVersion)
			.WithProperty(MetadataPropertyKeys.StreamName, streamName)
			.WithProperty(MetadataPropertyKeys.StreamPosition, streamPosition);
	}

	/// <summary>
	/// Sets the global position in the event store.
	/// </summary>
	/// <param name="builder"> The builder instance. </param>
	/// <param name="globalPosition"> The global position across all streams. </param>
	/// <returns> The builder instance for method chaining. </returns>
	public static IMessageMetadataBuilder WithGlobalPosition(this IMessageMetadataBuilder builder, long globalPosition)
		=> builder.WithProperty(MetadataPropertyKeys.GlobalPosition, globalPosition);

	/// <summary>
	/// Sets the event type name.
	/// </summary>
	/// <param name="builder"> The builder instance. </param>
	/// <param name="eventType"> The type name of the event. </param>
	/// <returns> The builder instance for method chaining. </returns>
	public static IMessageMetadataBuilder WithEventType(this IMessageMetadataBuilder builder, string? eventType)
		=> builder.WithProperty(MetadataPropertyKeys.EventType, eventType);

	/// <summary>
	/// Sets the event version for event schema evolution.
	/// </summary>
	/// <param name="builder"> The builder instance. </param>
	/// <param name="eventVersion"> The version of the event schema. </param>
	/// <returns> The builder instance for method chaining. </returns>
	public static IMessageMetadataBuilder WithEventVersion(this IMessageMetadataBuilder builder, int eventVersion)
		=> builder.WithProperty(MetadataPropertyKeys.EventVersion, eventVersion);
}

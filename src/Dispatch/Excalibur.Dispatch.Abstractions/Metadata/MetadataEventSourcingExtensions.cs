// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Abstractions;

/// <summary>
/// Extension methods for accessing event sourcing metadata from <see cref="IMessageMetadata.Properties"/>.
/// </summary>
public static class MetadataEventSourcingExtensions
{
	/// <summary>
	/// Gets the aggregate identifier for event sourcing scenarios.
	/// </summary>
	/// <param name="metadata"> The message metadata. </param>
	/// <returns> The aggregate identifier, or null if not set. </returns>
	public static string? GetAggregateId(this IMessageMetadata metadata)
		=> metadata.Properties.TryGetValue(MetadataPropertyKeys.AggregateId, out var value) ? value as string : null;

	/// <summary>
	/// Gets the type of the aggregate for event sourcing.
	/// </summary>
	/// <param name="metadata"> The message metadata. </param>
	/// <returns> The aggregate type, or null if not set. </returns>
	public static string? GetAggregateType(this IMessageMetadata metadata)
		=> metadata.Properties.TryGetValue(MetadataPropertyKeys.AggregateType, out var value) ? value as string : null;

	/// <summary>
	/// Gets the version of the aggregate after this event.
	/// </summary>
	/// <param name="metadata"> The message metadata. </param>
	/// <returns> The aggregate version, or null if not set. </returns>
	public static long? GetAggregateVersion(this IMessageMetadata metadata)
		=> metadata.Properties.TryGetValue(MetadataPropertyKeys.AggregateVersion, out var value) && value is long ver ? ver : null;

	/// <summary>
	/// Gets the name of the event stream.
	/// </summary>
	/// <param name="metadata"> The message metadata. </param>
	/// <returns> The stream name, or null if not set. </returns>
	public static string? GetStreamName(this IMessageMetadata metadata)
		=> metadata.Properties.TryGetValue(MetadataPropertyKeys.StreamName, out var value) ? value as string : null;

	/// <summary>
	/// Gets the position of this event in the stream.
	/// </summary>
	/// <param name="metadata"> The message metadata. </param>
	/// <returns> The stream position, or null if not set. </returns>
	public static long? GetStreamPosition(this IMessageMetadata metadata)
		=> metadata.Properties.TryGetValue(MetadataPropertyKeys.StreamPosition, out var value) && value is long pos ? pos : null;

	/// <summary>
	/// Gets the global position of this event in the event store.
	/// </summary>
	/// <param name="metadata"> The message metadata. </param>
	/// <returns> The global position, or null if not set. </returns>
	public static long? GetGlobalPosition(this IMessageMetadata metadata)
		=> metadata.Properties.TryGetValue(MetadataPropertyKeys.GlobalPosition, out var value) && value is long pos ? pos : null;

	/// <summary>
	/// Gets the event type for event sourcing.
	/// </summary>
	/// <param name="metadata"> The message metadata. </param>
	/// <returns> The event type, or null if not set. </returns>
	public static string? GetEventType(this IMessageMetadata metadata)
		=> metadata.Properties.TryGetValue(MetadataPropertyKeys.EventType, out var value) ? value as string : null;

	/// <summary>
	/// Gets the event version for schema evolution.
	/// </summary>
	/// <param name="metadata"> The message metadata. </param>
	/// <returns> The event version, or null if not set. </returns>
	public static int? GetEventVersion(this IMessageMetadata metadata)
		=> metadata.Properties.TryGetValue(MetadataPropertyKeys.EventVersion, out var value) && value is int ver ? ver : null;
}

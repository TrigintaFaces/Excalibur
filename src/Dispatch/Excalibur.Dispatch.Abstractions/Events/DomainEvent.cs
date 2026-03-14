// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Messaging;

namespace Excalibur.Dispatch.Abstractions;

/// <summary>
/// Base record for domain events with clean, parameterless construction.
/// </summary>
/// <remarks>
/// <para>
/// Provides a low-boilerplate base for defining domain events as records:
/// </para>
/// <code>
/// public record OrderCreated(string OrderId, decimal Total) : DomainEvent
/// {
///     public override string AggregateId => OrderId;
/// }
///
/// // With metadata
/// var evt = new OrderCreated("ord-1", 99.99m)
///     .WithCorrelationId(correlationId);
/// </code>
/// <para>
/// Default behavior:
/// <list type="bullet">
/// <item><description><see cref="EventId"/>: Auto-generated UUID v7 string for time-ordered uniqueness</description></item>
/// <item><description><see cref="AggregateId"/>: Empty string (override in derived records)</description></item>
/// <item><description><see cref="Version"/>: 0 (set by infrastructure during event sourcing)</description></item>
/// <item><description><see cref="OccurredAt"/>: UTC timestamp at construction time</description></item>
/// <item><description><see cref="EventType"/>: Derived type name</description></item>
/// <item><description><see cref="Metadata"/>: Null (attach via fluent API or infrastructure)</description></item>
/// </list>
/// </para>
/// </remarks>
public abstract record DomainEvent : IDomainEvent
{
	/// <inheritdoc/>
	public virtual string EventId { get; init; } = Uuid7Extensions.GenerateGuid().ToString();

	/// <inheritdoc/>
	public virtual string AggregateId { get; init; } = string.Empty;

	/// <inheritdoc/>
	public virtual long Version { get; init; }

	/// <inheritdoc/>
	public virtual DateTimeOffset OccurredAt { get; init; } = TimeProvider.System.GetUtcNow();

	/// <inheritdoc/>
	public virtual string EventType => GetType().Name;

	/// <inheritdoc/>
	public virtual IDictionary<string, object>? Metadata { get; init; }

	/// <summary>
	/// Adds metadata to this event.
	/// </summary>
	/// <param name="key">The metadata key.</param>
	/// <param name="value">The metadata value.</param>
	/// <returns>This event instance for method chaining.</returns>
	public DomainEvent WithMetadata(string key, object value)
	{
		Dictionary<string, object> metadata;
		if (Metadata is Dictionary<string, object> existing)
		{
			metadata = new Dictionary<string, object>(existing, StringComparer.Ordinal);
		}
		else if (Metadata is not null)
		{
			metadata = new Dictionary<string, object>(StringComparer.Ordinal);
			foreach (var kvp in Metadata)
			{
				metadata[kvp.Key] = kvp.Value;
			}
		}
		else
		{
			metadata = new Dictionary<string, object>(StringComparer.Ordinal);
		}
		metadata[key] = value;
		return this with { Metadata = metadata };
	}

	/// <summary>
	/// Adds a correlation ID to this event's metadata.
	/// </summary>
	/// <param name="correlationId">The correlation ID for tracking across services.</param>
	/// <returns>This event instance for method chaining.</returns>
	public DomainEvent WithCorrelationId(Guid correlationId)
	{
		if (correlationId != Guid.Empty)
		{
			return WithMetadata("CorrelationId", correlationId.ToString());
		}

		return this;
	}

	/// <summary>
	/// Adds a causation ID to this event's metadata.
	/// </summary>
	/// <param name="causationId">The ID of the event that caused this event.</param>
	/// <returns>This event instance for method chaining.</returns>
	public DomainEvent WithCausationId(string causationId)
	{
		if (!string.IsNullOrEmpty(causationId))
		{
			return WithMetadata("CausationId", causationId);
		}

		return this;
	}
}

// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Messaging;

namespace Excalibur.Dispatch;

/// <summary>
/// Base record for domain events with clean, parameterless construction.
/// </summary>
/// <remarks>
/// <para>
/// Provides a low-boilerplate base for defining domain events as records:
/// </para>
/// <code>
/// public record OrderCreated(string OrderId, decimal Total) : DomainEvent;
///
/// // With metadata
/// var evt = new OrderCreated("ord-1", 99.99m)
///     .WithCorrelationId(correlationId);
/// </code>
/// <para>
/// Default behavior:
/// <list type="bullet">
/// <item><description><see cref="EventId"/>: Auto-generated UUID v7 string for time-ordered uniqueness</description></item>
/// <item><description><see cref="OccurredAt"/>: UTC timestamp at construction time</description></item>
/// <item><description><see cref="Metadata"/>: Null (attach via fluent API or infrastructure)</description></item>
/// </list>
/// </para>
/// </remarks>
public abstract record DomainEvent : IDomainEvent
{
	/// <inheritdoc/>
	public virtual string EventId { get; init; } = Uuid7Extensions.GenerateGuid().ToString();

	/// <inheritdoc/>
	public virtual DateTimeOffset OccurredAt { get; init; } = TimeProvider.System.GetUtcNow();


	/// <inheritdoc/>
	public virtual IDictionary<string, object>? Metadata { get; init; }

	/// <inheritdoc/>
	/// <remarks>
	/// Computed from <see cref="Metadata"/>, not an independent init-only field. An independent
	/// backing field used to exist here and was a second, unread carrier: <see cref="WithCorrelationId(string?)"/>
	/// wrote to it while every real consumer (<c>EventSourcedRepository</c>'s Activity-context enrichment,
	/// and all six event-store <c>ExtractCorrelationId</c> implementations) reads <see cref="Metadata"/>
	/// only. Re-declared here (rather than left to <see cref="IDomainEvent"/>'s default implementation) so
	/// a <see cref="DomainEvent"/>-typed reference can still read it directly; the get body matches the
	/// interface default exactly (same key priority — see <see cref="IDomainEvent.CorrelationId"/>), so
	/// this is a pure, non-divergent projection of <see cref="Metadata"/>.
	/// </remarks>
	public virtual string? CorrelationId =>
		Metadata?.TryGetValue(OutboxHeaderNames.CorrelationId, out var v1) == true ? v1?.ToString() :
		Metadata?.TryGetValue("CorrelationId", out var v2) == true ? v2?.ToString() :
		Metadata?.TryGetValue("correlationId", out var v3) == true ? v3?.ToString() : null;

	/// <inheritdoc/>
	/// <remarks>
	/// Computed from <see cref="Metadata"/>. See <see cref="CorrelationId"/> for why this is redeclared
	/// rather than left to the interface default.
	/// </remarks>
	public virtual string? CausationId =>
		Metadata?.TryGetValue(OutboxHeaderNames.CausationId, out var v1) == true ? v1?.ToString() :
		Metadata?.TryGetValue("CausationId", out var v2) == true ? v2?.ToString() :
		Metadata?.TryGetValue("causationId", out var v3) == true ? v3?.ToString() : null;

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
	/// Sets the correlation ID for tracking across services.
	/// </summary>
	/// <param name="correlationId">The correlation ID.</param>
	/// <returns>A new event instance with the correlation ID set.</returns>
	public DomainEvent WithCorrelationId(Guid correlationId)
	{
		return correlationId != Guid.Empty
			? WithMetadata(OutboxHeaderNames.CorrelationId, correlationId.ToString())
			: this;
	}

	/// <summary>
	/// Sets the correlation ID for tracking across services.
	/// </summary>
	/// <param name="correlationId">The correlation ID string.</param>
	/// <returns>A new event instance with the correlation ID set.</returns>
	public DomainEvent WithCorrelationId(string? correlationId)
	{
		return !string.IsNullOrEmpty(correlationId)
			? WithMetadata(OutboxHeaderNames.CorrelationId, correlationId)
			: this;
	}

	/// <summary>
	/// Sets the causation ID identifying the command or event that caused this event.
	/// </summary>
	/// <param name="causationId">The ID of the event that caused this event.</param>
	/// <returns>A new event instance with the causation ID set.</returns>
	public DomainEvent WithCausationId(string? causationId)
	{
		return !string.IsNullOrEmpty(causationId)
			? WithMetadata(OutboxHeaderNames.CausationId, causationId)
			: this;
	}
}

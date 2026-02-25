// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Text.Json.Serialization;

using Excalibur.Dispatch.Abstractions.Messaging;

namespace Excalibur.Dispatch.Abstractions;

/// <summary>
/// Base record for domain events providing immutable, auto-generated event properties.
/// </summary>
/// <remarks>
/// This record provides:
/// <list type="bullet">
/// <item>Auto-generated EventId using UUID v7 for time-ordered, globally unique identifiers</item>
/// <item>Clock abstraction for testable OccurredAt timestamps</item>
/// <item>Immutable record semantics for thread safety and predictability</item>
/// <item>Zero-allocation hot paths through aggressive inlining</item>
/// </list>
/// </remarks>
public abstract record DomainEvent : IDomainEvent
{
	private readonly Dictionary<string, object> _metadata;

	/// <summary>
	/// Initializes a new instance of the <see cref="DomainEvent"/> record using the system clock.
	/// </summary>
	/// <param name="aggregateId">The identifier of the aggregate that raised this event.</param>
	/// <param name="version">The version of the aggregate after this event was applied.</param>
	protected DomainEvent(string aggregateId, long version)
		: this(aggregateId, version, TimeProvider.System)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="DomainEvent"/> record with a custom time provider.
	/// </summary>
	/// <param name="aggregateId">The identifier of the aggregate that raised this event.</param>
	/// <param name="version">The version of the aggregate after this event was applied.</param>
	/// <param name="timeProvider">The time provider to use for timestamp generation.</param>
	protected DomainEvent(string aggregateId, long version, TimeProvider timeProvider)
	{
		ArgumentNullException.ThrowIfNull(timeProvider);

		var id = Uuid7Extensions.GenerateGuid();
		var timestamp = timeProvider.GetUtcNow();

		Id = id;
		EventId = id.ToString();
		MessageId = EventId;
		AggregateId = aggregateId ?? string.Empty;
		Version = version;
		Timestamp = timestamp;
		OccurredAt = timestamp;
		_metadata = new Dictionary<string, object>(StringComparer.Ordinal);
	}

	/// <summary>
	/// Gets the unique identifier for this event as a GUID.
	/// </summary>
	/// <value>A UUID v7 identifier providing time-ordered uniqueness.</value>
	public Guid Id { get; init; }

	/// <inheritdoc/>
	public string EventId { get; init; }

	/// <inheritdoc/>
	public string AggregateId { get; init; }

	/// <inheritdoc/>
	public long Version { get; init; }

	/// <inheritdoc/>
	public DateTimeOffset OccurredAt { get; init; }

	/// <inheritdoc/>
	public string EventType => GetType().Name;

	/// <inheritdoc/>
	public IDictionary<string, object>? Metadata => _metadata;

	/// <inheritdoc/>
	public string MessageId { get; init; }

	/// <inheritdoc/>
	public string MessageType => EventType;

	/// <inheritdoc/>
	public MessageKinds Kind => MessageKinds.Event;

	/// <inheritdoc/>
	public DateTimeOffset Timestamp { get; init; }

	/// <inheritdoc/>
	[JsonIgnore]
	public IReadOnlyDictionary<string, object> Headers => _metadata;

	/// <inheritdoc/>
	[JsonIgnore]
	public object Body => this;

	/// <inheritdoc/>
	[JsonIgnore]
	public IMessageFeatures Features { get; init; } = new DefaultMessageFeatures();

	/// <summary>
	/// Adds metadata to this event.
	/// </summary>
	/// <param name="key">The metadata key.</param>
	/// <param name="value">The metadata value.</param>
	/// <returns>This event instance for method chaining.</returns>
	public DomainEvent WithMetadata(string key, object value)
	{
		_metadata[key] = value;
		return this;
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
			_metadata["CorrelationId"] = correlationId.ToString();
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
			_metadata["CausationId"] = causationId;
		}

		return this;
	}
}

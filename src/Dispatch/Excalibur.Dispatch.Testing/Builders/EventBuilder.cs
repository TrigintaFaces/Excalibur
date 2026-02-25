// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Testing.Builders;

/// <summary>
/// Fluent builder for creating <see cref="IDomainEvent"/> instances in tests.
/// Provides sensible defaults for all properties with override capability.
/// </summary>
/// <remarks>
/// <para>
/// Example:
/// <code>
/// var domainEvent = new EventBuilder()
///     .WithAggregateId("order-123")
///     .WithEventType("OrderPlaced")
///     .WithData("test-payload")
///     .Build();
/// </code>
/// </para>
/// </remarks>
public sealed class EventBuilder
{
	private string? _eventId;
	private string? _aggregateId;
	private long _version;
	private DateTimeOffset? _occurredAt;
	private string _eventType = "TestEvent";
	private string _data = string.Empty;
	private IDictionary<string, object>? _metadata;

	/// <summary>
	/// Sets the event ID. If not set, a new GUID is generated.
	/// </summary>
	/// <param name="eventId">The event ID.</param>
	/// <returns>This builder for chaining.</returns>
	public EventBuilder WithEventId(string eventId)
	{
		_eventId = eventId;
		return this;
	}

	/// <summary>
	/// Sets the aggregate ID. If not set, a new GUID is generated.
	/// </summary>
	/// <param name="aggregateId">The aggregate ID.</param>
	/// <returns>This builder for chaining.</returns>
	public EventBuilder WithAggregateId(string aggregateId)
	{
		_aggregateId = aggregateId;
		return this;
	}

	/// <summary>
	/// Sets the event version.
	/// </summary>
	/// <param name="version">The event version.</param>
	/// <returns>This builder for chaining.</returns>
	public EventBuilder WithVersion(long version)
	{
		_version = version;
		return this;
	}

	/// <summary>
	/// Sets the occurred at timestamp.
	/// </summary>
	/// <param name="occurredAt">The timestamp.</param>
	/// <returns>This builder for chaining.</returns>
	public EventBuilder WithOccurredAt(DateTimeOffset occurredAt)
	{
		_occurredAt = occurredAt;
		return this;
	}

	/// <summary>
	/// Sets the event type.
	/// </summary>
	/// <param name="eventType">The event type.</param>
	/// <returns>This builder for chaining.</returns>
	public EventBuilder WithEventType(string eventType)
	{
		_eventType = eventType;
		return this;
	}

	/// <summary>
	/// Sets the event data payload.
	/// </summary>
	/// <param name="data">The data payload.</param>
	/// <returns>This builder for chaining.</returns>
	public EventBuilder WithData(string data)
	{
		_data = data;
		return this;
	}

	/// <summary>
	/// Sets the event metadata.
	/// </summary>
	/// <param name="metadata">The metadata dictionary.</param>
	/// <returns>This builder for chaining.</returns>
	public EventBuilder WithMetadata(IDictionary<string, object> metadata)
	{
		_metadata = metadata;
		return this;
	}

	/// <summary>
	/// Adds a single metadata entry.
	/// </summary>
	/// <param name="key">The metadata key.</param>
	/// <param name="value">The metadata value.</param>
	/// <returns>This builder for chaining.</returns>
	public EventBuilder WithMetadata(string key, object value)
	{
		_metadata ??= new Dictionary<string, object>();
		_metadata[key] = value;
		return this;
	}

	/// <summary>
	/// Builds a <see cref="TestDomainEvent"/> with the configured properties.
	/// </summary>
	/// <returns>A new test domain event.</returns>
	public TestDomainEvent Build()
	{
		return new TestDomainEvent
		{
			EventId = _eventId ?? Guid.NewGuid().ToString(),
			AggregateId = _aggregateId ?? Guid.NewGuid().ToString(),
			Version = _version,
			OccurredAt = _occurredAt ?? DateTimeOffset.UtcNow,
			EventType = _eventType,
			Data = _data,
			Metadata = _metadata
		};
	}

	/// <summary>
	/// Builds multiple events for the same aggregate.
	/// </summary>
	/// <param name="count">Number of events to build.</param>
	/// <returns>A list of test domain events.</returns>
	public List<TestDomainEvent> BuildMany(int count)
	{
		var aggregateId = _aggregateId ?? Guid.NewGuid().ToString();
		return [.. Enumerable.Range(0, count).Select(i =>
		{
			var builder = new EventBuilder()
				.WithAggregateId(aggregateId)
				.WithVersion(i)
				.WithEventType(_eventType)
				.WithData($"{_data}-{i}");

			if (_metadata is not null)
			{
				builder.WithMetadata(new Dictionary<string, object>(_metadata));
			}

			return builder.Build();
		})];
	}

}

/// <summary>
/// Test domain event implementation used by the <see cref="EventBuilder"/>.
/// </summary>
public sealed class TestDomainEvent : IDomainEvent
{
	/// <inheritdoc/>
	public string EventId { get; set; } = Guid.NewGuid().ToString();

	/// <inheritdoc/>
	public string AggregateId { get; set; } = string.Empty;

	/// <inheritdoc/>
	public long Version { get; set; }

	/// <inheritdoc/>
	public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.UtcNow;

	/// <summary>
	/// Gets or sets the event type name.
	/// </summary>
	public string EventType { get; set; } = "TestEvent";

	/// <inheritdoc/>
	string IDomainEvent.EventType => EventType;

	/// <inheritdoc/>
	public IDictionary<string, object>? Metadata { get; set; }

	/// <summary>
	/// Gets or sets the event data payload.
	/// </summary>
	public string Data { get; set; } = string.Empty;
}

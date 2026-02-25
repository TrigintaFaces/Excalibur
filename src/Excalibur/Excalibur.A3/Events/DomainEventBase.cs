// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Text.Json.Serialization;

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.A3.Events;

/// <summary>
/// Base class for domain events in the A3 (Audit, Authorization, Authentication) system.
/// </summary>
/// <remarks>
/// This base class provides the common IDispatchMessage implementation for all A3 events, ensuring consistent behavior across audit,
/// authorization, and authentication events.
/// </remarks>
public abstract class DomainEventBase : IDomainEvent
{
	/// <summary>
	/// Initializes a new instance of the <see cref="DomainEventBase" /> class.
	/// </summary>
	protected DomainEventBase()
	{
		MessageId = Guid.NewGuid().ToString();
		Id = Guid.NewGuid();
		Timestamp = DateTimeOffset.UtcNow;
		Headers = new Dictionary<string, object>(StringComparer.Ordinal);
		Features = new DefaultMessageFeatures();
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="DomainEventBase" /> class with a correlation ID.
	/// </summary>
	/// <param name="correlationId"> The correlation ID for tracking this event across services. </param>
	protected DomainEventBase(Guid correlationId)
		: this()
	{
		if (correlationId != Guid.Empty)
		{
			((Dictionary<string, object>)Headers)["CorrelationId"] = correlationId.ToString();
		}
	}

	/// <inheritdoc />
	public virtual string MessageId { get; protected set; }

	/// <inheritdoc />
	public virtual Guid Id { get; protected set; }

	/// <inheritdoc />
	public virtual MessageKinds Kind => MessageKinds.Event;

	/// <inheritdoc />
	public virtual DateTimeOffset Timestamp { get; protected set; }

	/// <inheritdoc />
	public virtual IReadOnlyDictionary<string, object> Headers { get; protected set; }

	/// <inheritdoc />
	[JsonIgnore]
	public virtual object Body => this;

	/// <inheritdoc />
	public virtual string MessageType => GetType().Name;

	/// <inheritdoc />
	[JsonIgnore]
	public virtual IMessageFeatures Features { get; protected set; }

	/// <inheritdoc />
	public virtual string EventId => MessageId;

	/// <inheritdoc />
	public virtual string AggregateId { get; protected set; } = string.Empty;

	/// <inheritdoc />
	public virtual long Version { get; protected set; }

	/// <inheritdoc />
	public virtual DateTimeOffset OccurredAt => Timestamp;

	/// <inheritdoc />
	public virtual string EventType => MessageType;

	/// <inheritdoc />
	[JsonIgnore]
	public virtual IDictionary<string, object>? Metadata => Headers as IDictionary<string, object>;
}

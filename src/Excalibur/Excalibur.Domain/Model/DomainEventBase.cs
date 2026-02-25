// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Domain.Model;

/// <summary>
/// Base record for domain events, implementing <see cref="IDomainEvent"/> with sensible defaults.
/// </summary>
/// <remarks>
/// <para>
/// Provides a low-boilerplate base for defining domain events as records:
/// </para>
/// <code>
/// public record OrderCreated(string OrderId, decimal Total) : DomainEventBase
/// {
///     public override string AggregateId => OrderId;
/// }
/// </code>
/// <para>
/// Default behavior:
/// <list type="bullet">
/// <item><description><see cref="EventId"/>: Auto-generated GUID string</description></item>
/// <item><description><see cref="AggregateId"/>: Empty string (override in derived records)</description></item>
/// <item><description><see cref="Version"/>: 0 (set by infrastructure during event sourcing)</description></item>
/// <item><description><see cref="OccurredAt"/>: <see cref="DateTimeOffset.UtcNow"/> at construction time</description></item>
/// <item><description><see cref="EventType"/>: Derived type name</description></item>
/// <item><description><see cref="Metadata"/>: Null (attach via infrastructure or override)</description></item>
/// </list>
/// </para>
/// </remarks>
public abstract record DomainEventBase : IDomainEvent
{
	/// <inheritdoc />
	public virtual string EventId { get; init; } = Guid.NewGuid().ToString();

	/// <inheritdoc />
	public virtual string AggregateId { get; init; } = string.Empty;

	/// <inheritdoc />
	public virtual long Version { get; init; }

	/// <inheritdoc />
	public virtual DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;

	/// <inheritdoc />
	public virtual string EventType => GetType().Name;

	/// <inheritdoc />
	public virtual IDictionary<string, object>? Metadata { get; init; }
}

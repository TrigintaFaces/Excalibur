// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.EventSourcing.Abstractions;

namespace Excalibur.EventSourcing.Decorators;

/// <summary>
/// Abstract base class for <see cref="IEventStore"/> decorators.
/// All methods are virtual and forward to the inner store by default.
/// </summary>
/// <remarks>
/// <para>
/// Follows the <c>DelegatingHandler</c> / <c>DelegatingChatClient</c> pattern from Microsoft.
/// Subclasses override only the methods they need to intercept.
/// </para>
/// </remarks>
public abstract class DelegatingEventStore : IEventStore
{
	/// <summary>
	/// Gets the inner event store being decorated.
	/// </summary>
	protected IEventStore Inner { get; }

	/// <summary>
	/// Initializes a new instance of the <see cref="DelegatingEventStore"/> class.
	/// </summary>
	/// <param name="inner">The inner event store to delegate to.</param>
	protected DelegatingEventStore(IEventStore inner)
	{
		Inner = inner ?? throw new ArgumentNullException(nameof(inner));
	}

	/// <inheritdoc />
	public virtual ValueTask<IReadOnlyList<StoredEvent>> LoadAsync(
		string aggregateId,
		string aggregateType,
		CancellationToken cancellationToken)
		=> Inner.LoadAsync(aggregateId, aggregateType, cancellationToken);

	/// <inheritdoc />
	public virtual ValueTask<IReadOnlyList<StoredEvent>> LoadAsync(
		string aggregateId,
		string aggregateType,
		long fromVersion,
		CancellationToken cancellationToken)
		=> Inner.LoadAsync(aggregateId, aggregateType, fromVersion, cancellationToken);

	/// <inheritdoc />
	public virtual ValueTask<AppendResult> AppendAsync(
		string aggregateId,
		string aggregateType,
		IEnumerable<IDomainEvent> events,
		long expectedVersion,
		CancellationToken cancellationToken)
		=> Inner.AppendAsync(aggregateId, aggregateType, events, expectedVersion, cancellationToken);

	/// <inheritdoc />
	public virtual ValueTask<IReadOnlyList<StoredEvent>> GetUndispatchedEventsAsync(
		int batchSize,
		CancellationToken cancellationToken)
		=> Inner.GetUndispatchedEventsAsync(batchSize, cancellationToken);

	/// <inheritdoc />
	public virtual ValueTask MarkEventAsDispatchedAsync(
		string eventId,
		CancellationToken cancellationToken)
		=> Inner.MarkEventAsDispatchedAsync(eventId, cancellationToken);
}

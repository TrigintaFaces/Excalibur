// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;

namespace Excalibur.EventSourcing.Decorators;

/// <summary>
/// Base for the mediating views an <see cref="IsolatingEventStoreDecorator"/> returns from
/// <c>WrapCapability</c> for a capability that derives from <see cref="IEventStore"/>.
/// </summary>
/// <remarks>
/// A capability such as <see cref="ITransactionalEventStore"/> is also an event store, so a view over one
/// would breach the decorator's invariant if its inherited surface reached past the decorator. This base
/// routes that whole surface back through the decorator, leaving a derived view to mediate only the member
/// its capability adds.
/// </remarks>
/// <param name="outer">The decorator whose invariant the view must uphold.</param>
public abstract class EventStoreCapabilityView(IEventStore outer) : IEventStore
{
	/// <summary>
	/// Gets the decorator this view defers to for every operation it does not itself mediate.
	/// </summary>
	/// <value>The decorating store; never <see langword="null"/>.</value>
	protected IEventStore Outer { get; } = outer ?? throw new ArgumentNullException(nameof(outer));

	/// <inheritdoc />
	public ValueTask<IReadOnlyList<StoredEvent>> LoadAsync(
		string aggregateId,
		string aggregateType,
		CancellationToken cancellationToken) =>
		Outer.LoadAsync(aggregateId, aggregateType, cancellationToken);

	/// <inheritdoc />
	public ValueTask<IReadOnlyList<StoredEvent>> LoadAsync(
		string aggregateId,
		string aggregateType,
		long fromVersion,
		CancellationToken cancellationToken) =>
		Outer.LoadAsync(aggregateId, aggregateType, fromVersion, cancellationToken);

	/// <inheritdoc />
	public ValueTask<AppendResult> AppendAsync(
		string aggregateId,
		string aggregateType,
		IEnumerable<IDomainEvent> events,
		long expectedVersion,
		CancellationToken cancellationToken) =>
		Outer.AppendAsync(aggregateId, aggregateType, events, expectedVersion, cancellationToken);
}

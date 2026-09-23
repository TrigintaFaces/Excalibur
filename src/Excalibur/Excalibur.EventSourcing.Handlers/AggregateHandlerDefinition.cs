// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;
using Excalibur.Domain.Model;
using Excalibur.EventSourcing;

namespace Excalibur.EventSourcing.Handlers;

/// <summary>
/// Carries the identity resolver and the domain decision supplied at registration, so the handler that
/// uses them can be registered by TYPE rather than by a closure.
/// </summary>
/// <remarks>
/// <para>
/// This type exists for a dispatch reason, not a tidiness one. A handler registered through a factory
/// closure leaves <c>ServiceDescriptor.ImplementationType</c> and <c>ImplementationInstance</c> both
/// null, and the handler index is built from those two members — so a closure-registered handler
/// contributes no entry and its message is dispatched to nothing. Moving the two captured delegates into
/// a registered service lets the handler be registered as a plain implementation type, which the index
/// can see.
/// </para>
/// <para>
/// It is deliberately not a record with value equality: two registrations holding different delegates
/// for the same message type are different registrations, and the container keys them by type alone.
/// </para>
/// </remarks>
/// <typeparam name="TAggregate">The aggregate the handler loads and saves.</typeparam>
/// <typeparam name="TKey">The aggregate's identity type.</typeparam>
/// <typeparam name="TMessage">The dispatched message (command) type.</typeparam>
internal sealed class AggregateHandlerDefinition<TAggregate, TKey, TMessage>
	where TAggregate : class, IAggregateRoot<TKey>, IAggregateSnapshotSupport
	where TKey : notnull
	where TMessage : IDispatchAction
{
	/// <summary>
	/// Initializes a new instance of the <see cref="AggregateHandlerDefinition{TAggregate, TKey, TMessage}"/> class.
	/// </summary>
	/// <param name="resolveId">Resolves the aggregate identity from the message.</param>
	/// <param name="decide">The domain decision applied to the loaded aggregate.</param>
	public AggregateHandlerDefinition(
		Func<TMessage, TKey> resolveId,
		Func<TAggregate, TMessage, CancellationToken, Task> decide)
	{
		ArgumentNullException.ThrowIfNull(resolveId);
		ArgumentNullException.ThrowIfNull(decide);

		ResolveId = resolveId;
		Decide = decide;
	}

	/// <summary>Gets the identity resolver supplied at registration.</summary>
	public Func<TMessage, TKey> ResolveId { get; }

	/// <summary>Gets the domain decision supplied at registration.</summary>
	public Func<TAggregate, TMessage, CancellationToken, Task> Decide { get; }
}

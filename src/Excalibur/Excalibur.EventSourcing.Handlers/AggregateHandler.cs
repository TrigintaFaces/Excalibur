// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Data;
using Excalibur.Dispatch;
using Excalibur.Dispatch.Delivery;
using Excalibur.Domain.Model;
using Excalibur.EventSourcing;

namespace Excalibur.EventSourcing.Handlers;

/// <summary>
/// Routes a dispatched message to an event-sourced aggregate using the Decider pattern:
/// resolve the aggregate identity from the message, load the aggregate, invoke the domain
/// decision (which raises the aggregate's own events), then save with optimistic concurrency.
/// </summary>
/// <typeparam name="TAggregate">The event-sourced aggregate type.</typeparam>
/// <typeparam name="TKey">The aggregate identifier type.</typeparam>
/// <typeparam name="TMessage">The dispatched message (command) type.</typeparam>
/// <remarks>
/// <para>
/// The functional core is the caller-supplied <c>decide</c> delegate. It invokes domain methods
/// on the loaded aggregate, which raise events through the aggregate's pattern-match apply. This
/// handler owns only the load/save orchestration and the honest not-found and concurrency signals.
/// </para>
/// <para>
/// Concurrency is optimistic: the ETag observed at load time is passed as the expected ETag on
/// save. A concurrent write that advances the aggregate surfaces as
/// <see cref="ConcurrencyException"/>. A message that targets a non-existent aggregate surfaces as
/// <see cref="ResourceNotFoundException"/> — no aggregate state is fabricated.
/// </para>
/// </remarks>
internal sealed class AggregateHandler<TAggregate, TKey, TMessage> : IActionHandler<TMessage>
	where TAggregate : class, IAggregateRoot<TKey>, IAggregateSnapshotSupport
	where TKey : notnull
	where TMessage : IDispatchAction
{
	private readonly IEventSourcedRepository<TAggregate, TKey> _repository;
	private readonly Func<TMessage, TKey> _resolveId;
	private readonly Func<TAggregate, TMessage, CancellationToken, Task> _decide;

	/// <summary>
	/// Initializes a new instance of the <see cref="AggregateHandler{TAggregate, TKey, TMessage}"/> class.
	/// </summary>
	/// <param name="repository">The event-sourced repository for the target aggregate.</param>
	/// <param name="definition">The identity resolver and domain decision supplied at registration.</param>
	/// <remarks>
	/// The resolver and the decision arrive as a registered service rather than as captured closure
	/// arguments so that this handler can be registered by implementation TYPE. The handler index is
	/// built from a descriptor's implementation type and instance; a closure registration supplies
	/// neither, so such a handler is never indexed and never invoked.
	/// </remarks>
	public AggregateHandler(
		IEventSourcedRepository<TAggregate, TKey> repository,
		AggregateHandlerDefinition<TAggregate, TKey, TMessage> definition)
	{
		ArgumentNullException.ThrowIfNull(repository);
		ArgumentNullException.ThrowIfNull(definition);

		_repository = repository;
		_resolveId = definition.ResolveId;
		_decide = definition.Decide;
	}

	/// <inheritdoc/>
	public async Task HandleAsync(TMessage action, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(action);

		var aggregateId = _resolveId(action);

		// Honest not-found: a message targeting a non-existent aggregate throws rather than fabricating
		// state — the caller decides whether a create-flow or a rejection is appropriate.
		var aggregate = await _repository.GetByIdAsync(aggregateId, cancellationToken).ConfigureAwait(false)
			?? throw new ResourceNotFoundException(typeof(TAggregate).Name, aggregateId.ToString());

		// Capture the ETag observed at load time BEFORE the decision mutates the aggregate, so the
		// save enforces optimistic concurrency against the exact version we decided on.
		var expectedETag = aggregate.ETag;

		await _decide(aggregate, action, cancellationToken).ConfigureAwait(false);

		await _repository.SaveAsync(aggregate, expectedETag, cancellationToken).ConfigureAwait(false);
	}
}

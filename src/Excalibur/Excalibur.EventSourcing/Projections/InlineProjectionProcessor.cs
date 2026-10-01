// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;
using Excalibur.EventSourcing.Diagnostics;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Excalibur.EventSourcing.Projections;

/// <summary>
/// Applies committed events to all inline projections. Invoked by
/// <see cref="IEventNotificationBroker"/> during the inline projection
/// phase of <c>SaveAsync</c>.
/// </summary>
/// <remarks>
/// <para>
/// Different projection types run concurrently via <c>Task.WhenAll</c>.
/// Within each projection, events are applied sequentially in commit order.
/// </para>
/// <para>
/// Partial failure semantics: if some projections succeed and others fail,
/// the successful writes remain committed. Only the failed projection needs recovery
/// via <see cref="IProjectionRecovery"/>.
/// </para>
/// </remarks>
internal sealed class InlineProjectionProcessor
{
	private readonly IProjectionRegistry _registry;
	private readonly IServiceScopeFactory _scopeFactory;
	private readonly ILogger<InlineProjectionProcessor> _logger;
	private readonly ProjectionHealthState? _healthState;
	private readonly ProjectionObservability? _observability;

	public InlineProjectionProcessor(
		IProjectionRegistry registry,
		IServiceScopeFactory scopeFactory,
		ILogger<InlineProjectionProcessor> logger,
		ProjectionHealthState? healthState = null,
		ProjectionObservability? observability = null)
	{
		ArgumentNullException.ThrowIfNull(registry);
		ArgumentNullException.ThrowIfNull(scopeFactory);
		ArgumentNullException.ThrowIfNull(logger);

		_registry = registry;
		_scopeFactory = scopeFactory;
		_logger = logger;
		_healthState = healthState;
		_observability = observability;
	}

	/// <summary>
	/// Applies the committed events to all registered inline projections.
	/// </summary>
	/// <param name="events">The committed domain events in order.</param>
	/// <param name="context">The notification context.</param>
	/// <param name="failurePolicy">The configured failure policy.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>A task representing the asynchronous operation.</returns>
	/// <exception cref="AggregateException">
	/// Thrown when one or more projections fail and the failure policy is
	/// <see cref="NotificationFailurePolicy.Propagate"/>.
	/// </exception>
	internal async Task ProcessAsync(
		IReadOnlyList<IDomainEvent> events,
		EventNotificationContext context,
		NotificationFailurePolicy failurePolicy,
		CancellationToken cancellationToken)
	{
		var inlineRegistrations = _registry.GetByMode(ProjectionMode.Inline);
		if (inlineRegistrations.Count == 0)
		{
			return;
		}

		// Run all inline projection types concurrently. Each projection runs in its own
		// DI scope: IProjectionStore<T> is scoped, but this processor is a singleton, so resolving
		// stores from a captured root provider would throw under scope validation ("Cannot resolve
		// scoped service ... from root provider"). A scope per projection also isolates scoped state
		// (e.g. DB connections) across the concurrently-applied projections.
		var tasks = new Task[inlineRegistrations.Count];
		for (var i = 0; i < inlineRegistrations.Count; i++)
		{
			tasks[i] = ApplyInScopeAsync(inlineRegistrations[i], events, context, cancellationToken);
		}

		// Collect exceptions from all tasks (partial failure --
		// successful writes stay committed, only failed projections need recovery)
		var exceptions = new List<Exception>();
		for (var j = 0; j < tasks.Length; j++)
		{
			try
			{
				await tasks[j].ConfigureAwait(false);
			}
#pragma warning disable CA1031 // Catch general exceptions -- collecting for aggregate throw
			catch (Exception ex)
#pragma warning restore CA1031
			{
				exceptions.Add(ex);

				// Fire-and-forget observability -- metrics MUST NOT propagate
				try
				{
					var projectionType = inlineRegistrations[j].ProjectionType.Name;
					_healthState?.RecordInlineError(projectionType);
					_observability?.RecordError(projectionType, ex.GetType().Name);
				}
				catch
				{
					// Swallow -- metrics failure must not affect projection pipeline
				}
			}
		}

		if (exceptions.Count == 0)
		{
			return;
		}

		if (failurePolicy == NotificationFailurePolicy.Propagate)
		{
			// WHERE THIS SENDS THE CONSUMER MATTERS, and it used to send them somewhere that cannot work.
			// The message named IProjectionRecovery.ReapplyAsync as THE remedy. On a store that records
			// positions that is a dead end: every ProjectionEvent this processor builds carries
			// GlobalPosition: null (see ApplyInScopeAsync below), so the inline write is structurally
			// unconditional and leaves the row with no position a later write can advance from --
			// and recovery refuses exactly that row, terminally, on its first attempt. So the consumer
			// followed the framework to recovery, and recovery told them to rebuild: two hops, with the
			// first presented as the answer.
			//
			// This processor cannot see which kind of store is configured, so the message names both and
			// says which is which rather than picking one and being wrong half the time.
			throw new AggregateException(
				"One or more inline projections failed after the events were committed. Do NOT retry "
				+ "SaveAsync: the events are already durable and re-saving would append them again. "
				+ "Which repair you need depends on your projection store. IF IT RECORDS POSITIONS "
				+ "(IPositionedProjectionStore), REBUILD the affected projection from the event stream -- "
				+ "inline apply supplies no global position, so it writes unconditionally and leaves the "
				+ "row with no position a later write can advance from. IProjectionRecovery.ReapplyAsync "
				+ "will REFUSE such a row terminally rather than repair it, so it is not the remedy in "
				+ "that case. If your store records no positions, ReapplyAsync re-folds the aggregate and "
				+ "is the remedy.",
				exceptions);
		}

		foreach (var ex in exceptions)
		{
			_logger.LogError(
				ex,
				// "will catch up via async path" WAS FALSE FOR TWO INDEPENDENT REASONS, and it was the
				// worst of the three false remedies in this file because it told an operator the system
				// would self-heal -- so the correct response to it was to do nothing, and nothing is what
				// happened.
				//
				// First: a projection is registered in exactly ONE mode. InMemoryProjectionRegistry keys
				// by type with indexer assignment, and this processor reads GetByMode(Inline) while the
				// async host reads GetByMode(Async) -- disjoint sets. An Inline projection is never
				// delivered to the async host, so there is no async path for it to catch up through.
				//
				// Second, and independently: even if it were delivered, the async host writes POSITIONED
				// (it passes StoredEvent.GlobalPosition, which is non-nullable), while inline apply
				// hard-codes GlobalPosition: null and so leaves the row with no position a write can
				// advance from. That write would be refused.
				"Inline projection failed for aggregate {AggregateType}/{AggregateId} at version {Version}. " +
				"FailurePolicy is LogAndContinue, so the save succeeded and the events are durable -- but " +
				"THIS PROJECTION IS NOT RECOVERED AUTOMATICALLY and there is no async catch-up for it: a " +
				"projection is registered in exactly one mode, so an Inline projection is never delivered " +
				"to the async host. Repair it explicitly. REBUILD it from the event stream if its store " +
				"records positions, because an inline write leaves the row with no position a later write " +
				"can advance from; otherwise call IProjectionRecovery.ReapplyAsync for this aggregate.",
				context.AggregateType,
				context.AggregateId,
				context.CommittedVersion);
		}
	}

	private async Task ApplyInScopeAsync(
		ProjectionRegistration registration,
		IReadOnlyList<IDomainEvent> events,
		EventNotificationContext context,
		CancellationToken cancellationToken)
	{
		await using var scope = _scopeFactory.CreateAsyncScope();
		// The save path has no global position: these events are being committed now, and the store
		// assigns positions inside that transaction. A null position means this apply cannot participate
		// in a position-conditional write, which is correct -- there is nothing yet to be conditional on.
		var projectionEvents = new List<ProjectionEvent>(events.Count);
		foreach (var domainEvent in events)
		{
			projectionEvents.Add(new ProjectionEvent(domainEvent, context.AggregateId, GlobalPosition: null));
		}

		await registration.InlineApply!(projectionEvents, context, scope.ServiceProvider, cancellationToken)
			.ConfigureAwait(false);
	}
}

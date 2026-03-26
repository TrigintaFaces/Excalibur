// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.EventSourcing.Abstractions;
using Excalibur.EventSourcing.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Excalibur.EventSourcing.Projections;

/// <summary>
/// Applies committed events to all inline projections. Invoked by
/// <see cref="IEventNotificationBroker"/> during the inline projection
/// phase of <c>SaveAsync</c>.
/// </summary>
/// <remarks>
/// <para>
/// Different projection types run concurrently via <c>Task.WhenAll</c> (R27.20).
/// Within each projection, events are applied sequentially in commit order.
/// </para>
/// <para>
/// Partial failure semantics (R27.20a): if some projections succeed and others fail,
/// the successful writes remain committed. Only the failed projection needs recovery
/// via <see cref="IProjectionRecovery"/>.
/// </para>
/// </remarks>
internal sealed class InlineProjectionProcessor
{
	private readonly IProjectionRegistry _registry;
	private readonly IServiceProvider _serviceProvider;
	private readonly ILogger<InlineProjectionProcessor> _logger;
	private readonly ProjectionHealthState? _healthState;
	private readonly ProjectionObservability? _observability;

	public InlineProjectionProcessor(
		IProjectionRegistry registry,
		IServiceProvider serviceProvider,
		ILogger<InlineProjectionProcessor> logger,
		ProjectionHealthState? healthState = null,
		ProjectionObservability? observability = null)
	{
		ArgumentNullException.ThrowIfNull(registry);
		ArgumentNullException.ThrowIfNull(serviceProvider);
		ArgumentNullException.ThrowIfNull(logger);

		_registry = registry;
		_serviceProvider = serviceProvider;
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

		// Run all inline projection types concurrently (R27.20)
		var tasks = new Task[inlineRegistrations.Count];
		for (var i = 0; i < inlineRegistrations.Count; i++)
		{
			var registration = inlineRegistrations[i];
			tasks[i] = registration.InlineApply!(
				events, context, _serviceProvider, cancellationToken);
		}

		// Collect exceptions from all tasks (R27.20a: partial failure --
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

				// Fire-and-forget observability -- metrics MUST NOT propagate (R27.51)
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
			throw new AggregateException(
				"One or more inline projections failed after events were committed. " +
				"Do NOT retry SaveAsync. Use IProjectionRecovery.ReapplyAsync to recover.",
				exceptions);
		}

		foreach (var ex in exceptions)
		{
			_logger.LogError(
				ex,
				"Inline projection failed for aggregate {AggregateType}/{AggregateId} at version {Version}. " +
				"FailurePolicy is LogAndContinue; projection will catch up via async path.",
				context.AggregateType,
				context.AggregateId,
				context.CommittedVersion);
		}
	}
}

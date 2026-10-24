// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization.Metadata;

using Excalibur.EventSourcing.Subscriptions;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Excalibur.EventSourcing.Projections;

/// <inheritdoc />
internal sealed class ProjectionSessionReader : IProjectionSessionReader
{
	private readonly IServiceScopeFactory _scopeFactory;
	private readonly ISubscriptionCheckpointStore? _checkpointStore;
	private readonly IOptions<ProjectionSessionOptions> _options;
	private readonly IOptions<GlobalStreamProjectionOptions> _projectionOptions;
	private readonly ProjectionHealthState? _healthState;
	private readonly TimeProvider _timeProvider;
	private readonly IProjectionRegistry? _registry;
	private readonly InlineProjectionCoordinator? _inline;

	public ProjectionSessionReader(
		IServiceScopeFactory scopeFactory,
		IOptions<ProjectionSessionOptions> options,
		IOptions<GlobalStreamProjectionOptions> projectionOptions,
		ISubscriptionCheckpointStore? checkpointStore = null,
		ProjectionHealthState? healthState = null,
		TimeProvider? timeProvider = null,
		IProjectionRegistry? registry = null,
		InlineProjectionCoordinator? inline = null)
	{
		ArgumentNullException.ThrowIfNull(scopeFactory);
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(projectionOptions);

		_scopeFactory = scopeFactory;
		_checkpointStore = checkpointStore;
		_options = options;
		_projectionOptions = projectionOptions;
		_healthState = healthState;
		_timeProvider = timeProvider ?? TimeProvider.System;
		_registry = registry;
		_inline = inline;
	}

	/// <inheritdoc />
	[RequiresUnreferencedCode("Projection stores serialize the projection type reflectively.")]
	[RequiresDynamicCode("Projection stores serialize the projection type reflectively.")]
	public async ValueTask<ProjectionRead<TProjection>> ReadAsync<TProjection>(
		string projectionId,
		SaveResult session,
		CancellationToken cancellationToken)
		where TProjection : class
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(projectionId);

		var freshness = session.SessionToken is { } token
			? await AwaitTokenAsync(token, _registry?.GetRegistration(typeof(TProjection))?.Mode == ProjectionMode.Inline,
				typeof(TProjection), cancellationToken).ConfigureAwait(false)
			: ProjectionFreshness.NotRequested;

		// READ AFTER THE WAIT, ALWAYS -- including when the bound expired. Returning early on expiry
		// would hand the caller a null projection and no way to tell "no such row" from "gave up", and
		// the row may well exist with older content, which is exactly what BoundExpired describes.
		await using var scope = _scopeFactory.CreateAsyncScope();
		var store = scope.ServiceProvider.GetRequiredService<IProjectionStore<TProjection>>();
		var projection = await store.GetByIdAsync(projectionId, cancellationToken).ConfigureAwait(false);

		return new ProjectionRead<TProjection>(projection, freshness);
	}

    /// <inheritdoc />
    public async ValueTask<ProjectionRead<TProjection>> ReadAsync<TProjection>(string projectionId, SaveResult session,
        JsonTypeInfo<TProjection> typeInfo, CancellationToken cancellationToken) where TProjection : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectionId);
        ArgumentNullException.ThrowIfNull(typeInfo);
        await using var scope = _scopeFactory.CreateAsyncScope();
        var store = ProjectionStoreOperations<TProjection>.Generated(scope.ServiceProvider, typeInfo);
        var freshness = session.SessionToken is { } token
            ? await AwaitTokenAsync(token, _registry?.GetRegistration(typeof(TProjection))?.Mode == ProjectionMode.Inline,
                typeof(TProjection), cancellationToken).ConfigureAwait(false)
            : ProjectionFreshness.NotRequested;
        var projection = await store.GetByIdAsync(projectionId, cancellationToken).ConfigureAwait(false);
        return new ProjectionRead<TProjection>(projection, freshness);
    }

	private async ValueTask<ProjectionFreshness> AwaitTokenAsync(
		long token,
		bool inline,
		Type projectionType,
		CancellationToken cancellationToken)
	{
		var opts = _options.Value;
		var subscription = _projectionOptions.Value.ProjectionName;
		var started = _timeProvider.GetTimestamp();

		// THE BOUND BOUNDS THE I/O, NOT JUST THE LOOP.
		//
		// The elapsed check below only runs between attempts, so a bound of five seconds against a
		// checkpoint query that takes sixty waited sixty -- and against one that never returned, it
		// waited forever. A configured bound that a single slow read can overrun is not a bound; it is a
		// hope. A store outage is the ordinary case for this, not an exotic one.
		//
		// Linked to the caller's token so cancellation still propagates, and kept distinct from it so
		// expiry can be told apart from a caller who gave up: the former is BoundExpired, the latter is
		// the caller's own OperationCanceledException.
		using var boundExpiry = new CancellationTokenSource(opts.Bound, _timeProvider);
		using var linked = CancellationTokenSource.CreateLinkedTokenSource(
			cancellationToken, boundExpiry.Token);

		while (true)
		{
			try
			{
				if (inline && _inline is not null
					? await _inline.HasAppliedAsync(projectionType, token, linked.Token).ConfigureAwait(false)
					: await HasAppliedAsync(CursorNameFor(subscription, projectionType), token, linked.Token)
						.ConfigureAwait(false))
				{
					return ProjectionFreshness.Fresh;
				}
			}
			catch (OperationCanceledException) when (boundExpiry.IsCancellationRequested
				&& !cancellationToken.IsCancellationRequested)
			{
				// The bound elapsed DURING the progress read. Unknown, reported as unknown.
				return ProjectionFreshness.BoundExpired;
			}

			// Checked AFTER the first look, so a projection that is already caught up never waits -- the
			// common case for a read issued any time after the write.
			var remaining = opts.Bound - _timeProvider.GetElapsedTime(started);
			if (remaining <= TimeSpan.Zero)
			{
				return ProjectionFreshness.BoundExpired;
			}

			// CAPPED AT WHAT IS LEFT. A full poll interval could otherwise carry the wait past the
			// bound the caller asked for, which is the same overrun one step smaller.
			var delay = opts.PollInterval < remaining ? opts.PollInterval : remaining;

			try
			{
				await Task.Delay(delay, _timeProvider, linked.Token).ConfigureAwait(false);
			}
			catch (OperationCanceledException) when (boundExpiry.IsCancellationRequested
				&& !cancellationToken.IsCancellationRequested)
			{
				return ProjectionFreshness.BoundExpired;
			}
		}
	}

	/// <summary>
	/// The cursor key for one projection under a subscription, matching what the processing host writes.
	/// </summary>
	/// <remarks>
	/// Cursors are PER REGISTRATION. Reading the bare subscription name finds nothing, so a reader that
	/// used it would wait out its whole bound and report every read as <c>BoundExpired</c> while the
	/// projection was in fact caught up. The key must be derived the same way on both sides.
	/// </remarks>
	private static string CursorNameFor(string subscription, Type projectionType) =>
		AsyncProjectionProcessingHost.CheckpointNameFor(subscription, projectionType);

	private async ValueTask<bool> HasAppliedAsync(
		string cursorName, long token, CancellationToken cancellationToken)
	{
		// Both marks certify this registration only; a sibling cannot attest its progress.
		if (_healthState?.AppliedPosition(cursorName) is { } applied && applied >= token)
		{
			return true;
		}

		// OPTIONAL, AND THE OPTIONALITY IS THE FIX FOR A REAL DEFECT. The checkpoint store is registered
		// by EnableProjectionProcessing, not by UseEventNotification -- which is what registers THIS
		// reader. A required dependency therefore made IProjectionSessionReader unresolvable from the
		// very container that registers it: the seam serving read-your-writes threw on resolution unless
		// a second, unstated call had also been made. Advertised-but-unresolvable, on the very seam that
		// serves read-your-writes.
		//
		// Absent, it degrades honestly rather than failing: progress is then the in-process mark alone,
		// which is complete for a single-process host and blind to other processes. A read that cannot
		// confirm reports BoundExpired, which is the truth -- we do not know -- rather than a guess.
		if (_checkpointStore is null)
		{
			return false;
		}

		var checkpoint = await _checkpointStore
			.GetCheckpointAsync(cursorName, cancellationToken)
			.ConfigureAwait(false);

		return checkpoint is { } durable && durable >= token;
	}
}

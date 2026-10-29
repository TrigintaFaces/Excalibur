// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;
using Excalibur.EventSourcing.DependencyInjection;
using Excalibur.EventSourcing.Subscriptions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.EventSourcing.Projections;

/// <summary>Foreground completion and recovery share one ordered Inline cursor, independent of Async.</summary>
internal sealed class InlineProjectionCoordinator : IAsyncDisposable
{
	private readonly IServiceProvider _services;
	private readonly SemaphoreSlim _hostGate = new(1, 1);
	private readonly IOptions<EventNotificationOptions> _notificationOptions;
	private readonly ILogger<InlineProjectionCoordinator> _logger;
	private readonly TimeProvider _clock;
	private Task<AsyncProjectionProcessingHost>? _hostTask;

	public InlineProjectionCoordinator(IServiceProvider services, IOptions<EventNotificationOptions> notificationOptions,
		ILogger<InlineProjectionCoordinator> logger, TimeProvider? clock = null)
	{
		_services = services;
		_notificationOptions = notificationOptions;
		_logger = logger;
		_clock = clock ?? TimeProvider.System;
	}

	internal static void Register(IServiceCollection services)
	{
		services.TryAddSingleton<InlineProjectionCoordinator>();
		services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, InlineProjectionHostedService>());

		// A COMPOSITION THAT CAN NEVER COMPLETE A SAVE IS REFUSED AT STARTUP, not on the first write.
		// Registered as a startup validator as well as a hosted service, because the hosted-service half
		// never runs in a host that does not run hosted services -- which is exactly the host this
		// refusal most needs to reach.
		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IHostedService, DependencyInjection.InlineProjectionPrerequisiteValidator>());
		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IStartupPrerequisiteValidator, DependencyInjection.InlineProjectionPrerequisiteValidator>());

		// The pull entry point must drive INLINE too. Without this, a host that runs no background
		// service had no way to recover an incomplete fold: the save's own attempt was the only driver,
		// so the last save before the process went idle was never recovered.
		ProjectionProcessorRegistration.EnsureRegistered(services);
	}

	/// <summary>Drives the Inline cursor to the stream head, for a host with no background loop.</summary>
	/// <remarks>
	/// No required set: this is the whole-subscription question, not one save's. A caller asking for
	/// pending work wants every Inline projection advanced as far as it can go, and a halt in one is
	/// reported rather than hidden.
	/// </remarks>
	internal async Task<ProjectionProcessingResult> ProcessPendingAsync(CancellationToken token)
	{
		// MATERIALIZE THE DEFINITIONS BEFORE READING THE REGISTRY, the same step StartAsync performs.
		// Projection registrations are deferred to startup as closed generics, so a registry read on an
		// un-started container sees NOTHING and this method would report a vacuous CaughtUp having folded
		// not one event -- a silent success, in the one method a host-less consumer relies on.
		_ = _services.GetRequiredService<IEventNotificationBroker>();

		var host = await HostAsync().WaitAsync(token).ConfigureAwait(false);

		return await host.ProcessThroughAsync(null, required: null, token).ConfigureAwait(false);
	}

	internal async Task StartAsync(CancellationToken token)
	{
		// Resolving the broker materializes the deferred, closed-generic projection definitions.
		_ = _services.GetRequiredService<IEventNotificationBroker>();
		await (await HostAsync().ConfigureAwait(false)).StartAsync(token).ConfigureAwait(false);
	}

	internal async Task StopAsync(CancellationToken token)
	{
		if (_hostTask is { IsCompletedSuccessfully: true } started)
		{
			await (await started.ConfigureAwait(false)).StopAsync(token).ConfigureAwait(false);
		}
	}

	/// <summary>
	/// The created host, shared by every caller, with a FAULT DELIBERATELY NOT CACHED.
	/// </summary>
	/// <remarks>
	/// This was a <see cref="Lazy{T}"/> over the creation task, which caches a faulted task
	/// permanently: one transient failure during creation -- a checkpoint store briefly unreachable --
	/// was then handed to every later save for the lifetime of the process, recoverable only by a
	/// restart and reported as a committed-but-pending save every time. Where the hosted service runs
	/// that is moot, because creation happens at startup and a failure correctly kills the process. It
	/// bites the pull-driven composition, where creation happens on the first save instead.
	/// </remarks>
	private Task<AsyncProjectionProcessingHost> HostAsync()
	{
		var cached = _hostTask;
		return cached is not null && !cached.IsFaulted && !cached.IsCanceled ? cached : CreateHostOnceAsync();
	}

	private async Task<AsyncProjectionProcessingHost> CreateHostOnceAsync()
	{
		await _hostGate.WaitAsync().ConfigureAwait(false);
		try
		{
			var cached = _hostTask;
			if (cached is not null && !cached.IsFaulted && !cached.IsCanceled)
			{
				return await cached.ConfigureAwait(false);
			}

			// Published BEFORE the await so concurrent savers join this creation rather than starting
			// their own, then cleared on failure so the NEXT caller retries instead of inheriting it.
			var created = CreateValidatedHostAsync();
			_hostTask = created;
			try
			{
				return await created.ConfigureAwait(false);
			}
			catch
			{
				_hostTask = null;
				throw;
			}
		}
		finally
		{
			_ = _hostGate.Release();
		}
	}

	internal async Task<ProjectionCompletion> CompleteAsync(
		EventNotificationContext context,
		IReadOnlyList<IDomainEvent> events)
	{
        ArgumentNullException.ThrowIfNull(events);
        try
        {
            return await CompleteCoreAsync(context, events).ConfigureAwait(false);
        }
        catch (Exception error)
        {
            // This boundary includes registry/options resolution and provider initialization. SDKs
            // can wrap outages in configuration-shaped exceptions; the append is already durable.
            // Startup and explicit processing retain their original fail-fast behavior.
            _logger.LogError(error,
                "Inline processing failed after committing position {Position} for aggregate {AggregateId}. "
                + "The events are durable; projection progress is unconfirmed. Do not repeat the command.",
                context.GlobalPosition, context.AggregateId);
            return new ProjectionCompletion(ProjectionFreshness.BoundExpired);
        }
    }

    private async Task<ProjectionCompletion> CompleteCoreAsync(EventNotificationContext context,
        IReadOnlyList<IDomainEvent> events)
    {
		if (_services.GetRequiredService<IProjectionRegistry>().GetByMode(ProjectionMode.Inline).Count == 0)
		{
			// THE ROSTER IS READ BEFORE THE ANSWER, not after. This return cannot reach the processing
			// loop, so the quantifier enforced inside it never runs here and the roster is this path's
			// only source for it. See NothingOwedAsync for why an unhosted member is not NotRequested.
			return new ProjectionCompletion(await NothingOwedWithoutAHostAsync(context).ConfigureAwait(false));
		}

		if (context.GlobalPosition is not > 0)
		{
			// A MISCONFIGURATION, reported rather than thrown. The events are already committed, so an
			// exception here would tell the caller a durable save failed when it did not. The read model
			// genuinely is behind and will stay behind, which is exactly what BoundExpired says -- but
			// unlike an expired wait this will never resolve, so it is logged at Error rather than
			// Warning. Startup validation is where this composition should be refused outright.
			_logger.LogError(
				"Inline projections are registered but the event store reported no committed global position "
				+ "for aggregate {AggregateId}. The events are committed; the read model cannot be advanced "
				+ "and will stay behind until a positioned event store is configured.",
				context.AggregateId);
			return new ProjectionCompletion(ProjectionFreshness.BoundExpired);
		}

		// A BULK WRITER OPTED OUT OF THE PER-SAVE WAIT. Checked before the host is touched, because
		// reaching the host is most of what suppression exists to avoid: the point of a batch is that it
		// does not pay a per-record coordination cost, and computing the required set would pay one.
		//
		// The cost of checking this early is that a save feeding NO inline projection reports Deferred
		// rather than NotRequested inside a scope. That over-states the obligation -- it tells the caller
		// to drive the cursor when this particular save gave it nothing to do -- and over-stating is the
		// safe direction: the caller already owes that one call for the batch as a whole.
		if (InlineProjectionWait.IsSuppressed)
		{
			return new ProjectionCompletion(ProjectionFreshness.Deferred);
		}

		using var bound = new CancellationTokenSource(_notificationOptions.Value.PostCommitTimeout, _clock);
		Task<ProjectionProcessingResult>? work = null;
		try
		{
			var host = await HostAsync().WaitAsync(bound.Token).ConfigureAwait(false);

			// WAIT ONLY ON THE PROJECTIONS THIS SAVE FEEDS. A projection consuming none of these event
			// types is not advanced by this save and is not attested by it, so holding the save open for
			// it would be waiting on work the save did not cause -- which is how one faulting projection
			// came to stall every writer.
			var committedTypes = new Type[events.Count];
			for (var i = 0; i < events.Count; i++)
			{
				committedTypes[i] = events[i].GetType();
			}

			var required = host.RequiredRegistrations(committedTypes);
			if (required.Count == 0)
			{
				// THE ROSTER IS READ BEFORE THE ANSWER, not after. RequiredRegistrations reads the LOCAL
				// registry, which is silent about members bound by another deployment, so an empty
				// required set says only that no projection THIS process hosts consumes these events.
				// Returning NotRequested on that alone was the defect; the roster decides.
				return new ProjectionCompletion(await NothingOwedAsync(host, context, bound.Token).ConfigureAwait(false));
			}

			// RETRY EXACTLY ONE HALT REASON, AND ONLY INSIDE THE BUDGET THE CALLER ALREADY GRANTED.
			//
			// Failing a committed save is the most expensive answer this method can give: it tells the
			// caller its durable write did not complete. So a transient stop must not produce it --
			// PrefixUnavailable means the next contiguous prefix is not readable YET, the normal reading
			// of an append still in flight, and it clears on its own.
			//
			// Every other reason is terminal here and must NOT be retried. A faulted projection, an
			// undeserializable event, or a write whose acknowledgement was lost do not clear by waiting,
			// and retrying them would convert an unconfirmed write into a reported success -- the silent
			// loss this seam exists to prevent. An earlier version of this method retried on the OUTCOME
			// alone and did exactly that; the regression arm for a lost acknowledgement caught it.
			//
			// The DEADLINE bounds the wait, never an attempt count: the budget is the contract.
			var backoff = TimeSpan.FromMilliseconds(25);
			while (true)
			{
				work = host.ProcessThroughAsync(context.GlobalPosition, required, bound.Token);
				var result = await work.WaitAsync(bound.Token).ConfigureAwait(false);
				if (result.Outcome == ProjectionProcessingOutcome.CaughtUp)
				{
					// Attested, not Fresh: this is what the save OBSERVED against the roster it read, and the
					// roster can change before the caller reads. Fresh is the read-time verdict that
					// IProjectionSessionReader issues after re-validating against current state.
					return new ProjectionCompletion(ProjectionFreshness.Attested, result.Attestation);
				}

				if (result.HaltReason != ProjectionHaltReason.PrefixUnavailable)
				{
					_logger.LogWarning(
						"Inline projections did not reach position {Position} for aggregate {AggregateId}: {Reason}. "
						+ "The events are committed; the read model is behind. Do not repeat the command.",
						context.GlobalPosition.Value, context.AggregateId, result.HaltReason);
					return new ProjectionCompletion(ProjectionFreshness.BoundExpired);
				}

				await Task.Delay(backoff, _clock, bound.Token).ConfigureAwait(false);
				backoff = backoff.TotalMilliseconds >= 250 ? backoff : backoff * 2;
			}
		}
		catch (OperationCanceledException) when (bound.IsCancellationRequested)
		{
			// The BUDGET expired, which is the ordinary bounded-wait outcome and not an error: the events
			// are committed and the read model is merely behind. The abandoned work is still observed so
			// its failure is logged rather than swallowed as an unobserved task exception.
			if (work is not null && !work.IsCompleted)
			{
				_ = ObserveAsync(work);
			}

			_logger.LogWarning(
				"Inline projections did not reach position {Position} for aggregate {AggregateId} within the "
				+ "post-commit budget. The events are committed; the read model is behind. Do not repeat the command. "
				+ "If this is a bulk writer rather than a request, the per-save wait is the wrong price: an inline "
				+ "projection advances one event at a time, so any per-page cost on the projection store becomes a "
				+ "per-event cost -- a store set to make each page searchable before its checkpoint advances will "
				+ "perform one index refresh per event. Wrap the batch in InlineProjectionWait.Suppress() and call "
				+ "IProjectionProcessor.ProcessPendingAsync once when it finishes.",
				context.GlobalPosition.Value, context.AggregateId);
			return new ProjectionCompletion(ProjectionFreshness.BoundExpired);
		}


	}

	/// <summary>
	/// "Nothing was owed" -- but only when the durable roster permits this process to say it.
	/// </summary>
	/// <remarks>
	/// A save with no local Inline work still cannot report that nothing is owed while the subscription's
	/// roster holds a member this process does not host. The roster deliberately does NOT record which
	/// event types each member consumes -- storing that was considered and rejected -- so this process
	/// cannot determine whether an absent member consumes the events just committed. Lacking that it
	/// cannot establish the freshness property at all, and the conclusion for a save that cannot establish
	/// it is that the read model may be behind. NotRequested would assert a fact this process has no basis
	/// for, and NotRequested tells the caller nobody owes a follow-up -- so the caller would read a member
	/// that is behind, and nothing downstream would ever learn it had.
	/// <para>
	/// NotRequested stays correct for the narrow case it was written for: the roster is empty or fully
	/// hosted here, AND no local Inline projection needed these events.
	/// </para>
	/// </remarks>
	private async Task<ProjectionFreshness> NothingOwedAsync(
		AsyncProjectionProcessingHost host,
		EventNotificationContext context,
		CancellationToken token)
	{
		var unattestable = await host.UnattestableMembersAsync(token).ConfigureAwait(false);
		if (unattestable.Count == 0)
		{
			return ProjectionFreshness.NotRequested;
		}

		_logger.LogWarning(
			"This save fed no Inline projection hosted here, but the subscription roster holds members "
			+ "this process does not host ({Members}) for aggregate {AggregateId}. Whether those members "
			+ "consume these events is not recorded, so freshness cannot be established. The events are "
			+ "committed; the read model may be behind. Do not repeat the command.",
			string.Join(", ", unattestable), context.AggregateId);
		return ProjectionFreshness.BoundExpired;
	}

	/// <summary>The same roster question, on the path that has not created the host yet.</summary>
	/// <remarks>
	/// This path previously could not fail at all, so every failure reaching it is NEWLY reachable and
	/// must be reported rather than thrown: the events are already durable, and an exception here would
	/// tell the caller a committed save failed when it did not.
	/// <para>
	/// That includes the composition with NO checkpoint store registered, where the host cannot be built.
	/// The answer is the same as for any other unreadable roster -- the roster could not be read, so
	/// freshness cannot be established -- and it does not strand a working consumer: a composition with
	/// Inline projections and no checkpoint store is refused at startup, so reaching this branch means
	/// the read model genuinely cannot be advanced.
	/// </para>
	/// </remarks>
	private async Task<ProjectionFreshness> NothingOwedWithoutAHostAsync(EventNotificationContext context)
	{
		using var bound = new CancellationTokenSource(_notificationOptions.Value.PostCommitTimeout, _clock);
		try
		{
			var host = await HostAsync().WaitAsync(bound.Token).ConfigureAwait(false);
			return await NothingOwedAsync(host, context, bound.Token).ConfigureAwait(false);
		}
		catch (Exception error)
		{
			_logger.LogError(error,
				"The subscription roster could not be read while completing a save for aggregate "
				+ "{AggregateId}, so no Inline projection can be attested. The events are committed; the "
				+ "read model may be behind. Do not repeat the command.",
				context.AggregateId);
			return ProjectionFreshness.BoundExpired;
		}
	}

	private async Task ObserveAsync(Task work)
	{
		try { await work.ConfigureAwait(false); }
		catch (Exception error) { _logger.LogError(error, "Inline processing failed after its foreground deadline."); }
	}

    internal async Task<SubscriptionRoster> ReadRosterAsync(CancellationToken token) =>
        await (await HostAsync().WaitAsync(token).ConfigureAwait(false)).ReadRosterAsync(token).ConfigureAwait(false);

    internal async Task<string> DestinationFingerprintAsync(Type projectionType, string? identity, string? scope, CancellationToken token) =>
        (await HostAsync().WaitAsync(token).ConfigureAwait(false)).DestinationFingerprint(projectionType, identity, scope);

	internal async Task<bool> HasAppliedAsync(Type projectionType, long position, CancellationToken token) =>
		await (await HostAsync().WaitAsync(token).ConfigureAwait(false))
			.HasAppliedAsync(projectionType, position, token).ConfigureAwait(false);

	private async Task<AsyncProjectionProcessingHost> CreateValidatedHostAsync()
    {
        await GeneratedProjectionValidation.ValidateAsync(_services, CancellationToken.None).ConfigureAwait(false);
        return await CreateHostAsync().ConfigureAwait(false);
    }

    private Task<AsyncProjectionProcessingHost> CreateHostAsync()
    {
		var registry = _services.GetRequiredService<IProjectionRegistry>();
		var configured = _services.GetRequiredService<IOptions<GlobalStreamProjectionOptions>>().Value;
		ValidateName(configured);
		var source = _services.GetService<InlineProjectionSource>();
		var checkpoints = ResolveCheckpoints(_services);
		var scopeIdentity = configured.SubscriptionScope;
		if (string.IsNullOrWhiteSpace(scopeIdentity))
		{
			if (source is null)
			{
				throw new InvalidOperationException("Set GlobalStreamProjectionOptions.SubscriptionScope for this custom Inline event source.");
			}
            scopeIdentity = source.Identity;
		}
		var options = new GlobalStreamProjectionOptions
		{
			ProjectionName = SubscriptionName(configured),
			SubscriptionScope = scopeIdentity,
			SubscriptionRevision = configured.SubscriptionRevision,
			BatchSize = configured.BatchSize,
			IdlePollingInterval = configured.IdlePollingInterval,
		};
		ValidateEffectiveOptions(options);
		return Task.FromResult(new AsyncProjectionProcessingHost(registry, _services.GetRequiredService<IEventSerializer>(),
			checkpoints, Options.Create(options), _services, _services.GetRequiredService<ILogger<AsyncProjectionProcessingHost>>(),
			_notificationOptions, _clock, ProjectionMode.Inline) { RequireDestinationScope = string.IsNullOrWhiteSpace(configured.SubscriptionScope) });
	}

    internal static string SubscriptionName(GlobalStreamProjectionOptions options)
    {
        ValidateName(options);
        return "Inline:" + options.ProjectionName;
    }

    internal static ISubscriptionCheckpointStore ResolveCheckpoints(IServiceProvider services)
    {
        var source = services.GetService<InlineProjectionSource>();
		if (source is not null && !ReferenceEquals(source.Query, services.GetRequiredService<Excalibur.EventSourcing.Queries.IGlobalStreamQuery>()))
		{
			throw new InvalidOperationException("The global event reader was replaced independently of its SQL Inline checkpoint binding. "
				+ "Configure a source-affine reader/checkpoint pair; automatic Inline setup cannot certify this composition.");
		}
		var checkpoints = source?.Checkpoints;
		if (checkpoints is null)
		{
			if (services.GetService<ISubscriptionCheckpointDurability>() is null
				&& services.GetService<InMemoryProjectionCheckpointAcknowledgement>() is null)
			{
				throw new InvalidOperationException("Inline projections require a durable checkpoint store. "
					+ "UseSqlServer supplies one automatically; other providers must register their checkpoint store.");
			}
			checkpoints = services.GetRequiredService<ISubscriptionCheckpointStore>();
		}
        return checkpoints;
    }

	private static void ValidateName(GlobalStreamProjectionOptions options)
	{
		if (string.IsNullOrWhiteSpace(options.ProjectionName))
		{
			throw new OptionsValidationException(Options.DefaultName, typeof(GlobalStreamProjectionOptions),
				["ProjectionName must not be blank."]);
		}
	}

	private static void ValidateEffectiveOptions(GlobalStreamProjectionOptions options)
	{
		var validation = new GlobalStreamProjectionOptionsValidator().Validate(Options.DefaultName, options);
		if (validation.Failed)
		{
			throw new OptionsValidationException(Options.DefaultName, typeof(GlobalStreamProjectionOptions), validation.Failures);
		}
	}

	public async ValueTask DisposeAsync()
	{
		var created = _hostTask;
		if (created is not null)
		{
			try { (await created.ConfigureAwait(false)).Dispose(); }
			catch (Exception error) { _logger.LogDebug(error, "Inline initialization failed before disposal."); }
		}

		_hostGate.Dispose();
	}
}

internal sealed class InlineProjectionHostedService(InlineProjectionCoordinator coordinator) : IHostedService
{
	public Task StartAsync(CancellationToken cancellationToken) => coordinator.StartAsync(cancellationToken);
	public Task StopAsync(CancellationToken cancellationToken) => coordinator.StopAsync(cancellationToken);
}

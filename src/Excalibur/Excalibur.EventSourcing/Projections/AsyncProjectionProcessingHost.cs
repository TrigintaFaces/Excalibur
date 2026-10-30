// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using System.Threading.RateLimiting;

using Excalibur.Dispatch;
using Excalibur.EventSourcing.DependencyInjection;
using Excalibur.EventSourcing.Diagnostics;
using Excalibur.EventSourcing.Queries;
using Excalibur.EventSourcing.Subscriptions;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.EventSourcing.Projections;

/// <summary>
/// Background service that processes all <see cref="ProjectionMode.Async"/> projections
/// by polling the global event stream and dispatching events to registered projection handlers.
/// </summary>
/// <remarks>
/// <para>
/// Cooperating instances of this host write persisted projections using positioned compare-and-set writes. It polls the global stream and applies
/// events in position order, which is what lets a projection row's position mean what it claims.
/// Projections were once also folded during <c>SaveAsync</c> by a second, uncoordinated writer that
/// wrote without a position and so could lose a fold. That writer is gone. Inline projections are now
/// DRIVEN THROUGH THIS HOST rather than applied beside it: the save waits for this ordered processor to
/// reach the committed position, so there is still exactly one writer per projection row and the lost
/// fold is not expressible. A caller that does not want to wait passes the session token
/// <c>SaveAsync</c> returns to <c>IProjectionSessionReader</c> instead.
/// </para>
/// <para>
/// Register via <c>es.EnableProjectionProcessing()</c> on <see cref="DependencyInjection.IEventSourcingBuilder"/>.
/// Requires an <see cref="IGlobalStreamQuery"/> implementation (e.g., from <c>UseSqlServer()</c>).
/// </para>
/// </remarks>
internal sealed partial class AsyncProjectionProcessingHost : BackgroundService, IProjectionProcessor
{
	private readonly IProjectionRegistry _registry;
	private readonly IEventSerializer _eventSerializer;
	private readonly ISubscriptionCheckpointStore _checkpointStore;
	private readonly IOptions<GlobalStreamProjectionOptions> _options;
	private readonly IServiceProvider _serviceProvider;
	private readonly ILogger<AsyncProjectionProcessingHost> _logger;
	private readonly ProjectionObservability? _observability;
	private readonly ProjectionHealthState? _healthState;
	private readonly EventNotificationOptions _notificationOptions;
	private readonly TimeProvider _timeProvider;
	private readonly ProjectionMode _mode;

	// One finite page per lease. FIFO acquisition lets request-driven work make progress
	// while the background reader remains live; the lease is never held during idle polling.
	private readonly ConcurrencyLimiter _pageGate = new(new ConcurrencyLimiterOptions
	{
		PermitLimit = 1,
		QueueLimit = int.MaxValue,
		QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
	});

	/// <summary>
	/// One registration's in-process cursor identity: the subscription and the projection as a single
	/// string, for the health map that keys progress by it.
	/// </summary>
	/// <remarks>
	/// NOT the durable key. A cursor is stored under the PAIR -- the subscription name and the projection
	/// key in their own columns -- because the two parts are bounded independently and a composed name
	/// exceeds every column wide enough for the name alone.
	/// </remarks>
	private static string CheckpointNameFor(string subscription, ProjectionRegistration registration) =>
		CheckpointNameFor(subscription, registration.ProjectionType);

	/// <summary>The cursor key for one projection type. The single source of truth for that string.</summary>
	/// <remarks>
	/// Internal rather than private because the session reader and the tests must derive the SAME key.
	/// A second copy of this expression is a silent failure waiting to happen: a reader looking under a
	/// name nothing writes finds no cursor, waits out its whole bound, and reports a caught-up
	/// projection as stale.
	/// </remarks>
	internal static string CheckpointNameFor(string subscription, Type projectionType)
	{
		ArgumentNullException.ThrowIfNull(projectionType);

		// Delegated, not re-spelled: the session reader derives the same in-process identity and the two
		// must agree, or a reader waits out its whole bound on a progress entry nothing writes.
		return SubscriptionKeys.CheckpointName(subscription, ProjectionKeyFor(projectionType));
	}

	/// <summary>
	/// One projection's durable key within a subscription. The single source of truth for that string.
	/// </summary>
	/// <remarks>
	/// Shared with the roster binding deliberately: a member row and its cursor are keyed on the same
	/// pair, so a projection key that bound successfully is a projection key whose cursor is addressable.
	/// Two derivations that disagreed would bind a member whose cursor nothing could ever write.
	/// </remarks>
	internal static string ProjectionKeyFor(Type projectionType)
	{
		ArgumentNullException.ThrowIfNull(projectionType);

		return SubscriptionKeys.ProjectionKey(projectionType);
	}

	/// <summary>
	/// The registrations a save must wait for: those consuming at least one of its committed event types.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The required set depends only on committed event types, never on scheduling or failures.
	/// A null <c>HandlesEvent</c> means the registration handles everything, so it is always required.
	/// The predicate is complete: an event whose handler lookup misses is skipped by the fold before
	/// anything else runs, and the override-id escape hatch changes which ROW a fold targets rather
	/// than which TYPES a registration consumes.
	/// </para>
	/// </remarks>
	internal IReadOnlyList<Type> RequiredRegistrations(IReadOnlyList<Type> committedEventTypes)
	{
		ArgumentNullException.ThrowIfNull(committedEventTypes);

		var required = new List<Type>();
		foreach (var registration in _registry.GetByMode(_mode))
		{
			if (registration.HandlesEvent is not { } handles
				|| committedEventTypes.Any(handles))
			{
				required.Add(registration.ProjectionType);
			}
		}

		return required;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="AsyncProjectionProcessingHost"/> class.
	/// </summary>
	public AsyncProjectionProcessingHost(
		IProjectionRegistry registry,
		IEventSerializer eventSerializer,
		ISubscriptionCheckpointStore checkpointStore,
		IOptions<GlobalStreamProjectionOptions> options,
		IServiceProvider serviceProvider,
		ILogger<AsyncProjectionProcessingHost> logger,
		IOptions<EventNotificationOptions>? notificationOptions = null,
		TimeProvider? timeProvider = null,
		ProjectionMode mode = ProjectionMode.Async)
	{
		_registry = registry ?? throw new ArgumentNullException(nameof(registry));
		_eventSerializer = eventSerializer ?? throw new ArgumentNullException(nameof(eventSerializer));
		_checkpointStore = checkpointStore ?? throw new ArgumentNullException(nameof(checkpointStore));
		_options = options ?? throw new ArgumentNullException(nameof(options));
		_serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));

		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _checkpointStore = ProjectionCapacitySource.Checkpoints(serviceProvider, checkpointStore);

		_notificationOptions = notificationOptions?.Value ?? new EventNotificationOptions();
		_timeProvider = timeProvider ?? TimeProvider.System;
		_mode = mode;

		_observability = serviceProvider.GetService(typeof(ProjectionObservability)) as ProjectionObservability;
		_healthState = serviceProvider.GetService(typeof(ProjectionHealthState)) as ProjectionHealthState;
	}

    internal bool RequireDestinationScope { get; init; }

	/// <inheritdoc />
	public override async Task StartAsync(CancellationToken cancellationToken)
    {
        await GeneratedProjectionValidation.ValidateAsync(_serviceProvider, cancellationToken).ConfigureAwait(false);
		// .NET 10 runs all ExecuteAsync code in the background. Definition mismatches
		// must prevent the application from serving traffic, so validate here explicitly.
		_ = _serviceProvider.GetRequiredService<IGlobalStreamQuery>();
		ValidateSubscriptionOptions(_options.Value);
		_ = await BindDefinitionAsync(_registry.GetByMode(_mode), _options.Value, cancellationToken, strict: true)
			.ConfigureAwait(false);
		await base.StartAsync(cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc />
	public async Task<ProjectionProcessingResult> ProcessPendingAsync(CancellationToken cancellationToken)
		=> await ProcessThroughAsync(null, required: null, cancellationToken).ConfigureAwait(false);

	/// <summary>Whether ONE projection has folded through <paramref name="position"/>.</summary>
	/// <remarks>
	/// Per projection, never a minimum over all of them. Cursors are per registration, so a reader of
	/// one projection must be answered from THAT projection's cursor; a minimum would make a reader
	/// wait on projections it is not reading, which is the shared fate this design removes.
	/// </remarks>
	internal async Task<bool> HasAppliedAsync(Type projectionType, long position, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(projectionType);

		var registrations = _registry.GetByMode(_mode);
		var bound = await BindDefinitionAsync(registrations, _options.Value, cancellationToken).ConfigureAwait(false);

		var registration = registrations.FirstOrDefault(r => r.ProjectionType == projectionType);
		if (registration is null)
		{
			// Not a projection this host drives, so this host can make no claim about it.
			return false;
		}

        var member = bound.Roster.Members.FirstOrDefault(m => m.ProjectionKey == ProjectionKeyFor(projectionType));
        return !bound.Failed.Contains(projectionType) && member?.Position >= position;
    }

    internal Task<SubscriptionRoster> ReadRosterAsync(CancellationToken cancellationToken) =>
        _checkpointStore.ReadSubscriptionAsync(_options.Value.ProjectionName, cancellationToken);

	/// <summary>The subscription's roster members this process does not host, read from the durable store.</summary>
	/// <param name="cancellationToken">Cancels the binding read.</param>
	/// <returns>The unhosted members' projection keys, empty when the roster is fully hosted here.</returns>
	/// <remarks>
	/// Exposed so the foreground completion path can consult the roster on the returns where it does NOT
	/// process a page. A save with nothing local to advance still cannot report that nothing is owed while
	/// the roster holds a member this process cannot attest, and those returns never reach
	/// <see cref="IsCaughtUp"/>, where the same quantifier is enforced for a save that does process one.
	/// <para>
	/// It binds through the SAME path the processing loop uses rather than reading the store a second way.
	/// A second bind path would be free to drift from the first, and the two would then disagree about who
	/// hosts what -- a worse defect than the one this accessor exists to close.
	/// </para>
	/// </remarks>
	internal async Task<IReadOnlyList<string>> UnattestableMembersAsync(CancellationToken cancellationToken)
	{
		var registrations = _registry.GetByMode(_mode);
		var bound = await BindDefinitionAsync(registrations, _options.Value, cancellationToken).ConfigureAwait(false);
		return [.. bound.UnattestableMembers(registrations), .. bound.Failed.Select(ProjectionKeyFor)];
	}

	internal async Task<ProjectionProcessingResult> ProcessThroughAsync(
		long? committedPosition,
		IReadOnlyList<Type>? required,
		CancellationToken cancellationToken)
	{
		long applied = 0;
		try
		{
			var query = _serviceProvider.GetRequiredService<IGlobalStreamQuery>();
			// A finite target: concurrent producers cannot keep a request draining forever.
			var target = committedPosition ?? await ProjectionCapacitySource.ReadAsync(_serviceProvider, query, query.GetHeadPositionAsync, cancellationToken).ConfigureAwait(false);
			while (true)
			{
				var page = await ProcessPageAsync(query, cancellationToken, target, required).ConfigureAwait(false);
				applied += page.Applied;
				cancellationToken.ThrowIfCancellationRequested();

				// CAUGHT UP IS TESTED BEFORE THE HALT, AND THE ORDER IS THE WHOLE FIX. A page can report a
				// fault for a registration this caller does not depend on while every registration it DOES
				// depend on advanced past the target. Testing the halt first would fail this save for
				// somebody else's broken projection, which is the shared fate being removed.
				if (IsCaughtUp(page, required, target))
				{
					var current = await _checkpointStore.ReadSubscriptionAsync(_options.Value.ProjectionName, cancellationToken).ConfigureAwait(false);
                    if (page.Roster is null || !string.Equals(current.Identity, page.Roster.Identity, StringComparison.Ordinal))
                    {
                        return new ProjectionProcessingResult(applied, ProjectionProcessingOutcome.Contested, ProjectionHaltReason.None);
                    }
                    var covered = required is null ? page.Cursors.Keys : required;
                    var keys = covered.Select(ProjectionKeyFor).ToHashSet(StringComparer.Ordinal);
                    if (keys.Any(key => !current.Members.Any(member => member.ProjectionKey == key && member.Position >= target)))
                    {
                        return new ProjectionProcessingResult(applied, page.Outcome == ProjectionProcessingOutcome.Halted
                            ? page.Outcome : ProjectionProcessingOutcome.Contested, page.HaltReason);
                    }
                    return new ProjectionProcessingResult(applied, ProjectionProcessingOutcome.CaughtUp, ProjectionHaltReason.None)
                    {
                        Attestation = new ProjectionAttestation(current.SubscriptionName, current.Identity, target,
                            current.Members.Where(m => keys.Contains(m.ProjectionKey)).ToDictionary(static m => m.ProjectionKey, static m => m.BindingId, StringComparer.Ordinal)),
                    };
				}

				if (page.Outcome == ProjectionProcessingOutcome.Halted)
				{
                    if (required is not null && !required.Any(page.Failed.Contains))
                    {
                        if (required.Any(page.Progressed.Contains)) { continue; }
                        return new ProjectionProcessingResult(applied, ProjectionProcessingOutcome.Halted,
                            ProjectionHaltReason.PrefixUnavailable);
                    }
					// The page's own reason is propagated, never re-derived here: this frame cannot see
					// whether the apply faulted, an event was undeserializable, or the checkpoint write
					// failed, and guessing is what made Halted unactionable in the first place.
					return new ProjectionProcessingResult(applied, page.Outcome, page.HaltReason);
				}

				if (page.Applied == 0 && page.Outcome != ProjectionProcessingOutcome.Contested)
				{
					// A temporarily unreadable prefix is pending, never falsely caught up. This is the
					// one halt that clears by waiting, so it is the one a caller may retry.
					return new ProjectionProcessingResult(applied, ProjectionProcessingOutcome.Halted, ProjectionHaltReason.PrefixUnavailable);
				}
			}
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
			return new ProjectionProcessingResult(applied, ProjectionProcessingOutcome.Cancelled, ProjectionHaltReason.None);
		}
	}

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		var query = _serviceProvider.GetRequiredService<IGlobalStreamQuery>();
		while (!stoppingToken.IsCancellationRequested)
		{
			try
			{
				var page = await ProcessPageAsync(query, stoppingToken).ConfigureAwait(false);
				if (page.Applied == 0 || page.Outcome == ProjectionProcessingOutcome.Halted)
				{
					await Task.Delay(_options.Value.IdlePollingInterval, stoppingToken).ConfigureAwait(false);
				}
			}
			catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
			{
				break;
			}
			catch (Exception ex)
			{
				LogAsyncProjectionHostError(ex);
				_healthState?.RecordHalt(_options.Value.ProjectionName);
				await Task.Delay(_options.Value.IdlePollingInterval, stoppingToken).ConfigureAwait(false);
			}
		}
	}

	/// <summary>The completion predicate: a quantifier over what the caller depends on.</summary>
	/// <remarks>
	/// With no required set, every registration must have reached the target. A required subset
	/// cannot be held back by unrelated failures, but scheduling never changes what either claim means.
	/// </remarks>
	private static bool IsCaughtUp(PageResult page, IReadOnlyList<Type>? required, long target)
	{
		// A MEMBER THIS PROCESS DOES NOT HOST CANNOT BE ATTESTED BY IT, so its presence alone denies a
		// caught-up report regardless of how far the hosted projections have advanced. This is the half
		// of the predicate the local registry cannot supply: the registry is silent about work it cannot
		// see, and silence here would read as consent.
		//
		// FIRST, AND ABOVE THE NULL BRANCH. This guard was initially placed after the `required is null`
		// early return, which left that branch able to report caught-up with unhosted members present --
		// the whole defect, surviving on one path. Every return below this point presumes the roster is
		// fully hosted here, so the presumption is established before any of them can be reached.
		if (page.UnattestableMembers.Count > 0)
		{
			return false;
		}

		if (required is null)
		{
			return page.Failed.Count == 0 && page.Cursors.Values.All(at => at >= target);
		}

		foreach (var projectionType in required)
		{
			if (page.Failed.Contains(projectionType) || !page.Cursors.TryGetValue(projectionType, out var at) || at < target)
			{
				return false;
			}
		}

		return true;
	}

	private async Task<PageResult> ProcessPageAsync(IGlobalStreamQuery query, CancellationToken cancellationToken, long? target = null, IReadOnlyList<Type>? required = null)
	{
		using var lease = await _pageGate.AcquireAsync(1, cancellationToken).ConfigureAwait(false);
		if (!lease.IsAcquired)
		{
			throw new InvalidOperationException("The projection page queue is full or disposed.");
		}

        var hostedRegistrations = _registry.GetByMode(_mode);
        // Foreground work must not wait for unrelated providers, including their scope discovery.
        // The full registry still witnesses which durable members this process hosts.
        var registrations = required is null ? hostedRegistrations
            : hostedRegistrations.Where(registration => required.Contains(registration.ProjectionType)).ToArray();
		var options = _options.Value;
		var name = options.ProjectionName;
		ValidateSubscriptionOptions(options);

		var bound = await BindDefinitionAsync(registrations, options, cancellationToken).ConfigureAwait(false);
		var destinations = bound.Destinations;

		// THE REQUIRED SET'S STORE-SOURCED HALF. Read from the durable roster, never from the local
		// registry: a member bound by another deployment is work this process cannot perform, so it
		// cannot be attested here, and a page carrying one can never report caught-up. Computing this
		// from _registry instead would let this process fold what it can see, report Fresh, and say
		// nothing about a sibling that has not folded the same events — an attestation that is false and
		// that nothing downstream can detect, because the caller was told it need not re-check.
		var unattestable = bound.UnattestableMembers(hostedRegistrations);
		if (unattestable.Count > 0)
		{
			// Warning, not a third outcome. The guarantee contract states there are exactly two —
			// caught-up or behind — so the REASON is logged and the OUTCOME stays binary.
			LogUnattestableRosterMembers(name, string.Join(", ", unattestable));
		}

		// ONE CURSOR READ PER REGISTRATION. The expected value keeps its nullability, because the store
		// distinguishes "no cursor yet" from "a cursor at zero" and the compare-and-set below relies on
		// that distinction.
		var keys = new string[registrations.Count];
		var expectedAt = new long?[registrations.Count];
		var bindings = new Guid[registrations.Count];
		var rosterMembers = bound.Roster.Members.ToDictionary(static m => m.ProjectionKey, StringComparer.Ordinal);
		var cursorAt = new long[registrations.Count];
		var cursors = new Dictionary<Type, long>(registrations.Count);
		for (var i = 0; i < registrations.Count; i++)
		{
			keys[i] = CheckpointNameFor(name, registrations[i]);
            if (bound.Failed.Contains(registrations[i].ProjectionType))
            {
                cursors[registrations[i].ProjectionType] = 0;
                continue;
            }
			var member = rosterMembers[ProjectionKeyFor(registrations[i].ProjectionType)];
			expectedAt[i] = member.Position;
			bindings[i] = member.BindingId;
			cursorAt[i] = expectedAt[i] ?? 0;
			cursors[registrations[i].ProjectionType] = cursorAt[i];
		}

		if (registrations.Count == 0)
		{
			// A process hosting NO projections still cannot attest a roster that has members. This is the
			// extreme of the divergence case: every bound member is unattestable here, so a caught-up
			// report would be a claim about work this process cannot even enumerate.
			return new PageResult(0, 0, unattestable.Count == 0 ? ProjectionProcessingOutcome.CaughtUp : ProjectionProcessingOutcome.Halted,
                unattestable.Count == 0 ? ProjectionHaltReason.None : ProjectionHaltReason.SubscriptionMembersNotHosted,
				cursors, unattestable) { Roster = bound.Roster };
		}

        long applied = 0;
        var failed = new HashSet<Type>(bound.Failed);
        var progressed = new HashSet<Type>();
        var outcome = failed.Count == 0 ? ProjectionProcessingOutcome.CaughtUp : ProjectionProcessingOutcome.Halted;
        var reason = failed.Count == 0 ? ProjectionHaltReason.None : ProjectionHaltReason.ProjectionFaulted;
        // Share a read only when every recipient starts at that exact durable cursor. A failed
        // registration remains eligible on the next round, but never receives a later sibling's page.
        foreach (var group in Enumerable.Range(0, registrations.Count).Where(i => !bound.Failed.Contains(registrations[i].ProjectionType)).GroupBy(i => cursorAt[i]))
        {
            var indices = group.ToArray();
            var members = indices.Select(i => registrations[i]).ToArray();
            var expectations = indices.Select(i => expectedAt[i]).ToArray();
            var page = await ProcessCursorGroupAsync(query, members, expectations, indices.Select(i => bindings[i]).ToArray(), destinations,
                group.Key, options, target, cancellationToken).ConfigureAwait(false);
            applied += page.Applied;
            failed.UnionWith(page.Failed);
            progressed.UnionWith(page.Progressed);
            foreach (var entry in page.Cursors) { cursors[entry.Key] = entry.Value; }
            if (page.Outcome == ProjectionProcessingOutcome.Halted && outcome != ProjectionProcessingOutcome.Halted)
            {
                outcome = page.Outcome;
                reason = page.HaltReason;
            }
            else if (page.Outcome == ProjectionProcessingOutcome.Contested && outcome == ProjectionProcessingOutcome.CaughtUp)
            {
                outcome = page.Outcome;
            }
        }
        if (outcome != ProjectionProcessingOutcome.Halted) { _healthState?.RecordApplied(name, cursors.Values.Min()); }
        return new PageResult(applied, cursors.Values.Min(), outcome, reason, cursors, unattestable)
        { Failed = failed, Progressed = progressed, Roster = bound.Roster };
    }

    private async Task<PageResult> ProcessCursorGroupAsync(IGlobalStreamQuery query,
        IReadOnlyList<ProjectionRegistration> registrations, long?[] expectedAt, Guid[] bindings,
        IReadOnlyDictionary<Type, string?> destinations, long position, GlobalStreamProjectionOptions options,
        long? target, CancellationToken cancellationToken)
    {
        var name = options.ProjectionName;
        var keys = registrations.Select(r => CheckpointNameFor(name, r)).ToArray();
        var cursorAt = expectedAt.Select(at => at ?? 0).ToArray();
        var cursors = registrations.ToDictionary(r => r.ProjectionType, _ => position);
        var failed = new HashSet<Type>();
        var progressed = new HashSet<Type>();
        // Empty unattestable set, and this is the ONE place that is correct. This is a per-cursor-group
        // page whose result is aggregated by ProcessPageAsync, which supplies the subscription's real
        // unattestable set on the PageResult it returns. The caught-up predicate only ever reads that
        // outer page, never this inner one. If this local function ever becomes reachable from
        // ProcessThroughAsync directly, this argument becomes wrong and must carry the real set.
        PageResult Result(long applied, ProjectionProcessingOutcome outcome, ProjectionHaltReason reason) =>
            new(applied, cursors.Values.Min(), outcome, reason, cursors, []) { Failed = failed, Progressed = progressed };
		var events = await ProjectionCapacitySource.ReadAsync(_serviceProvider, query, token => query.ReadAllAsync(
			new GlobalStreamPosition(position, DateTimeOffset.MinValue), options.BatchSize, token), cancellationToken)
			.ConfigureAwait(false);
        if (events.Count == 0 && position == 0)
        {
            var initializationContested = false;
            for (var i = 0; i < registrations.Count; i++)
            {
                if (expectedAt[i] is not null) { continue; }
                var type = registrations[i].ProjectionType;
                try
                {
                    var initialized = await _checkpointStore.AdvanceCheckpointAsync(name, ProjectionKeyFor(type), bindings[i], null, 0, cancellationToken).ConfigureAwait(false);
                    if (initialized == CheckpointAdvanceOutcome.Superseded)
                    {
                        var snapshot = await _checkpointStore.ReadSubscriptionAsync(name, cancellationToken).ConfigureAwait(false);
                        var current = snapshot.Members.FirstOrDefault(m => m.ProjectionKey == ProjectionKeyFor(type));
                        if (current?.BindingId != bindings[i] || current.Position is null)
                        {
                            initializationContested = true;
                            continue;
                        }
                        cursors[type] = current.Position.Value;
                    }
                }
                catch (Exception error) when (error is not OperationCanceledException)
                {
                    LogAsyncProjectionCheckpointWriteFailed(error);
                    failed.Add(type);
                }
            }
            return failed.Count > 0
                ? Result(0, ProjectionProcessingOutcome.Halted, ProjectionHaltReason.CheckpointWriteFailed)
                : Result(0, initializationContested ? ProjectionProcessingOutcome.Contested : ProjectionProcessingOutcome.CaughtUp,
                    ProjectionHaltReason.None);
        }
		var batch = new List<ProjectionEvent>(events.Count);
		StoredEvent? last = null;
		var processed = 0;
		var poison = false;
		var faulted = false;
		var contested = false;
		var checkpointFailed = false;
		var advanced = false;
		foreach (var stored in events)
		{
			cancellationToken.ThrowIfCancellationRequested();
			if (target is { } limit && stored.GlobalPosition > limit) { break; }
			var expectedNext = (last?.GlobalPosition ?? position) + 1;
			if (stored.GlobalPosition < expectedNext)
			{
				throw new InvalidOperationException("The global stream returned an unordered or non-advancing page.");
			}
			if (stored.GlobalPosition > expectedNext)
			{
				// CONTIGUITY IS ASSERTED HERE because this is the site that WRITES the checkpoint, and
				// the checkpoint is the claim contiguity protects: advancing the cursor to this page's
				// last position asserts every position at or below it has been folded. A gap breaks
				// that claim silently and unrecoverably -- the skipped event stays below the cursor
				// forever, so nothing redelivers it and nothing downstream ever learns.
				//
				// ContiguousGlobalStreamQuery normally truncates a gapped page before it reaches here,
				// so in a provider-registered composition this never fires. It is NOT redundant with
				// that decorator: the decorator is applied with TryAdd on a public interface, so the
				// FIRST registration wins and a consumer -- or a second UseXxx() call -- can displace
				// it, at which point the guarantee is silently absent. Asserting it at the invariant's
				// own site makes the bad state unreachable regardless of who registered what, and
				// leaves the decorator as the early-truncate-and-log optimisation it should be.
				throw new InvalidOperationException(
					$"The global stream returned position {stored.GlobalPosition} where {expectedNext} was required. "
					+ "A page containing a gap cannot advance a projection checkpoint: the missing positions would "
					+ "sit below the cursor without having been folded, and would never be redelivered. An "
					+ "IGlobalStreamQuery must return the contiguous run from the requested position, stopping at "
					+ "the first gap rather than reading past it.");
			}
			if (!ErasedEventMarker.IsErased(stored.EventType))
			{
				try
				{
					batch.Add(new ProjectionEvent(DeserializeOrThrow(stored), stored.AggregateId, stored.GlobalPosition, stored.TenantId)
                    { Envelope = stored });
				}
				catch (Exception ex) when (ex is not OperationCanceledException)
				{
					LogAsyncProjectionEventError(stored.EventId, ex);
					RecordPoison(name, ex);
					poison = true;
					break;
				}
			}
			last = stored;
			processed++;
		}
		// ALL-FALSE WHEN NOTHING WAS DISPATCHED. An all-tombstone page delivers no batch, and the cursor
		// must still advance past it -- otherwise the host re-reads the same tombstones forever. Nothing
		// was applied, so nothing can have faulted.
		var faults = new bool[registrations.Count];
		if (batch.Count > 0)
		{
			var context = new EventNotificationContext(last!.AggregateId, last.AggregateType,
				last.Version, last.Timestamp, IsReplay: false, GlobalPosition: last.GlobalPosition);
			faults = await DispatchToProjectionsAsync(registrations, batch, context, destinations, cancellationToken).ConfigureAwait(false);
			cancellationToken.ThrowIfCancellationRequested();
		}

		{

			// A FAULT NO LONGER STOPS ITS SIBLINGS. Each registration's cursor advances on its own, so a
			// faulting projection holds only its own cursor back. Advancing a shared cursor past a
			// registration that did not fold is what made this a stall for everyone -- and is still
			// forbidden, which is exactly what a per-registration cursor expresses.
			for (var i = 0; i < registrations.Count; i++)
			{
				var projectionType = registrations[i].ProjectionType;
				if (faults[i])
				{
					faulted = true;
					failed.Add(projectionType);
					continue;
				}


				// NEVER ADVANCE BACKWARDS. A page bounded by a save's target can end BELOW a registration
				// that is already further ahead -- the registration folded nothing here because it had
				// already folded all of it. Its cursor must stay put: the store refuses a lower value
				// outright, so without this guard the common case throws in the write path.
				if (last is null || last.GlobalPosition <= cursorAt[i])
				{
					continue;
				}

				// THE VALUE WRITTEN IS "DELIVERED THROUGH", AND IT EQUALS THE PAGE'S LAST POSITION ONLY
				// BECAUSE EVERY REGISTRATION RECEIVES THE WHOLE PROCESSED PREFIX. Read that sentence
				// before changing the dispatch.
				//
				// A cursor advanced to P asserts that nothing at or below P is still owed to THIS
				// registration. `last` can sit above the highest BATCHED position -- trailing erased
				// events advance `last` without entering the batch -- and that is sound here, because a
				// tombstone carries nothing to fold and every registration was handed the same batch.
				//
				var deliveredThrough = last.GlobalPosition;

				try
				{
					var outcome = await _checkpointStore.AdvanceCheckpointAsync(
						name, ProjectionKeyFor(projectionType), bindings[i], expectedAt[i], deliveredThrough,
						cancellationToken).ConfigureAwait(false);
					if (outcome == CheckpointAdvanceOutcome.Superseded)
					{
						// Another instance advanced this registration. Its fold is idempotent at the row,
						// so the work is not lost -- re-read and let the caller iterate.
						var snapshot = await _checkpointStore.ReadSubscriptionAsync(name, cancellationToken).ConfigureAwait(false);
						var winner = snapshot.Members.FirstOrDefault(m => m.ProjectionKey == ProjectionKeyFor(projectionType));
						cursors[projectionType] = winner?.BindingId == bindings[i] ? winner.Position ?? 0 : 0;
                        if (cursors[projectionType] > cursorAt[i]) { progressed.Add(projectionType); }
						contested = true;
						continue;
					}

					cursors[projectionType] = deliveredThrough;
                    progressed.Add(projectionType);
                    _healthState?.RecordApplied(keys[i], deliveredThrough);
					advanced = true;
				}
				catch (Exception ex) when (ex is not OperationCanceledException)
				{
					LogAsyncProjectionCheckpointWriteFailed(ex);
					checkpointFailed = true;
                    failed.Add(projectionType);
				}
			}
		}

		// A handler that returns after catching cancellation cannot acknowledge the page.
		// Every page starts from durable state; a crash or failed CAS simply redelivers it.
		cancellationToken.ThrowIfCancellationRequested();

        // A subscription-wide mark may only describe the minimum across ALL registrations.
        // Per-projection session reads use the individual durable/confirmed cursor instead.
        if (poison) { failed.UnionWith(registrations.Select(r => r.ProjectionType)); }
		if (checkpointFailed)
		{
			_healthState?.RecordHalt(name);
			return Result(0, ProjectionProcessingOutcome.Halted, ProjectionHaltReason.CheckpointWriteFailed);
		}

		if (poison)
		{
			_healthState?.RecordHalt(name);
            return Result(processed, ProjectionProcessingOutcome.Halted, ProjectionHaltReason.EventUndeserializable);
		}

		if (faulted)
		{
			// Reported as a halt so a caller waiting on THIS registration learns the cause, while the
			// healthy registrations above have already advanced regardless.
			//
			// APPLIED REFLECTS WHETHER ANY CURSOR MOVED, not whether a page was read. When the only
			// projection on the page faulted, nothing is durably acknowledged and the honest count is
			// zero -- the pre-change contract, and what a caller inspecting progress depends on. When a
			// sibling DID advance, zero would understate real durable progress.
			_healthState?.RecordHalt(name);
			return Result(advanced ? processed : 0, ProjectionProcessingOutcome.Halted, ProjectionHaltReason.ProjectionFaulted);
		}

		if (contested)
		{
			return Result(processed, ProjectionProcessingOutcome.Contested, ProjectionHaltReason.None);
		}

		return Result(processed, ProjectionProcessingOutcome.CaughtUp, ProjectionHaltReason.None);
	}

	private static void ValidateSubscriptionOptions(GlobalStreamProjectionOptions options)
	{
		// SECOND LINE, NOT THE FIRST. GlobalStreamProjectionOptionsValidator refuses these at startup
		// through ValidateOnStart, which is where a consumer can act on them. This check remains for
		// options constructed directly rather than resolved from the container -- a test, or a host
		// driving IProjectionProcessor by hand -- and it names WHICH option is blank, because naming
		// both sends the reader to check one that was never wrong.
		if (string.IsNullOrWhiteSpace(options.SubscriptionScope))
		{
			throw new InvalidOperationException(
				$"Projection processing requires {nameof(GlobalStreamProjectionOptions.SubscriptionScope)}, "
				+ "which has no default. Set it to a stable name identifying this event source and "
				+ "projection destination together; every cooperating process must use the same value.");
		}

		if (string.IsNullOrWhiteSpace(options.SubscriptionRevision))
		{
			throw new InvalidOperationException(
				$"Projection processing requires a non-blank "
				+ $"{nameof(GlobalStreamProjectionOptions.SubscriptionRevision)}. It defaults to \"1\", so a "
				+ "blank value means something explicitly set it.");
		}

	}

	private async Task<BoundSubscription> BindDefinitionAsync(
		IReadOnlyList<ProjectionRegistration> registrations,
		GlobalStreamProjectionOptions options, CancellationToken cancellationToken, bool strict = false)
	{
		if (registrations.Any(r => r.InlineApply is null))
		{
			throw new InvalidOperationException("Every asynchronous projection must have a bound apply delegate.");
		}
		var destinations = new Dictionary<Type, string?>();
		var identities = new Dictionary<Type, string?>();
        var destinationScopes = new Dictionary<Type, string?>();
        var failed = new HashSet<Type>();
		await using (var scope = _serviceProvider.CreateAsyncScope())
		{
            var probes = scope.ServiceProvider.GetServices<IPositionedProjectionProbe>().ToDictionary(probe => probe.ProjectionType);
            foreach (var registration in registrations)
            {
                try
                {
                    if (!probes.TryGetValue(registration.ProjectionType, out var probe))
                    {
                        if (RequireDestinationScope) { throw new InvalidOperationException($"Projection '{registration.ProjectionType.Name}' cannot identify its storage scope."); }
                        continue;
                    }
                    if (!probe.IsPositioned(scope.ServiceProvider))
                    {
                        throw new InvalidOperationException($"Asynchronous projection '{probe.ProjectionTypeName}' requires positioned writes.");
                    }
                    var identity = probe.GetDestinationIdentity(scope.ServiceProvider);
                    var destinationScope = await probe.GetDestinationScopeAsync(scope.ServiceProvider, cancellationToken).ConfigureAwait(false);
                    if (RequireDestinationScope && string.IsNullOrWhiteSpace(destinationScope))
                    {
                        throw new InvalidOperationException($"Projection '{registration.ProjectionType.Name}' cannot identify its cluster automatically. Set GlobalStreamProjectionOptions.SubscriptionScope explicitly.");
                    }
                    destinations[registration.ProjectionType] = DestinationDescriptor(_mode, identity, destinationScope);
                    identities[registration.ProjectionType] = identity;
                    destinationScopes[registration.ProjectionType] = destinationScope;
                }
                catch (Exception error) when (!strict && !cancellationToken.IsCancellationRequested)
                {
                    failed.Add(registration.ProjectionType);
                    LogAsyncProjectionHostError(error);
                    _healthState?.RecordHalt(options.ProjectionName);
                }
            }
		}

		// ONE FINGERPRINT PER PROJECTION, not one over the registration set. A set-wide fingerprint
		// refuses every cooperating host whenever a projection is added, removed or renamed, or any one
		// destination moves -- and the only remedy re-folds EVERY projection from zero, including the ones
		// that did not change. Per member, an additive change is additive, and a moved destination refuses
		// only the projection that moved.
		//
		// Scope and revision remain inputs to EVERY member's fingerprint, so a deliberate change to either
		// still refuses the whole roster. That was never the problem and is still the behaviour we want.
		//
		// The length-prefixed encoding is retained verbatim: it is what keeps a fingerprint unambiguous
		// when a part contains the separator, so two different definitions cannot collide.
		var members = new List<SubscriptionMember>(registrations.Count);
		foreach (var registration in registrations.OrderBy(
			r => r.ProjectionType.Assembly.GetName().Name, StringComparer.Ordinal)
			.ThenBy(r => r.ProjectionType.FullName, StringComparer.Ordinal))
		{
            if (failed.Contains(registration.ProjectionType)) { continue; }
			var destination = destinations.GetValueOrDefault(registration.ProjectionType) ?? string.Empty;
			var projectionKey = ProjectionKeyFor(registration.ProjectionType);

			members.Add(new SubscriptionMember(
				ProjectionKey: projectionKey,
				DefinitionHash: SubscriptionKeys.DefinitionFingerprint(
					options.SubscriptionScope,
					options.SubscriptionRevision,
					registration.ProjectionType.Assembly.GetName().Name ?? string.Empty,
					projectionKey,
					destination),
				Destination: identities.GetValueOrDefault(registration.ProjectionType) ?? string.Empty)
            { DestinationScope = destinationScopes.GetValueOrDefault(registration.ProjectionType) });
		}

		// The roster is RETURNED and must not be discarded. It carries members bound by other deployments
		// that this process does not host, and those are precisely the ones this process cannot attest --
		// the required-set quantifier reads it rather than the local registry.
		var roster = await _checkpointStore
			.BindSubscriptionAsync(options.ProjectionName, members, cancellationToken)
			.ConfigureAwait(false);

		return new BoundSubscription(destinations, roster, failed);
	}

	/// <summary>
	/// What a binding established: each hosted projection's destination, and the subscription's COMPLETE
	/// roster as the durable store holds it.
	/// </summary>
	/// <param name="Destinations">The destination identity of each projection this process hosts.</param>
    /// <param name="Failed">Hosted projections whose destination could not be resolved in this round.</param>
	/// <param name="Roster">
	/// Every member bound to the subscription, including any this process does not host. A member absent
	/// from this process is work it cannot perform, and therefore cannot attest.
	/// </param>
	private sealed record BoundSubscription(
		IReadOnlyDictionary<Type, string?> Destinations,
		SubscriptionRoster Roster,
        IReadOnlySet<Type> Failed)
	{
		/// <summary>
		/// Roster members this process does not host, and therefore cannot attest.
		/// </summary>
		/// <param name="registrations">The projections this process hosts.</param>
		/// <returns>The unhosted members' projection keys, empty when the roster is fully hosted here.</returns>
		// Qualified: the nested record's own method name would otherwise shadow the outer static and
		// recurse into itself.
		internal IReadOnlyList<string> UnattestableMembers(IReadOnlyList<ProjectionRegistration> registrations) =>
			AsyncProjectionProcessingHost.UnattestableMembers(Roster, registrations);
	}

	/// <summary>
	/// The roster members a process does not host, and therefore cannot attest.
	/// </summary>
	/// <param name="roster">The subscription's roster, as the durable store holds it.</param>
	/// <param name="registrations">The projections this process hosts.</param>
	/// <returns>The unhosted members' projection keys, empty when the roster is fully hosted here.</returns>
	/// <remarks>
	/// <para>
	/// This is the store-sourced half of the required set. The hosted half is derived from the local
	/// registry by event type, which is correct for work this process can perform; it is silent about
	/// work it cannot see, and the roster is the only place that work is visible.
	/// </para>
	/// <para>
	/// Lifted out of the binding result so there is ONE implementation rather than two: the host reads it
	/// to decide whether it may attest, and the generated property suite drives this same method over
	/// random roster-versus-registry set differences. A property suite computing the difference itself
	/// would assert that its own arithmetic is consistent and say nothing about the host.
	/// </para>
	/// <para>
	/// Ordinal comparison, matching how a projection key is compared in storage: two keys differing only
	/// by case are two members, so treating them as one here would silently drop one from the
	/// unattestable set and permit an attestation over work this process does not host.
	/// </para>
	/// </remarks>
	internal static IReadOnlyList<string> UnattestableMembers(
		SubscriptionRoster roster,
		IReadOnlyList<ProjectionRegistration> registrations)
	{
		ArgumentNullException.ThrowIfNull(roster);
		ArgumentNullException.ThrowIfNull(registrations);

		var hosted = new HashSet<string>(StringComparer.Ordinal);
		foreach (var registration in registrations)
		{
			_ = hosted.Add(ProjectionKeyFor(registration.ProjectionType));
		}

		return [.. roster.Members
			.Select(static member => member.ProjectionKey)
			.Where(key => !hosted.Contains(key))];
	}

	private static void AppendDescriptorPart(StringBuilder builder, string part) =>
		builder.Append(part.Length.ToString(CultureInfo.InvariantCulture)).Append(':').Append(part);


    internal string DestinationFingerprint(Type type, string? identity, string? scope) =>
        SubscriptionKeys.DefinitionFingerprint(_options.Value.SubscriptionScope, _options.Value.SubscriptionRevision,
            type.Assembly.GetName().Name ?? string.Empty, ProjectionKeyFor(type),
            DestinationDescriptor(_mode, identity, scope) ?? string.Empty);

    internal static string? DestinationDescriptor(ProjectionMode mode, string? identity, string? scope)
    {
        if (mode != ProjectionMode.Inline && string.IsNullOrWhiteSpace(scope)) { return identity; }
        var descriptor = new StringBuilder();
        AppendDescriptorPart(descriptor, scope ?? string.Empty);
        AppendDescriptorPart(descriptor, identity ?? string.Empty);
        return descriptor.ToString();
    }

    private async ValueTask<string?> GetDestinationAsync(IPositionedProjectionProbe probe, IServiceProvider provider, CancellationToken cancellationToken) =>
        DestinationDescriptor(_mode, probe.GetDestinationIdentity(provider), await probe.GetDestinationScopeAsync(provider, cancellationToken).ConfigureAwait(false));

	/// <param name="Applied">Stream events in this page, including tombstones.</param>
	/// <param name="Position">
	/// The minimum cursor over every registration in this page: the point from which the
	/// next page is owed, not the furthest any single projection has reached.
	/// </param>
	/// <param name="Outcome">Why this page stopped.</param>
	/// <param name="HaltReason">The cause of a halt, or <see cref="ProjectionHaltReason.None"/>.</param>
	/// <param name="Cursors">
	/// Every registration's cursor AFTER this page. Carried rather than recomputed so a caller can test
	/// its own required subset without a second round of store reads.
	/// </param>
	/// <param name="UnattestableMembers">
	/// Projection keys bound to this subscription that THIS PROCESS DOES NOT HOST. Their work cannot be
	/// performed here, so it cannot be attested here either, and a page carrying any of them can never
	/// report caught-up.
	/// <para>
	/// POSITIONAL, NOT AN INIT PROPERTY WITH A DEFAULT, AND THAT IS DELIBERATE. An empty list means
	/// "every bound member is hosted here", which is the value that permits an attestation — the unsafe
	/// one. An init property defaulting to empty would let a construction site select it by saying
	/// nothing, which is exactly how this class of defect ships. Making it positional means the compiler
	/// names every site that must answer the question.
	/// </para>
	/// </param>
	private readonly record struct PageResult(
		long Applied,
		long Position,
		ProjectionProcessingOutcome Outcome,
		ProjectionHaltReason HaltReason,
		IReadOnlyDictionary<Type, long> Cursors,
		IReadOnlyList<string> UnattestableMembers)
    {
        internal SubscriptionRoster? Roster { get; init; }
        internal IReadOnlySet<Type> Failed { get; init; } = new HashSet<Type>();
        internal IReadOnlySet<Type> Progressed { get; init; } = new HashSet<Type>();
    }

	/// <summary>
	/// Resolves and deserializes a stored event to a domain event, throwing on a null result rather
	/// than silently dropping it (a null deserialization is a poison event, not an empty batch).
	/// </summary>
	[UnconditionalSuppressMessage("AOT", "IL3050",
		Justification = "Event deserialization is inherently dynamic; projection host requires runtime type resolution.")]
	[UnconditionalSuppressMessage("Trimming", "IL2026",
		Justification = "Event deserialization requires type metadata; consumers must preserve event types.")]
	private IDomainEvent DeserializeOrThrow(StoredEvent storedEvent)
	{
		var eventType = _eventSerializer.ResolveType(storedEvent.EventType);
		return _eventSerializer.DeserializeEvent(StoredEventPayload.Require(storedEvent), eventType)
			?? throw new InvalidOperationException(
				$"Event '{storedEvent.EventId}' (type '{storedEvent.EventType}') deserialized to null; refusing to skip it.");
	}

	/// <summary>
	/// Records a poison event against observability and host health, swallowing telemetry failures so
	/// they never affect the projection pipeline.
	/// </summary>
	private void RecordPoison(string projectionName, Exception ex)
	{
		try
		{
			_observability?.RecordError(projectionName, ex.GetType().Name);
		}
		catch
		{
			// Swallow -- metrics must not affect the projection pipeline.
		}

		try
		{
			_healthState?.RecordInlineError(projectionName);
		}
		catch
		{
			// Swallow -- health recording must not affect the projection pipeline.
		}
	}

	/// <summary>
	/// Applies a page to projection registrations concurrently and reports each registration's failure.
	/// </summary>
	/// <returns>
	/// Failure flags in registration order. A <see langword="true"/> entry prevents that registration's
	/// checkpoint from advancing past the page, so its failed apply can be retried.
	/// </returns>
	/// <remarks>
	/// Every registration is attempted independently. Successful registrations can advance their own
	/// checkpoints even when another registration fails. These outcomes describe applying the stream
	/// page; individual read-model row positions do not establish that the whole page was processed.
	/// </remarks>
	private async Task<bool[]> DispatchToProjectionsAsync(
		IReadOnlyList<ProjectionRegistration> registrations,
		List<ProjectionEvent> domainEvents,
		EventNotificationContext context,
		IReadOnlyDictionary<Type, string?> destinations,
		CancellationToken cancellationToken)
	{
		var tasks = new Task[registrations.Count];

		for (var i = 0; i < registrations.Count; i++)
		{
			var registration = registrations[i];

			if (registration.InlineApply is not null)
			{
				// IProjectionStore<T> is scoped; this host is a singleton BackgroundService, so the
				// apply delegate must receive a provider from a created scope, not the captured root
				// provider (which throws under DI scope validation). A scope per projection also
				// isolates scoped state across the concurrently-applied projections.
				tasks[i] = ApplyInScopeAsync(registration, domainEvents, context, destinations.GetValueOrDefault(registration.ProjectionType), cancellationToken);
			}
			else
			{
				tasks[i] = Task.CompletedTask;
			}
		}

		var faulted = new bool[tasks.Length];
		for (var j = 0; j < tasks.Length; j++)
		{
			try
			{
				await tasks[j].ConfigureAwait(false);
			}
#pragma warning disable CA1031 // Catch general exceptions -- partial failure; log, record, and report to halt the batch
			catch (Exception ex)
#pragma warning restore CA1031
			{
				// Fault-independence: still await every projection so an independent one is not abandoned.
				// But REPORT the fault so the caller does not advance the checkpoint past it (halt-at-failure),
				// preventing silent read-model drift from the event log.
				faulted[j] = true;
				var projectionName = registrations[j].ProjectionType.Name;
				LogAsyncProjectionDispatchError(projectionName, ex);

				try
				{
					_healthState?.RecordInlineError(projectionName);
					_observability?.RecordError(projectionName, ex.GetType().Name);
				}
				catch
				{
					// Swallow -- metrics must not affect projection pipeline
				}
			}
		}

		return faulted;
	}

	/// <remarks>
	/// Timed because a slow apply is the earliest observable cause of a degraded read: a
	/// session-consistent read waits on this projection reaching the caller's token, so an apply that
	/// is merely slow surfaces to the consumer as a read whose bound expired. The per-projection
	/// threshold wins over the global one when both are set.
	/// </remarks>
	private async Task ApplyInScopeAsync(
		ProjectionRegistration registration,
		List<ProjectionEvent> domainEvents,
		EventNotificationContext context,
		string? expectedDestination,
		CancellationToken cancellationToken)
	{
		var started = _timeProvider.GetTimestamp();
		if (registration.HandlesEvent is { } handles && !domainEvents.Any(e => handles(e.Domain.GetType())))
		{
			return;
		}

		await using (var scope = _serviceProvider.CreateAsyncScope())
		{
			foreach (var probe in scope.ServiceProvider.GetServices<IPositionedProjectionProbe>())
			{
				if (probe.ProjectionType == registration.ProjectionType
					&& !string.Equals(expectedDestination, await GetDestinationAsync(probe, scope.ServiceProvider, cancellationToken).ConfigureAwait(false), StringComparison.Ordinal))
				{
					throw new InvalidOperationException($"Projection '{probe.ProjectionTypeName}' changed destination between binding and applying the page.");
				}
			}
			await registration.InlineApply!(domainEvents, context, scope.ServiceProvider, cancellationToken)
				.ConfigureAwait(false);
			// Even a fold skipped because another process already wrote must establish
			// SEARCH visibility before this process acknowledges the page.
			foreach (var probe in scope.ServiceProvider.GetServices<IPositionedProjectionProbe>())
			{
				if (probe.ProjectionType == registration.ProjectionType)
				{
					if (!string.Equals(expectedDestination, await GetDestinationAsync(probe, scope.ServiceProvider, cancellationToken).ConfigureAwait(false), StringComparison.Ordinal))
					{
						throw new InvalidOperationException("The projection destination changed while applying the page; progress cannot be acknowledged.");
					}
					await probe.EnsureVisibleAsync(scope.ServiceProvider, cancellationToken).ConfigureAwait(false);
				}
			}

		}

		var elapsed = _timeProvider.GetElapsedTime(started);
		var threshold = registration.Options?.WarningThreshold
			?? _notificationOptions.ProjectionApplyWarningThreshold;

		if (elapsed > threshold)
		{
			LogProjectionApplySlow(
				registration.ProjectionType.Name,
				domainEvents.Count,
				elapsed.TotalMilliseconds,
				threshold.TotalMilliseconds);
		}
	}

	/// <inheritdoc />
	public override void Dispose()
	{
		_pageGate.Dispose();
		base.Dispose();
	}

	#region Logging

	[LoggerMessage(EventSourcingEventId.AsyncProjectionHostStarted, LogLevel.Information,
		"Async projection processing host started with {ProjectionCount} async projection(s).")]
	private partial void LogAsyncProjectionHostStarted(int projectionCount);

	[LoggerMessage(EventSourcingEventId.AsyncProjectionHostStopped, LogLevel.Information,
		"Async projection processing host stopped.")]
	private partial void LogAsyncProjectionHostStopped();

	[LoggerMessage(EventSourcingEventId.AsyncProjectionBatchProcessed, LogLevel.Debug,
		"Async projections processed batch of {EventCount} events, position now at {Position}.")]
	private partial void LogAsyncProjectionBatchProcessed(int eventCount, long position);

	/// <remarks>
	/// Warning rather than Error: nothing is broken and nothing is lost. Writes still commit; what the
	/// process cannot do is attest that every projection bound to this subscription has folded them,
	/// because some of those projections are hosted elsewhere. The expected cause is a rolling deployment
	/// that has added a projection, and it resolves when every instance runs the same registration set.
	/// It is logged rather than encoded as an outcome because the guarantee contract admits exactly two.
	/// </remarks>
	[LoggerMessage(EventSourcingEventId.UnattestableRosterMembers, LogLevel.Warning,
		"Subscription '{SubscriptionName}' is bound to projections this process does not host ({ProjectionKeys}), "
		+ "so saves it serves report their read models as behind rather than fresh. This is expected during a "
		+ "rolling deployment that adds a projection, and resolves when every instance hosts the same set.")]
	private partial void LogUnattestableRosterMembers(string subscriptionName, string projectionKeys);

	[LoggerMessage(EventSourcingEventId.ErasedEventSkipped, LogLevel.Debug,
		"Skipping erased (tombstoned) event {EventId} at global position {GlobalPosition}; advancing past it")]
	private partial void LogErasedEventSkipped(string eventId, long globalPosition);

	[LoggerMessage(EventSourcingEventId.AsyncProjectionCheckpointSaved, LogLevel.Debug,
		"Async projection checkpoint saved at position {Position}.")]
	private partial void LogAsyncProjectionCheckpointSaved(long position);

	[LoggerMessage(EventSourcingEventId.AsyncProjectionEventError, LogLevel.Error,
		"Poison event {EventId} halted the async projection host; checkpoint not advanced past it.")]
	private partial void LogAsyncProjectionEventError(string eventId, Exception ex);

	[LoggerMessage(EventSourcingEventId.AsyncProjectionApplySlow, LogLevel.Warning,
		"Applying {EventCount} event(s) to projection {ProjectionName} took {ElapsedMs}ms, " +
		"over the {ThresholdMs}ms warning threshold; session-consistent reads of it may time out.")]
	private partial void LogProjectionApplySlow(
		string projectionName, int eventCount, double elapsedMs, double thresholdMs);

	[LoggerMessage(EventSourcingEventId.AsyncProjectionDispatchError, LogLevel.Error,
		"Error dispatching events to async projection {ProjectionName}.")]
	private partial void LogAsyncProjectionDispatchError(string projectionName, Exception ex);

	[LoggerMessage(EventSourcingEventId.AsyncProjectionHostError, LogLevel.Error,
		"Async projection processing host encountered an error.")]
	private partial void LogAsyncProjectionHostError(Exception ex);

	[LoggerMessage(EventSourcingEventId.AsyncProjectionCheckpointWriteFailed, LogLevel.Error,
		"The projection checkpoint write failed. The host rewound to its last durable mark rather than "
		+ "continuing to apply events whose progress nothing durable records; the span since that mark "
		+ "will be re-applied once the checkpoint store accepts writes again.")]
	private partial void LogAsyncProjectionCheckpointWriteFailed(Exception ex);

	[LoggerMessage(EventSourcingEventId.AsyncProjectionNoGlobalStreamQuery, LogLevel.Warning,
		"No IGlobalStreamQuery registered. Async projection processing cannot start. " +
		"Ensure your event store provider (e.g., UseSqlServer) registers IGlobalStreamQuery.")]
	private partial void LogNoGlobalStreamQuery();

	#endregion Logging
}

// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Data.MongoDB.Diagnostics;
using Excalibur.Dispatch.LeaderElection;
using Excalibur.Dispatch.LeaderElection.Fencing;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using MongoDB.Bson;
using MongoDB.Driver;

namespace Excalibur.LeaderElection.MongoDB;

/// <summary>
/// MongoDB implementation of <see cref="ILeaderElection"/> using findAndModify with TTL.
/// </summary>
/// <remarks>
/// <para>
/// Uses MongoDB's findOneAndUpdate with upsert semantics and condition checks to implement
/// distributed leader election. A TTL index on the lock collection ensures stale leaders
/// are automatically cleaned up.
/// </para>
/// <para>
/// The lease renewal loop runs on a background task, periodically extending the TTL
/// while this instance holds leadership.
/// </para>
/// <para>
/// <b>Split-brain safety invariant (at-most-one-leader-EFFECT).</b> Every leader-gated side effect is
/// protected by EITHER (a) a monotonic fencing token checked at the action's authority, OR (b) a monotonic
/// self-relinquish bound at the incumbent (grace period &gt; clock skew). This provider satisfies <b>(a)</b>:
/// it is a <em>native-authority</em> election whose fencing token is the monotonic counter carried on the
/// lease document, so a stale (partitioned) incumbent's fenced writes carry a strictly lower token and are
/// rejected fail-closed at the resource. A monotonic self-relinquish at the incumbent is therefore not
/// required for correctness — safety is carried by the fencing token, not by the incumbent demoting itself
/// (adding one would make <c>IsLeader</c> honest during a partition: a defense-in-depth liveness improvement,
/// not a safety fix).
/// </para>
/// </remarks>
public sealed partial class MongoDbLeaderElection : ILeaderElection, IAsyncDisposable
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2213:Disposable fields should be disposed", Justification = "Owned field is disposed by memoized DisposeCoreAsync after producer seal and join.")]

    private readonly CancellationTokenSource _lifetimeStop = new();
    private readonly Lock _lifecycleGate = new();
    private readonly LeaderElectionProducerLifetime _producers = new();
    private readonly LeaderElectionNotifications _notifications;
    private Task? _disposalTask;
    private Task? _startTask;
    private Task? _stopTask;

    /// <inheritdoc />
    public Task NotificationsCompleted => _notifications.NotificationsCompleted;

    private async Task RunProducerAsync(Func<Task> action)
    {
        if (!_producers.TryEnter(out var lease)) { throw new ObjectDisposedException(GetType().Name); }
        using (lease) { await LeaderElectionExecution.RunAsync(action).ConfigureAwait(false); }
    }

    private void QueueEvent<T>(EventHandler<T>? subscribers, T args) where T : EventArgs
    {
        if (subscribers is null) { return; }
        _ = _notifications.Enqueue(() => _notifications.InvokeEach(subscribers, this, args));
    }

    private async Task WaitForStartAsync(Task start, CancellationToken cancellationToken)
    {
        try { await start.WaitAsync(cancellationToken).ConfigureAwait(false); }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Retain shutdown ownership when a canceled waiter leaves admitted startup work behind.
            try { _ = StopAsync(CancellationToken.None); }
            catch (ObjectDisposedException) when (_disposed) { }
            throw;
        }
    }

    private static void ThrowFailures(List<Exception> failures)
    {
        if (failures.Count == 1) { System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failures[0]).Throw(); }
        if (failures.Count > 1) { throw new AggregateException("Leader election resource cleanup failed.", failures); }
    }

	private readonly TimeProvider _timeProvider;
	private readonly IMongoCollection<MongoDbLeaderElectionDocument> _collection;
	private readonly string _resourceName;
	private readonly MongoDbLeaderElectionOptions _options;
	private readonly LeaderElectionOptions _electionOptions;
	private readonly ILogger<MongoDbLeaderElection> _logger;

	// Optional monotonic fencing token provider. Null when the consumer did not enable fencing (opt-in,
	// backward compatible). When supplied, a token is minted on acquisition BEFORE leadership is declared;
	// if the token domain is exhausted the candidate relinquishes rather than leading with an un-advanced fence.
	private readonly IFencingTokenIssuer? _fencingTokenProvider;

	// The fencing token minted for the current leadership term, carried on the LeaderChanged event so
	// subscribers capture the exact token issued at this transition (no TOCTOU re-read). Null when no
	// provider is configured or leadership is not held.
	// Sentinel for "no current fencing token" — a real monotonic token is never long.MinValue. The field is
	// accessed via Interlocked from both the acquire/renew flow and the LeaderChanged event raise, so a plain
	// long? (non-atomic, no memory barrier) could tear or read stale across those threads.
	private const long NoFencingToken = long.MinValue;
	private long _currentFencingToken = NoFencingToken;

	private readonly Lock _lock = new();
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2213:Disposable fields should be disposed", Justification = "Owned field is disposed by memoized DisposeCoreAsync after producer seal and join.")]

	private CancellationTokenSource? _renewalCts;
	private Task? _renewalTask;
	private bool _isStarted;
	private volatile bool _isLeader;
	private string? _currentLeaderId;
	private volatile bool _disposed;
	// the instant the current leadership tenure began, read together with _currentFencingToken
	// (and _isLeader) under _lock via CurrentLeadership.
	private DateTimeOffset _leadershipAcquiredAt;

	/// <summary>
	/// Initializes a new instance of the <see cref="MongoDbLeaderElection"/> class.
	/// </summary>
	/// <remarks>
	/// Every constructor parameter is either an injected collaborator (<paramref name="client"/>,
	/// <paramref name="logger"/>, the optional <paramref name="fencingTokenProvider"/>), an identity value
	/// (<paramref name="resourceName"/>), or a strongly-typed options bundle already following the
	/// <see cref="IOptions{TOptions}"/> configuration pattern (<paramref name="mongoOptions"/>,
	/// <paramref name="electionOptions"/>). The two distinct options types configure independent concerns —
	/// MongoDB storage versus provider-agnostic election timing — and are deliberately kept separate rather
	/// than merged, so the modest parameter count reflects genuine, non-collapsible dependencies.
	/// </remarks>
	/// <param name="client">The MongoDB client.</param>
	/// <param name="resourceName">The resource name for the election lock.</param>
	/// <param name="mongoOptions">The MongoDB leader election options.</param>
	/// <param name="electionOptions">The leader election options.</param>
	/// <param name="logger">The logger instance.</param>
	/// <param name="fencingTokenProvider">
	/// Optional monotonic fencing token provider. When supplied, a token is minted on acquisition before
	/// leadership is declared; if the token domain is exhausted the candidate relinquishes (fail-closed).
	/// </param>
	/// <param name="timeProvider">
	/// Optional time provider used for event timestamps. Defaults to <see cref="TimeProvider.System"/>.
	/// Inject a controllable provider to make emitted timestamps deterministic in tests.
	/// </param>
	public MongoDbLeaderElection(
		IMongoClient client,
		string resourceName,
		IOptions<MongoDbLeaderElectionOptions> mongoOptions,
		IOptions<LeaderElectionOptions> electionOptions,
		ILogger<MongoDbLeaderElection> logger,
		IFencingTokenIssuer? fencingTokenProvider = null,
		TimeProvider? timeProvider = null)
	{
		ArgumentNullException.ThrowIfNull(client);
		ArgumentException.ThrowIfNullOrWhiteSpace(resourceName);
		ArgumentNullException.ThrowIfNull(mongoOptions);
		ArgumentNullException.ThrowIfNull(electionOptions);
		ArgumentNullException.ThrowIfNull(logger);

		// durable fencing is the DEFAULT for framework-protected resources. When no provider
		// is supplied, fall back to the durable per-resource counter (a SEPARATE, TTL-free collection) rather
		// than the store-arbitrated token that lives in the lock document — the lock document is destroyed by
		// ReleaseLockAsync's DeleteOne and by the ttl_expiresAt TTL index, which would reset that token to 1 on
		// a graceful restart and let a zombie's stale token validate as current (split-brain, D2≡L5). Defaulting
		// here (not only in DI) makes the reset-to-1 branch below (`_fencingTokenProvider is null`) unreachable
		// by construction on every path; a consumer that supplies its own provider still overrides.
		_fencingTokenProvider = fencingTokenProvider ?? new MongoDbFencingTokenProvider(client, mongoOptions);
		_resourceName = resourceName;
		_options = mongoOptions.Value;
		_options.Validate();
		_electionOptions = electionOptions.Value;
		_logger = logger;
        _notifications = new LeaderElectionNotifications(LogElectionError);
		_timeProvider = timeProvider ?? TimeProvider.System;

		CandidateId = Excalibur.Dispatch.LeaderElection.LeaderCandidateIdentity.Resolve(_electionOptions.InstanceId);

		var database = client.GetDatabase(_options.DatabaseName);
		_collection = database.GetCollection<MongoDbLeaderElectionDocument>(_options.CollectionName);
	}

	/// <inheritdoc/>
	public event EventHandler<LeaderElectionEventArgs>? BecameLeader;

	/// <inheritdoc/>
	public event EventHandler<LeaderElectionEventArgs>? LostLeadership;

	/// <inheritdoc/>
	public event EventHandler<LeaderChangedEventArgs>? LeaderChanged;

	/// <inheritdoc/>
	public event EventHandler<LeaderElectionAcquisitionFailedEventArgs>? AcquisitionFailed;

	/// <inheritdoc/>
	public string CandidateId { get; }

	/// <inheritdoc/>
	public bool IsLeader => !_disposed && _isLeader;

	/// <inheritdoc/>
	public Leadership? CurrentLeadership
	{
		get
		{
			lock (_lock)
			{
				if (_disposed || !_isLeader)
				{
					return null;
				}

				var token = Interlocked.Read(ref _currentFencingToken);
				return new Leadership(token == NoFencingToken ? null : token, _leadershipAcquiredAt);
			}
		}
	}

	/// <inheritdoc/>
	public string? CurrentLeaderId
	{
		get
		{
			lock (_lock)
			{
				return _currentLeaderId;
			}
		}
	}

	/// <inheritdoc/>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _notifications.ThrowIfReentrant();
        Task start;
        lock (_lifecycleGate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_isStarted) { return WaitForStartAsync(_startTask ?? Task.CompletedTask, cancellationToken); }
            if (_stopTask is { IsCompletedSuccessfully: false }) { throw new InvalidOperationException("Previous stop has not completed successfully."); }
            _isStarted = true;
            _stopTask = null;
            _renewalCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetimeStop.Token);
            _startTask = start = RunProducerAsync(() => StartCoreAsync(_renewalCts.Token));
        }
        return WaitForStartAsync(start, cancellationToken);
    }

    private async Task StartCoreAsync(CancellationToken cancellationToken)
    {
        await _notifications.DrainTask.WaitAsync(cancellationToken).ConfigureAwait(false);
        await EnsureTtlIndexAsync(cancellationToken).ConfigureAwait(false);
        if (_disposed || !_isStarted) { return; }
        cancellationToken.ThrowIfCancellationRequested();
        await RunElectionStepAsync(cancellationToken).ConfigureAwait(false);
        lock (_lifecycleGate)
        {
            if (!_disposed && _isStarted)
            {
                _renewalTask = RunProducerAsync(() => RunElectionLoopAsync(_renewalCts!.Token));
            }
        }
        _notifications.EnqueueDiagnostic(LogStarted, _resourceName, CandidateId);
    }

	/// <inheritdoc/>
    public Task StopAsync(CancellationToken cancellationToken)
    {
        Task stop;
        lock (_lifecycleGate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_stopTask is not null) { return _stopTask.WaitAsync(cancellationToken); }
            if (!_isStarted) { return Task.CompletedTask; }
            _isStarted = false;
            _stopTask = stop = RunProducerAsync(StopCoreAsync);
        }
        return stop.WaitAsync(cancellationToken);
    }

	/// <summary>
	/// The stop sequence, without the disposed guard.
	/// </summary>
	/// <remarks>
	/// Disposal must run this as well, and it runs it after marking the instance disposed, so it
	/// cannot go through the public method: that method's first act is to reject a disposed instance,
	/// and disposal's own catch was swallowing the rejection. The election loop was then never
	/// cancelled and never awaited, so it kept renewing the lock for the life of the process. Keeping
	/// the guard on the public entry point and the work in here means disposal cannot be refused by
	/// the guard that exists to protect callers from it.
	/// </remarks>
    private async Task StopCoreAsync()
    {
        var failures = new List<Exception>();
        var source = _renewalCts;
        if (source is not null)
        {
            try { await source.CancelAsync().ConfigureAwait(false); }
            catch (Exception error) { failures.Add(error); }
        }
        foreach (var task in new[] { _startTask, _renewalTask })
        {
            if (task is null) { continue; }
            try { await task.ConfigureAwait(false); }
            catch (OperationCanceledException) when (source?.IsCancellationRequested == true) { }
            catch (Exception error) { failures.Add(error); }
        }
        if (_isLeader)
        {
            try { await ReleaseLockAsync().ConfigureAwait(false); }
            catch (Exception error) { failures.Add(error); }
            finally { SetLeader(false, null); }
        }
        lock (_lifecycleGate)
        {
            if (ReferenceEquals(_renewalCts, source)) { _renewalCts = null; }
        }
        try { source?.Dispose(); }
        catch (Exception error) { failures.Add(error); }
        _notifications.EnqueueDiagnostic(LogStopped, _resourceName, CandidateId);
        ThrowFailures(failures);
    }

	/// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        lock (_lifecycleGate)
        {
            if (_disposalTask is not null) { return new ValueTask(_disposalTask); }
            _disposed = true;
            _isStarted = false;
            _disposalTask = LeaderElectionExecution.RunAsync(DisposeCoreAsync);
            return new ValueTask(_disposalTask);
        }
    }

    private async Task DisposeCoreAsync()
    {
        var failures = new List<Exception>();
        var producers = _producers.Seal();
        try
        {
            try { await _lifetimeStop.CancelAsync().ConfigureAwait(false); }
            catch (Exception error) { failures.Add(error); }
            try { await producers.ConfigureAwait(false); }
            catch (Exception error) { failures.Add(error); }
            if (_isLeader)
            {
                try { await ReleaseLockAsync().ConfigureAwait(false); }
                catch (Exception error) { failures.Add(error); }
                finally { SetLeader(false, null); }
            }
            CancellationTokenSource? remainingRunSource;
            lock (_lifecycleGate)
            {
                remainingRunSource = _renewalCts;
                _renewalCts = null;
            }
            try { remainingRunSource?.Dispose(); }
            catch (Exception error) { failures.Add(error); }
            try { _lifetimeStop.Dispose(); }
            catch (Exception error) { failures.Add(error); }
        }
        finally { _notifications.Seal(); }
        ThrowFailures(failures);
    }

	/// <summary>
	/// Performs one election step: renew the lease while leading, otherwise attempt to acquire it.
	/// </summary>
	/// <remarks>
	/// Extracted from the election loop so <see cref="StartAsync" /> can await a single attempt before it
	/// returns. Errors are absorbed here rather than propagated: a step run from <see cref="StartAsync" />
	/// must not turn a transient acquisition failure into a start failure, because losing the race is the
	/// ordinary outcome for a follower. Cancellation still propagates.
	/// </remarks>
	private async Task RunElectionStepAsync(CancellationToken cancellationToken)
	{
		try
		{
			if (_isLeader)
			{
				var renewed = await TryRenewLockAsync(cancellationToken).ConfigureAwait(false);
				if (!renewed)
				{
					SetLeader(false, null);
				}

				return;
			}

			await _notifications.DrainTask.WaitAsync(cancellationToken).ConfigureAwait(false);
            if (_disposed || !_isStarted) { return; }
			var acquired = await TryAcquireLockAsync(cancellationToken).ConfigureAwait(false);
			if (acquired)
			{
				// Mint the fencing token BEFORE declaring leadership so the fence is always advanced
				// before this candidate acts as leader. On ANY mint failure, relinquish: release the lock
				// document this candidate just took, then report. The cause is deliberately not
				// discriminated — a candidate that cannot fence its writes must not lead, and one that
				// also keeps the lock leaves the resource with no leader and no successor until the TTL
				// index reaps the row.
				var mintFailure = await TryMintFencingTokenAsync(cancellationToken).ConfigureAwait(false);
				if (mintFailure is null)
				{
                    var published = false;
                    lock (_lifecycleGate)
                    {
                        if (!_disposed && _isStarted && !cancellationToken.IsCancellationRequested)
                        {
                            SetLeader(true, CandidateId);
                            published = true;
                        }
                    }
                    if (!published) { await ReleaseLockAsync().ConfigureAwait(false); }
				}
				else
				{
					await ReleaseLockAsync().ConfigureAwait(false);
					RaiseAcquisitionFailed("fencing token mint failed", mintFailure);
				}

				return;
			}

			// Check who the current leader is
			var currentLeader = await GetCurrentLeaderAsync(cancellationToken).ConfigureAwait(false);
			UpdateCurrentLeader(currentLeader);
			RaiseAcquisitionFailed("lost the acquisition race", exception: null);
		}
		catch (OperationCanceledException ex) when (ex.CancellationToken.IsCancellationRequested)
		{
			throw;
		}
		catch (Exception ex)
		{
			_notifications.EnqueueDiagnostic(LogElectionError, ex);

			// An error while attempting to acquire (not while holding leadership) is a failed acquisition.
			if (!_isLeader)
			{
				RaiseAcquisitionFailed("error during acquisition", ex);
			}
		}
	}

	private async Task RunElectionLoopAsync(CancellationToken cancellationToken)
	{
		while (!cancellationToken.IsCancellationRequested)
		{
			try
			{
				// The delay leads the step because StartAsync has already performed the first attempt;
				// stepping immediately here would duplicate it.
				var interval = _isLeader
					? TimeSpan.FromSeconds(_options.RenewIntervalSeconds)
					: _electionOptions.RetryInterval;

				await Task.Delay(interval, _timeProvider, cancellationToken).ConfigureAwait(false);

				await RunElectionStepAsync(cancellationToken).ConfigureAwait(false);
			}
			catch (OperationCanceledException ex) when (ex.CancellationToken.IsCancellationRequested)
			{
				break;
			}
		}
	}

	/// <summary>
	/// Mints a monotonic fencing token on acquisition (when a provider is configured), returning
	/// <see langword="null"/> if leadership may proceed and otherwise the failure that forbids it — the
	/// fail-closed relinquish signal. The caller must release the lock it took before reporting.
	/// </summary>
	/// <remarks>
	/// Every mint failure answers the same way, whatever its cause. A superseded version of this contract
	/// relinquished only on <see cref="FencingTokenExhaustedException"/> and reasoned that "a transient
	/// mint error propagates to the election loop's handler; the lock lease expires on its own, so
	/// leadership is never declared without an advanced fence." Both halves of that are true and the
	/// conclusion does not follow: never declaring leadership is the safety half, while the lock stayed
	/// held by a candidate that was not leading, so no other candidate could acquire until the lease
	/// expired. Returning the failure rather than a flag keeps the cause available to a subscriber, which
	/// a boolean cannot carry.
	/// </remarks>
	private async Task<Exception?> TryMintFencingTokenAsync(CancellationToken cancellationToken)
	{
		if (_fencingTokenProvider is null)
		{
			return null;
		}

		try
		{
			var token = await _fencingTokenProvider.IssueTokenAsync(_resourceName, cancellationToken).ConfigureAwait(false);
			_ = Interlocked.Exchange(ref _currentFencingToken, token);
			_notifications.EnqueueDiagnostic(LogFencingTokenIssued, CandidateId, token, _resourceName);
			return null;
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
			// Shutdown, not a mint failure: the stop path owns the release.
			throw;
		}
		catch (Exception ex)
		{
			_notifications.EnqueueDiagnostic(LogFencingTokenIssueError, ex, CandidateId, _resourceName);
			return ex;
		}
	}

	/// <summary>
	/// Raises <see cref="AcquisitionFailed"/>, guarding the invocation so a throwing subscriber
	/// can never break the acquisition/renewal loop.
	/// </summary>
	private void RaiseAcquisitionFailed(string reason, Exception? exception)
	{
		try
		{
			QueueEvent(AcquisitionFailed, new LeaderElectionAcquisitionFailedEventArgs(CandidateId, _resourceName, reason, _timeProvider.GetUtcNow(), exception));
		}
		catch (Exception)
		{
			// A throwing subscriber must never break the acquire loop.
		}
	}

	// Sentinel default for a missing/absent recorded expiry (first creation via upsert): the Unix epoch is
	// always "expired" relative to $$NOW, so a brand-new document is immediately eligible for takeover.
	private static readonly BsonValue s_absentExpiryDefault = new BsonDateTime(0);

	/// <summary>
	/// Atomically acquires (or self-renews) the leadership lock using a single server-clock-only
	/// aggregation-pipeline <c>findOneAndUpdate</c>. Every takeover comparison is evaluated by the MongoDB
	/// server against <c>$$NOW</c> — the candidate's local wall clock never participates in the decision, so
	/// a clock-skewed candidate cannot judge a still-valid incumbent lease as expired.
	/// </summary>
	/// <remarks>
	/// Takeover is permitted, server-side, iff the caller is the current holder (self-renew) OR the
	/// recorded lease has been expired for at least <see cref="MongoDbLeaderElectionOptions.TakeoverGraceSeconds"/>
	/// beyond its own expiry — the grace window absorbs the incumbent's own renewal jitter and network
	/// delay, not inter-node clock skew, since the comparison itself never touches a local clock. When
	/// takeover proceeds against a <em>different</em> prior holder, the store-arbitrated <c>fencingToken</c>
	/// strictly increases (computed server-side, equivalent to an atomic increment); a self-renewal leaves
	/// it unchanged. The whole decision, the token increment, and the lease write happen in one atomic
	/// document operation — no read-decide-write race window exists between candidates.
	/// </remarks>
	private async Task<bool> TryAcquireLockAsync(CancellationToken cancellationToken)
	{
		var leaseMs = (long)TimeSpan.FromSeconds(_options.LeaseDurationSeconds).TotalMilliseconds;
		var graceMs = (long)TimeSpan.FromSeconds(_options.TakeoverGraceSeconds).TotalMilliseconds;

		// NOTE: do NOT $set _id here. On an upsert-INSERT MongoDB derives _id from the filter's
		// equality (ResourceName == _id), and $set-ting the immutable _id during that insert fails the
		// operation ("would modify the immutable field '_id'") — which silently blocks a FRESH candidate
		// from acquiring an uncontested resource. The filter already pins _id; setting it is redundant.
		// the takeover decision (`isSelf`/`leaseExpired`/`takeover`) and every output field must be
		// computed in ONE atomic stage, but MongoDB `$set`/`$addFields` evaluate all sibling expressions
		// against the document AS IT ENTERS the stage — a field set in the same stage is NOT visible to its
		// siblings. Computing `_takeover = $or($_isSelf, $_leaseExpired)` (and the outputs from `$_takeover`)
		// in one `$set` therefore read the intermediates as MISSING → takeover never fired → a fresh/expired
		// candidate could never acquire. Fix: bind the intermediates with `$let` (visible only in its `in`).
		// `$let` variables cannot reference each other within one `vars` block, so `takeover` (which depends
		// on `isSelf`/`leaseExpired`) is bound by a NESTED `$let` inside the outer `in`. `$mergeObjects` over
		// `$$ROOT` applies the computed fields in one document rewrite; `_id` is carried from `$$ROOT` and
		// never written (setting the immutable `_id` on an upsert-INSERT fails and blocks a fresh acquire).
		var replaceStage = new BsonDocument("$replaceWith", new BsonDocument("$let", new BsonDocument
		{
			["vars"] = new BsonDocument
			{
				["isSelf"] = new BsonDocument("$eq", new BsonArray { "$candidateId", CandidateId }),
				["leaseExpired"] = new BsonDocument("$lte", new BsonArray
				{
					new BsonDocument("$add", new BsonArray
					{
						new BsonDocument("$ifNull", new BsonArray { "$expiresAt", s_absentExpiryDefault }), graceMs
					}),
					"$$NOW"
				})
			},
			["in"] = new BsonDocument("$let", new BsonDocument
			{
				["vars"] = new BsonDocument
				{
					["takeover"] = new BsonDocument("$or", new BsonArray { "$$isSelf", "$$leaseExpired" })
				},
				["in"] = new BsonDocument("$mergeObjects", new BsonArray
				{
					"$$ROOT",
					new BsonDocument
					{
						["fencingToken"] = new BsonDocument("$cond", new BsonDocument
						{
							["if"] = "$$isSelf",
							["then"] = new BsonDocument("$ifNull", new BsonArray { "$fencingToken", 0L }),
							["else"] = new BsonDocument("$cond", new BsonDocument
							{
								["if"] = "$$takeover",
								["then"] = new BsonDocument("$add", new BsonArray
								{
									new BsonDocument("$ifNull", new BsonArray { "$fencingToken", 0L }), 1L
								}),
								["else"] = "$fencingToken"
							})
						}),
						["candidateId"] = new BsonDocument("$cond", new BsonDocument
						{
							["if"] = "$$takeover", ["then"] = CandidateId, ["else"] = "$candidateId"
						}),
						["acquiredAt"] = new BsonDocument("$cond", new BsonDocument
						{
							["if"] = "$$isSelf",
							["then"] = new BsonDocument("$ifNull", new BsonArray { "$acquiredAt", "$$NOW" }),
							["else"] = new BsonDocument("$cond", new BsonDocument
							{
								["if"] = "$$takeover", ["then"] = "$$NOW", ["else"] = "$acquiredAt"
							})
						}),
						["expiresAt"] = new BsonDocument("$cond", new BsonDocument
						{
							["if"] = "$$takeover",
							["then"] = new BsonDocument("$add", new BsonArray { "$$NOW", leaseMs }),
							["else"] = "$expiresAt"
						}),
						["lastRenewedAt"] = new BsonDocument("$cond", new BsonDocument
						{
							["if"] = "$$takeover", ["then"] = "$$NOW", ["else"] = "$lastRenewedAt"
						})
					}
				})
			})
		}));

		PipelineDefinition<MongoDbLeaderElectionDocument, MongoDbLeaderElectionDocument> pipeline =
			new[] { replaceStage };

		var filter = Builders<MongoDbLeaderElectionDocument>.Filter.Eq(d => d.ResourceName, _resourceName);

		var options = new FindOneAndUpdateOptions<MongoDbLeaderElectionDocument>
		{
			IsUpsert = true,
			ReturnDocument = ReturnDocument.After
		};

		try
		{
			var result = await _collection
				.FindOneAndUpdateAsync(filter, Builders<MongoDbLeaderElectionDocument>.Update.Pipeline(pipeline), options, cancellationToken)
				.ConfigureAwait(false);

			if (result?.CandidateId != CandidateId)
			{
				return false;
			}

			// Prefer the store-arbitrated term as this tenure's fencing token. When an external
			// IFencingTokenIssuer is configured, TryMintFencingTokenAsync (invoked by the caller
			// immediately after this method returns true, before SetLeader) supersedes this value with its
			// own fail-closed mint, preserving the pre-existing domain-exhaustion relinquish behavior for
			// that opt-in path.
			if (_fencingTokenProvider is null)
			{
				_ = Interlocked.Exchange(ref _currentFencingToken, result.FencingToken);
			}

			return true;
		}
		catch (MongoCommandException ex) when (ex.Code == 11000)
		{
			// Duplicate key: another candidate won the concurrent upsert race.
			return false;
		}
	}

	/// <summary>
	/// Atomically extends the caller's own lease using the MongoDB server clock for both the new expiry
	/// and the renewal timestamp. The filter already restricts the match to documents this candidate holds,
	/// so no takeover decision is needed here — only the lease-extension arithmetic must avoid the local
	/// clock.
	/// </summary>
	private async Task<bool> TryRenewLockAsync(CancellationToken cancellationToken)
	{
		var leaseMs = (long)TimeSpan.FromSeconds(_options.LeaseDurationSeconds).TotalMilliseconds;

		var filter = Builders<MongoDbLeaderElectionDocument>.Filter.And(
			Builders<MongoDbLeaderElectionDocument>.Filter.Eq(d => d.ResourceName, _resourceName),
			Builders<MongoDbLeaderElectionDocument>.Filter.Eq(d => d.CandidateId, CandidateId));

		var renewStage = new BsonDocument("$set", new BsonDocument
		{
			["expiresAt"] = new BsonDocument("$add", new BsonArray { "$$NOW", leaseMs }), ["lastRenewedAt"] = "$$NOW"
		});

		PipelineDefinition<MongoDbLeaderElectionDocument, MongoDbLeaderElectionDocument> pipeline = new[] { renewStage };

		var result = await _collection
			.UpdateOneAsync(filter, Builders<MongoDbLeaderElectionDocument>.Update.Pipeline(pipeline), cancellationToken: cancellationToken)
			.ConfigureAwait(false);

		return result.MatchedCount > 0;
	}

	private async Task ReleaseLockAsync()
	{
		var filter = Builders<MongoDbLeaderElectionDocument>.Filter.And(
			Builders<MongoDbLeaderElectionDocument>.Filter.Eq(d => d.ResourceName, _resourceName),
			Builders<MongoDbLeaderElectionDocument>.Filter.Eq(d => d.CandidateId, CandidateId));

		_ = await _collection.DeleteOneAsync(filter).ConfigureAwait(false);
	}

	/// <summary>
	/// Reads the current lock holder, judged against the MongoDB server clock (never the caller's local
	/// clock) via an <c>$expr</c> match on <c>$$NOW</c> — kept consistent with the server-clock-only
	/// takeover decision in <see cref="TryAcquireLockAsync"/>.
	/// </summary>
	private async Task<string?> GetCurrentLeaderAsync(CancellationToken cancellationToken)
	{
		var filter = Builders<MongoDbLeaderElectionDocument>.Filter.And(
			Builders<MongoDbLeaderElectionDocument>.Filter.Eq(d => d.ResourceName, _resourceName),
			new BsonDocument("$expr", new BsonDocument("$gte", new BsonArray { "$expiresAt", "$$NOW" })));

		var doc = await _collection.Find(filter).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
		return doc?.CandidateId;
	}

	private void SetLeader(bool isLeader, string? leaderId)
	{
		var wasLeader = _isLeader;
		string? previousLeaderId;

		lock (_lock)
		{
			previousLeaderId = _currentLeaderId;
			_isLeader = isLeader;
			_currentLeaderId = leaderId;
			if (isLeader && !wasLeader)
			{
				_leadershipAcquiredAt = _timeProvider.GetUtcNow();
			}
		}

		if (isLeader && !wasLeader)
		{
			QueueEvent(BecameLeader, new LeaderElectionEventArgs(CandidateId, _resourceName, timestamp: _timeProvider.GetUtcNow()));
			var fencingToken = Interlocked.Read(ref _currentFencingToken);
			QueueEvent(LeaderChanged, new LeaderChangedEventArgs(previousLeaderId, CandidateId, _resourceName, fencingToken == NoFencingToken ? null : fencingToken, timestamp: _timeProvider.GetUtcNow()));
			_notifications.EnqueueDiagnostic(LogBecameLeader, _resourceName, CandidateId);
		}
		else if (!isLeader && wasLeader)
		{
			_ = Interlocked.Exchange(ref _currentFencingToken, NoFencingToken);
			QueueEvent(LostLeadership, new LeaderElectionEventArgs(CandidateId, _resourceName, timestamp: _timeProvider.GetUtcNow()));
			QueueEvent(LeaderChanged, new LeaderChangedEventArgs(CandidateId, leaderId, _resourceName, timestamp: _timeProvider.GetUtcNow()));
			_notifications.EnqueueDiagnostic(LogLostLeadership, _resourceName, CandidateId);
		}
	}

	private void UpdateCurrentLeader(string? leaderId)
	{
		string? previousLeaderId;
		lock (_lock)
		{
			previousLeaderId = _currentLeaderId;
			if (previousLeaderId == leaderId)
			{
				return;
			}

			_currentLeaderId = leaderId;
		}

		QueueEvent(LeaderChanged, new LeaderChangedEventArgs(previousLeaderId, leaderId, _resourceName, timestamp: _timeProvider.GetUtcNow()));
	}

    private Task EnsureTtlIndexAsync(CancellationToken cancellationToken) =>
        MongoDbLeaderElectionSchema.EnsureTtlIndexAsync(_collection, cancellationToken);

	[LoggerMessage(DataMongoDbEventId.LeaderElectionStarted, LogLevel.Information,
		"MongoDB leader election started for resource '{ResourceName}' with candidate '{CandidateId}'")]
	private partial void LogStarted(string resourceName, string candidateId);

	[LoggerMessage(DataMongoDbEventId.LeaderElectionStopped, LogLevel.Information,
		"MongoDB leader election stopped for resource '{ResourceName}' with candidate '{CandidateId}'")]
	private partial void LogStopped(string resourceName, string candidateId);

	[LoggerMessage(DataMongoDbEventId.LeaderElectionBecameLeader, LogLevel.Information,
		"Candidate became leader for resource '{ResourceName}' with candidate '{CandidateId}'")]
	private partial void LogBecameLeader(string resourceName, string candidateId);

	[LoggerMessage(DataMongoDbEventId.LeaderElectionLostLeadership, LogLevel.Warning,
		"Candidate lost leadership for resource '{ResourceName}' with candidate '{CandidateId}'")]
	private partial void LogLostLeadership(string resourceName, string candidateId);

	[LoggerMessage(DataMongoDbEventId.LeaderElectionError, LogLevel.Error, "Error in leader election loop")]
	private partial void LogElectionError(Exception exception);

	[LoggerMessage(DataMongoDbEventId.LeaderElectionFencingTokenIssued, LogLevel.Information,
		"Candidate {CandidateId} minted fencing token {Token} for resource {Resource}")]
	private partial void LogFencingTokenIssued(string candidateId, long token, string resource);

	[LoggerMessage(DataMongoDbEventId.LeaderElectionFencingTokenMintFailed, LogLevel.Critical,
		"Candidate {CandidateId} could not mint a fencing token for resource {Resource}; releasing the lock and relinquishing leadership (fail-closed) rather than leading with an un-advanced fence")]
	private partial void LogFencingTokenIssueError(Exception exception, string candidateId, string resource);

	[LoggerMessage(DataMongoDbEventId.LeaderElectionDisposeError, LogLevel.Warning, "Error during leader election dispose")]
	private partial void LogDisposeError(Exception exception);
}

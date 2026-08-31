// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;

using Excalibur.Inbox.Observability;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Inbox.InMemory;

/// <summary>
/// In-memory implementation of <see cref="IInboxStore"/> for testing and development.
/// </summary>
/// <remarks>
/// <para>
/// Messages are keyed by a composite of (Tenant, MessageId, HandlerType), allowing the same message
/// to be processed independently by multiple handlers, and two tenants to present distinct messages
/// under one message id without colliding. In a single-tenant deployment the tenant term is the
/// reserved untenanted marker, so the key shape does not vary by deployment.
/// </para>
/// <para>
/// Every operation on an entry — claiming it, marking it, releasing it, evicting it, and reading it back
/// — is serialised on one lock. The property that has to hold is that at most one caller is ever told it
/// holds a given key, and that no caller's write replaces or removes a record another caller is entitled
/// to: a live claim, or a processed marker. That is a statement about all the mutators together, not
/// about any one of them, so a per-operation atomicity that only holds against the same operation running
/// twice cannot establish it.
/// </para>
/// <para>
/// This store is intended for testing scenarios only. Data is lost on application restart.
/// </para>
/// </remarks>
internal sealed class InMemoryInboxStore : IInboxStore, IProcessingTrackingInboxStore, IClaimableInboxStore, ILeasedInboxStore, IInboxStoreAdmin, IAsyncDisposable, IDisposable
{
	// One lock over both maps below, taken by EVERY method that reads or writes them. Not one per claim
	// path: an entry has six mutators, so a lock held only by the lease claim serialises that claim
	// against copies of itself and against nothing else. Its read-decide-write then straddles another
	// path's write, both callers are told they hold the message, and the second write destroys the first
	// caller's record.
	//
	// Plain dictionaries rather than concurrent ones, deliberately. The transitions here span two maps and
	// several statements, so no per-operation atomicity a collection could offer is sufficient; a
	// concurrent collection would advertise one that is not relied upon and would invite the next writer
	// to reach for it and reopen exactly this hole. With plain dictionaries the lock is the only way to
	// touch the state at all.
	//
	// The cost is that every operation serialises, reads included. This store exists for tests and local
	// development, where correctness of the claim is the whole point and throughput under contention is
	// not; a striped or per-entry lock would scale further and is not worth the reasoning it would cost.
	private readonly System.Threading.Lock _stateLock = new();
	private readonly Dictionary<InboxKey, InboxEntry> _entries = [];

	// Companion lease-expiry map (unix-ms) for the lease-based claim overload. A single-process store has
	// no distributed clock skew, so the local wall clock is the authority here. Absence of a key here means
	// "claim carries no expiry", never "expired" — see the reclaim guard in the lease overload.
	private readonly Dictionary<InboxKey, long> _leaseExpiryUnixMs = [];
	private readonly InMemoryInboxOptions _options;
	private readonly ITenantContext _tenantContext;
	private readonly ILogger<InMemoryInboxStore> _logger;
	private readonly TimeProvider _timeProvider;
	private readonly Timer? _cleanupTimer;
	private volatile bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="InMemoryInboxStore"/> class.
	/// </summary>
	/// <param name="options">The configuration options.</param>
	/// <param name="tenantContext">
	/// Resolves the tenant each operation addresses. Consulted per call rather than captured, because one
	/// registered store serves every caller and the tenant belongs to the operation. Its resolved value
	/// becomes part of the deduplication key, so two tenants presenting distinct messages under one
	/// message id do not collide.
	/// <para>
	/// Optional because tenant isolation is opt-in: a single-tenant deployment has no tenant context, and
	/// omitting one here resolves every operation to the reserved untenanted partition — an explicit term,
	/// not an absent one. The dependency-injection registration always supplies the host's context, so a
	/// multi-tenant deployment never reaches this default. It is not a licence to skip tenancy: when
	/// multi-tenancy IS active and no tenant is resolved, the scope conversion fails closed rather than
	/// producing a key with no tenant term.
	/// </para>
	/// </param>
	/// <param name="logger">The logger instance.</param>
	/// <param name="timeProvider">
	/// Optional time provider used for lease-expiry and entry timestamps. Defaults to
	/// <see cref="TimeProvider.System"/>. Inject a controllable provider to make lease expiry
	/// deterministic in tests.
	/// </param>
	public InMemoryInboxStore(
		IOptions<InMemoryInboxOptions> options,
		ILogger<InMemoryInboxStore> logger,
		ITenantContext tenantContext,
		TimeProvider? timeProvider = null)
	{
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(logger);
		ArgumentNullException.ThrowIfNull(tenantContext);

		_options = options.Value;
		_tenantContext = tenantContext;
		_logger = logger;
		_timeProvider = timeProvider ?? TimeProvider.System;

		// Only start the cleanup timer when EnableAutomaticCleanup is true
		if (_options.EnableAutomaticCleanup)
		{
			_cleanupTimer = new Timer(
				_ => PerformScheduledCleanup(),
				state: null,
				_options.CleanupInterval,
				_options.CleanupInterval);
		}
	}

	/// <inheritdoc/>
	public ValueTask<InboxEntry> CreateEntryAsync(
		string messageId,
		string handlerType,
		string messageType,
		byte[] payload,
		IDictionary<string, object> metadata,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
		ArgumentException.ThrowIfNullOrWhiteSpace(handlerType);
		ArgumentException.ThrowIfNullOrWhiteSpace(messageType);
		ArgumentNullException.ThrowIfNull(payload);
		ArgumentNullException.ThrowIfNull(metadata);
		ObjectDisposedException.ThrowIf(_disposed, this);

		using var activity = InboxActivitySource.StartCreateEntryActivity(messageId, handlerType);

		var key = GetKey(messageId, handlerType);

		// Stamped so the entry carries its own partition. Eviction scans across tenants and must rebuild
		// this entry's key from ITS tenant, not the ambient one.
		var entry = new InboxEntry(messageId, handlerType, messageType, payload, metadata)
		{
			TenantId = CurrentTenantScope.TenantId,
		};

		lock (_stateLock)
		{
			// Enforce capacity limits before attempting to add
			if (_options.MaxEntries > 0 && _entries.Count >= _options.MaxEntries)
			{
				EvictOldestEntry();
			}

			if (!_entries.TryAdd(key, entry))
			{
				throw new InvalidOperationException(
					$"Inbox entry already exists for message '{messageId}' and handler '{handlerType}'.");
			}
		}

		_logger.LogDebug("Created inbox entry for message {MessageId} and handler {HandlerType}",
			messageId, handlerType);

		return new ValueTask<InboxEntry>(entry);
	}

	/// <inheritdoc/>
	public ValueTask MarkProcessedAsync(string messageId, string handlerType, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
		ArgumentException.ThrowIfNullOrWhiteSpace(handlerType);
		ObjectDisposedException.ThrowIf(_disposed, this);

		using var activity = InboxActivitySource.StartMarkProcessedActivity(messageId, handlerType);

		var key = GetKey(messageId, handlerType);

		lock (_stateLock)
		{
			if (!_entries.TryGetValue(key, out var entry))
			{
				throw new InvalidOperationException(
					$"Inbox entry not found for message '{messageId}' and handler '{handlerType}'.");
			}

			if (entry.Status == InboxStatus.Processed)
			{
				throw new InvalidOperationException(
					$"Message '{messageId}' for handler '{handlerType}' is already marked as processed.");
			}

			entry.MarkProcessed();

			// The claim this finalises is over, so its expiry goes with it. A lease record outliving its
			// claim is not merely stale: this key can be removed and re-admitted later (release, cleanup,
			// eviction), and the next lease-less claim on it would then find an ancient expiry sitting
			// beside a live Processing entry and read it as a dead processor to reclaim.
			_ = _leaseExpiryUnixMs.Remove(key);
		}

		_logger.LogDebug("Marked inbox entry as processed for message {MessageId} and handler {HandlerType}",
			messageId, handlerType);

		return default;
	}

	/// <inheritdoc/>
	public ValueTask MarkProcessingAsync(string messageId, string handlerType, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
		ArgumentException.ThrowIfNullOrWhiteSpace(handlerType);
		ObjectDisposedException.ThrowIf(_disposed, this);

		var key = GetKey(messageId, handlerType);

		lock (_stateLock)
		{
			if (!_entries.TryGetValue(key, out var entry))
			{
				throw new InvalidOperationException(
					$"Inbox entry not found for message '{messageId}' and handler '{handlerType}'.");
			}

			// Durably mark Processing. The stored entry is the live reference, so the transition (and the
			// LastAttemptAt stamp the stuck-processing timeout reads) is observable via GetEntryAsync.
			entry.MarkProcessing();
		}

		_logger.LogDebug("Marked inbox entry as processing for message {MessageId} and handler {HandlerType}",
			messageId, handlerType);

		return default;
	}

	/// <inheritdoc/>
	public ValueTask<bool> TryMarkAsProcessedAsync(string messageId, string handlerType, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
		ArgumentException.ThrowIfNullOrWhiteSpace(handlerType);
		ObjectDisposedException.ThrowIf(_disposed, this);

		var key = GetKey(messageId, handlerType);

		// First-writer-wins. Under _stateLock, so this add is atomic against every other mutator of the
		// key and not merely against another caller of this method.
		var entry = new InboxEntry
		{
			MessageId = messageId,
			HandlerType = handlerType,
			MessageType = string.Empty,
			Payload = [],
			Status = InboxStatus.Processed,
			ProcessedAt = _timeProvider.GetUtcNow(),
			// Stamped so the entry carries its own partition — see CreateEntryAsync.
			TenantId = CurrentTenantScope.TenantId
		};

		bool added;
		lock (_stateLock)
		{
			added = _entries.TryAdd(key, entry);
		}

		if (added)
		{
			_logger.LogDebug("First processor for message {MessageId} and handler {HandlerType}",
				messageId, handlerType);
			return new ValueTask<bool>(true);
		}

		_logger.LogDebug("Duplicate detected for message {MessageId} and handler {HandlerType}",
			messageId, handlerType);
		return new ValueTask<bool>(false);
	}

	/// <inheritdoc/>
	public ValueTask<bool> TryClaimAsync(string messageId, string handlerType, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
		ArgumentException.ThrowIfNullOrWhiteSpace(handlerType);
		ObjectDisposedException.ThrowIf(_disposed, this);

		var key = GetKey(messageId, handlerType);

		// First-writer-wins claim into the NON-TERMINAL Processing state. A successful claim is finalized
		// to Processed via MarkProcessedAsync, or removed via ReleaseAsync on handler failure. This claim
		// carries no expiry: it is held until one of those two happens, and nothing may take it away in
		// the meantime.
		var entry = new InboxEntry
		{
			MessageId = messageId,
			HandlerType = handlerType,
			MessageType = string.Empty,
			Payload = [],
			Status = InboxStatus.Processing,
			// Stamped so the entry carries its own partition — see CreateEntryAsync.
			TenantId = CurrentTenantScope.TenantId
		};

		bool claimed;
		lock (_stateLock)
		{
			// Enforce capacity limits before attempting to add.
			if (_options.MaxEntries > 0 && _entries.Count >= _options.MaxEntries)
			{
				EvictOldestEntry();
			}

			claimed = _entries.TryAdd(key, entry);
		}

		if (claimed)
		{
			_logger.LogDebug("Claimed inbox entry for message {MessageId} and handler {HandlerType}",
				messageId, handlerType);
			return new ValueTask<bool>(true);
		}

		_logger.LogDebug("Claim denied (already claimed/processed) for message {MessageId} and handler {HandlerType}",
			messageId, handlerType);
		return new ValueTask<bool>(false);
	}

	/// <inheritdoc/>
	public ValueTask<LeaseToken?> TryAcquireLeaseAsync(
		string messageId,
		string handlerType,
		TimeSpan leaseDuration,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
		ArgumentException.ThrowIfNullOrWhiteSpace(handlerType);
		ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(leaseDuration, TimeSpan.Zero);
		ObjectDisposedException.ThrowIf(_disposed, this);

		var key = GetKey(messageId, handlerType);
		var nowMs = _timeProvider.GetUtcNow().ToUnixTimeMilliseconds();

		// Single atomic lease CAS under the store lock: claim IFF absent, Received, Failed, or a Processing
		// entry whose lease has run out (reclaiming a dead processor). A live claim, leased or not, and a
		// terminal Processed entry are denied.
		lock (_stateLock)
		{
			var exists = _entries.TryGetValue(key, out var existing);

			// A Processing entry with NO lease record was taken through the lease-less overload, which is
			// documented as held until finalised or released. Absent is therefore "no expiry", not "expired
			// infinitely long ago": reading the miss as expiry would hand a message to a second caller
			// while the first caller's handler is still running. Only a real expiry, actually in the past,
			// makes a claim reclaimable.
			var claimable = !exists
				|| existing!.Status == InboxStatus.Received
				|| existing.Status == InboxStatus.Failed
				|| (existing.Status == InboxStatus.Processing
					&& _leaseExpiryUnixMs.TryGetValue(key, out var expiry)
					&& expiry < nowMs);

			if (!claimable)
			{
				_logger.LogDebug("Lease-claim denied (live lease or processed) for message {MessageId} and handler {HandlerType}",
					messageId, handlerType);
				return new ValueTask<LeaseToken?>((LeaseToken?)null);
			}

			if (exists)
			{
				// Re-admit the record that is already here; never replace it. A replacement carries forward
				// only the fields its author enumerated, and everything left out is destroyed by the very
				// claim that exists to protect it — the payload a drain is about to redeliver, the message
				// type that resolves that payload, the metadata, the tenant the entry was stamped with. A
				// mutation preserves them by construction rather than by an enumeration that has to stay in
				// step with the record. Retry history survives for the same reason, and the shared finalize
				// (MarkFailedAsync/FailAsync) remains the single monotonic incrementer, so the count is still
				// exactly-once per attempt.
				existing!.Status = InboxStatus.Processing;
			}
			else
			{
				if (_options.MaxEntries > 0 && _entries.Count >= _options.MaxEntries)
				{
					EvictOldestEntry();
				}

				_entries[key] = new InboxEntry
				{
					MessageId = messageId,
					HandlerType = handlerType,
					Status = InboxStatus.Processing,
					ReceivedAt = _timeProvider.GetUtcNow(),
					// Stamped so the entry carries its own partition. Eviction scans across tenants and
					// rebuilds this entry's key from ITS tenant, not the ambient one; an unstamped entry
					// rebuilds under the untenanted sentinel, matches nothing, and is never reclaimed.
					TenantId = CurrentTenantScope.TenantId
				};
			}

			var expiresAtMs = nowMs + (long)leaseDuration.TotalMilliseconds;
			_leaseExpiryUnixMs[key] = expiresAtMs;

			_logger.LogDebug("Lease-claimed inbox entry for message {MessageId} and handler {HandlerType}",
				messageId, handlerType);

			// The term is the expiry this call just wrote. Reclaim above requires the recorded expiry to be
			// STRICTLY less than now, and this replacement is now plus a non-negative duration, so a newly
			// written term is always strictly greater than the one it displaced. That is what makes the
			// value usable as an identity rather than merely a deadline.
			return new ValueTask<LeaseToken?>(ToLeaseToken(expiresAtMs));
		}
	}

	/// <inheritdoc/>
	public ValueTask<bool> CompleteAsync(string messageId, string handlerType, LeaseToken lease, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
		ArgumentException.ThrowIfNullOrWhiteSpace(handlerType);
		ObjectDisposedException.ThrowIf(_disposed, this);

		using var activity = InboxActivitySource.StartMarkProcessedActivity(messageId, handlerType);

		var key = GetKey(messageId, handlerType);

		lock (_stateLock)
		{
			// The term, not the status, is what separates this caller from the one that replaced it. At this
			// instant the entry is legitimately Processing either way; only the expiry says whose.
			if (!HoldsLease(key, lease) || !_entries.TryGetValue(key, out var entry))
			{
				_logger.LogDebug("Lease-fenced complete rejected (term lapsed) for message {MessageId} and handler {HandlerType}",
					messageId, handlerType);
				return new ValueTask<bool>(false);
			}

			entry.MarkProcessed();
			_ = _leaseExpiryUnixMs.Remove(key);
		}

		_logger.LogDebug("Marked inbox entry as processed for message {MessageId} and handler {HandlerType}",
			messageId, handlerType);

		return new ValueTask<bool>(true);
	}

	/// <inheritdoc/>
	public ValueTask<bool> FailAsync(
		string messageId,
		string handlerType,
		LeaseToken lease,
		string errorMessage,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
		ArgumentException.ThrowIfNullOrWhiteSpace(handlerType);
		ArgumentNullException.ThrowIfNull(errorMessage);
		ObjectDisposedException.ThrowIf(_disposed, this);

		using var activity = InboxActivitySource.StartMarkFailedActivity(messageId, handlerType);

		var key = GetKey(messageId, handlerType);

		lock (_stateLock)
		{
			if (!HoldsLease(key, lease) || !_entries.TryGetValue(key, out var entry))
			{
				_logger.LogDebug("Lease-fenced fail rejected (term lapsed) for message {MessageId} and handler {HandlerType}",
					messageId, handlerType);
				return new ValueTask<bool>(false);
			}

			entry.MarkFailed(errorMessage);

			// The attempt is over; its lease goes with it, so a Failed entry never carries an expiry that
			// could be compared against a later claim on the same key.
			_ = _leaseExpiryUnixMs.Remove(key);
		}

		_logger.LogWarning("Marked inbox entry as failed for message {MessageId} and handler {HandlerType}: {Error}",
			messageId, handlerType, errorMessage);

		return new ValueTask<bool>(true);
	}

	/// <summary>
	/// Renders a lease expiry as the opaque ownership term handed back to the caller.
	/// </summary>
	private static LeaseToken ToLeaseToken(long expiresAtUnixMs) =>
		new(expiresAtUnixMs.ToString(System.Globalization.CultureInfo.InvariantCulture));

	/// <summary>
	/// Reports whether <paramref name="lease"/> is still the term recorded for <paramref name="key"/>.
	/// Callers MUST hold <c>_stateLock</c>.
	/// </summary>
	private bool HoldsLease(InboxKey key, LeaseToken lease) =>
		_leaseExpiryUnixMs.TryGetValue(key, out var expiry) && ToLeaseToken(expiry) == lease;

	/// <inheritdoc/>
	public ValueTask ReleaseAsync(string messageId, string handlerType, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
		ArgumentException.ThrowIfNullOrWhiteSpace(handlerType);
		ObjectDisposedException.ThrowIf(_disposed, this);

		var key = GetKey(messageId, handlerType);

		// Remove the claim so a redelivery can re-admit. No-op if already removed or never claimed.
		//
		// Never delete a finalized (Processed) entry. A caller whose own claim lapsed can arrive here
		// after a second processor reclaimed the entry and finalized it; deleting it then would erase
		// the record of a message that really was processed and re-admit it on the next delivery.
		lock (_stateLock)
		{
			if (_entries.TryGetValue(key, out var entry) && entry.Status == InboxStatus.Processed)
			{
				return default;
			}

			RemoveEntry(key);
		}

		_logger.LogDebug("Released inbox claim for message {MessageId} and handler {HandlerType}",
			messageId, handlerType);

		return default;
	}

	/// <inheritdoc/>
	public ValueTask<bool> IsProcessedAsync(string messageId, string handlerType, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
		ArgumentException.ThrowIfNullOrWhiteSpace(handlerType);
		ObjectDisposedException.ThrowIf(_disposed, this);

		using var activity = InboxActivitySource.StartExistsActivity(messageId, handlerType);

		var key = GetKey(messageId, handlerType);

		lock (_stateLock)
		{
			var isProcessed = _entries.TryGetValue(key, out var entry) &&
							  entry.Status == InboxStatus.Processed;

			return new ValueTask<bool>(isProcessed);
		}
	}

	/// <inheritdoc/>
	public ValueTask<InboxEntry?> GetEntryAsync(string messageId, string handlerType, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
		ArgumentException.ThrowIfNullOrWhiteSpace(handlerType);
		ObjectDisposedException.ThrowIf(_disposed, this);

		var key = GetKey(messageId, handlerType);

		lock (_stateLock)
		{
			_ = _entries.TryGetValue(key, out var entry);

			return new ValueTask<InboxEntry?>(entry);
		}
	}

	/// <inheritdoc/>
	public ValueTask MarkFailedAsync(string messageId, string handlerType, string errorMessage, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
		ArgumentException.ThrowIfNullOrWhiteSpace(handlerType);
		ArgumentNullException.ThrowIfNull(errorMessage);
		ObjectDisposedException.ThrowIf(_disposed, this);

		using var activity = InboxActivitySource.StartMarkFailedActivity(messageId, handlerType);

		var key = GetKey(messageId, handlerType);

		lock (_stateLock)
		{
			if (!_entries.TryGetValue(key, out var entry))
			{
				throw new InvalidOperationException(
					$"Inbox entry not found for message '{messageId}' and handler '{handlerType}'.");
			}

			// Processed is absorbing: refuse rather than demote a finalized entry to Failed, which
			// would make it re-admittable and run the handler again.
			if (entry.Status == InboxStatus.Processed)
			{
				return default;
			}

			entry.MarkFailed(errorMessage);

			// The attempt is over; its lease goes with it, so a Failed entry never carries an expiry that
			// could be read beside a later claim on the same key.
			_ = _leaseExpiryUnixMs.Remove(key);
		}

		_logger.LogWarning("Marked inbox entry as failed for message {MessageId} and handler {HandlerType}: {Error}",
			messageId, handlerType, errorMessage);

		return default;
	}

	/// <inheritdoc/>
	public ValueTask MarkFailedAsync(string messageId, string handlerType, string errorMessage, int retryCount, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
		ArgumentException.ThrowIfNullOrWhiteSpace(handlerType);
		ArgumentNullException.ThrowIfNull(errorMessage);
		ObjectDisposedException.ThrowIf(_disposed, this);

		using var activity = InboxActivitySource.StartMarkFailedActivity(messageId, handlerType);

		var key = GetKey(messageId, handlerType);

		lock (_stateLock)
		{
			if (!_entries.TryGetValue(key, out var entry))
			{
				throw new InvalidOperationException(
					$"Inbox entry not found for message '{messageId}' and handler '{handlerType}'.");
			}

			// Processed is absorbing: refuse rather than demote a finalized entry to Failed, which
			// would make it re-admittable and run the handler again.
			if (entry.Status == InboxStatus.Processed)
			{
				return default;
			}

			// Set the retry count EXACTLY (no increment) so a transient short-circuit (e.g. an open circuit
			// breaker) leaves the entry re-admittable without consuming a delivery attempt.
			entry.Status = InboxStatus.Failed;
			entry.LastError = errorMessage;
			entry.RetryCount = retryCount;
			entry.LastAttemptAt = _timeProvider.GetUtcNow();

			_ = _leaseExpiryUnixMs.Remove(key);
		}

		_logger.LogWarning("Marked inbox entry as failed for message {MessageId} and handler {HandlerType}: {Error}",
			messageId, handlerType, errorMessage);

		return default;
	}

	/// <inheritdoc/>
	public ValueTask<IEnumerable<InboxEntry>> GetAllTenantsFailedEntriesAsync(
		int maxRetries,
		DateTimeOffset? olderThan,
		int batchSize,
		CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		lock (_stateLock)
		{
			return SelectFailedEntriesUnderLock(maxRetries, olderThan, batchSize);
		}
	}

	// The drain's re-admission predicate. A Failed entry is retryable, and so is a Processing entry whose
	// lease has run out: that is a processor that died holding the entry, and admitting it is the only way
	// it ever reaches a terminal state. Without the expired-lease arm, leasing the drain would move an
	// entry to Processing and then never select it again -- trading a duplicate dispatch for a permanently
	// stranded entry, which is the worse failure. A Processing entry with NO lease record was taken through
	// the lease-less claim overload, which is held until its own caller finalises or releases it; absent is
	// "no expiry", never "expired", so it is not admitted here. Requires _stateLock held.
	private bool IsRetryEligibleUnderLock(InboxKey key, InboxEntry entry, long nowMs)
	{
		if (entry.Status == InboxStatus.Failed)
		{
			return true;
		}

		// The key comes from the entry's own dictionary slot, never recomposed from the ambient tenant:
		// this scan is estate-wide, so a key rebuilt from whatever tenant happened to be current would
		// address a different partition and silently find no lease -- reading every other tenant's dead
		// processor as a live one.
		return entry.Status == InboxStatus.Processing
			&& _leaseExpiryUnixMs.TryGetValue(key, out var expiry)
			&& expiry < nowMs;
	}

	// Split out only so the two-pass scan is not re-indented under the lock. Requires _stateLock held.
	private ValueTask<IEnumerable<InboxEntry>> SelectFailedEntriesUnderLock(
		int maxRetries,
		DateTimeOffset? olderThan,
		int batchSize)
	{
		var nowMs = _timeProvider.GetUtcNow().ToUnixTimeMilliseconds();

		// Use array-based approach to avoid ToList() allocation
		var count = 0;
		foreach (var (key, e) in _entries)
		{
			if (IsRetryEligibleUnderLock(key, e, nowMs) &&
				(maxRetries <= 0 || e.RetryCount < maxRetries) &&
				(!olderThan.HasValue || e.LastAttemptAt < olderThan.Value))
			{
				count++;
			}
		}

		if (count == 0)
		{
			return new ValueTask<IEnumerable<InboxEntry>>(Array.Empty<InboxEntry>());
		}

		var candidates = new InboxEntry[count];
		var idx = 0;
		foreach (var (key, e) in _entries)
		{
			if (IsRetryEligibleUnderLock(key, e, nowMs) &&
				(maxRetries <= 0 || e.RetryCount < maxRetries) &&
				(!olderThan.HasValue || e.LastAttemptAt < olderThan.Value))
			{
				candidates[idx++] = e;
			}
		}

		Array.Sort(candidates, static (a, b) =>
		{
			var retryCompare = a.RetryCount.CompareTo(b.RetryCount);
			return retryCompare != 0 ? retryCompare : Nullable.Compare(a.LastAttemptAt, b.LastAttemptAt);
		});

		var resultSize = Math.Min(batchSize, candidates.Length);
		var failedEntries = resultSize == candidates.Length
			? candidates
			: candidates.AsSpan(0, resultSize).ToArray();

		return new ValueTask<IEnumerable<InboxEntry>>(failedEntries);
	}

	/// <inheritdoc/>
	public ValueTask<IEnumerable<InboxEntry>> GetAllTenantsEntriesAsync(CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		lock (_stateLock)
		{
			// Use array-based approach to avoid ToList() allocation
			var entries = new InboxEntry[_entries.Count];
			_entries.Values.CopyTo(entries, 0);
			return new ValueTask<IEnumerable<InboxEntry>>(entries);
		}
	}

	/// <inheritdoc/>
	public ValueTask<InboxStatistics> GetAllTenantsStatisticsAsync(CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		// Single-pass counting without multiple enumeration
		var total = 0;
		var processed = 0;
		var failed = 0;
		var pending = 0;

		lock (_stateLock)
		{
			foreach (var entry in _entries.Values)
			{
				total++;
				switch (entry.Status)
				{
					case InboxStatus.Processed:
						processed++;
						break;

					case InboxStatus.Failed:
						failed++;
						break;

					case InboxStatus.Received:
					case InboxStatus.Processing:
						pending++;
						break;
				}
			}
		}

		return new ValueTask<InboxStatistics>(new InboxStatistics
		{
			TotalEntries = total,
			ProcessedEntries = processed,
			FailedEntries = failed,
			PendingEntries = pending
		});
	}

	/// <inheritdoc/>
	public ValueTask<int> CleanupAllTenantsProcessedEntriesAsync(DateTimeOffset olderThan, CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		using var activity = InboxActivitySource.StartCleanupActivity();

		int count;

		lock (_stateLock)
		{
			count = RemoveProcessedOlderThan(olderThan);
		}

		_logger.LogInformation("Cleaned up {Count} processed inbox entries older than {CutoffDate}",
			count, olderThan);

		return new ValueTask<int>(count);
	}

	/// <inheritdoc/>
	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		_cleanupTimer?.Dispose();

		lock (_stateLock)
		{
			_entries.Clear();
			_leaseExpiryUnixMs.Clear();
		}

		_disposed = true;
	}

	/// <inheritdoc/>
	public ValueTask DisposeAsync()
	{
		Dispose();
		return ValueTask.CompletedTask;
	}

	/// <summary>
	/// Removes an entry and the lease record that belongs to it. Requires <c>_stateLock</c> held.
	/// </summary>
	/// <remarks>
	/// The single removal path for both maps, so the two cannot drift apart. A lease record left behind by
	/// a removed entry outlives the claim it describes; when the key is re-admitted later, the next
	/// lease-less claim on it sits beside an expiry from a claim that ended long ago, and a lease claimer
	/// reads that as a dead processor and takes a live claim away.
	/// </remarks>
	private void RemoveEntry(InboxKey key)
	{
		_ = _entries.Remove(key);
		_ = _leaseExpiryUnixMs.Remove(key);
	}

	/// <summary>
	/// Removes every processed entry finalised at or before <paramref name="cutoff"/>, returning the count.
	/// Requires <c>_stateLock</c> held.
	/// </summary>
	private int RemoveProcessedOlderThan(DateTimeOffset cutoff)
	{
		var count = 0;

		// Snapshotted because the loop removes from the map it is scanning.
		foreach (var kvp in _entries.ToArray())
		{
			var entry = kvp.Value;
			if (entry is { Status: InboxStatus.Processed, ProcessedAt: not null } &&
				entry.ProcessedAt.Value <= cutoff)
			{
				RemoveEntry(kvp.Key);
				count++;
			}
		}

		return count;
	}

	private void PerformScheduledCleanup()
	{
		if (_disposed)
		{
			return;
		}

		try
		{
			var cutoff = _timeProvider.GetUtcNow().Subtract(_options.RetentionPeriod);
			int count;

			lock (_stateLock)
			{
				count = RemoveProcessedOlderThan(cutoff);
			}

			if (count > 0)
			{
				_logger.LogDebug("Scheduled cleanup removed {Count} processed inbox entries older than {CutoffDate}",
					count, cutoff);
			}
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error during scheduled inbox cleanup");
		}
	}

	/// <summary>
	/// The tenant partition the current call addresses, re-resolved per call.
	/// </summary>
	/// <remarks>
	/// Re-read rather than captured because this store is registered once and serves every caller: the
	/// tenant is a property of the operation, not of the instance. <see cref="TenantScope.FromContext"/>
	/// fails closed when multi-tenancy is active but no tenant is resolved, so the store cannot reach a
	/// key with no tenant term in it, and yields the reserved untenanted marker in a single-tenant
	/// deployment.
	/// </remarks>
	private TenantScope CurrentTenantScope => TenantScope.FromContext(_tenantContext);

	/// <summary>
	/// The single-tenant deployment's context: one partition, named by the reserved untenanted marker.
	/// </summary>
	/// <remarks>
	/// Resolves an explicit term rather than <see langword="null"/>. "No tenancy is configured here" and
	/// "this entry belongs to no tenant" are different statements, and a context returning null would make
	/// the scope conversion fail closed on a deployment that is behaving correctly.
	/// </remarks>
	private sealed class UntenantedContext : ITenantContext
	{
		internal static readonly UntenantedContext Instance = new();

		public string? TenantId => TenantScope.UntenantedSentinel;

		public bool HasTenant => true;
	}

	/// <summary>
	/// Composes the storage key for an entry belonging to the calling tenant.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The tenant term is part of the key because in an inbox the tenant is part of a message's IDENTITY,
	/// not merely data carried alongside it. Message ids are chosen by producers and are unique only
	/// within the system that issued them, so two tenants routinely present distinct messages under one
	/// id. Keyed on the pair alone, both resolve to a single entry: the second tenant's claim is refused
	/// as a duplicate and its message is never processed and never retried. Nothing throws, so the failure
	/// is invisible -- silent message loss on the success path, and a cross-tenant isolation breach, since
	/// one tenant's traffic then decides whether another's is delivered.
	/// </para>
	/// <para>
	/// In a single-tenant deployment this resolves to the reserved untenanted marker, so the partition is
	/// a real, explicit term rather than an absent one.
	/// </para>
	/// </remarks>
	private InboxKey GetKey(string messageId, string handlerType)
		=> GetKey(CurrentTenantScope.TenantId, messageId, handlerType);

	/// <summary>
	/// Composes the storage key for an entry whose tenant is known explicitly.
	/// </summary>
	/// <remarks>
	/// Required wherever a key is reconstructed for an entry that may belong to a tenant OTHER than the
	/// caller's -- eviction scans every partition, so recomposing from the ambient tenant there would
	/// build a key that matches nothing and silently fail to reclaim.
	/// </remarks>
	private static InboxKey GetKey(string tenantId, string messageId, string handlerType)
		=> new(tenantId, messageId, handlerType);

	/// <summary>
	/// The three terms that together identify one inbox entry, carried as a tuple rather than joined into
	/// a delimited string.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The terms were previously joined as <c>{tenantId}:{messageId}:{handlerType}</c>. That join is not
	/// injective: a tenant id or message id may itself contain a colon -- neither is validated against any
	/// charset -- so <c>("a:b", "c", h)</c> and <c>("a", "b:c", h)</c> both render <c>a:b:c:h</c> and
	/// occupy the same entry. In a dedup store a collision does not throw. The second message is refused
	/// as an already-seen duplicate and is never processed and never retried: silent loss, and across a
	/// tenant boundary when the colliding term is the tenant.
	/// </para>
	/// <para>
	/// A tuple removes the join, so there is no delimiter for a term to cross and injectivity holds for
	/// every string input by construction rather than by an escaping rule that has to stay correct. The
	/// key is in-process only -- it is never persisted, so no stored data is keyed by the old shape.
	/// </para>
	/// </remarks>
	private readonly record struct InboxKey(string TenantId, string MessageId, string HandlerType);

	// Requires _stateLock held: the scan below decides which entry to reclaim and then removes it, and the
	// entry must not change state in between.
	private void EvictOldestEntry()
	{
		// Dijkstra D5 — eviction must FAIL CLOSED, never silently drop a live dedup record.
		// (Supersedes 's "bounded memory takes precedence" fallback: a silently-evicted live dedup
		// marker lets a redelivery re-admit and re-process the same message — a duplicate side-effect, the
		// exact thing the inbox exists to prevent. Dedup correctness outranks bounded memory here.)
		//
		// Reclaim, in priority order, an entry whose removal CANNOT cause a duplicate:
		//   1. the oldest NON-live entry (Received/Failed) — neither a dedup marker nor an in-flight claim;
		//   2. else the oldest entry PAST the dedup window (a Processed marker older than RetentionPeriod no
		//      longer protects against a duplicate — same predicate as PerformScheduledCleanup).
		// If neither exists, every entry is a live dedup marker / in-flight claim within the window, so
		// evicting any of them would risk a duplicate: THROW instead.
		var reclaimable = _entries.Values
			.Where(static e => e.Status is not (InboxStatus.Processed or InboxStatus.Processing))
			.OrderBy(static e => e.ReceivedAt)
			.FirstOrDefault();

		if (reclaimable is null)
		{
			var cutoff = _timeProvider.GetUtcNow().Subtract(_options.RetentionPeriod);
			reclaimable = _entries.Values
				.Where(e => e is { Status: InboxStatus.Processed, ProcessedAt: not null } && e.ProcessedAt.Value <= cutoff)
				.OrderBy(static e => e.ReceivedAt)
				.FirstOrDefault();
		}

		if (reclaimable is not null)
		{
			// The reclaim scan above spans every tenant partition, so the key is rebuilt from the entry's
			// OWN tenant. Recomposing it from the ambient tenant would produce a key that matches nothing
			// whenever the reclaimable entry belongs to another tenant: the removal would silently fail,
			// the store would stay at capacity, and the fail-closed throw below would fire on a store that
			// did in fact have something safe to reclaim.
			RemoveEntry(
				GetKey(reclaimable.TenantId ?? TenantScope.UntenantedSentinel, reclaimable.MessageId, reclaimable.HandlerType));
			return;
		}

		throw new InvalidOperationException(
			$"The in-memory inbox is at capacity ({_options.MaxEntries}) and every entry is a live deduplication " +
			"record within the retention window. Evicting one would risk re-processing a duplicate message. " +
			"Increase MaxEntries or reduce RetentionPeriod.");
	}
}

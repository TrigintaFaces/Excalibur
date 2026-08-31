// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.QueryDsl;

using Excalibur.Data.ElasticSearch.Diagnostics;
using Excalibur.Dispatch;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Outbox.ElasticSearch;

/// <summary>
/// Elasticsearch-based implementation of <see cref="IOutboxStore"/> and <see cref="IOutboxStoreAdmin"/>.
/// </summary>
/// <remarks>
/// <para>
/// Uses ES document ID = message ID for direct access.
/// Messages are sorted by priority and creation time for ordered retrieval.
/// </para>
/// <para>
/// <b>Tenancy: the discriminator is carried on the document, never applied as a filter.</b> Each staged
/// document records the tenant of the message it projects, and every drained message hands that value
/// back, so the owning tenant is re-established from the document rather than inferred from ambient
/// state. This store reads no ambient tenant on any path, and its declaration of
/// <see cref="ITenantPartitionedStore"/> states that mechanism explicitly.
/// </para>
/// <para>
/// The drain is deliberately estate-wide, and that is a requirement rather than an omission: one
/// dispatcher serves every tenant, so a drain narrowed to an ambient tenant would read it as absent,
/// claim the empty set, and stall delivery for all of them. The admin surfaces say so in their own
/// names (<c>GetAllTenants*</c>, <c>CleanupAllTenants*</c>). The remaining statements read, update and
/// delete a document by its identity, where a tenant term could not exclude a foreign document -- the
/// identity already addresses at most one -- and could only turn the correct document into none. The
/// terminal transitions read the whole document and re-index it, so the recorded tenant survives every
/// compare-and-swap rather than being dropped by a partial update.
/// </para>
/// </remarks>
[SuppressMessage(
	"Maintainability",
	"CA1506:Avoid excessive class coupling",
	Justification = "This store already sat at the coupling limit; declaring ITenantPartitionedStore -- a "
		+ "marker carrying no members -- put it one type over. The alternative to this suppression is to leave "
		+ "the store unattested, which makes row-discriminator multi-tenancy refuse every host that selects "
		+ "this provider for a store that implements the mechanism the gate requires. Refactoring the coupling "
		+ "itself is a separate concern from declaring a mechanism the store already implements.")]
public sealed partial class ElasticsearchOutboxStore : IOutboxStore, IOutboxStoreAdmin, IDeadLetterableOutboxStore, ITenantPartitionedStore, IAsyncDisposable
{
	private readonly ElasticsearchClient _client;
	private readonly ElasticsearchOutboxOptions _options;
	private readonly ILogger<ElasticsearchOutboxStore> _logger;
	private readonly TimeProvider _timeProvider;

	/// <summary>
	/// Bound on compare-and-swap re-reads for a terminal transition. Each retry re-reads the document
	/// and re-evaluates its status, so a losing writer converges on the winner's state.
	/// </summary>
	private const int TerminalTransitionMaxRetries = 5;

	private string? _resolvedProcessorId;

	/// <summary>
	/// Initializes a new instance of the <see cref="ElasticsearchOutboxStore"/> class.
	/// </summary>
	/// <param name="client">The Elasticsearch client.</param>
	/// <param name="options">The outbox options.</param>
	/// <param name="logger">The logger instance.</param>
	/// <param name="timeProvider">
	/// The clock used for the instants this store records rather than compares — a send time, an attempt
	/// time, the failure floor. Defaults to <see cref="TimeProvider.System"/>.
	/// <para>
	/// It is deliberately NOT the clock that decides whether a lease is live. A lease is stamped by one
	/// dispatcher and judged by another, so a predicate reading this clock would compare two machines that
	/// have no reason to agree; that decision is made by the Elasticsearch node instead, inside the claim
	/// script. Injecting the clock here is what lets a test drive this store's clock arbitrarily far from
	/// the node's and observe that the claim does not move — the property, stated as a test.
	/// </para>
	/// </param>
	public ElasticsearchOutboxStore(
		ElasticsearchClient client,
		IOptions<ElasticsearchOutboxOptions> options,
		ILogger<ElasticsearchOutboxStore> logger,
		TimeProvider? timeProvider = null)
	{
		_client = client ?? throw new ArgumentNullException(nameof(client));
		_options = options?.Value ?? throw new ArgumentNullException(nameof(options));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		_timeProvider = timeProvider ?? TimeProvider.System;
	}

	/// <inheritdoc/>
	public async ValueTask StageMessageAsync(OutboundMessage message, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(message);

		var doc = ToDocument(message);

		var response = await _client.IndexAsync(
			doc,
			idx => idx
				.Index(_options.IndexName)
				.Id(message.Id)
				.OpType(OpType.Create)
				.Refresh(GetRefresh()),
			cancellationToken).ConfigureAwait(false);

		if (!response.IsValidResponse)
		{
			if (response.ElasticsearchServerError?.Status == 409)
			{
				throw new InvalidOperationException($"Outbox message already exists with ID '{message.Id}'.");
			}

			throw new InvalidOperationException(
				$"Failed to stage outbox message: {response.ElasticsearchServerError?.Error?.Reason ?? "Unknown error"}");
		}

		LogMessageStaged(message.Id, message.MessageType, message.Destination);
	}

	/// <inheritdoc/>
	[UnconditionalSuppressMessage(
		"AOT", "IL3050:Using RequiresDynamicCode member in AOT",
		Justification = "Outbox payloads use runtime serialization for message types.")]
	[UnconditionalSuppressMessage(
		"Trimming", "IL2026:Members annotated with RequiresUnreferencedCode may break with trimming",
		Justification = "Outbox payloads use runtime serialization for message types.")]
	[RequiresUnreferencedCode("Outbox stores serialize the message payload reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	[RequiresDynamicCode("Outbox stores serialize the message payload reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	public async ValueTask EnqueueAsync(IDispatchMessage message, IMessageContext context, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(message);
		ArgumentNullException.ThrowIfNull(context);

		var messageType = message.GetType().FullName ?? message.GetType().Name;
		var payload = JsonSerializer.SerializeToUtf8Bytes(message, message.GetType(), EventSerializationDefaults.Canonical);

		var outbound = OutboundMessage.FromContext(messageType, payload, messageType, context);

		await StageMessageAsync(outbound, cancellationToken).ConfigureAwait(false);
		LogMessageEnqueued(outbound.Id, messageType);
	}

	/// <inheritdoc/>
	/// <remarks>
	/// <para>
	/// This is an <b>atomic disjoint lease-claim</b>, not a plain search. Previously this method only
	/// searched for staged documents and mutated nothing, so two pollers running concurrently received
	/// byte-identical batches by construction and every message was delivered twice.
	/// </para>
	/// <para>
	/// Each candidate is now claimed with a compare-and-swap on the document's optimistic concurrency
	/// tokens (<c>if_seq_no</c>/<c>if_primary_term</c>) captured by the same search that returned it.
	/// Exactly one poller's conditional write can succeed per document; the loser observes a version
	/// conflict and skips that message rather than returning it. Only successfully claimed messages are
	/// returned, so concurrent callers receive disjoint sets.
	/// </para>
	/// <para>
	/// A claimed message keeps status <see cref="OutboxStatus.Staged"/> and is hidden from other pollers
	/// by its lease until the lease expires or a terminal transition clears it. An expired lease is
	/// reclaimable, so a poller that crashes mid-delivery cannot strand a message permanently. Delivery
	/// is at-least-once: handlers must be idempotent.
	/// </para>
	/// </remarks>
	[RequiresUnreferencedCode("Outbox stores serialize the message payload reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	[RequiresDynamicCode("Outbox stores serialize the message payload reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	public async ValueTask<IEnumerable<OutboundMessage>> GetUnsentMessagesAsync(int batchSize, CancellationToken cancellationToken)
	{
		ArgumentOutOfRangeException.ThrowIfLessThan(batchSize, 1);

		// No dispatcher-side clock is read anywhere in this method. Every one of the four clauses below, and
		// the claim that follows them, is decided on the Elasticsearch node's clock.

		// Claimable = no lease was ever stamped, or the previous holder's lease has expired.
		//
		// "now" here is date math Elasticsearch resolves on its OWN clock, not a value this process
		// computed. This clause only narrows the candidates — the claim below re-decides it authoritatively
		// — but it is the node's clock for the same reason the claim is: a lease is stamped by one
		// dispatcher and judged by another, and the two machines have no reason to agree.
		var claimable = new BoolQuery
		{
			Should =
			[
				new BoolQuery { MustNot = [new ExistsQuery { Field = new Field("leaseExpiresAt") }] },
				new BoolQuery { Must = [new DateRangeQuery("leaseExpiresAt") { Lte = (DateMath)"now" }] },
			],
			MinimumShouldMatch = 1,
		};

		// Past its failure-anchored floor = never failed, or the floor stamped at the failure instant has
		// elapsed. This is what keeps a retry from hot-looping without making a failure terminal: the message
		// is withheld for the floor and then returns to the claimable set on its own.
		//
		// "now" is date math Elasticsearch resolves on its OWN clock, for the same reason the lease clause
		// above does. The floor is stamped by the dispatcher that observed the failure and read back by
		// whichever dispatcher next polls, so a caller-computed bound would compare two machines that have no
		// reason to agree: a dispatcher running fast would treat a live floor as elapsed and retry inside it,
		// and a dispatcher running slow would withhold a message whose floor had genuinely passed. The stamp
		// is now written on the node's clock too — see the non-success transition script — so both sides of
		// this comparison come from one machine.
		var pastFloor = new BoolQuery
		{
			Should =
			[
				new BoolQuery { MustNot = [new ExistsQuery { Field = new Field("nextAttemptAt") }] },
				new BoolQuery { Must = [new DateRangeQuery("nextAttemptAt") { Lte = (DateMath)"now" }] },
			],
			MinimumShouldMatch = 1,
		};

		// Deliverable = staged (never attempted) or failed below the retry ceiling (attempted, still owed
		// at-least-once delivery). Failed belongs here: a store that only ever re-claims Staged withholds a
		// failed message forever, which reads as a well-behaved backoff and is actually a silent drop.
		// Sent, Sending and DeadLettered are terminal and never claimed.
		var deliverable = new BoolQuery
		{
			Should =
			[
				new TermQuery { Field = "status", Value = (int)OutboxStatus.Staged },
				new TermQuery { Field = "status", Value = (int)OutboxStatus.Failed },
			],
			MinimumShouldMatch = 1,
		};

		// Due = unscheduled, or scheduled for a time that has arrived. Resolved on the node's clock like its
		// two siblings: a schedule is an absolute instant a producer chose, judged by whichever dispatcher
		// happens to poll, so a dispatcher running ahead would release a scheduled message early.
		var due = new BoolQuery
		{
			Should =
			[
				new BoolQuery { MustNot = [new ExistsQuery { Field = new Field("scheduledAt") }] },
				new BoolQuery { Must = [new DateRangeQuery("scheduledAt") { Lte = (DateMath)"now" }] },
			],
			MinimumShouldMatch = 1,
		};

		var searchRequest = new SearchRequest(_options.IndexName)
		{
			Size = batchSize,

			// Required: the claim compare-and-swaps on these tokens, so the search must return them.
			SeqNoPrimaryTerm = true,
			Sort =
			[
				new SortOptions { Field = new FieldSort("priority") { Order = SortOrder.Asc } },
				new SortOptions { Field = new FieldSort("createdAt") { Order = SortOrder.Asc } },
			],
			Query = new BoolQuery { Must = [deliverable, due, claimable, pastFloor] },
		};

		var response = await _client.SearchAsync<ElasticsearchOutboxDocument>(
			searchRequest, cancellationToken).ConfigureAwait(false);

		if (!response.IsValidResponse)
		{
			return [];
		}

		var leaseTimeoutMs = (long)TimeSpan.FromSeconds(_options.LeaseTimeoutSeconds).TotalMilliseconds;
		var owner = GetProcessorId();
		var claimed = new List<OutboundMessage>();

		foreach (var hit in response.Hits)
		{
			cancellationToken.ThrowIfCancellationRequested();

			// A hit without concurrency tokens cannot be claimed safely - skip it rather than blind-write.
			if (hit.Source is null || hit.SeqNo is null || hit.PrimaryTerm is null)
			{
				continue;
			}

			var documentId = hit.Id ?? hit.Source.Id;
			if (string.IsNullOrEmpty(documentId))
			{
				continue;
			}

			var doc = hit.Source;

			if (await TryClaimWithServerClockAsync(
					documentId, leaseTimeoutMs, owner, hit.SeqNo, hit.PrimaryTerm, cancellationToken)
				.ConfigureAwait(false))
			{
				// The lease Elasticsearch stamped is not read back: the lease fields do not cross the
				// OutboundMessage boundary, so there is nothing here that would go stale.
				claimed.Add(FromDocument(doc));
			}

			// A conflict means another poller claimed it first. Skipping is what makes batches disjoint.
		}

		LogMessagesClaimed(claimed.Count, owner);
		return claimed;
	}

	/// <inheritdoc/>
	/// <remarks>
	/// The already-sent check and the write are a single compare-and-swap, not a check followed by an
	/// unconditional overwrite. Previously the status was read, tested, and written back across separate
	/// round trips, so two concurrent callers could both observe a non-sent message and both succeed.
	/// Now exactly one conditional write can land; the loser re-reads, observes the winner's terminal
	/// state, and throws the documented already-sent error.
	/// </remarks>
	public async ValueTask MarkSentAsync(string messageId, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);

		for (var attempt = 0; attempt < TerminalTransitionMaxRetries; attempt++)
		{
			var get = await _client.GetAsync<ElasticsearchOutboxDocument>(
				_options.IndexName, messageId, cancellationToken).ConfigureAwait(false);

			if (!get.IsValidResponse || !get.Found || get.Source is null)
			{
				throw new InvalidOperationException($"Outbox message not found with ID '{messageId}'.");
			}

			if (get.Source.Status == (int)OutboxStatus.Sent)
			{
				throw new InvalidOperationException($"Outbox message already sent with ID '{messageId}'.");
			}

			var doc = get.Source;
			doc.Status = (int)OutboxStatus.Sent;
			doc.SentAt = _timeProvider.GetUtcNow();

			// Terminal: release the lease so the message leaves the in-flight population for good.
			doc.LeaseExpiresAt = null;
			doc.LeasedBy = null;

			if (await TryWriteWithCasAsync(messageId, doc, get.SeqNo, get.PrimaryTerm, cancellationToken)
				.ConfigureAwait(false))
			{
				LogMessageSent(messageId);
				return;
			}

			// Version conflict: another writer changed the document. Re-read and re-evaluate - if it has
			// become Sent, the next iteration throws already-sent rather than overwriting it.
		}

		throw new InvalidOperationException(
			$"Failed to mark outbox message '{messageId}' as sent after {TerminalTransitionMaxRetries} "
			+ "concurrent-modification retries.");
	}

	/// <inheritdoc/>
	/// <remarks>
	/// <para>
	/// A delivery that has already been marked sent is never reopened. Previously this read the document
	/// and wrote the whole thing back unconditionally, so a failure report generated before a concurrent
	/// success could land after it and resurrect a delivered message back to Failed - the message was
	/// then re-sent, silently duplicating it. The write is now a compare-and-swap on the document's
	/// concurrency tokens, and a re-read that finds a sent message returns without writing.
	/// </para>
	/// <para>
	/// The compare-and-swap makes the write atomic; it does not by itself make it correct. Three further
	/// conditions are carried here. A report from a dispatcher that does not hold the lease is a silent
	/// no-op, so a superseded dispatcher cannot release a claim its successor is still delivering under.
	/// The recorded attempt count never decreases, so a stale low report cannot push the retry ceiling
	/// further away every time it arrives. And the message is withheld from the claim for the configured
	/// floor and then returns to it, so the retry neither hot-loops nor stops.
	/// </para>
	/// </remarks>
	public async ValueTask MarkFailedAsync(string messageId, string errorMessage, int retryCount, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
		ArgumentNullException.ThrowIfNull(errorMessage);

		var owner = GetProcessorId();

		var applied = await TryApplyNonSuccessTransitionAsync(
			messageId,
			// Ownership guard: an unleased message (failed without ever being claimed) proceeds, and so does one
			// leased by this dispatcher. A message leased by somebody else does not - that report comes from a
			// dispatcher whose claim has already been taken over, and honouring it would clear the successor's
			// lease and let a third dispatcher deliver the same message concurrently.
			owner,
			(int)OutboxStatus.Failed,
			errorMessage,
			// Monotonic: the retry ceiling that eventually gives up on a message is driven by this count, so a
			// late report carrying a lower number must not lower it. The comparison is made inside the script,
			// against the value the node holds at the instant of the write.
			retryCount,
			// The floor is anchored at the failure, not at the lease: a message that failed without ever being
			// claimed has no lease, so a floor derived from lease expiry would yield nothing for it. It travels
			// as a DURATION and the node adds it to its own clock, because the claim reads this gate back on
			// that clock and a value stamped here would be one side of a two-machine comparison.
			_options.FailureBackoffFloorSeconds,
			cancellationToken).ConfigureAwait(false);

		if (applied)
		{
			LogMessageFailed(messageId, errorMessage, retryCount);
		}
	}

	/// <inheritdoc/>
	/// <remarks>
	/// Shares the compare-and-swap transition used by <see cref="MarkFailedAsync"/>: a message that has
	/// already been marked sent is never reopened, so a late dead-letter report cannot resurrect a
	/// delivered message.
	/// </remarks>
	public async ValueTask MarkDeadLetteredAsync(string messageId, string reason, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
		ArgumentNullException.ThrowIfNull(reason);

		var applied = await TryApplyNonSuccessTransitionAsync(
			messageId,
			// Deliberately not ownership-guarded: dead-lettering is the terminal decision that a message cannot
			// succeed, and it must stay possible regardless of which dispatcher holds the lease. That is safe
			// precisely because it is terminal - it removes the message from the claim rather than returning it,
			// so it cannot enable a concurrent second delivery the way an unguarded release would.
			requiredLeaseOwner: null,
			(int)OutboxStatus.DeadLettered,
			reason,
			// Dead-lettering records no attempt count of its own and needs no floor: the message is terminal,
			// so there is no next attempt for a floor to gate.
			monotonicRetryCount: null,
			floorSeconds: null,
			cancellationToken).ConfigureAwait(false);

		if (applied)
		{
			_logger.LogWarning("Marked outbox message {MessageId} as dead-lettered: {Reason}", messageId, reason);
		}
	}

	/// <inheritdoc/>
	public async ValueTask<IEnumerable<OutboundMessage>> GetAllTenantsFailedMessagesAsync(
		int maxRetries,
		DateTimeOffset? olderThan,
		int batchSize,
		CancellationToken cancellationToken)
	{
		var mustClauses = new List<Query>
		{
			new TermQuery { Field = "status", Value = (int)OutboxStatus.Failed },
		};

		if (maxRetries > 0)
		{
			mustClauses.Add(new NumberRangeQuery("retryCount") { Lt = maxRetries });
		}

		if (olderThan.HasValue)
		{
			mustClauses.Add(new DateRangeQuery("lastAttemptAt") { Lt = (DateMath)olderThan.Value.DateTime });
		}

		var response = await _client.SearchAsync<ElasticsearchOutboxDocument>(s => s
			.Index(_options.IndexName)
			.Size(batchSize)
			.Query(q => q.Bool(b => b.Must(mustClauses.ToArray()))),
			cancellationToken).ConfigureAwait(false);

		if (!response.IsValidResponse)
		{
			return [];
		}

		return response.Documents.Select(FromDocument);
	}

	/// <inheritdoc/>
	public async ValueTask<IEnumerable<OutboundMessage>> GetAllTenantsScheduledMessagesAsync(
		DateTimeOffset scheduledBefore,
		int batchSize,
		CancellationToken cancellationToken)
	{
		var searchRequest = new SearchRequest(_options.IndexName)
		{
			Size = batchSize,
			Query = new BoolQuery
			{
				Must =
				[
					new ExistsQuery { Field = new Field("scheduledAt") },
					new DateRangeQuery("scheduledAt") { Lte = (DateMath)scheduledBefore.DateTime },
					new TermQuery { Field = "status", Value = (int)OutboxStatus.Staged },
				],
			},
		};

		var response = await _client.SearchAsync<ElasticsearchOutboxDocument>(
			searchRequest, cancellationToken).ConfigureAwait(false);

		if (!response.IsValidResponse)
		{
			return [];
		}

		return response.Documents.Select(FromDocument);
	}

	/// <inheritdoc/>
	public async ValueTask<int> CleanupAllTenantsSentMessagesAsync(DateTimeOffset olderThan, int batchSize, CancellationToken cancellationToken)
	{
		ArgumentOutOfRangeException.ThrowIfLessThan(batchSize, 1);

		// Bounded cleanup: delete ONLY Sent documents whose sentAt is strictly older than the cutoff,
		// on the CONFIGURED index. Previously this issued a MatchAll DeleteByQuery against a hardcoded
		// "excalibur-outbox" index literal, deleting the entire live outbox (Staged + recent Sent
		// included) regardless of olderThan — a data-loss bug (FR MS-A1). The status + sentAt-range
		// predicate makes "delete an unsent or recent document" inexpressible by this path.
		var deleteRequest = new DeleteByQueryRequest(_options.IndexName)
		{
			// The caller's batch size is a hard ceiling on how many documents one call may remove, not a
			// hint. Without it the delete-by-query removes every matching document in a single pass, so a
			// retention sweep sized to bound its own blast radius does not get the bound it asked for.
			// Remaining documents are removed by the next sweep.
			MaxDocs = batchSize,
			Query = new BoolQuery
			{
				Must =
				[
					new TermQuery { Field = "status", Value = (int)OutboxStatus.Sent },
					new DateRangeQuery("sentAt") { Lt = (DateMath)olderThan.UtcDateTime },
				],
			},
		};

		var response = await _client.DeleteByQueryAsync(
			deleteRequest, cancellationToken).ConfigureAwait(false);

		var deleted = (int)(response.Deleted ?? 0);
		LogMessagesCleanedUp(deleted, olderThan);
		return deleted;
	}

	/// <inheritdoc/>
	public async ValueTask<OutboxStatistics> GetAllTenantsStatisticsAsync(CancellationToken cancellationToken)
	{
		var now = _timeProvider.GetUtcNow();

		// Compute statistics with server-side counts and a single oldest-document lookup per status,
		// rather than materializing up to 10k documents into memory and aggregating client-side.
		var staged = await CountAsync(
			new TermQuery { Field = "status", Value = (int)OutboxStatus.Staged }, cancellationToken).ConfigureAwait(false);
		var sent = await CountAsync(
			new TermQuery { Field = "status", Value = (int)OutboxStatus.Sent }, cancellationToken).ConfigureAwait(false);
		var failed = await CountAsync(
			new TermQuery { Field = "status", Value = (int)OutboxStatus.Failed }, cancellationToken).ConfigureAwait(false);
		var scheduled = await CountAsync(
			new BoolQuery
			{
				Must =
				[
					new TermQuery { Field = "status", Value = (int)OutboxStatus.Staged },
					new ExistsQuery { Field = new Field("scheduledAt") },
				],
			},
			cancellationToken).ConfigureAwait(false);

		// In-flight = staged with a live (unexpired) lease. This was previously hardcoded to zero, which
		// was accurate only because no claim existed; now that a claim leases the message, report it.
		var sending = await CountAsync(
			new BoolQuery
			{
				Must =
				[
					new TermQuery { Field = "status", Value = (int)OutboxStatus.Staged },
					new DateRangeQuery("leaseExpiresAt") { Gt = (DateMath)now.UtcDateTime },
				],
			},
			cancellationToken).ConfigureAwait(false);

		var oldestStaged = await GetOldestCreatedAtAsync(OutboxStatus.Staged, cancellationToken).ConfigureAwait(false);
		var oldestFailed = await GetOldestCreatedAtAsync(OutboxStatus.Failed, cancellationToken).ConfigureAwait(false);

		return new OutboxStatistics
		{
			StagedMessageCount = staged,
			SendingMessageCount = sending,
			SentMessageCount = sent,
			FailedMessageCount = failed,
			ScheduledMessageCount = scheduled,
			OldestUnsentMessageAge = oldestStaged.HasValue ? now - oldestStaged.Value : null,
			OldestFailedMessageAge = oldestFailed.HasValue ? now - oldestFailed.Value : null,
			CapturedAt = now,
		};
	}

	/// <summary>
	/// Returns the server-side document count matching <paramref name="query"/> without materializing documents.
	/// </summary>
	private async ValueTask<int> CountAsync(Query query, CancellationToken cancellationToken)
	{
		var request = new CountRequest(_options.IndexName) { Query = query };
		var response = await _client.CountAsync(request, cancellationToken).ConfigureAwait(false);
		return response.IsValidResponse ? (int)response.Count : 0;
	}

	/// <summary>
	/// Returns the creation timestamp of the oldest document in the given status, fetching only that
	/// single document (size 1, sorted ascending) instead of paging the whole status partition.
	/// </summary>
	private async ValueTask<DateTimeOffset?> GetOldestCreatedAtAsync(OutboxStatus status, CancellationToken cancellationToken)
	{
		var request = new SearchRequest<ElasticsearchOutboxDocument>(_options.IndexName)
		{
			Size = 1,
			Query = new TermQuery { Field = "status", Value = (int)status },
			Sort = [new SortOptions { Field = new FieldSort("createdAt") { Order = SortOrder.Asc } }],
		};

		var response = await _client.SearchAsync<ElasticsearchOutboxDocument>(request, cancellationToken).ConfigureAwait(false);
		if (!response.IsValidResponse)
		{
			return null;
		}

		var oldest = response.Documents.FirstOrDefault();
		return oldest is null ? null : oldest.CreatedAt;
	}

	private Refresh GetRefresh() =>
		_options.RefreshPolicy == "true" ? Refresh.True
		: _options.RefreshPolicy == "false" ? Refresh.False
		: Refresh.WaitFor;

	private static ElasticsearchOutboxDocument ToDocument(OutboundMessage message) =>
		new()
		{
			Id = message.Id,
			MessageType = message.MessageType,
			PayloadBase64 = Convert.ToBase64String(message.Payload),
			Destination = message.Destination,
			CreatedAt = message.CreatedAt,
			Status = (int)message.Status,
			Priority = message.Priority,
			RetryCount = message.RetryCount,
			CorrelationId = message.CorrelationId,
			CausationId = message.CausationId,
			TenantId = KeyedTenantPartition.FromStoredValue(message.TenantId).TenantId,
			PartitionKey = message.PartitionKey,
			GroupKey = message.GroupKey,
			TargetTransports = message.TargetTransports,
			IsMultiTransport = message.IsMultiTransport,
			LastError = message.LastError,
			ScheduledAt = message.ScheduledAt,
			SentAt = message.SentAt,
			LastAttemptAt = message.LastAttemptAt,
			Headers = message.Headers.Count > 0
				? new Dictionary<string, object>(message.Headers, StringComparer.Ordinal)
				: null,
		};

	/// <summary>
	/// Takes the lease if it is free, on the Elasticsearch node's own clock.
	/// </summary>
	/// <remarks>
	/// Both the comparison and the stamp read one <c>System.currentTimeMillis()</c>, so the value a
	/// dispatcher writes and the value another dispatcher judges it by come from the same clock. Declining
	/// via <c>ctx.op = 'noop'</c> rather than by throwing keeps a live lease an ordinary not-claimed
	/// outcome instead of an error the caller has to interpret.
	/// </remarks>
	private const string ClaimLeaseScript =
		"long now = System.currentTimeMillis(); " +
		"def e = ctx._source.leaseExpiresAt; " +
		"if (e != null && ZonedDateTime.parse(e).toInstant().toEpochMilli() > now) { ctx.op = 'noop'; return; } " +
		"ctx._source.leaseExpiresAt = Instant.ofEpochMilli(now + params.leaseMs).toString(); " +
		"ctx._source.leasedBy = params.owner;";

	/// <summary>
	/// Applies a non-success transition on the Elasticsearch node, stamping every instant it records from the
	/// node's own clock.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The retry floor is the reason this is a script rather than a re-indexed document. The floor is written
	/// by whichever dispatcher observed the failure and read back by whichever dispatcher next polls; a value
	/// computed on the writer's clock and compared against the reader's is a comparison across two machines
	/// that need not agree, in a store whose whole point is that several dispatchers share it. Sending the
	/// floor as a DURATION and letting the node add it to <c>System.currentTimeMillis()</c> leaves one clock
	/// on both sides — the same reading the claim script already uses for the lease.
	/// </para>
	/// <para>
	/// A negative <c>floorMs</c> means "record no floor" (dead-lettering, which is terminal); a negative
	/// <c>retryCount</c> means "do not touch the count". Sentinels rather than absent keys, so the script
	/// never depends on how a missing parameter renders.
	/// </para>
	/// <para>
	/// The monotonic retry-count comparison is made here rather than in the caller for the same reason the
	/// clock is: it is evaluated against the value the node holds at the instant of the write.
	/// </para>
	/// </remarks>
	private const string NonSuccessTransitionScript =
		"long now = System.currentTimeMillis(); " +
		"ctx._source.status = params.status; " +
		"ctx._source.lastError = params.lastError; " +
		"if (params.retryCount >= 0) { def c = ctx._source.retryCount; " +
		"if (c == null || params.retryCount > c) { ctx._source.retryCount = params.retryCount; } } " +
		"ctx._source.lastAttemptAt = Instant.ofEpochMilli(now).toString(); " +
		"if (params.floorMs >= 0) { " +
		"ctx._source.nextAttemptAt = Instant.ofEpochMilli(now + params.floorMs).toString(); } " +
		"ctx._source.remove('leaseExpiresAt'); " +
		"ctx._source.remove('leasedBy');";

	private static OutboundMessage FromDocument(ElasticsearchOutboxDocument doc) =>
		new()
		{
			Id = doc.Id,
			MessageType = doc.MessageType,
			Payload = doc.PayloadBase64 != null ? Convert.FromBase64String(doc.PayloadBase64) : [],
			Destination = doc.Destination,
			CreatedAt = doc.CreatedAt,
			Status = (OutboxStatus)doc.Status,
			Priority = doc.Priority,
			RetryCount = doc.RetryCount,
			CorrelationId = doc.CorrelationId,
			CausationId = doc.CausationId,
			TenantId = KeyedTenantPartition.FromStoredValue(doc.TenantId).TenantId,
			PartitionKey = doc.PartitionKey,
			GroupKey = doc.GroupKey,
			TargetTransports = doc.TargetTransports,
			IsMultiTransport = doc.IsMultiTransport,
			LastError = doc.LastError,
			ScheduledAt = doc.ScheduledAt,
			SentAt = doc.SentAt,
			LastAttemptAt = doc.LastAttemptAt,
			Headers = doc.Headers is { Count: > 0 }
				? new Dictionary<string, object>(doc.Headers, StringComparer.Ordinal)
				: new Dictionary<string, object>(StringComparer.Ordinal),
		};

	/// <summary>
	/// Takes the lease on a candidate, deciding eligibility on the Elasticsearch node's clock.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The search that produced the candidate is only a filter; this is where a message is actually
	/// claimed, and the decision is made entirely inside the script. Painless reads
	/// <c>System.currentTimeMillis()</c> on the node, compares the stored lease against it, and stamps the
	/// new lease from the same reading — so the instant a lease is written and the instant it is judged
	/// come from one clock.
	/// </para>
	/// <para>
	/// This is what the compare-and-swap alone cannot do. The CAS decides which of two SIMULTANEOUS
	/// claimants wins; it says nothing about whether the predicate they raced on was true. Where two
	/// dispatchers' clocks differ by more than the lease timeout they are not simultaneous at all: the
	/// second reads a lease the first is actively delivering under as expired, and its conditional write
	/// then succeeds legitimately, being the only write at that instant. Nothing protects the predicate
	/// except evaluating it somewhere both dispatchers agree on. The CAS is kept as well, so a candidate
	/// that changed between the search and the claim is still refused.
	/// </para>
	/// <para>
	/// A live lease returns a <c>noop</c> result rather than an error, which is the message not being
	/// claimed; a concurrent claimant that got there first surfaces as a 409 and is likewise skipped.
	/// </para>
	/// </remarks>
	/// <param name="messageId">The candidate message.</param>
	/// <param name="leaseTimeoutMs">How long the lease is honoured, in milliseconds.</param>
	/// <param name="owner">This dispatcher's identity.</param>
	/// <param name="seqNo">The sequence number the search returned.</param>
	/// <param name="primaryTerm">The primary term the search returned.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns><see langword="true"/> when this dispatcher took the lease.</returns>
	private Task<bool> TryClaimWithServerClockAsync(
		string messageId,
		long leaseTimeoutMs,
		string owner,
		long? seqNo,
		long? primaryTerm,
		CancellationToken cancellationToken) =>
		TryScriptedWriteWithCasAsync(
			messageId,
			ClaimLeaseScript,
			new Dictionary<string, object>(StringComparer.Ordinal)
			{
				["leaseMs"] = leaseTimeoutMs,
				["owner"] = owner,
			},
			seqNo,
			primaryTerm,
			"claim",
			cancellationToken);

	/// <summary>
	/// Runs a painless script against one document under compare-and-swap, reporting whether it wrote.
	/// </summary>
	/// <remarks>
	/// This is the store's only server-clock mutation path, shared by the claim and by the non-success
	/// transitions so there is exactly one place where "the node decides the time" is implemented. The
	/// compare-and-swap on <c>if_seq_no</c>/<c>if_primary_term</c> and the script guard different things and
	/// are both kept: the CAS refuses a document that changed since the caller read it, while the script is
	/// what evaluates the predicate on a clock every dispatcher shares.
	/// </remarks>
	/// <param name="messageId">The document to write.</param>
	/// <param name="script">The painless source.</param>
	/// <param name="parameters">The script parameters.</param>
	/// <param name="seqNo">The sequence number the caller read.</param>
	/// <param name="primaryTerm">The primary term the caller read.</param>
	/// <param name="operation">The operation named in the failure message.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>
	/// <see langword="true"/> when the script wrote. <see langword="false"/> when it declined
	/// (<c>ctx.op = 'noop'</c>), when the document changed since the caller read it (409), or when it is no
	/// longer there (404) — all three are "this write did not land", which is what every caller acts on.
	/// </returns>
	private async Task<bool> TryScriptedWriteWithCasAsync(
		string messageId,
		string script,
		Dictionary<string, object> parameters,
		long? seqNo,
		long? primaryTerm,
		string operation,
		CancellationToken cancellationToken)
	{
		var response = await _client.UpdateAsync<ElasticsearchOutboxDocument, ElasticsearchOutboxDocument>(
			_options.IndexName,
			messageId,
			u => u
				.Script(sc => sc
					.Source(script)
					.Params(parameters))
				.IfSeqNo(seqNo)
				.IfPrimaryTerm(primaryTerm)
				.Refresh(GetRefresh()),
			cancellationToken).ConfigureAwait(false);

		if (response.IsValidResponse)
		{
			// noop = the script evaluated its guard and declined. Anything else means it wrote.
			return response.Result != Result.NoOp;
		}

		// 409 = the document changed between the caller's read and here. 404 = it is gone, which the store
		// contract treats as a silent no-op rather than an error.
		if (response.ElasticsearchServerError?.Status is 409 or 404)
		{
			return false;
		}

		throw new InvalidOperationException(
			$"Failed to {operation} outbox message '{messageId}': {response.ElasticsearchServerError?.Error?.Reason ?? "Unknown error"}");
	}

	/// <summary>
	/// Writes <paramref name="doc"/> only if the stored document still carries the concurrency tokens the
	/// caller read, returning <see langword="false"/> when it does not.
	/// </summary>
	/// <remarks>
	/// This is the store's only mutation path for an existing document. It replaced an unconditional
	/// full-document overwrite, which made a lost update expressible by ordinary use; a caller now cannot
	/// write without first proving it read the version it is replacing.
	/// </remarks>
	/// <returns>
	/// <see langword="true"/> when the write landed; <see langword="false"/> on a version conflict,
	/// meaning another writer modified the document first.
	/// </returns>
	private async Task<bool> TryWriteWithCasAsync(
		string messageId,
		ElasticsearchOutboxDocument doc,
		long? seqNo,
		long? primaryTerm,
		CancellationToken cancellationToken)
	{
		if (seqNo is null || primaryTerm is null)
		{
			throw new InvalidOperationException(
				$"Cannot safely update outbox message '{messageId}': the read returned no concurrency tokens.");
		}

		var response = await _client.IndexAsync(
			doc,
			idx => idx
				.Index(_options.IndexName)
				.Id(messageId)
				.IfSeqNo(seqNo)
				.IfPrimaryTerm(primaryTerm)
				.Refresh(GetRefresh()),
			cancellationToken).ConfigureAwait(false);

		if (response.IsValidResponse)
		{
			return true;
		}

		// 409 = the document changed since our read. The caller decides whether to retry or stand down.
		if (response.ElasticsearchServerError?.Status == 409)
		{
			return false;
		}

		throw new InvalidOperationException(
			$"Failed to update outbox document: {response.ElasticsearchServerError?.Error?.Reason ?? "Unknown error"}");
	}

	/// <summary>
	/// Applies a non-success transition (failed / dead-lettered) under compare-and-swap, refusing to reopen
	/// a message that has already been marked sent, and standing down when the caller does not hold the
	/// lease it claims to be reporting against.
	/// </summary>
	/// <param name="messageId">The message being transitioned.</param>
	/// <param name="requiredLeaseOwner">
	/// The dispatcher identity that must hold the lease for the transition to be applied, or
	/// <see langword="null"/> to apply it regardless of ownership. A message with no lease at all always
	/// proceeds: it was never claimed, so there is no successor whose claim could be disturbed. Ownership
	/// is re-checked against every fresh read rather than once up front, so a caller that lost its lease
	/// between the first read and a concurrency retry is not admitted on the strength of the stale one.
	/// </param>
	/// <param name="status">The <see cref="OutboxStatus"/> to record.</param>
	/// <param name="lastError">The failure or dead-letter reason to record.</param>
	/// <param name="monotonicRetryCount">
	/// The attempt count to record, applied only when it exceeds the stored one, or <see langword="null"/> to
	/// leave the count alone.
	/// </param>
	/// <param name="floorSeconds">
	/// The failure-anchored re-claim floor F in seconds, or <see langword="null"/> to record no floor. It is
	/// a DURATION: the node adds it to its own clock, so the gate and the claim predicate that reads it back
	/// come from one machine.
	/// </param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns><see langword="true"/> when the transition was applied.</returns>
	/// <remarks>
	/// The read establishes the guards and the write applies the transition, and the compare-and-swap between
	/// them is what makes that pair sound: a document that changed after the read is refused, so a guard
	/// decided on the read's snapshot cannot be honoured against a document that has since moved on. The
	/// write is a partial script rather than a re-indexed document, which is both what lets the node supply
	/// the instants and what keeps a field this model does not know about from being dropped.
	/// </remarks>
	private async Task<bool> TryApplyNonSuccessTransitionAsync(
		string messageId,
		string? requiredLeaseOwner,
		int status,
		string lastError,
		int? monotonicRetryCount,
		int? floorSeconds,
		CancellationToken cancellationToken)
	{
		for (var attempt = 0; attempt < TerminalTransitionMaxRetries; attempt++)
		{
			var get = await _client.GetAsync<ElasticsearchOutboxDocument>(
				_options.IndexName, messageId, cancellationToken).ConfigureAwait(false);

			if (!get.IsValidResponse || !get.Found || get.Source is null)
			{
				// Absent - the message may have been cleaned up. Silent, per the store contract.
				return false;
			}

			if (get.Source.Status == (int)OutboxStatus.Sent)
			{
				// Never resurrect a delivered message: a stale failure report must not re-queue it.
				LogTerminalTransitionSkippedForSentMessage(messageId);
				return false;
			}

			// The == operator on string is an ordinal comparison, which is what a dispatcher identity wants:
			// it is an opaque token, never culture-sensitive text.
			if (requiredLeaseOwner is not null
				&& get.Source.LeasedBy is not null
				&& get.Source.LeasedBy != requiredLeaseOwner)
			{
				// The caller is not entitled to this transition. Silent, like the absent case: such a report is
				// stale rather than erroneous, and the message is left exactly as its owner holds it.
				return false;
			}

			// The script releases the lease as well, so the message is not counted in-flight. What governs
			// the next claim is the floor it stamps, not a lingering lease.
			var parameters = new Dictionary<string, object>(StringComparer.Ordinal)
			{
				["status"] = status,
				["lastError"] = lastError,
				["retryCount"] = monotonicRetryCount ?? -1,
				["floorMs"] = floorSeconds is { } f ? (long)TimeSpan.FromSeconds(f).TotalMilliseconds : -1L,
			};

			if (await TryScriptedWriteWithCasAsync(
					messageId,
					NonSuccessTransitionScript,
					parameters,
					get.SeqNo,
					get.PrimaryTerm,
					"transition",
					cancellationToken).ConfigureAwait(false))
			{
				return true;
			}
		}

		return false;
	}

	// Resolves the lease-owner identifier once: the configured ProcessorId, or a stable generated id.
	// Diagnostic only - claim disjointness is enforced by the compare-and-swap, not by this value.
	private string GetProcessorId() =>
		_resolvedProcessorId ??= string.IsNullOrWhiteSpace(_options.ProcessorId)
			? $"es-outbox-{Environment.MachineName}-{Environment.ProcessId}"
			: _options.ProcessorId;

	[LoggerMessage(DataElasticsearchEventId.DocumentIndexed, LogLevel.Debug,
		"Staged outbox message {MessageId} of type {MessageType} to destination {Destination}")]
	private partial void LogMessageStaged(string messageId, string messageType, string destination);

	[LoggerMessage(DataElasticsearchEventId.DocumentRetrieved, LogLevel.Debug,
		"Enqueued outbox message {MessageId} of type {MessageType}")]
	private partial void LogMessageEnqueued(string messageId, string messageType);

	[LoggerMessage(DataElasticsearchEventId.DocumentUpdated, LogLevel.Debug,
		"Marked outbox message {MessageId} as sent")]
	private partial void LogMessageSent(string messageId);

	[LoggerMessage(DataElasticsearchEventId.OutboxMessagesClaimed, LogLevel.Debug,
		"Claimed {Count} outbox messages for delivery under lease owner {LeaseOwner}")]
	private partial void LogMessagesClaimed(int count, string leaseOwner);

	[LoggerMessage(DataElasticsearchEventId.OutboxTerminalTransitionSkipped, LogLevel.Debug,
		"Skipped a non-success transition for outbox message {MessageId}: it is already marked sent")]
	private partial void LogTerminalTransitionSkippedForSentMessage(string messageId);

	[LoggerMessage(DataElasticsearchEventId.VersionConflict, LogLevel.Warning,
		"Marked outbox message {MessageId} as failed: {ErrorMessage} (retry {RetryCount})")]
	private partial void LogMessageFailed(string messageId, string errorMessage, int retryCount);

	[LoggerMessage(DataElasticsearchEventId.BulkOperationCompleted, LogLevel.Information,
		"Cleaned up {Count} sent outbox messages older than {OlderThan}")]
	private partial void LogMessagesCleanedUp(int count, DateTimeOffset olderThan);

	/// <inheritdoc/>
	public ValueTask DisposeAsync()
	{
		// ElasticsearchClient does not require disposal.
		// This implementation satisfies the IAsyncDisposable contract for consistency
		// with other outbox store implementations and allows future resource cleanup.
		return ValueTask.CompletedTask;
	}
}

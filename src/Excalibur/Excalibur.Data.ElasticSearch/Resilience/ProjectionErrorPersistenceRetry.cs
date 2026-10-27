// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Microsoft.Extensions.Logging;

using Polly;
using Polly.Retry;

namespace Excalibur.Data.ElasticSearch.Resilience;

/// <summary>Scalar retry eligibility policies for create-only projection error records.</summary>
/// <remarks>
/// The generic client pipeline handles exceptions and circuit state; error persistence additionally
/// needs response and per-item classification. Its result is retry eligibility, not write success. It owns its budget and disables SDK retries per request.
/// Only policy is shared: responses, pending records and failure counts stay in each execution's closure.
/// </remarks>
internal static class ProjectionErrorPersistenceRetry
{
	/// <summary>Client errors that are a VERDICT: the request as sent will never succeed.</summary>
	/// <remarks>
	/// Enumerated rather than expressed as a 4xx range, and the direction of that choice is the point. A
	/// range has to be right about every code in it, and it was not: 408 means the server gave up partway
	/// through reading the request, so the write may still have committed -- indeterminate, not a verdict.
	/// 429 is a request to come back. An unlisted code therefore falls through to "unknown" and is
	/// replayed, which wastes at most the attempt budget; the range failed the other way and discarded the
	/// record. When a default is going to be wrong sometimes, it should be wrong in the cheap direction.
	/// </remarks>
	private static bool IsConclusiveClientError(int status) =>
		status is 400 or 401 or 403 or 404 or 405 or 413 or 422;

	/// <summary>The outcomes that SETTLE a create, so that everything else can be treated as unknown.</summary>
	/// <remarks>
	/// Elasticsearch answers a create with 201 when it stored the document and 409 when that id already
	/// exists; either way the record is durable. A conclusive client error settles it the other way. Every
	/// other answer -- a 2xx that is not 201, any 5xx, 408, 429, or no status at all -- is not something
	/// Elasticsearch produces for a create, so it tells us nothing about the document's fate.
	/// </remarks>
	internal static bool IsSettledCreateStatus(int status) =>
		status is 201 or 409 || IsConclusiveClientError(status);

	/// <summary>
	/// Whether a bulk ENVELOPE we could not interpret leaves its records' fate unknown.
	/// </summary>
	/// <remarks>
	/// PRECONDITION: call this only when the response carried no interpretable per-item list. That
	/// precondition is why 200 is unsettled here while it settles a healthy bulk response -- 200 IS the
	/// correct bulk envelope status, and what makes it uninterpretable is the absent item list, which the
	/// caller has already established and which this predicate cannot see. Replaying is safe for the same
	/// reason a create replay is: every operation is a create keyed on an id this process minted, so it
	/// either stores the document or answers conflict, and the pending set is only ever narrowed after a
	/// response passed its shape check.
	/// </remarks>
	internal static bool IsUnsettledBulkEnvelope(int? status) =>
		status is not { } knownStatus || !IsConclusiveClientError(knownStatus);

	/// <summary>What a bulk response says about ONE item, once that item's id has been attributed.</summary>
	internal enum BulkItemOutcome
	{
		/// <summary>Durable: Elasticsearch stored it, or refused it because an earlier attempt stored it.</summary>
		Stored,

		/// <summary>A verdict: this document will never be accepted as sent, so replaying it is pointless.</summary>
		Rejected,

		/// <summary>No interpretable outcome, so the document's fate is unknown and the item is replayed.</summary>
		Unknown,
	}

	/// <summary>Classifies one attributed bulk item, defaulting to unknown rather than to failure.</summary>
	/// <remarks>
	/// PRECONDITION: the response has passed its shape check, so this item's id is one of ours and appears
	/// exactly once. That is what makes selecting a replay subset here legitimate -- the hazard the bulk
	/// path guards against is mis-ATTRIBUTION, not replay, and attribution is already established upstream.
	/// <para>
	/// A conflict counts as STORED, which is specific to this store and not a general truth about creates:
	/// the id is a GUID this execution minted, so the only thing that can have created it is an earlier
	/// attempt of this same execution. Counting it a failure under-reported durable records, and it would
	/// have under-reported them far more once an uninterpretable response became a replay rather than a
	/// discard, because replays are then the normal recovery path rather than a rarity.
	/// </para>
	/// A non-null error alongside a stored status is not interpretable as stored, so it is replayed; an
	/// error alongside a rejection is just the rejection's reason.
	/// </remarks>
	internal static BulkItemOutcome ClassifyBulkItem(int status, bool hasError) =>
		status is 201 or 409
			? hasError ? BulkItemOutcome.Unknown : BulkItemOutcome.Stored
			: IsConclusiveClientError(status) ? BulkItemOutcome.Rejected : BulkItemOutcome.Unknown;

	/// <summary>
	/// Whether a create's outcome is still unknown and must therefore be replayed.
	/// </summary>
	/// <remarks>
	/// A create is keyed on the record id, so it is idempotent: it either stores the document or answers
	/// 409 because an earlier attempt already did. That makes the useful question "did we receive an
	/// outcome Elasticsearch actually produces for a create?" rather than "does this failure look
	/// transient?". Asking the second question requires naming every fault any transport layer might ever
	/// surface, and the set of answers that settle a create is small, closed, and fixed by Elasticsearch's
	/// own contract -- so this asks the first. An acknowledgement we could not interpret, whatever shape it
	/// arrives in, leaves the record's fate unknown and is replayed; replay cannot duplicate it, because a
	/// committed first attempt answers 409 and settles it without overwriting anything.
	/// This takes no exception, deliberately. Classifying the thrown object was a second allowlist wearing
	/// a smaller scope: the shapes a cancellation can take are not enumerable, and the client's own
	/// request timeout arrives as a bare cancellation with no inner exception, so treating "cancelled" as
	/// settled discarded the record on exactly the timeout this contract promises to replay. The caller's
	/// own token is the only witness of the caller's intent, and the pipeline already consults it before
	/// this predicate is reached -- so a withdrawn caller stops the write there, and everything that gets
	/// this far is judged solely on whether Elasticsearch answered.
	/// </remarks>
	internal static bool IsUnsettledCreateOutcome(int? status) =>
		status is not { } knownStatus || !IsSettledCreateStatus(knownStatus);

	internal static ResiliencePipeline<bool> Create(ProjectionRetryOptions settings, int maxAttempts,
		ILogger logger, string operation)
	{
		var builder = new ResiliencePipelineBuilder<bool>();
		if (!settings.Enabled)
		{
			return builder.Build();
		}

		ArgumentOutOfRangeException.ThrowIfLessThan(maxAttempts, 1);
		if (settings.BaseDelay < TimeSpan.Zero || settings.MaxDelay < TimeSpan.Zero)
		{
			throw new ArgumentOutOfRangeException(nameof(settings), "Retry delays must be non-negative.");
		}
		// The JitterFactor range check is gone with the property. It validated the magnitude of a symmetric
		// band that no longer exists; a boolean has no invalid value to reject.

		if (maxAttempts > 1)
		{
			_ = builder.AddRetry(new RetryStrategyOptions<bool>
			{
				MaxRetryAttempts = maxAttempts - 1,
				// A thrown exception means no response was interpreted at all, so the write's outcome is
				// unknown and is replayed -- never classified by the type of the thrown object, which is
				// the allowlist mistake this contract exists to avoid. Both writes are creates keyed on
				// ids this process minted, so a replay stores the document or is refused by the id; it
				// cannot duplicate one. The caller's token is consulted FIRST and conjoined, so a
				// withdrawn caller stops the write here and nothing downstream can override that.
				ShouldHandle = args => ValueTask.FromResult(!args.Context.CancellationToken.IsCancellationRequested &&
					(args.Outcome.Exception is not null || args.Outcome.Result)),
				// The schedule is the strategy's, not ours. This supplied a DelayGenerator computing
				// base * 2^attempt scaled by a symmetric band, 1 + f*(2r-1). A band keeps its mass near the
				// nominal, so hosts that failed in the same instant stayed clustered and retried together
				// -- noise without decorrelation. The generator also forced the cap to be reimplemented,
				// which its own comment conceded: "Custom Polly delay generators must apply their own cap."
				// Dropping it makes MaxDelay live and hands growth, jitter and ceiling to the dependency
				// this package already carries.
				BackoffType = settings.UseExponentialBackoff ? DelayBackoffType.Exponential : DelayBackoffType.Constant,
				Delay = settings.BaseDelay,
				MaxDelay = settings.MaxDelay,
				UseJitter = settings.UseJitter,
				OnRetry = args =>
				{
					logger.LogWarning("Retrying projection error persistence {Operation}: attempt {Attempt}/{MaxAttempts} after {RetryDelay}",
						operation, args.AttemptNumber + 2, maxAttempts, args.RetryDelay);
					return ValueTask.CompletedTask;
				},
			});
		}
		return builder.Build();
	}

}

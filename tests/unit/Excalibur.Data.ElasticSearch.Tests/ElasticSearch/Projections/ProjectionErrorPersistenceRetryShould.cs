// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Collections.Concurrent;
using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

using Elastic.Clients.Elasticsearch;
using Elastic.Transport;

using Excalibur.Data.ElasticSearch;
using Excalibur.Data.ElasticSearch.Projections;
using Excalibur.Data.ElasticSearch.Resilience;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Tests.Shared.Helpers;

namespace Excalibur.Data.Tests.ElasticSearch.Projections;

[Trait("Category", "Unit")]
[Trait("Component", "Data")]
[Trait("Pattern", "Resilience")]
public sealed class ProjectionErrorPersistenceRetryShould
{
	// The five tests that stood here exercised an allowlist of transient exception types which has been
	// deleted. It defaulted to "permanent" on anything it did not enumerate, and for an idempotent create
	// that default discarded the record; four separate data-loss defects came out of it. Each property it
	// asserted is accounted for rather than dropped:
	//
	//   "a known status outranks a secondary transport exception" -- now INEXPRESSIBLE. The replacement
	//     predicate takes no exception parameter at all, so nothing can violate it and no test can defend
	//     it. (PreferKnownHttpStatusToSecondaryTransportFailure, PreferKnownStatusOnSdkTransportExceptionChannel)
	//   "a conclusive 4xx is terminal" -- kept, and widened, by ReplayACreateOnlyUntilElasticsearchSettlesIt
	//     and SettleA408NeitherWay, which also pin the 408 case the old rule got wrong.
	//     (RefuseUnknownHttpAndNonTransportIoFaults)
	//   "only enumerated socket faults retry" -- DELIBERATELY REVERSED. Enumerating which faults deserve a
	//     replay is the defect; a create with no interpretable answer is replayed whatever the fault was.
	//     ReplayAThrownFailureWithinTheBudgetThenPropagateIt pins the new rule and the budget that bounds
	//     it. (RetryOnlyKnownSocketFaultsInsideHttpTransportChain,
	//     DistinguishKnownNetworkFaultsFromUnknownAndCancellationFailures)
	//
	// Caller cancellation is no longer classified from the thrown object either: it is decided by the
	// caller's own token, which PropagateCancellationDuringFirstWriteWithoutRetrying covers.

	[Fact]
	public async Task KeepConcurrentHandlerCallsAndRequestBudgetsIndependent()
	{
		var counters = new ConcurrentDictionary<string, int>(StringComparer.Ordinal);
		using var endpoint = new RecordingEndpoint((_, body) =>
		{
			using var json = JsonDocument.Parse(body);
			var id = json.RootElement.EnumerateObject().Single(static property => property.Name.Equals("documentId", StringComparison.OrdinalIgnoreCase)).Value.GetString()!;
			return counters.AddOrUpdate(id, 1, static (_, count) => count + 1) == 1 ? (503, "{}") : (201, "{\"result\":\"created\"}");
		});
		using var provider = Build(endpoint, true, 3);
		var handler = provider.GetRequiredService<IProjectionErrorHandler>();
		await Task.WhenAll(handler.HandleProjectionErrorAsync(Context("A"), CancellationToken.None),
			handler.HandleProjectionErrorAsync(Context("B"), CancellationToken.None));
		endpoint.Failures.ShouldBeEmpty(endpoint.DiagnosticSummary);
		endpoint.Writes.Count.ShouldBe(4, endpoint.DiagnosticSummary);
		counters["A"].ShouldBe(2);
		counters["B"].ShouldBe(2);
		endpoint.Writes.GroupBy(static request => request.Path).Select(static group => group.Count()).ShouldAllBe(static count => count == 2);
	}

	[Fact]
	public async Task KeepOverlappingExecutionEligibilityAndCountersIndependent()
	{
		using var logs = new CapturingLoggerProvider();
		var pipeline = ProjectionErrorPersistenceRetry.Create(new ProjectionRetryOptions { BaseDelay = TimeSpan.Zero }, 3, logs.CreateLogger("control"), "control");
		var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var firstAttempts = 0;
		var secondAttempts = 0;
		var first = pipeline.ExecuteAsync(async _ =>
		{
			if (Interlocked.Increment(ref firstAttempts) == 1)
			{
				entered.SetResult();
				await release.Task.ConfigureAwait(false);
				return true;
			}
			return false;
		}, CancellationToken.None).AsTask();
		await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
		var second = await pipeline.ExecuteAsync(_ =>
		{
			Interlocked.Increment(ref secondAttempts);
			return ValueTask.FromResult(false);
		}, CancellationToken.None);
		release.SetResult();
		(await first.WaitAsync(TimeSpan.FromSeconds(5))).ShouldBeFalse();
		second.ShouldBeFalse();
		firstAttempts.ShouldBe(2);
		secondAttempts.ShouldBe(1);
	}

	[Fact]
	public async Task ReplayAThrownFailureWithinTheBudgetThenPropagateIt()
	{
		// Renamed from PropagateTerminalExceptionAfterRetryEligibleResult, because there is no longer a
		// "terminal exception" category here and the old name asserted the defect. A thrown exception
		// means no response was interpreted, so the outcome is UNKNOWN and is replayed -- never judged by
		// the type of the thrown object, which is the allowlist mistake that discarded records. What makes
		// it terminate is the BUDGET, and what this pins is that the budget is finite and the exception
		// still reaches the caller rather than being swallowed by the retries.
		using var logs = new CapturingLoggerProvider();
		var pipeline = ProjectionErrorPersistenceRetry.Create(new ProjectionRetryOptions { BaseDelay = TimeSpan.Zero }, 3, logs.CreateLogger("control"), "control");
		var attempts = 0;
		await Should.ThrowAsync<InvalidOperationException>(() => pipeline.ExecuteAsync(_ =>
		{
			if (++attempts == 1) { return ValueTask.FromResult(true); }
			throw new InvalidOperationException("failure after a retry-eligible result");
		}, CancellationToken.None).AsTask());
		attempts.ShouldBe(3);
	}

	[Fact]
	public void KeepZeroDelayAndCapPositiveDelayAtVeryHighAttemptCounts()
	{
		ProjectionErrorPersistenceRetry.CalculateDelay(new ProjectionRetryOptions
		{
			BaseDelay = TimeSpan.Zero, MaxDelay = TimeSpan.FromSeconds(30),
		}, int.MaxValue, 0.5).ShouldBe(TimeSpan.Zero);
		ProjectionErrorPersistenceRetry.CalculateDelay(new ProjectionRetryOptions
		{
			BaseDelay = TimeSpan.FromSeconds(1), MaxDelay = TimeSpan.FromSeconds(30),
		}, int.MaxValue, 0.5).ShouldBe(TimeSpan.FromSeconds(30));
		ProjectionErrorPersistenceRetry.CalculateDelay(new ProjectionRetryOptions
		{
			BaseDelay = TimeSpan.FromSeconds(1), MaxDelay = TimeSpan.FromSeconds(30), JitterFactor = 1,
		}, int.MaxValue, 0).ShouldBe(TimeSpan.Zero);
	}

	[Fact]
	public async Task PropagateCancellationDuringFirstWriteWithoutRetrying()
	{
		using var cancelled = new CancellationTokenSource();
		using var endpoint = new RecordingEndpoint((_, _) => (503, "{}"));
		endpoint.OnWrite = (_, _) => cancelled.Cancel();
		using var provider = Build(endpoint, true, 3);
		await Should.ThrowAsync<OperationCanceledException>(() => provider.GetRequiredService<IProjectionErrorHandler>()
			.HandleProjectionErrorAsync(Context(), cancelled.Token));
		endpoint.Writes.Count.ShouldBe(1);
	}

	[Theory]
	// Elasticsearch's own create outcomes settle the write; everything else leaves it unknown. The
	// SETTLED rows are the liveness half: a stored record must not be replayed, or a success loops.
	[InlineData(201, false)]  // created
	[InlineData(409, false)]  // the id already exists, so an earlier attempt stored it
	[InlineData(400, false)]  // malformed document: replaying cannot help
	[InlineData(401, false)]
	[InlineData(403, false)]
	[InlineData(404, false)]
	[InlineData(413, false)]  // any conclusive client error, not just the ones we thought to name
	// The UNSETTLED rows are the safety half: no interpretable create outcome reached us.
	[InlineData(200, true)]   // Elasticsearch never answers a create with 200, so this is not its answer
	[InlineData(429, true)]   // a request to come back, not a verdict
	[InlineData(500, true)]
	[InlineData(502, true)]
	[InlineData(503, true)]
	[InlineData(504, true)]
	[InlineData(null, true)]  // no status at all
	public void ReplayACreateOnlyUntilElasticsearchSettlesIt(int? status, bool unsettled) =>
		ProjectionErrorPersistenceRetry.IsUnsettledCreateOutcome(status).ShouldBe(unsettled);

	[Theory]
	// The bulk ENVELOPE has its own settled set, and 200 is the reason it cannot share the create's.
	// 200 is the CORRECT status for a healthy bulk response; it is unsettled here only because this
	// predicate is reached solely when the response carried no interpretable item list.
	[InlineData(400, false)]
	[InlineData(422, false)]
	[InlineData(200, true)]   // a healthy bulk status, but no item list arrived with it
	[InlineData(201, true)]   // not a bulk envelope answer at all
	[InlineData(408, true)]
	[InlineData(429, true)]
	[InlineData(503, true)]
	[InlineData(null, true)]
	public void ReplayABulkEnvelopeItCouldNotInterpret(int? status, bool unsettled) =>
		ProjectionErrorPersistenceRetry.IsUnsettledBulkEnvelope(status).ShouldBe(unsettled);

	[Theory]
	// A bulk ITEM is settled only by a stored outcome or a verdict. The three Unknown rows below were
	// each counted a PERMANENT failure by the rule this replaced, so each one is a record that was
	// discarded on no evidence -- and a per-item 5xx is the commonest real bulk partial failure.
	[InlineData(201, false, "Stored")]
	[InlineData(409, false, "Stored")]
	[InlineData(400, true, "Rejected")]
	[InlineData(422, true, "Rejected")]
	[InlineData(408, false, "Unknown")]
	[InlineData(500, false, "Unknown")]
	[InlineData(0, false, "Unknown")]
	[InlineData(429, false, "Unknown")]
	[InlineData(503, false, "Unknown")]
	// Takes the expectation as a name, not the enum: the enum is internal and a public xUnit method
	// must not widen it. The test project reaches it via InternalsVisibleTo, not via a visibility change.
	public void ReplayABulkItemItCouldNotInterpret(int status, bool hasError, string expected) =>
		ProjectionErrorPersistenceRetry.ClassifyBulkItem(status, hasError).ToString().ShouldBe(expected);

	[Fact]
	public void CountAConflictAsStoredBecauseOnlyThisExecutionMintsTheId()
	{
		// The record id is Guid.NewGuid() minted by this handler, so nothing else can have created it:
		// a conflict is proof that an EARLIER ATTEMPT OF THIS EXECUTION stored the document. Counting it
		// a failure under-reported durable records, and would under-report them far more now that an
		// uninterpretable response is replayed rather than discarded, which makes replay the normal path.
		ProjectionErrorPersistenceRetry.ClassifyBulkItem(409, false)
			.ShouldBe(ProjectionErrorPersistenceRetry.BulkItemOutcome.Stored);

		// A stored status carrying an error is not interpretable as stored, so it is replayed. An error
		// alongside a verdict is merely that verdict's reason and does not change it.
		ProjectionErrorPersistenceRetry.ClassifyBulkItem(409, true)
			.ShouldBe(ProjectionErrorPersistenceRetry.BulkItemOutcome.Unknown);
		ProjectionErrorPersistenceRetry.ClassifyBulkItem(201, true)
			.ShouldBe(ProjectionErrorPersistenceRetry.BulkItemOutcome.Unknown);
	}

	[Fact]
	public void SettleA408NeitherWay()
	{
		// 408 means the server gave up partway through READING the request, so the document may already
		// be stored. Expressing the conclusive client errors as a 4xx range swept 408 in and discarded
		// the record; it is enumerated now precisely so this case falls through to "unknown".
		ProjectionErrorPersistenceRetry.IsUnsettledCreateOutcome(408).ShouldBeTrue();
		ProjectionErrorPersistenceRetry.IsUnsettledBulkEnvelope(408).ShouldBeTrue();
		ProjectionErrorPersistenceRetry.IsSettledCreateStatus(408).ShouldBeFalse();
	}

	[Theory]
	// A severed acknowledgement has two shapes and the platform decides which one we see: a reset before
	// any status (Windows) and a status whose body never arrives (Linux, macOS). Both leave the create's
	// outcome unknown, so both must be replayed. Covering only the first made this test pass on Windows
	// and fail on every other platform.
	[InlineData(-1)]
	[InlineData(200)]
	public async Task RecoverDroppedAcknowledgementWithinSameCreateBudget(int severance)
	{
		using var endpoint = new RecordingEndpoint((attempt, _) => attempt == 1 ? (severance, "") : (409, "{}"));
		using var provider = Build(endpoint, true, 3);
		await provider.GetRequiredService<IProjectionErrorHandler>().HandleProjectionErrorAsync(Context(), CancellationToken.None);
		var writes = endpoint.Writes.ToArray();
		writes.Length.ShouldBe(2, endpoint.DiagnosticSummary);
		writes[0].Path.ShouldBe(writes[1].Path);
		writes[0].Path.ShouldContain("/_create/");
		endpoint.Logs.Entries.Count(static entry => entry.Message.StartsWith("Retrying projection error persistence", StringComparison.Ordinal)).ShouldBe(1);
	}

	[Fact]
	public async Task PinAliasAcrossAmbiguousCommitAndConcurrentResolution()
	{
		var exists = false;
		var resolved = false;
		using var endpoint = new RecordingEndpoint((attempt, _) =>
		{
			if (attempt == 1)
			{
				resolved = true;
				return (503, "{}");
			}
			return (409, "{}");
		});
		endpoint.OnWrite = (path, _) =>
		{
			if (!exists) { exists = true; resolved = false; }
			else if (path.Contains("/_doc/", StringComparison.Ordinal)) { resolved = false; }
		};
		endpoint.Metadata = _ => endpoint.MetadataReads > 1
			? "{\"physical-errors-v2\":{\"aliases\":{\"custom-errors\":{\"is_write_index\":true}}}}"
			: "{\"physical-errors-v1\":{\"aliases\":{\"custom-errors\":{\"is_write_index\":true}}}}";
		using var provider = Build(endpoint, true, 3);
		await provider.GetRequiredService<IProjectionErrorHandler>().HandleProjectionErrorAsync(Context(), CancellationToken.None);
		// First create committed but its acknowledgement was lost; the existing record was resolved.
		// The endpoint would point another metadata lookup at v2. There must be no second lookup or overwrite.
		endpoint.MetadataReads.ShouldBe(1);
		var writes = endpoint.Writes.ToArray();
		writes.Length.ShouldBe(2);
		writes.All(static write => write.Path.StartsWith("/physical-errors-v1/_create/", StringComparison.Ordinal)).ShouldBeTrue();
		writes[0].Path.ShouldBe(writes[1].Path);
		resolved.ShouldBeTrue("an existing resolved error must not be overwritten by replaying an old create");
	}

	[Fact]
	public async Task RejectAmbiguousAliasBeforeCreatingRecords()
	{
		using var endpoint = new RecordingEndpoint((_, _) => (201, "{}"));
		endpoint.Metadata = static _ => "{\"v1\":{\"aliases\":{\"custom-errors\":{}}},\"v2\":{\"aliases\":{\"custom-errors\":{}}}}";
		using var provider = Build(endpoint, true, 3);
		await provider.GetRequiredService<IProjectionErrorHandler>().HandleProjectionErrorAsync(Context(), CancellationToken.None);
		endpoint.MetadataReads.ShouldBe(1);
		endpoint.Writes.ShouldBeEmpty();
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task PreserveLegacyConfigurationWithExplicitManagementPrecedence(bool explicitOverride)
	{
		using var endpoint = new RecordingEndpoint((_, _) => (201, "{\"result\":\"created\"}"));
		using var provider = Build(endpoint, true, 3, explicitOverride, true);
		await provider.GetRequiredService<IProjectionErrorHandler>().HandleProjectionErrorAsync(Context(), CancellationToken.None);
		endpoint.Writes.Single().Path.ShouldStartWith(explicitOverride ? "/custom-errors/_create/" : "/legacy-errors/_create/");
	}

	[Theory]
	[InlineData(true, 0, 0.0, 800)]
	[InlineData(true, 0, 1.0, 1200)]
	[InlineData(true, 1, 0.5, 2000)]
	[InlineData(true, 10, 1.0, 2500)]
	[InlineData(false, 10, 0.5, 1000)]
	public void HonorJitterMagnitudeBackoffAndCap(bool exponential, int attempt, double sample, int milliseconds)
	{
		ProjectionErrorPersistenceRetry.CalculateDelay(new ProjectionRetryOptions
		{
			BaseDelay = TimeSpan.FromSeconds(1), MaxDelay = TimeSpan.FromMilliseconds(2500),
			UseExponentialBackoff = exponential, JitterFactor = 0.2,
		}, attempt, sample).ShouldBe(TimeSpan.FromMilliseconds(milliseconds));
	}

	[Fact]
	public void RejectInvalidDelayAndNonFiniteJitter()
	{
		var validator = new ElasticSearchProjectionManagementOptionsValidator();
		validator.Validate(null, new ElasticSearchProjectionManagementOptions { RetryPolicy = new ProjectionRetryOptions { BaseDelay = TimeSpan.FromSeconds(-1) } }).Failed.ShouldBeTrue();
		validator.Validate(null, new ElasticSearchProjectionManagementOptions { RetryPolicy = new ProjectionRetryOptions { JitterFactor = double.NaN } }).Failed.ShouldBeTrue();
	}

	[Fact]
	public async Task RecordingTransportAcceptsHeadAndActualSdkWrite()
	{
		using var endpoint = new RecordingEndpoint((_, _) => (201, "{\"result\":\"created\"}"));
		var client = new ElasticsearchClient(new ElasticsearchClientSettings(endpoint.Uri).DisablePing().RequestTimeout(TimeSpan.FromSeconds(5)));
		var exists = await client.Indices.ExistsAsync("control", CancellationToken.None);
		exists.Exists.ShouldBeTrue();
		var write = await client.IndexAsync(new { Id = "control" }, d => d.Index("control"), CancellationToken.None);
		endpoint.Failures.ShouldBeEmpty();
		write.IsValidResponse.ShouldBeTrue(write.DebugInformation);
		endpoint.Writes.Count.ShouldBe(1);
	}

	[Theory]
	[InlineData(true, 3, 503, 3)]
	[InlineData(false, 3, 503, 1)]
	[InlineData(true, 1, 503, 1)]
	[InlineData(true, 3, 400, 1)]
	[InlineData(true, 3, 401, 1)]
	[InlineData(true, 3, 409, 1)]
	[InlineData(true, 3, 429, 3)]
	public async Task HonorConfiguredTotalAttemptsAndPermanentRejections(bool enabled, int attempts, int status, int expected)
	{
		using var endpoint = new RecordingEndpoint((_, _) => (status, "{}"));
		using var provider = Build(endpoint, enabled, attempts);
		await provider.GetRequiredService<IProjectionErrorHandler>().HandleProjectionErrorAsync(Context(), CancellationToken.None);
		endpoint.Failures.ShouldBeEmpty();
		endpoint.Writes.Count.ShouldBe(expected, endpoint.DiagnosticSummary);
		endpoint.Writes.All(static r => r.Path.StartsWith("/custom-errors/_create/", StringComparison.Ordinal)).ShouldBeTrue();
		endpoint.Writes.Select(static r => r.Path).Distinct(StringComparer.Ordinal).Count().ShouldBe(1);
		endpoint.Writes.Select(static r => r.Body).Distinct(StringComparer.Ordinal).Count().ShouldBe(1);
		var writes = endpoint.BoundRequests.Where(static request => request.Path.Contains("/_create/", StringComparison.Ordinal)).ToArray();
		writes.Length.ShouldBe(expected);
		writes.All(static request => request.Retries == 0 && request.ForcedNode is not null).ShouldBeTrue();
		provider.GetRequiredService<ElasticsearchClient>().ElasticsearchClientSettings.MaxRetries.ShouldBe(4);
		if (expected > 1)
		{
			writes.Select(static request => request.ForcedNode!.Port).Distinct().Count().ShouldBe(2);
		}
		var retries = endpoint.Logs.Entries.Where(static entry => entry.Message.StartsWith("Retrying projection error persistence", StringComparison.Ordinal)).ToArray();
		retries.Length.ShouldBe(expected - 1);
		if (retries.Length > 0)
		{
			retries[^1].Message.ShouldContain($"attempt {expected}/{attempts}");
		}
	}

	[Fact]
	public async Task RecoverTransientFailureWithoutOverwritingAnExistingError()
	{
		using var endpoint = new RecordingEndpoint((attempt, _) => attempt == 1 ? (503, "{}") : (201, "{\"result\":\"created\"}"));
		using var provider = Build(endpoint, true, 4);
		await provider.GetRequiredService<IProjectionErrorHandler>().HandleProjectionErrorAsync(Context(), CancellationToken.None);
		endpoint.Writes.Count.ShouldBe(2);
		endpoint.BoundRequests.Where(static request => request.Path.Contains("/_create/", StringComparison.Ordinal))
			.Select(static request => request.ForcedNode!.Port).Distinct().Count().ShouldBe(2);
	}

	[Fact]
	public async Task PropagateAlreadyCancelledCallerWithoutSendingRequests()
	{
		using var endpoint = new RecordingEndpoint((_, _) => (503, "{}"));
		using var provider = Build(endpoint, true, 3);
		using var cancelled = new CancellationTokenSource();
		cancelled.Cancel();
		await Should.ThrowAsync<OperationCanceledException>(() => provider.GetRequiredService<IProjectionErrorHandler>()
			.HandleProjectionErrorAsync(Context(), cancelled.Token));
		endpoint.Writes.ShouldBeEmpty();
	}

	[Theory]
	[InlineData(200)]
	[InlineData(503)]
	// 500 and 507 are the rows that matter, and nothing covered them. A status guard stood on this path
	// enumerating which envelope codes were transient (429, 502, 503, 504) and treating every other 4xx
	// and 5xx as a verdict, so an envelope like these -- arriving WITH a perfectly usable item list --
	// ended the loop and discarded the batch. The per-item outcomes below are where the answer actually
	// is; the envelope status says nothing once the items parsed. Both rows RED if that guard returns.
	[InlineData(500)]
	[InlineData(507)]
	public async Task RetryOnlyTransientFailedBulkItems(int firstStatus)
	{
		using var endpoint = new RecordingEndpoint((attempt, body) =>
		{
			var ids = body.Split('\n', StringSplitOptions.RemoveEmptyEntries).Where((_, index) => index % 2 == 0)
				.Select(line => JsonDocument.Parse(line).RootElement.EnumerateObject().Single().Value.GetProperty("_id").GetString()).ToArray();
			var items = ids.Select((id, index) => new { create = new { _id = id, status = attempt == 1 ? index switch { 0 => 201, 1 => 400, _ => 429 } : 201 } });
			return (attempt == 1 ? firstStatus : 200, JsonSerializer.Serialize(new { errors = attempt == 1, items }));
		});
		using var provider = Build(endpoint, true, 3);
		await provider.GetRequiredService<IProjectionErrorHandler>().HandleBulkOperationErrorsAsync(new BulkOperationErrorContext
		{
			ProjectionType = "orders", OperationType = "BulkIndex", IndexName = "orders", TotalDocuments = 3, SuccessfulDocuments = 0,
			Failures = Enumerable.Range(0, 3).Select(i => new BulkOperationFailure { DocumentId = i.ToString(), ErrorMessage = "failed" }).ToArray(),
		}, CancellationToken.None);
		var writes = endpoint.Writes.ToArray();
		endpoint.Failures.ShouldBeEmpty(endpoint.DiagnosticSummary);
		writes.Length.ShouldBe(2, endpoint.DiagnosticSummary);
		writes[0].Body.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length.ShouldBe(6);
		writes[1].Body.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length.ShouldBe(2);
		var first = writes[0].Body.Split('\n', StringSplitOptions.RemoveEmptyEntries);
		writes[1].Body.ShouldContain(first[4]);
		writes[1].Body.ShouldNotContain(first[0]);
		writes[1].Body.ShouldNotContain(first[2]);
		endpoint.Logs.Entries.ShouldContain(static entry => entry.Message.Contains("Unable to confirm storage of 1/3", StringComparison.Ordinal));
	}

	[Fact]
	public async Task ReplayAnIncompleteBulkResponseBecauseAbsenceIsNotEvidence()
	{
		// A response listing FEWER items than we sent, every one of them ours, says nothing about the
		// records it omitted. That is the same physical event as a severed response -- loss omits, it
		// cannot invent -- so the omitted records must be replayed rather than counted as failures.
		// This arm is the sixth and last site where an absence of evidence was read as a verdict.
		var attempts = 0;
		using var endpoint = new RecordingEndpoint((attempt, body) =>
		{
			attempts = attempt;
			var ids = body.Split('\u000A', StringSplitOptions.RemoveEmptyEntries).Where((_, index) => index % 2 == 0)
				.Select(line => JsonDocument.Parse(line).RootElement.EnumerateObject().Single().Value.GetProperty("_id").GetString()).ToArray();
			// Attempt 1 answers about only the first of the two records it was sent; attempt 2 answers fully.
			var items = ids.Select(id => new { create = new { _id = id, status = 201 } });
			return (200, JsonSerializer.Serialize(new { errors = attempt == 1, items = attempt == 1 ? items.Take(1) : items }));
		});
		using var provider = Build(endpoint, true, 3);
		await provider.GetRequiredService<IProjectionErrorHandler>().HandleBulkOperationErrorsAsync(new BulkOperationErrorContext
		{
			ProjectionType = "orders", OperationType = "BulkIndex", IndexName = "orders", TotalDocuments = 2, SuccessfulDocuments = 0,
			Failures = [new BulkOperationFailure { DocumentId = "1", ErrorMessage = "failed" }, new BulkOperationFailure { DocumentId = "2", ErrorMessage = "failed" }],
		}, CancellationToken.None);
		attempts.ShouldBe(2, endpoint.DiagnosticSummary);
		endpoint.Writes.Count.ShouldBe(2, endpoint.DiagnosticSummary);
	}

	[Theory]
	// These three are UNATTRIBUTABLE: the response talks about something we did not send, so it is
	// evidence about the responder rather than about our records, and a bounded retry cannot change who
	// answered. `duplicate-id` belongs here even though every id in it is ours -- answering the same
	// question twice is incoherent, not partial, and loss cannot produce it.
	[InlineData("wrong-operation")]
	[InlineData("unexpected-id")]
	[InlineData("duplicate-id")]
	public async Task RefuseMalformedBulkAuthority(string defect)
	{
		using var endpoint = new RecordingEndpoint((_, body) =>
		{
			var ids = body.Split('\n', StringSplitOptions.RemoveEmptyEntries).Where((_, index) => index % 2 == 0)
				.Select(line => JsonDocument.Parse(line).RootElement.EnumerateObject().Single().Value.GetProperty("_id").GetString()).ToArray();
			var items = ids.Select((id, index) => new Dictionary<string, object>
			{
				[defect == "wrong-operation" ? "index" : "create"] = new { _id = defect == "unexpected-id" ? "unknown" : defect == "duplicate-id" ? ids[0] : id, status = 429 },
			}).ToArray();
			return (200, JsonSerializer.Serialize(new { errors = true, items = defect == "missing-item" ? items.Take(1).ToArray() : items }));
		});
		using var provider = Build(endpoint, true, 3);
		await provider.GetRequiredService<IProjectionErrorHandler>().HandleBulkOperationErrorsAsync(new BulkOperationErrorContext
		{
			ProjectionType = "orders", OperationType = "BulkIndex", IndexName = "orders", TotalDocuments = 2, SuccessfulDocuments = 0,
			Failures = [new BulkOperationFailure { DocumentId = "1", ErrorMessage = "failed" }, new BulkOperationFailure { DocumentId = "2", ErrorMessage = "failed" }],
		}, CancellationToken.None);
		endpoint.Writes.Count.ShouldBe(1);
	}

	private static ProjectionErrorContext Context(string? documentId = null) => new()
	{
		ProjectionType = "orders", OperationType = "Index", IndexName = "orders", DocumentId = documentId, Exception = new InvalidOperationException("failed"),
	};

	private static ServiceProvider Build(RecordingEndpoint endpoint, bool enabled, int attempts, bool explicitOverride = true, bool legacy = false)
	{
		var values = new Dictionary<string, string?>
		{
			["ElasticSearch:ProjectionManagement:ErrorHandling:ErrorIndexName"] = "custom-errors",
			["ElasticSearch:ProjectionManagement:RetryPolicy:Enabled"] = enabled.ToString(),
			["ElasticSearch:ProjectionManagement:RetryPolicy:MaxIndexAttempts"] = attempts.ToString(),
			["ElasticSearch:ProjectionManagement:RetryPolicy:MaxBulkAttempts"] = attempts.ToString(),
			["ElasticSearch:ProjectionManagement:RetryPolicy:BaseDelay"] = "00:00:00",
			["ElasticSearch:ProjectionManagement:RetryPolicy:MaxDelay"] = "00:00:00",
			["ElasticSearch:ProjectionManagement:RetryPolicy:JitterFactor"] = "0",
		};
		if (legacy)
		{
			values["ElasticSearch:Projections:ErrorHandling:ErrorIndexName"] = "legacy-errors";
		}
		if (!explicitOverride)
		{
			values.Remove("ElasticSearch:ProjectionManagement:ErrorHandling:ErrorIndexName");
		}
		var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
		var services = new ServiceCollection();
		services.AddLogging(builder => builder.AddProvider(endpoint.Logs));
		services.AddMetrics();
		// Multiple nodes and a nonzero SDK default expose accidental retry multiplication.
		services.AddSingleton(new ElasticsearchClient(new ElasticsearchClientSettings(new Elastic.Transport.StaticNodePool([endpoint.Uri, endpoint.AlternateUri]), new RecordingInvoker(endpoint))
			.MaximumRetries(4).DisablePing().RequestTimeout(TimeSpan.FromSeconds(5))));
		services.AddElasticSearchProjectionManagement(configuration);
		return services.BuildServiceProvider();
	}

	private sealed class RecordingEndpoint : IDisposable
	{
		private readonly HttpListener _listener = new();
		private readonly Func<int, string, (int Status, string Body)> _answer;
		private int _attempt;
		public RecordingEndpoint(Func<int, string, (int Status, string Body)> answer)
		{
			_answer = answer;
			using var socket = new TcpListener(IPAddress.Loopback, 0);
			socket.Start();
			var port = ((IPEndPoint)socket.LocalEndpoint).Port;
			socket.Stop();
			Uri = new Uri($"http://127.0.0.1:{port}/");
			socket.Start();
			var alternatePort = ((IPEndPoint)socket.LocalEndpoint).Port;
			socket.Stop();
			AlternateUri = new Uri($"http://127.0.0.1:{alternatePort}/");
			_listener.Prefixes.Add(Uri.ToString());
			_listener.Prefixes.Add(AlternateUri.ToString());
			_listener.Start();
			_ = Task.Run(ServeAsync);
		}
		public Uri Uri { get; }
		public Uri AlternateUri { get; }
		public CapturingLoggerProvider Logs { get; } = new();
		public Func<string, string>? Metadata { get; set; }
		public Action<string, string>? OnWrite { get; set; }
		public int MetadataReads { get; private set; }
		public ConcurrentQueue<(string Path, string Body)> Writes { get; } = new();
		public ConcurrentQueue<Exception> Failures { get; } = new();
		public ConcurrentQueue<string> Calls { get; } = new();
		public ConcurrentQueue<string> Responses { get; } = new();
		public ConcurrentQueue<(string Path, int Retries, Uri? ForcedNode)> BoundRequests { get; } = new();
		public string DiagnosticSummary => string.Join(" | ", Calls.Concat(Writes.Select(static write => write.Path + " " + write.Body))
			.Concat(Responses)
			.Concat(Failures.Select(static error => error.ToString()))
			.Concat(Logs.Entries.Select(static entry => entry.Message + " " + entry.Exception)));
		public void Dispose() => _listener.Close();
		private async Task ServeAsync()
		{
			try { await ServeRequestsAsync().ConfigureAwait(false); }
			catch (Exception ex) when (ex is not ObjectDisposedException) { Failures.Enqueue(ex); }
		}
		private async Task ServeRequestsAsync()
		{
			while (_listener.IsListening)
			{
				HttpListenerContext context;
				try { context = await _listener.GetContextAsync().ConfigureAwait(false); }
				catch (Exception ex) when (ex is HttpListenerException or ObjectDisposedException) { return; }
				var path = context.Request.Url!.AbsolutePath;
				Calls.Enqueue(context.Request.HttpMethod + " " + path);
				var response = (Status: 200, Body: string.Empty);
				if (context.Request.HttpMethod == "GET")
				{
					MetadataReads++;
					response = (200, Metadata?.Invoke(path) ?? JsonSerializer.Serialize(new Dictionary<string, object> { [path.Trim('/')] = new { aliases = new { } } }));
				}
				else if (context.Request.HttpMethod != "HEAD")
				{
					using var input = context.Request.Headers["Content-Encoding"]?.ToUpperInvariant() switch
					{
						"GZIP" => new GZipStream(context.Request.InputStream, CompressionMode.Decompress),
						"DEFLATE" => new DeflateStream(context.Request.InputStream, CompressionMode.Decompress),
						"BR" => new BrotliStream(context.Request.InputStream, CompressionMode.Decompress),
						null or "" or "IDENTITY" => context.Request.InputStream,
						_ => throw new InvalidOperationException("Unsupported fixture request content encoding."),
					};
					using var reader = new StreamReader(input);
					var body = await reader.ReadToEndAsync().ConfigureAwait(false);
					Writes.Enqueue((path, body));
					OnWrite?.Invoke(path, body);
					response = _answer(Interlocked.Increment(ref _attempt), body);
				}
				context.Response.Headers["X-Elastic-Product"] = "Elasticsearch";
				context.Response.ContentType = "application/json";
				if (response.Status == -1)
				{
					// Drop before sending a status: known 201 is terminal even if its body later fails.
					context.Response.Abort();
					continue;
				}
				context.Response.StatusCode = response.Status;
				var bytes = Encoding.UTF8.GetBytes(response.Body);
				context.Response.ContentLength64 = bytes.Length;
				if (bytes.Length > 0)
				{
					await context.Response.OutputStream.WriteAsync(bytes).ConfigureAwait(false);
				}
				context.Response.Close();
			}
		}
	}

	private sealed class RecordingInvoker(RecordingEndpoint recorder) : IRequestInvoker
	{
		private readonly HttpRequestInvoker _inner = new();

		public ResponseFactory ResponseFactory => _inner.ResponseFactory;

		public async Task<TResponse> RequestAsync<TResponse>(Endpoint endpoint, BoundConfiguration boundConfiguration, PostData? postData,
			CancellationToken cancellationToken)
			where TResponse : TransportResponse, new()
		{
			recorder.BoundRequests.Enqueue((endpoint.PathAndQuery, boundConfiguration.MaxRetries, boundConfiguration.ForceNode));
			try
			{
				var response = await _inner.RequestAsync<TResponse>(endpoint, boundConfiguration, postData, cancellationToken).ConfigureAwait(false);
				RecordResponse(endpoint, response);
				return response;
			}
			catch (Exception exception)
			{
				recorder.Responses.Enqueue(endpoint.PathAndQuery + " threw " + exception);
				throw;
			}
		}

		public TResponse Request<TResponse>(Endpoint endpoint, BoundConfiguration boundConfiguration, PostData? postData)
			where TResponse : TransportResponse, new()
		{
			recorder.BoundRequests.Enqueue((endpoint.PathAndQuery, boundConfiguration.MaxRetries, boundConfiguration.ForceNode));
			var response = _inner.Request<TResponse>(endpoint, boundConfiguration, postData);
			RecordResponse(endpoint, response);
			return response;
		}

		private void RecordResponse(Endpoint endpoint, TransportResponse response)
		{
			var details = response.ApiCallDetails;
			recorder.Responses.Enqueue(endpoint.PathAndQuery + " status=" + details?.HttpStatusCode + " exception=" + details?.OriginalException);
		}

		public void Dispose() => _inner.Dispose();
	}
}

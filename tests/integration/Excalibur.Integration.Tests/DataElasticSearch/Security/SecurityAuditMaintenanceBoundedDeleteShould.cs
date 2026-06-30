// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;

using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.QueryDsl;

using Excalibur.AuditLogging;
using Excalibur.Data.ElasticSearch.Security;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Integration.Tests.DataElasticSearch.Security;

/// <summary>
/// bd-5jo6tm-ES (S853 review P3) — independent regression lock (author≠impl, TestsDeveloper) for
/// <c>SecurityAuditMaintenanceService</c> BOUNDED + PAGINATED archived-event deletion against a real
/// Elasticsearch (TestContainers).
/// <para>
/// The catastrophic unbounded <c>DeleteByQuery(MatchAll)</c> was ALREADY ID-filtered (the delete only
/// targets the IDs actually written to the archive). This lock targets the SUBSEQUENT hardening
/// (<c>DeleteArchivedEventsByIdsAsync</c>): the confirmed-archived IDs are paginated into chunks of
/// <c>batchSize = MaxQueryResultSize</c>, and each <c>DeleteByQuery</c> is capped with
/// <c>MaxDocs(chunk.Length)</c> — so no single mass-delete is ever issued AND the paginated loop drains
/// every archived document across passes.
/// </para>
/// <para>
/// Non-vacuity (RED on the realistic hardening-absent variant): with <c>batchSize = 2</c> and FIVE
/// events at/below the cutoff (plus two in-retention events that must survive), the correct paginated
/// impl deletes all five old events across three passes ([2, 2, 1]) and leaves exactly the two recent
/// events. A regression that caps a SINGLE pass at <c>MaxDocs(batchSize)</c> WITHOUT pagination would
/// delete only two of the five old events, leaving three archived-but-undeleted documents (a silent
/// archive/index inconsistency) → five would remain instead of two → RED. The lock simultaneously
/// guards against over-deletion: the two in-retention events must remain.
/// </para>
/// <para>
/// Scope note (honest limitation): a single <i>fully-unbounded</i> <c>DeleteByQuery(Ids = allArchived)</c>
/// with no <c>MaxDocs</c> would yield the same end-state (two remain) and so is NOT caught by an
/// end-state assertion — but that fully-unbounded-yet-ID-filtered shape is the ALREADY-fixed case, not
/// the hardening this lock guards. The per-call <c>MaxDocs</c> cap is a resource-safety property not
/// observable through ES end-state; the observable invariant asserted here is the bounded-PAGINATION
/// drain (all archived deleted across batched passes, nothing in-retention deleted), which is the
/// regression the pagination prevents.
/// </para>
/// <para>
/// Non-skip: <see cref="ElasticsearchIntegrationTestBase"/> structurally REQUIRES the real container
/// (throws on Docker-unavailable rather than skipping), so this lock can never silently pass by being
/// skipped; the base does not expose a <c>DockerAvailable</c> property, so the explicit
/// <c>Client.ShouldNotBeNull</c> guard documents the never-skipped intent. Run serially (<c>-m:1</c>).
/// </para>
/// </summary>
[Trait("Category", "Integration")]
[Trait("Database", "Elasticsearch")]
[Trait("Component", "Compliance")]
public sealed class SecurityAuditMaintenanceBoundedDeleteShould : ElasticsearchIntegrationTestBase
{
	private const string AuditIndex = "security-audit-test";

	// batchSize for the paginated delete = MaxQueryResultSize. Kept well below the number of archived
	// events so the delete MUST span multiple bounded passes to fully drain.
	private const int BatchSize = 2;

	[Fact]
	public async Task DeleteAllArchivedEventsAcrossBoundedPasses_WhenArchivedCountExceedsBatchSize()
	{
		// Never-skipped: the real ES client must be live (the base throws if Docker is unavailable).
		Client.ShouldNotBeNull(
			"5jo6tm-ES bounded-delete drain is a real-ES behavioral lock — it must never be skipped");

		// Arrange — five events at/below the cutoff (eligible for archive+delete) and two in-retention
		// events that must survive. Five old > BatchSize(2) forces a paginated drain of [2, 2, 1].
		var now = DateTimeOffset.UtcNow;
		var cutoff = now.AddDays(-30);
		var oldEvents = new[]
		{
			NewAuditEvent(now.AddDays(-120)),
			NewAuditEvent(now.AddDays(-110)),
			NewAuditEvent(now.AddDays(-100)),
			NewAuditEvent(now.AddDays(-90)),
			NewAuditEvent(now.AddDays(-60)),
		};
		var recentEvents = new[]
		{
			NewAuditEvent(now.AddDays(-10)),
			NewAuditEvent(now.AddDays(-1)),
		};

		await IndexDocumentsAsync(AuditIndex, [.. oldEvents, .. recentEvents]).ConfigureAwait(false);
		(await CountAuditEventsAsync().ConfigureAwait(false)).ShouldBe(7, "all seven events should be indexed");

		var service = CreateMaintenanceService();
		var archiveDir = Path.Combine(Path.GetTempPath(), "audit-archive-" + Guid.NewGuid().ToString("N"));

		// Act — date-bound archive; the five old events are archived then deleted in bounded pages of
		// BatchSize, draining across passes.
		await InvokeArchiveAsync(service, cutoff, archiveDir).ConfigureAwait(false);

		// Assert — exactly the two in-retention events remain. RED on a cap-without-pagination regression
		// (a single DeleteByQuery capped at MaxDocs(BatchSize) would leave 3 old events → 5 remain), and
		// RED on any over-deletion of the in-retention events (→ <2 remain).
		var remaining = await SearchAuditEventsAsync().ConfigureAwait(false);
		remaining.Count.ShouldBe(
			2,
			"the paginated bounded delete must drain ALL five archived events across passes while leaving the two in-retention events");
		remaining.ShouldAllBe(
			e => e.Timestamp > cutoff,
			"every surviving event must be newer than the cutoff (no over-deletion)");
	}

	// SecurityAuditMaintenanceService is internal; this assembly is not a friend assembly, so construct it
	// via its internal ctor (ElasticsearchClient, AuditOptions, IAuditSigningKeyProvider, ILogger) and invoke
	// through reflection — the established pattern for internal components (mirrors the sibling archival lock).
	private object CreateMaintenanceService()
	{
		var serviceType = typeof(AuditOptions).Assembly
			.GetType("Excalibur.Data.ElasticSearch.Security.SecurityAuditMaintenanceService")
			?? throw new InvalidOperationException("Expected internal SecurityAuditMaintenanceService type.");

		// MaxQueryResultSize doubles as the archival scroll page size AND the delete batchSize. Setting it
		// to BatchSize(2) forces the delete to paginate across multiple bounded passes.
		var options = new AuditOptions { MaxQueryResultSize = BatchSize };

		return Activator.CreateInstance(
			serviceType,
			BindingFlags.NonPublic | BindingFlags.Instance,
			binder: null,
			args: [Client, options, new TestAuditSigningKeyProvider(), NullLogger.Instance],
			culture: null)!;
	}

	// Minimal in-test IAuditSigningKeyProvider double. The path under test is date-bound, bounded delete;
	// it does not mint or verify integrity tags here, so a fixed non-empty key satisfies the ctor
	// dependency without affecting the deletion behavior being asserted.
	private sealed class TestAuditSigningKeyProvider : IAuditSigningKeyProvider
	{
		private static readonly byte[] SigningKey = new byte[32];

		public ValueTask<(string KeyId, byte[] Key)> GetCurrentSigningKeyAsync(CancellationToken cancellationToken)
			=> ValueTask.FromResult(("test-key", SigningKey));

		public ValueTask<byte[]?> GetSigningKeyAsync(string keyId, CancellationToken cancellationToken)
			=> ValueTask.FromResult<byte[]?>(SigningKey);
	}

	private static async Task InvokeArchiveAsync(object service, DateTimeOffset cutoff, string archiveLocation)
	{
		var method = service.GetType().GetMethod(
			"ArchiveAuditEventsAsync",
			BindingFlags.NonPublic | BindingFlags.Instance,
			binder: null,
			types: [typeof(DateTimeOffset), typeof(string), typeof(CancellationToken)],
			modifiers: null)
			?? throw new InvalidOperationException("Expected ArchiveAuditEventsAsync(DateTimeOffset, string, CancellationToken).");

		var task = (Task)method.Invoke(service, [cutoff, archiveLocation, CancellationToken.None])!;
		await task.ConfigureAwait(false);
	}

	private async Task<int> CountAuditEventsAsync()
	{
		_ = await Client.Indices.RefreshAsync("security-audit-*").ConfigureAwait(false);
		var response = await Client.SearchAsync<SecurityAuditEvent>(s => s
			.Indices("security-audit-*")
			.Query(new MatchAllQuery())
			.Size(1000)).ConfigureAwait(false);
		response.IsValidResponse.ShouldBeTrue("audit count search should succeed");
		return response.Documents.Count;
	}

	private async Task<IReadOnlyCollection<SecurityAuditEvent>> SearchAuditEventsAsync()
	{
		_ = await Client.Indices.RefreshAsync("security-audit-*").ConfigureAwait(false);
		var response = await Client.SearchAsync<SecurityAuditEvent>(s => s
			.Indices("security-audit-*")
			.Query(new MatchAllQuery())
			.Size(1000)).ConfigureAwait(false);
		response.IsValidResponse.ShouldBeTrue("audit search should succeed");
		return response.Documents;
	}

	private static SecurityAuditEvent NewAuditEvent(DateTimeOffset timestamp) => new()
	{
		EventId = Guid.NewGuid().ToString(),
		Timestamp = timestamp,
	};
}

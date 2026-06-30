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
/// bd-74s2he (S840, AC-4/AC-5) — independent regression lock (author≠impl, TestsDeveloper) for
/// <c>SecurityAuditMaintenanceService</c> archival against a real Elasticsearch (TestContainers).
/// <para>
/// Audit archival MUST be date-bound: <c>ArchiveAuditEventsAsync(cutoff, …)</c> archives — and then
/// deletes — ONLY the events at or before the cutoff (a <c>DateRangeQuery("timestamp")</c>). The pre-fix
/// code used <c>MatchAllQuery</c>, which scrolled the ENTIRE index into the archive and then deleted
/// every event regardless of age — silent data loss of in-retention audit records.
/// </para>
/// <list type="bullet">
/// <item>AC-4: mixed-age index → only the old events are archived+deleted; the recent (in-retention)
/// events REMAIN. RED on the pre-fix MatchAll (it deletes the recent events too → 0 remain).</item>
/// <item>AC-5: when the archive write fails (an unwritable <c>archiveLocation</c>), the operation throws
/// and NO deletion is attempted — the gzip is flushed/closed before any delete, so a write failure
/// leaves every event intact.</item>
/// </list>
/// </summary>
[Trait("Category", "Integration")]
[Trait("Database", "Elasticsearch")]
[Trait("Component", "Compliance")]
public sealed class SecurityAuditMaintenanceArchivalShould : ElasticsearchIntegrationTestBase
{
	private const string AuditIndex = "security-audit-test";

	[Fact]
	public async Task ArchiveAndDeleteOnlyEventsAtOrBeforeTheCutoff_LeavingRecentEventsIntact()
	{
		// Arrange — index a mixed-age set into a security-audit-* index. Three events are well before the
		// cutoff (eligible for archival) and two are well after it (in-retention, must survive).
		var now = DateTimeOffset.UtcNow;
		var cutoff = now.AddDays(-30);
		var oldEvents = new[]
		{
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
		(await CountAuditEventsAsync().ConfigureAwait(false)).ShouldBe(5, "all five events should be indexed");

		var service = CreateMaintenanceService();
		var archiveDir = Path.Combine(Path.GetTempPath(), "audit-archive-" + Guid.NewGuid().ToString("N"));

		// Act — date-bound archive (archives the <=cutoff set, then deletes only those IDs).
		await InvokeArchiveAsync(service, cutoff, archiveDir).ConfigureAwait(false);

		// Assert — only the two recent (in-retention) events remain; the three old ones were archived+deleted.
		// RED on the pre-fix MatchAll: it would archive+delete ALL five → 0 remain.
		var remaining = await SearchAuditEventsAsync().ConfigureAwait(false);
		remaining.Count.ShouldBe(2, "only the two events newer than the cutoff must survive");
		remaining.ShouldAllBe(e => e.Timestamp > cutoff, "every surviving event must be newer than the cutoff");
	}

	[Fact]
	public async Task NotDeleteAnyEventsWhenTheArchiveWriteFails()
	{
		// Arrange — same mixed-age set.
		var now = DateTimeOffset.UtcNow;
		var cutoff = now.AddDays(-30);
		await IndexDocumentsAsync(
			AuditIndex,
			NewAuditEvent(now.AddDays(-100)),
			NewAuditEvent(now.AddDays(-90)),
			NewAuditEvent(now.AddDays(-10))).ConfigureAwait(false);
		(await CountAuditEventsAsync().ConfigureAwait(false)).ShouldBe(3);

		var service = CreateMaintenanceService();

		// An archiveLocation that is actually an existing FILE → Directory.CreateDirectory(...) throws,
		// so the archive write fails before any event is deleted (the gzip is flushed/closed before delete).
		var blockingFile = Path.Combine(Path.GetTempPath(), "audit-block-" + Guid.NewGuid().ToString("N"));
		await File.WriteAllTextAsync(blockingFile, "not a directory").ConfigureAwait(false);

		try
		{
			// Act — the archive must fail (write error). Whether it throws or returns, the invariant under
			// test is the same: NO deletion may occur when archival did not complete.
			var threw = false;
			try
			{
				await InvokeArchiveAsync(service, cutoff, blockingFile).ConfigureAwait(false);
			}
			catch
			{
				threw = true;
			}

			threw.ShouldBeTrue("a failed archive write must surface as a failure, not a silent success");

			// Assert — every event is still present (the failed archive deleted nothing). RED if deletion
			// is attempted before the archive is durably written.
			(await CountAuditEventsAsync().ConfigureAwait(false)).ShouldBe(3, "no event may be deleted when the archive write fails");
		}
		finally
		{
			File.Delete(blockingFile);
		}
	}

	// SecurityAuditMaintenanceService is internal; this assembly is not a friend assembly, so construct it
	// via its internal ctor (ElasticsearchClient, AuditOptions, IAuditSigningKeyProvider, ILogger) and invoke
	// through reflection — the established pattern for internal components. SecurityAuditEvent, AuditOptions,
	// and IAuditSigningKeyProvider are public.
	private object CreateMaintenanceService()
	{
		var serviceType = typeof(AuditOptions).Assembly
			.GetType("Excalibur.Data.ElasticSearch.Security.SecurityAuditMaintenanceService")
			?? throw new InvalidOperationException("Expected internal SecurityAuditMaintenanceService type.");

		var options = new AuditOptions { MaxQueryResultSize = 1000 };

		return Activator.CreateInstance(
			serviceType,
			BindingFlags.NonPublic | BindingFlags.Instance,
			binder: null,
			args: [Client, options, new TestAuditSigningKeyProvider(), NullLogger.Instance],
			culture: null)!;
	}

	// Minimal in-test IAuditSigningKeyProvider double. The archival path under test is date-bound
	// delete; it does not mint or verify integrity tags here, so a fixed non-empty key satisfies the
	// ctor dependency without affecting the archival behavior being asserted.
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

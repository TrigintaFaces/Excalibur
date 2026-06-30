// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;

using Excalibur.AuditLogging;
using Excalibur.Data.ElasticSearch.Security;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using MsOptions = Microsoft.Extensions.Options.Options;

namespace Excalibur.Integration.Tests.DataElasticSearch.Security;

/// <summary>
/// bd-qa71t5 (S856, SECURITY) real-Elasticsearch round-trip lock (author≠impl, TestsDeveloper;
/// <c>verify-against-real-infra-not-mock</c>): an audit event written through <c>SecurityAuditor</c> with
/// keyed-MAC integrity is persisted to a REAL Elasticsearch and <c>SecurityAuditMaintenanceService.
/// ValidateAuditIntegrityAsync</c> validates it end-to-end — clean under the signing key, and **RED
/// (corrupted) when the stored MAC does not verify** (here: validated under a different key — the same
/// detection path a content-tampered record hits).
/// </summary>
/// <remarks>
/// <para>
/// This is the ES-wiring half of qa71t5 (the crypto/canonicalization core is unit-proven in
/// <c>Excalibur.Data.Tests.ElasticSearch.Security.Auditing.AuditIntegrityShould</c>, forgery RED-proven via a
/// MAC-compare mutant). The chain-reorder case is carved to <c>nkz47q</c> (SA 17568).
/// </para>
/// <para>
/// <b>Isolation:</b> <c>ValidateAuditIntegrityAsync</c> validates ALL <c>security-audit-*</c> docs
/// (MatchAll), so the index is deleted up front to avoid cross-test contamination of the corrupted-count.
/// Never skipped (<c>DockerAvailable.ShouldBeTrue</c>).
/// </para>
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Database", "Elasticsearch")]
[Trait("Component", "Compliance")]
public sealed class AuditIntegrityRealElasticsearchShould : ElasticsearchIntegrationTestBase
{
	private static readonly byte[] KeyOne =
		System.Text.Encoding.UTF8.GetBytes("qa71t5-realES-key-ONE-256bits-of-entropy!!");

	private static readonly byte[] KeyTwo =
		System.Text.Encoding.UTF8.GetBytes("qa71t5-realES-key-TWO-different-entropy!!!!");

	[Fact]
	public async Task ValidateKeyedMacOfStoredAuditEvents_CleanUnderTheKey_AndCorruptedUnderAWrongKey()
	{
		var auditOptions = new AuditOptions
		{
			Enabled = true,
			AuditAuthentication = true,
			EnsureLogIntegrity = true,         // turns on the keyed-MAC write + validation
			MaskPiiInAuditEvents = false,      // keep fields raw — isolate integrity from masking (pbnn9g)
			ComplianceFrameworks = [],
		};

		// Clean the shared audit index so the MatchAll validation only sees this test's record.
		_ = await Client.Indices.DeleteAsync("security-audit-*", CancellationToken.None);

		// Write one event with integrity computed under KeyOne.
		await using (var auditor = new SecurityAuditor(
			Client,
			MsOptions.Create(auditOptions),
			MsOptions.Create(new SecurityMonitoringOptions()),
			BuildStrategy(KeyOne),
			sanitizer: null,
			LoggerFactory.CreateLogger<SecurityAuditor>()))
		{
			var authEvent = new AuthenticationEvent(
				Guid.NewGuid().ToString(), DateTimeOffset.UtcNow, "user-qa71t5", "password", AuthenticationResult.InvalidCredentials)
			{
				IpAddress = "203.0.113.9",
			};

			(await auditor.RecordAuthenticationEventAsync(authEvent, CancellationToken.None)).ShouldBeTrue();
			await DrainAsync(auditor);
		}

		_ = await Client.Indices.RefreshAsync("security-audit-*", CancellationToken.None);

		var start = DateTimeOffset.UtcNow.AddHours(-1);
		var end = DateTimeOffset.UtcNow.AddHours(1);

		// Validate under the CORRECT key → clean (the stored MAC verifies).
		var underCorrectKey = new SecurityAuditMaintenanceService(
			Client, auditOptions, BuildStrategy(KeyOne), LoggerFactory.CreateLogger<SecurityAuditMaintenanceService>());
		var clean = await underCorrectKey.ValidateAuditIntegrityAsync(start, end, CancellationToken.None);
		clean.TotalEvents.ShouldBeGreaterThan(0, "the written event must be present in ES for validation");
		clean.IsValid.ShouldBeTrue("a record's keyed-MAC must verify under its own signing key (clean round-trip).");

		// Validate under a DIFFERENT key → the stored MAC does not verify ⇒ corrupted (the tamper-detection path).
		var underWrongKey = new SecurityAuditMaintenanceService(
			Client, auditOptions, BuildStrategy(KeyTwo), LoggerFactory.CreateLogger<SecurityAuditMaintenanceService>());
		var corrupted = await underWrongKey.ValidateAuditIntegrityAsync(start, end, CancellationToken.None);
		corrupted.IsValid.ShouldBeFalse(
			"qa71t5 real-ES: a stored record whose keyed-MAC does not verify (wrong key = the same path a "
			+ "content-tampered record hits) MUST be reported corrupted — never a silent pass.");
	}

	private static IAuditIntegrityStrategy BuildStrategy(byte[] key)
	{
		var services = new ServiceCollection();
		services.AddSingleton(MsOptions.Create(new AuditIntegrityOptions { SigningKey = key, KeyId = "qa71t5-realES" }));
		_ = services.AddAuditIntegrity();
		return services.BuildServiceProvider().GetRequiredService<IAuditIntegrityStrategy>();
	}

	// Awaits the private, awaitable bulk-index worker so the write is deterministic (no timer race) — pbnn9g pattern.
	private static async Task DrainAsync(SecurityAuditor auditor)
	{
		var init = typeof(SecurityAuditor).GetMethod("InitializeAuditIndicesAsync", BindingFlags.NonPublic | BindingFlags.Instance);
		if (init is not null)
		{
			await ((Task)init.Invoke(auditor, [])!).ConfigureAwait(false);
		}

		var drain = typeof(SecurityAuditor).GetMethod("ProcessAuditEventQueueCoreAsync", BindingFlags.NonPublic | BindingFlags.Instance)
			?? throw new InvalidOperationException(
				"qa71t5 real-ES lock: 'ProcessAuditEventQueueCoreAsync' not found on SecurityAuditor — update the drain seam.");
		await ((Task)drain.Invoke(auditor, [])!).ConfigureAwait(false);
	}
}

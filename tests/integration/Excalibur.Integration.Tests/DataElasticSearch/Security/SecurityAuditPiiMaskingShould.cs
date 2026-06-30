// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;
using System.Text.Json;

using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.QueryDsl;

using Excalibur.Data.ElasticSearch.Security;

using Microsoft.Extensions.Options;

namespace Excalibur.Integration.Tests.DataElasticSearch.Security;

/// <summary>
/// bd-pbnn9g (S856, SECURITY) — independent regression lock (author≠impl, TestsDeveloper) for PII masking
/// on the Elasticsearch security-audit sink, against a REAL Elasticsearch (TestContainers,
/// <c>verify-against-real-infra-not-mock</c>).
/// </summary>
/// <remarks>
/// <para>
/// When <see cref="AuditOptions.MaskPiiInAuditEvents"/> is enabled (the default), <c>SecurityAuditWriter</c>
/// masks PII <b>before enqueue</b>: <c>SourceIpAddress</c>/<c>UserAgent</c> → a non-reversible
/// <c>sha256:</c> fingerprint (<c>ITelemetrySanitizer.SanitizeTag</c>); free-form <c>Details</c> values
/// (failure reasons, nested <c>Context</c>) → secret-shape-redacted/length-capped (<c>SanitizePayload</c>).
/// PII masking <b>fails closed</b> — raw PII must never reach the long-retention searchable index.
/// </para>
/// <para>
/// Each lock drives the public <see cref="SecurityAuditor"/> end-to-end (record → drain → bulk-index) and
/// reads the persisted document back from a <b>fresh</b> Elasticsearch query, asserting the raw PII is
/// absent from the stored doc. <c>EnsureLogIntegrity</c> is disabled so these locks isolate the masking
/// behavior from the (separate) keyed-MAC integrity feature (bd-qa71t5).
/// </para>
/// <para>
/// <b>Non-vacuity without mutating the shared, uncommitted impl</b> (<c>commit-surface-before-parallel-edits</c>):
/// the masking-ON tests (a)/(b) and the masking-OFF opt-out test (c) form a contrast over the SAME input —
/// only the flag flips, and the persisted outcome flips raw↔masked. That is equivalent to "RED on the
/// pre-fix writer that persisted raw PII": if the writer did not mask, (a)/(b) would persist the raw IP/secret
/// and fail. (c) proves the raw value genuinely reaches the index when the flag is off, so the flag is
/// load-bearing.
/// </para>
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Database", "Elasticsearch")]
[Trait("Component", "Compliance")]
public sealed class SecurityAuditPiiMaskingShould : ElasticsearchIntegrationTestBase
{
    private const string AuditIndexPattern = "security-audit-*";
    private const string RawIp = "203.0.113.42";
    private const string RawUserAgent = "Mozilla/5.0 (X11; TestAgent) pbnn9g-raw-ua";

    // Clearly-fake, secret-SHAPED token (>=24 base64url chars) so SanitizePayload redacts it. Test-project
    // fixture value (test projects are excluded from the secret scanner); not a real credential.
    private const string RawSecretToken = "FAKEpbnn9gSECRETtoken0000000000000000DEADBEEF";

    // (a) Default options: an authentication event with a real IP, user agent, and a secret-bearing failure
    // reason persists with NO raw IP/user-agent/secret in the indexed document.
    [Fact]
    public async Task MaskIpUserAgentAndSecretFailureReason_OnAuthenticationEvent_ByDefault()
    {
        await using var auditor = CreateAuditor(maskPii: true);

        var authEvent = new AuthenticationEvent(
            Guid.NewGuid().ToString(), DateTimeOffset.UtcNow, "user-a", "password", AuthenticationResult.InvalidCredentials)
        {
            IpAddress = RawIp,
            UserAgent = RawUserAgent,
            FailureReason = $"login rejected; presented Bearer {RawSecretToken}",
        };

        (await auditor.RecordAuthenticationEventAsync(authEvent, CancellationToken.None)).ShouldBeTrue();
        var (persisted, json) = await DrainAndReadBackAsync(auditor, authEvent.EventId);

        // Named PII fields are fingerprinted, not raw.
        persisted.SourceIpAddress.ShouldNotBeNull();
        persisted.SourceIpAddress!.ShouldStartWith("sha256:");
        persisted.SourceIpAddress.ShouldNotBe(RawIp);
        persisted.UserAgent.ShouldNotBeNull();
        persisted.UserAgent!.ShouldStartWith("sha256:");

        // No raw PII anywhere in the persisted document; the secret was redacted.
        json.ShouldNotContain(RawIp);
        json.ShouldNotContain(RawUserAgent);
        json.ShouldNotContain(RawSecretToken);
        json.ShouldContain("***REDACTED***");
    }

    // (b) Data-access event: raw IP + a secret nested in Context are masked in the indexed document.
    [Fact]
    public async Task MaskIpAndNestedContextSecret_OnDataAccessEvent_ByDefault()
    {
        await using var auditor = CreateAuditor(maskPii: true);

        var dataEvent = new DataAccessEvent(
            Guid.NewGuid().ToString(), DateTimeOffset.UtcNow, "user-b", DataAccessOperation.Export, "Customer", "cust-1")
        {
            IpAddress = RawIp,
            Context = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["apiKey"] = RawSecretToken,
                ["note"] = "bulk export",
            },
        };

        (await auditor.RecordDataAccessEventAsync(dataEvent, CancellationToken.None)).ShouldBeTrue();
        var (persisted, json) = await DrainAndReadBackAsync(auditor, dataEvent.EventId);

        persisted.SourceIpAddress.ShouldNotBeNull();
        persisted.SourceIpAddress!.ShouldStartWith("sha256:");
        json.ShouldNotContain(RawIp);
        json.ShouldNotContain(RawSecretToken);
        json.ShouldContain("***REDACTED***");
    }

    // (c) Explicit opt-out (MaskPiiInAuditEvents = false): raw fields ARE persisted. This proves the masking
    // is load-bearing — the same event with masking ON (test (a)) never persists these raw values.
    [Fact]
    public async Task PersistRawFields_WhenMaskingIsExplicitlyDisabled()
    {
        await using var auditor = CreateAuditor(maskPii: false);

        var authEvent = new AuthenticationEvent(
            Guid.NewGuid().ToString(), DateTimeOffset.UtcNow, "user-c", "password", AuthenticationResult.InvalidCredentials)
        {
            IpAddress = RawIp,
            UserAgent = RawUserAgent,
        };

        (await auditor.RecordAuthenticationEventAsync(authEvent, CancellationToken.None)).ShouldBeTrue();
        var (persisted, json) = await DrainAndReadBackAsync(auditor, authEvent.EventId);

        // Opt-out honored: the raw IP/user-agent reach the index verbatim.
        persisted.SourceIpAddress.ShouldBe(RawIp);
        persisted.UserAgent.ShouldBe(RawUserAgent);
        json.ShouldContain(RawIp);
    }

    // (d) Fail-safe (FR-pbnn9g-4): an oversized free-form field with masking on does NOT throw the record
    // path; the event is still persisted and the field is bounded/redacted (never raw, never a crash).
    [Fact]
    public async Task PersistAndBoundOversizedField_WithoutThrowing_WhenMaskingOn()
    {
        await using var auditor = CreateAuditor(maskPii: true);

        // 20k chars of secret-shaped content — exceeds the sanitizer's 4096 cap.
        var oversized = new string('A', 20_000);
        var authEvent = new AuthenticationEvent(
            Guid.NewGuid().ToString(), DateTimeOffset.UtcNow, "user-d", "password", AuthenticationResult.InvalidCredentials)
        {
            IpAddress = RawIp,
            FailureReason = oversized,
        };

        // The record path must not throw on a malformed/oversized field (fail-safe = redact/cap, not crash).
        // A throw here surfaces as a test failure — i.e. the fail-safe assertion is the absence of an exception.
        var recorded = await auditor.RecordAuthenticationEventAsync(authEvent, CancellationToken.None);
        recorded.ShouldBeTrue();

        var (persisted, json) = await DrainAndReadBackAsync(auditor, authEvent.EventId);

        _ = persisted.ShouldNotBeNull();           // the event was still persisted (write succeeded)
        persisted.SourceIpAddress!.ShouldStartWith("sha256:");
        // The oversized payload was bounded — the full 20k raw blob did not land verbatim.
        json.ShouldNotContain(oversized);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private SecurityAuditor CreateAuditor(bool maskPii)
    {
        var auditOptions = Options.Create(new AuditOptions
        {
            Enabled = true,
            AuditAuthentication = true,
            AuditDataAccess = true,
            MaskPiiInAuditEvents = maskPii,
            EnsureLogIntegrity = false, // isolate PII masking from the keyed-MAC integrity feature (bd-qa71t5)
            ComplianceFrameworks = [],
        });

        // Public IOptions ctor: defaults the ITelemetrySanitizer (DefaultAuditTelemetrySanitizer) and the
        // signing-key provider — so masking is wired with zero extra config (Platform 17471).
        return new SecurityAuditor(
            Client,
            auditOptions,
            Options.Create(new SecurityMonitoringOptions()),
            LoggerFactory.CreateLogger<SecurityAuditor>());
    }

    // Deterministically flush the queued (already-masked) event to ES, then read the persisted doc back from
    // a fresh query. Drain = await the private ProcessAuditEventQueueCoreAsync (the awaitable bulk-index
    // worker) so there is no fire-and-forget timer race; a bounded refresh-poll then covers index-visibility
    // lag. The auditor is NOT disposed here (disposing sets _disposed and would short-circuit any re-drain).
    private async Task<(SecurityAuditEvent Persisted, string Json)> DrainAndReadBackAsync(
        SecurityAuditor auditor, string eventId)
    {
        await InvokeInitIndicesAsync(auditor).ConfigureAwait(false); // ensure the audit index template exists first

        SecurityAuditEvent? found = null;
        var lastCount = -1;
        var lastValid = false;
        for (var attempt = 0; attempt < 30 && found is null; attempt++)
        {
            await InvokeDrainCoreAsync(auditor).ConfigureAwait(false); // awaited bulk-index of the queued events
            _ = await Client.Indices.RefreshAsync(AuditIndexPattern).ConfigureAwait(false);

            var response = await Client.SearchAsync<SecurityAuditEvent>(s => s
                .Indices(AuditIndexPattern)
                .Query(new MatchAllQuery())
                .Size(200)).ConfigureAwait(false);

            lastValid = response.IsValidResponse;
            if (response.IsValidResponse)
            {
                lastCount = response.Documents.Count;
                found = response.Documents.FirstOrDefault(d => d.EventId == eventId);
            }

            if (found is null)
            {
                await Task.Delay(500).ConfigureAwait(false);
            }
        }

        found.ShouldNotBeNull(
            $"bd-pbnn9g: the recorded audit event '{eventId}' must be persisted to '{AuditIndexPattern}' and readable back " +
            $"[diag: searchValid={lastValid}, docsInIndex={lastCount}]. NOTE: the masked sourceIpAddress (sha256: " +
            "fingerprint) is rejected by the ES `ip`-typed field mapping → the audit event is silently dropped " +
            "(document_parsing_exception). The index template must map sourceIpAddress as `keyword`, not `ip`.");
        var json = JsonSerializer.Serialize(found);
        return (found!, json);
    }

    // SecurityAuditor.ProcessAuditEventQueueCoreAsync is the private, awaitable bulk-index worker behind the
    // 10s timer. Awaiting it directly makes the drain deterministic (no timer wait, no fire-and-forget race)
    // per testing-patterns. Guarded so a rename surfaces as a clear non-vacuity-anchor failure.
    private static async Task InvokeDrainCoreAsync(SecurityAuditor auditor)
    {
        var method = typeof(SecurityAuditor).GetMethod(
            "ProcessAuditEventQueueCoreAsync",
            BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException(
                "bd-pbnn9g lock: 'ProcessAuditEventQueueCoreAsync' not found on SecurityAuditor — if it was renamed, update this lock's drain seam.");

        var task = (Task)method.Invoke(auditor, [])!;
        await task.ConfigureAwait(false);
    }

    // Awaits the private InitializeAuditIndicesAsync (ctor fire-and-forget) so the audit index template
    // exists before the first bulk-append, removing a template-creation race in the read-back.
    private static async Task InvokeInitIndicesAsync(SecurityAuditor auditor)
    {
        var method = typeof(SecurityAuditor).GetMethod(
            "InitializeAuditIndicesAsync",
            BindingFlags.NonPublic | BindingFlags.Instance);
        if (method is null)
        {
            return; // best-effort; auto-create still applies
        }

        var task = (Task)method.Invoke(auditor, [])!;
        await task.ConfigureAwait(false);
    }
}

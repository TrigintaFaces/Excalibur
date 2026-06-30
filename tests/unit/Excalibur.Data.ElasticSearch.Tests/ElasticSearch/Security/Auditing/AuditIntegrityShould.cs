// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.Json;

using Excalibur.AuditLogging;
using Excalibur.Data.ElasticSearch.Security;

using Microsoft.Extensions.DependencyInjection;

using MsOptions = Microsoft.Extensions.Options.Options;

namespace Excalibur.Data.Tests.ElasticSearch.Security.Auditing;

/// <summary>
/// bd-qa71t5 (S856, SECURITY — audit-trail integrity) independent regression lock (author≠impl,
/// TestsDeveloper) for the ES security-audit sink's keyed-MAC integrity, on SA's ruled seam (17568, option
/// A — genesis-null <b>per-event</b> keyed MAC via the shared <see cref="IAuditIntegrityStrategy"/>, no
/// write-time chain). The chain-reorder case is carved to <c>nkz47q</c> (ES ordering-detection).
/// </summary>
/// <remarks>
/// <para>Binds the four load-bearing properties of the integrity substrate:</para>
/// <list type="number">
/// <item><b>Determinism</b> — <see cref="SecurityAuditMaintenanceService.Canonicalize"/> (the write/verify
/// input) is byte-stable across calls and <b>excludes</b> the <see cref="SecurityAuditEvent.IntegrityHash"/>
/// output (so write and verify canonicalize identical bytes).</item>
/// <item><b>Injectivity</b> — distinct events (incl. a field-boundary shift that naive concatenation would
/// collide) produce distinct canonical bytes, so a forgery can't masquerade as a different valid record.</item>
/// <item><b>Forgery</b> — a keyed strategy's tag verifies for the original bytes but is RED (false) once any
/// canonical field is tampered.</item>
/// <item><b>Fail-closed</b> — with no signing key configured, the compute path throws and the verify path
/// returns false (never a silent pass) — the unkeyed path is inexpressible.</item>
/// </list>
/// <para>
/// Unit-level against the real shared strategy + canonicalizer (no mock of the crypto). The real-ES
/// write→tamper→re-validate round-trip (<c>ValidateAuditIntegrityAsync</c>) is the sibling integration lock.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
[Trait("Feature", "AuditIntegrity")]
public sealed class AuditIntegrityShould
{
	private static readonly byte[] TestKey =
		System.Text.Encoding.UTF8.GetBytes("qa71t5-test-signing-key-256-bits-of-entropy!!");

	[Fact]
	public void Canonicalize_IsDeterministic_AndExcludesTheIntegrityHashOutput()
	{
		var evt = MakeEvent(userId: "user-a", source: "svc");

		var first = SecurityAuditMaintenanceService.Canonicalize(evt).ToArray();
		var second = SecurityAuditMaintenanceService.Canonicalize(evt).ToArray();
		first.ShouldBe(second, "Canonicalize must be deterministic — identical input ⇒ identical bytes.");

		// The IntegrityHash is the OUTPUT, not an input: mutating it must not change the canonical bytes
		// (else write-time and verify-time would canonicalize different content and verify could never pass).
		evt.IntegrityHash = "tampered-or-set-output-hash";
		var afterHashSet = SecurityAuditMaintenanceService.Canonicalize(evt).ToArray();
		afterHashSet.ShouldBe(first, "Canonicalize must EXCLUDE IntegrityHash (it is the output, not an input).");
	}

	[Fact]
	public void Canonicalize_IsInjective_AcrossFieldValuesAndFieldBoundaries()
	{
		var baseEvent = MakeEvent(userId: "user-a", source: "svc");
		var canonicalBase = SecurityAuditMaintenanceService.Canonicalize(baseEvent).ToArray();

		// A single differing field ⇒ different canonical bytes.
		var differentUser = SecurityAuditMaintenanceService.Canonicalize(MakeEvent(userId: "user-b", source: "svc")).ToArray();
		differentUser.ShouldNotBe(canonicalBase, "a different UserId must change the canonical bytes.");

		// Field-boundary ambiguity: {UserId="ab", Source="c"} vs {UserId="a", Source="bc"} concatenate to the
		// same "abc" — a length/field-delimited canonicalizer must keep them DISTINCT (no boundary collision).
		var left = SecurityAuditMaintenanceService.Canonicalize(MakeEvent(userId: "ab", source: "c")).ToArray();
		var right = SecurityAuditMaintenanceService.Canonicalize(MakeEvent(userId: "a", source: "bc")).ToArray();
		left.ShouldNotBe(right,
			"field-boundary injectivity: shifting a character across a field boundary must change the canonical "
			+ "bytes (a naive concatenation would collide and let one record forge another).");
	}

	// SA 17652: deterministic (non-Docker) unit gate for the round-trip-stability invariant the real-ES lock
	// (AuditIntegrityRealElasticsearchShould) caught — "write-time canonical bytes == reload-time canonical
	// bytes for the same event". RED on the pre-fix impl (Timestamp.ToString("o") full ticks vs ES millisecond
	// precision; Details Convert.ToString on a runtime-typed box vs a JsonElement); GREEN once Canonicalize
	// quantizes the Timestamp to ms and normalizes Details through a stable JSON form.
	[Fact]
	public void Canonicalize_IsRoundTripStable_AcrossStoragePrecisionAndJsonTypeCoercion()
	{
		// "Native" pre-persistence form: full-ticks timestamp + native CLR Details values.
		var native = MakeEvent(userId: "user-a", source: "svc");
		native.Timestamp = DateTimeOffset.UnixEpoch.AddTicks(123_4567); // sub-millisecond ticks present
		native.Details = new Dictionary<string, object>(StringComparer.Ordinal)
		{
			["count"] = 5,        // native int
			["enabled"] = true,   // native bool
			["note"] = "ok",      // native string
			// Nested object/dictionary — the AuthenticationEvent's `Context` field (real-ES round-trip found
			// this class). Multi-key + 2-level + DELIBERATELY UNSORTED insertion order, so this only passes if
			// the fix normalizes RECURSIVELY with ordinal-sorted keys at every level (SA 17665) — not a naive
			// in-order JSON emit. The reloaded form below carries the SAME data in a DIFFERENT key order.
			["context"] = new Dictionary<string, object>(StringComparer.Ordinal)
			{
				["zeta"] = "z",
				["alpha"] = new Dictionary<string, object>(StringComparer.Ordinal) { ["y"] = "2", ["x"] = "1" },
			},
		};

		// "Reloaded-from-ES" form of the SAME logical event: the date field comes back at millisecond
		// precision, and Dictionary<string,object> values come back as JsonElement (System.Text.Json).
		var reloaded = MakeEvent(userId: "user-a", source: "svc");
		reloaded.Timestamp = DateTimeOffset.FromUnixTimeMilliseconds(native.Timestamp.ToUnixTimeMilliseconds());
		reloaded.Details = new Dictionary<string, object>(StringComparer.Ordinal)
		{
			["count"] = JsonDocument.Parse("5").RootElement.Clone(),
			["enabled"] = JsonDocument.Parse("true").RootElement.Clone(),
			["note"] = JsonDocument.Parse("\"ok\"").RootElement.Clone(),
			// Reloaded form of the SAME nested data, but key order differs from the native CLR insertion order
			// at BOTH levels (as ES may legitimately return) — only a sort-both-sides recursive normalization
			// makes write-bytes == reload-bytes. A reload path that trusts JsonElement.GetRawText() raw order
			// (SA 17665 pitfall) leaves this RED.
			["context"] = JsonDocument.Parse("{\"alpha\":{\"x\":\"1\",\"y\":\"2\"},\"zeta\":\"z\"}").RootElement.Clone(),
		};

		var canonicalNative = SecurityAuditMaintenanceService.Canonicalize(native).ToArray();
		var canonicalReloaded = SecurityAuditMaintenanceService.Canonicalize(reloaded).ToArray();

		canonicalReloaded.ShouldBe(
			canonicalNative,
			"qa71t5 round-trip stability (SA 17652): Canonicalize must produce identical bytes for the in-memory "
			+ "(write) form and the ES-reloaded form of the same event — else the keyed-MAC flags every stored "
			+ "record as corrupted. Quantize Timestamp to ms; normalize Details via a stable JSON form.");
	}

	[Fact]
	public async Task KeyedStrategy_DetectsForgery_WhenACanonicalFieldIsTampered()
	{
		var strategy = BuildStrategy(TestKey);

		var original = MakeEvent(userId: "user-a", source: "svc");
		var tag = await strategy.ComputeTagAsync(
			SecurityAuditMaintenanceService.Canonicalize(original), priorTag: null, CancellationToken.None);
		tag.ShouldNotBeNullOrEmpty("a keyed strategy must produce a non-empty MAC tag.");

		// The untampered record verifies.
		(await strategy.VerifyAsync(
			SecurityAuditMaintenanceService.Canonicalize(original), priorTag: null, tag, CancellationToken.None))
			.ShouldBeTrue("the original record's tag must verify against its own canonical bytes.");

		// A tampered record (any canonical field changed) is RED — the tag no longer verifies.
		var tampered = MakeEvent(userId: "user-EVIL", source: "svc");
		(await strategy.VerifyAsync(
			SecurityAuditMaintenanceService.Canonicalize(tampered), priorTag: null, tag, CancellationToken.None))
			.ShouldBeFalse("qa71t5 forgery: a tampered canonical field must FAIL keyed-MAC verification.");
	}

	[Fact]
	public async Task FailClosed_WhenNoSigningKeyConfigured()
	{
		var failClosed = BuildStrategy(key: null); // no signing key configured

		var canonical = SecurityAuditMaintenanceService.Canonicalize(MakeEvent(userId: "user-a", source: "svc"));

		// Compute path fails closed (throws) rather than silently producing an unkeyed tag.
		_ = await Should.ThrowAsync<InvalidOperationException>(
			async () => await failClosed.ComputeTagAsync(canonical, priorTag: null, CancellationToken.None),
			"qa71t5 fail-closed: with no signing key, the compute path must throw — never emit an unkeyed tag.");

		// Verify path fails closed (false) rather than silently passing an unverifiable record.
		(await failClosed.VerifyAsync(canonical, priorTag: null, tag: "any-tag", CancellationToken.None))
			.ShouldBeFalse("qa71t5 fail-closed: with no signing key, verification must return false (never a silent pass).");
	}

	// ── Helpers ──────────────────────────────────────────────────────────────

	private static IAuditIntegrityStrategy BuildStrategy(byte[]? key)
	{
		var services = new ServiceCollection();
		services.AddSingleton(MsOptions.Create(new AuditIntegrityOptions { SigningKey = key, KeyId = "qa71t5-test" }));
		_ = services.AddAuditIntegrity();
		return services.BuildServiceProvider().GetRequiredService<IAuditIntegrityStrategy>();
	}

	private static SecurityAuditEvent MakeEvent(string userId, string source) => new()
	{
		EventId = "fixed-event-id-qa71t5",
		Timestamp = DateTimeOffset.UnixEpoch, // fixed ⇒ determinism is about the canonicalizer, not the clock
		Source = source,
		UserId = userId,
		SourceIpAddress = "203.0.113.7",
		UserAgent = "qa71t5-agent",
		Details = new Dictionary<string, object>(StringComparer.Ordinal)
		{
			["action"] = "login",
			["result"] = "denied",
		},
	};
}

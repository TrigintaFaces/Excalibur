// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Generic;

using Excalibur.AuditLogging;
using Excalibur.Compliance;

namespace Excalibur.AuditLogging.Tests;

/// <summary>
/// author≠impl compliance-grade FREEZE lock for <see cref="AuditEventCanonicalizer.Canonicalize(AuditEvent)"/>.
/// The canonical byte representation feeds the tamper-evidence <c>EventHash</c> hash-chain; any reorder,
/// addition, or removal of a hashed field — or any change to the metadata ordering — silently breaks
/// hash-chain verification of every previously-persisted audit record. The golden constant below is
/// captured from the pre-refactor implementation and MUST remain byte-identical across the
/// audit-envelope-projection extraction (lcumof / SA 30778 §2).
/// </summary>
/// <remarks>
/// SAFETY (freeze): a fixed, fully-populated <see cref="AuditEvent"/> canonicalizes to a pinned golden hex.
/// RED if the projection reorders / adds / drops a hashed field or changes the metadata sort.
/// LIVENESS (injectivity): events differing in exactly one field — or one metadata value — produce
/// different canonical bytes, so the freeze is not satisfied by a constant/degenerate encoder.
/// METADATA ORDER: unsorted metadata input yields the same bytes as sorted input (the <c>OrderBy</c> is
/// part of the frozen contract, not incidental to insertion order).
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class AuditEventCanonicalizerHashFreezeShould
{
	// Golden captured from the pre-extraction AuditEventCanonicalizer (HEAD). Do NOT edit to make a
	// refactor pass — a changed value means the hash-covered canonical form moved and the audit hash-chain
	// of every existing record would fail to verify. If this must change, it is a deliberate,
	// version-stamped format break, not a refactor.
	private const string GoldenCanonicalHex =
		"0101000000086576742D303030310100000001340100000006557064617465010000000132010000000D31373030303030303030"
		+ "30303001000000086163746F722D343201000000045573657201000000067265732D39390100000008437573746F6D657201000000"
		+ "0133010000000874656E616E742D37010000000D657863616C696275722D6170700100000008636F72722D31323301000000087365"
		+ "73732D616263010000000B3230332E302E3131332E3701000000096167656E742F312E30010000000D706F6C6963792D7265766965"
		+ "770100000005616C7068610100000005612D76616C01000000047A65746101000000057A2D76616C";

	[Fact]
	public void ProduceTheFrozenGoldenCanonicalBytes_ForAFullyPopulatedEvent()
	{
		// SAFETY — freeze. Byte-identical canonical form vs the pinned golden.
		var canonical = AuditEventCanonicalizer.Canonicalize(FullyPopulated());

		Convert.ToHexString(canonical).ShouldBe(
			GoldenCanonicalHex,
			"AuditEventCanonicalizer.Canonicalize produced different bytes than the frozen golden. The "
			+ "canonical form feeds the tamper-evidence EventHash; any reorder/add/drop of a hashed field (or "
			+ "a metadata-ordering change) breaks hash-chain verification of every existing audit record. This "
			+ "must stay byte-identical across the audit-envelope-projection extraction.");
	}

	[Fact]
	public void ProduceDifferentBytes_WhenExactlyOneScalarFieldDiffers()
	{
		// LIVENESS — injectivity across scalar fields (the encoder is not degenerate/constant).
		var baseline = AuditEventCanonicalizer.Canonicalize(FullyPopulated());
		var mutated = AuditEventCanonicalizer.Canonicalize(FullyPopulated() with { Action = "Create" });

		Convert.ToHexString(mutated).ShouldNotBe(
			Convert.ToHexString(baseline),
			"Two events differing in exactly one hashed field must canonicalize to different bytes — otherwise "
			+ "the freeze is satisfied vacuously by an encoder that ignores field content.");
	}

	[Fact]
	public void ProduceDifferentBytes_WhenExactlyOneMetadataValueDiffers()
	{
		// LIVENESS — injectivity across metadata values.
		var baseline = AuditEventCanonicalizer.Canonicalize(FullyPopulated());
		var mutated = AuditEventCanonicalizer.Canonicalize(
			FullyPopulated() with
			{
				Metadata = new Dictionary<string, string>(StringComparer.Ordinal)
				{
					["zeta"] = "z-val",
					["alpha"] = "CHANGED",
				},
			});

		Convert.ToHexString(mutated).ShouldNotBe(
			Convert.ToHexString(baseline),
			"Changing a single metadata value must change the canonical bytes — metadata key AND value are "
			+ "hash-covered, so distinct metadata sets cannot collide.");
	}

	[Fact]
	public void ProduceIdenticalBytes_RegardlessOfMetadataInsertionOrder()
	{
		// METADATA ORDER — the OrderBy(key, Ordinal) is part of the frozen contract: unsorted == sorted.
		var unsorted = AuditEventCanonicalizer.Canonicalize(
			FullyPopulated() with
			{
				Metadata = new Dictionary<string, string>(StringComparer.Ordinal)
				{
					["zeta"] = "z-val",
					["alpha"] = "a-val",
				},
			});
		var sorted = AuditEventCanonicalizer.Canonicalize(
			FullyPopulated() with
			{
				Metadata = new Dictionary<string, string>(StringComparer.Ordinal)
				{
					["alpha"] = "a-val",
					["zeta"] = "z-val",
				},
			});

		Convert.ToHexString(unsorted).ShouldBe(
			Convert.ToHexString(sorted),
			"Metadata must be canonicalized in Ordinal key order regardless of insertion order; if insertion "
			+ "order leaks into the bytes, two logically-identical events would hash differently.");
	}

	// Fixed, fully-populated event — all 17 hash-covered scalars set to stable non-default values, plus two
	// metadata keys in NON-sorted insertion order (zeta before alpha) so the golden also pins the metadata
	// ordering. Timestamp is a fixed instant (canonicalized via ToUnixTimeMilliseconds).
	private static AuditEvent FullyPopulated() => new()
	{
		EventId = "evt-0001",
		EventType = AuditEventType.DataModification,
		Action = "Update",
		Outcome = AuditOutcome.Denied,
		Timestamp = DateTimeOffset.FromUnixTimeMilliseconds(1_700_000_000_000),
		ActorId = "actor-42",
		ActorType = "User",
		ResourceId = "res-99",
		ResourceType = "Customer",
		ResourceClassification = DataClassification.Restricted,
		TenantId = "tenant-7",
		ApplicationName = "excalibur-app",
		CorrelationId = "corr-123",
		SessionId = "sess-abc",
		IpAddress = "203.0.113.7",
		UserAgent = "agent/1.0",
		Reason = "policy-review",
		Metadata = new Dictionary<string, string>(StringComparer.Ordinal)
		{
			["zeta"] = "z-val",
			["alpha"] = "a-val",
		},
	};
}

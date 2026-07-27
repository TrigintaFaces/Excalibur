// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text;

using Excalibur.Compliance;

namespace Excalibur.AuditLogging.Tests;

/// <summary>
/// wtk7gf — author≠impl integrity-stability lock (TestsDeveloper) for
/// <see cref="AuditEventCanonicalizer"/>'s handling of <see cref="AuditEvent.ResourceClassification"/>.
/// The canonical (integrity-hash) form MUST render the enum by its stable underlying <c>(int)</c>, NOT its
/// member name — else a future enum-member RENAME would silently change the canonical bytes and break
/// integrity verification of previously-signed records. The absent case stays distinct from any present value.
/// </summary>
/// <remarks>
/// <b>RED mutant:</b> render <c>ResourceClassification?.ToString()</c> (the member name) instead of
/// <c>((int)value)</c> ⇒ the canonical bytes contain <c>"Confidential"</c> (the pre-fix behavior) — RED.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
[Trait("Feature", "AuditIntegrity")]
public sealed class AuditEventCanonicalizerResourceClassificationShould
{
	[Fact]
	public void RenderResourceClassificationAsUnderlyingInt_NotMemberName()
	{
		var canonical = AuditEventCanonicalizer.Canonicalize(MakeEvent(DataClassification.Confidential));
		var text = Encoding.UTF8.GetString(canonical);

		text.ShouldNotContain(
			nameof(DataClassification.Confidential),
			Case.Insensitive,
			"the canonical/integrity form must use the STABLE underlying int, never the enum member name (a rename "
			+ "would otherwise silently break integrity verification of previously-signed records).");
	}

	[Fact]
	public void ProduceDistinctBytes_PerClassificationValue()
	{
		var confidential = AuditEventCanonicalizer.Canonicalize(MakeEvent(DataClassification.Confidential));
		var restricted = AuditEventCanonicalizer.Canonicalize(MakeEvent(DataClassification.Restricted));

		restricted.ShouldNotBe(confidential,
			"a different ResourceClassification must change the canonical bytes — the field is genuinely integrity-covered.");
	}

	[Fact]
	public void DistinguishAbsentClassificationFromAnyPresentValue()
	{
		var present = AuditEventCanonicalizer.Canonicalize(MakeEvent(DataClassification.Public));
		var absent = AuditEventCanonicalizer.Canonicalize(MakeEvent(classification: null));

		absent.ShouldNotBe(present,
			"an absent ResourceClassification must canonicalize distinctly from a present value (including Public=0).");
	}

	private static AuditEvent MakeEvent(DataClassification? classification) => new()
	{
		EventId = "fixed-event-id-wtk7gf",
		EventType = AuditEventType.Authorization,
		Action = "read",
		Outcome = default,
		Timestamp = DateTimeOffset.UnixEpoch,
		ActorId = "actor-1",
		ResourceClassification = classification,
	};
}

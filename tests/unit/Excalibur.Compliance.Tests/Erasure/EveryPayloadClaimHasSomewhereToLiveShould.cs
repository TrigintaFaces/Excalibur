// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Reflection;

namespace Excalibur.Compliance.Tests.Erasure;

/// <summary>
/// A tripwire on <see cref="ErasureCertificatePayload"/>'s field set.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why a tripwire and not a round-trip.</b> The real proof that a store loses nothing is a round-trip
/// against real infrastructure, and that lives in the conformance kit. This arm covers the failure that
/// happens <i>before</i> anyone runs the kit: a claim is added to the payload, no store gains a column for
/// it, and the loss is silent. Because the signature covers the payload whole, such a field does not merely
/// go missing — the reassembled payload stops matching what was signed and the certificate reports as
/// TAMPERED.
/// </para>
/// <para>
/// <b>This arm cannot tell you a store is correct.</b> It can only stop a new claim being added without
/// anyone looking at the stores. When it fails, the fix is not to edit the list below first — it is to add
/// the column to SQL Server, then add the name here.
/// </para>
/// <para>
/// <b>Scoped to SQL Server, and the scope is the finding.</b> The Postgres store persists the certificate's
/// canonical form verbatim and restores it whole, so a claim added to the payload is carried there by
/// construction and no column is owed. SQL Server still reassembles the document from a column per claim,
/// which is what this arm guards. The real proof for both is
/// <c>SaveCertificateAsync_ShouldRoundTripACertificateThatStillVerifies</c> in the conformance kit, which
/// runs against real engines; this one is the cheap tripwire that fires without a database.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class EveryPayloadClaimHasSomewhereToLiveShould
{
	// Each name here has a column in the SQL Server store (SqlServerErasureStore.cs -- CREATE TABLE,
	// INSERT, both SELECTs, CertificateRow and ToCertificate) and is carried whole by the in-memory and
	// Postgres stores.
	private static readonly HashSet<string> PersistedByEveryStore =
	[
		nameof(ErasureCertificatePayload.CertificateId),
		nameof(ErasureCertificatePayload.RequestId),
		nameof(ErasureCertificatePayload.DataSubjectReference),
		nameof(ErasureCertificatePayload.RequestReceivedAt),
		nameof(ErasureCertificatePayload.CompletedAt),
		nameof(ErasureCertificatePayload.Method),
		nameof(ErasureCertificatePayload.Summary),
		nameof(ErasureCertificatePayload.Verification),
		nameof(ErasureCertificatePayload.LegalBasis),
		nameof(ErasureCertificatePayload.Exceptions),
		nameof(ErasureCertificatePayload.GeneratedAt),
		nameof(ErasureCertificatePayload.RetainUntil),
		nameof(ErasureCertificatePayload.Version),
	];

	[Fact]
	public void Fail_when_a_claim_is_added_to_the_payload_without_a_column_in_every_store()
	{
		var declared = typeof(ErasureCertificatePayload)
			.GetProperties(BindingFlags.Public | BindingFlags.Instance)
			.Select(p => p.Name)
			.Where(n => !string.Equals(n, "EqualityContract", StringComparison.Ordinal))
			.ToHashSet(StringComparer.Ordinal);

		var unpersisted = declared.Except(PersistedByEveryStore).OrderBy(n => n, StringComparer.Ordinal);
		var stale = PersistedByEveryStore.Except(declared).OrderBy(n => n, StringComparer.Ordinal);

		string.Join(", ", unpersisted).ShouldBeEmpty(
			"these payload claims are signed but have no column in the SQL Server erasure store, which "
			+ "restores a certificate from its columns. A certificate read back will not match its own "
			+ "signature and will report as TAMPERED. Add the column to SqlServerErasureStore -- CREATE "
			+ "TABLE, INSERT, BOTH SELECTs, the row type, ToCertificate, and the column allowlist -- "
			+ "before adding the name to this list.");

		string.Join(", ", stale).ShouldBeEmpty(
			"this list names claims the payload no longer declares; the stores are carrying dead columns.");
	}
}

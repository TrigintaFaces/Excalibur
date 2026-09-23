// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;
using Excalibur.Inbox;

using Shouldly;

using Xunit;

namespace Excalibur.Inbox.Oracle.Tests;

/// <summary>
/// Pure (no-infra) lock on the host-less fail-closed floor: an ambient tenant identity is admissible only
/// on a key that can distinguish it (<see cref="InboxSchemaContract.VerifyTenantAdmissible"/>).
/// </summary>
/// <remarks>
/// <para>
/// <b>THE DEFECT THIS GUARDS.</b> A single-tenant inbox table is keyed by <c>(MessageId, HandlerType)</c>
/// alone. If a real tenant identity is nonetheless resolved on that key, two tenants' messages collide:
/// the second tenant's message is discarded as an already-processed duplicate and never handled. That is
/// silent cross-tenant LOSS, not a leak — nothing surfaces, because an absent message is indistinguishable
/// from a deduplicated one.
/// </para>
/// <para>
/// <b>WHY THE SIBLING SUITE DOES NOT COVER IT.</b> <see cref="InboxSchemaContractShould"/> locks
/// <c>Verify</c> — the deployment-mode ↔ physical-schema handshake, checked once at the schema read. This
/// member asks a different question at a different cadence: not <i>does the schema match the configured
/// mode</i> but <i>is the identity THIS operation is running as admissible on that schema</i>. It is
/// evaluated per call, which is the whole point of the split described below.
/// </para>
/// <para>
/// <b>THE TRAP THIS SHAPE AVOIDS, recorded because the naive fix is worse than nothing.</b> The obvious
/// implementation puts the tenant check inside the store's cached <c>EnsureSchemaAsync</c> body. It would
/// then land after the cache's early return and run exactly once — and the schema-validation hosted
/// service calls that method at host start, where no request is in flight and the ambient identity is the
/// benign default. The guard would pass under a safe identity, cache its pass, and wave every real
/// cross-tenant call straight through: a gate reporting a PASS IT DID NOT EARN. The three relational
/// stores therefore SPLIT it — the schema fact stays cached in <c>ReadSchemaOnceAsync</c>, this check sits
/// outside it and runs every call.
/// </para>
/// <para>
/// <b>THE LIVENESS ARMS ARE THE LOAD-BEARING HALF HERE.</b> Three separate compositions must NOT throw,
/// and an earlier revision of this check rejected one of them — the ordinary single-tenant host with no
/// resolved tenant, which is the shape most single-tenant consumers have. A safety-only suite would have
/// certified that regression as correct.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Inbox")]
public sealed class InboxTenantAdmissibilityShould
{
	private const string Table = "inbox_messages";

	/// <summary>An ambient context whose resolved identity the arm chooses outright.</summary>
	private sealed class AmbientTenant(string? tenantId) : ITenantContext
	{
		public string? TenantId { get; } = tenantId;

		public bool HasTenant => !string.IsNullOrEmpty(TenantId);
	}

	private static void Verify(bool hasTenantColumn, string? ambientTenant) =>
		InboxSchemaContract.VerifyTenantAdmissible(Table, hasTenantColumn, new AmbientTenant(ambientTenant));

	/// <summary>
	/// SAFETY — the headline. A real tenant identity on an undiscriminating key must fail closed.
	/// </summary>
	[Fact]
	public void RefuseARealTenantIdentityOnAKeyThatCannotDistinguishIt()
	{
		var thrown = Should.Throw<InvalidOperationException>(
			() => Verify(hasTenantColumn: false, ambientTenant: "tenant-a"));

		thrown.Message.ShouldContain(
			Table,
			Case.Sensitive,
			"the operator has to know WHICH table is mis-paired; the message is the only place they learn it");

		thrown.Message.ShouldContain(
			"tenant-a",
			Case.Sensitive,
			"and which identity triggered it — without it they cannot tell a misconfiguration from a stray "
			+ "request");
	}

	/// <summary>
	/// SAFETY. A second, different real identity must be refused too — so the arm above is not satisfied by
	/// a check that happens to reject one hard-coded string.
	/// </summary>
	[Fact]
	public void RefuseAnyRealTenantIdentity_NotMerelyOneSpelling() =>
		_ = Should.Throw<InvalidOperationException>(
			() => Verify(hasTenantColumn: false, ambientTenant: "some-other-tenant"));

	/// <summary>
	/// LIVENESS. The key discriminates, so any identity is admissible — this is the multi-tenant shape and
	/// it must not be touched by the guard.
	/// </summary>
	[Fact]
	public void AdmitARealTenantIdentityWhenTheKeyCarriesTheTenantColumn() =>
		Should.NotThrow(() => Verify(hasTenantColumn: true, ambientTenant: "tenant-a"));

	/// <summary>
	/// LIVENESS, and the regression an earlier revision actually shipped. No tenant resolved is the
	/// ORDINARY single-tenant composition, not a violation.
	/// </summary>
	[Fact]
	public void AdmitTheOrdinarySingleTenantHostThatResolvesNoTenantAtAll() =>
		Should.NotThrow(
			() => Verify(hasTenantColumn: false, ambientTenant: null),
			"an unresolved ambient tenant is an ABSENCE, not a foreign identity. Rejecting it would refuse "
			+ "the shape most single-tenant consumers have");

	/// <summary>
	/// LIVENESS. The reserved stored spelling of "no tenant" is a constant identity, not a second tenant.
	/// </summary>
	[Fact]
	public void AdmitTheReservedUntenantedSentinel() =>
		Should.NotThrow(() => Verify(hasTenantColumn: false, ambientTenant: TenantScope.UntenantedSentinel));

	/// <summary>
	/// LIVENESS. A host may legitimately run as a named single tenant via the configured default.
	/// </summary>
	[Fact]
	public void AdmitTheConfiguredDefaultTenantIdentity() =>
		Should.NotThrow(() => Verify(hasTenantColumn: false, ambientTenant: TenantDefaults.DefaultTenantId));
}

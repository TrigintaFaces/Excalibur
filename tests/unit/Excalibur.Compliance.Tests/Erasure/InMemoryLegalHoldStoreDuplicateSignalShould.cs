// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Compliance.Erasure;
using Excalibur.Dispatch;

using Microsoft.Extensions.Options;

namespace Excalibur.Compliance.Tests.Erasure;

/// <summary>
/// Binds the two answers <c>SaveHoldAsync</c> can give a caller and proves they are distinguishable:
/// "this hold is already on file" and "no ambient tenant was resolved, so nothing was written".
/// </summary>
/// <remarks>
/// <para>
/// Both used to surface as a bare <see cref="InvalidOperationException"/>, and the second one derives
/// from the first - <c>TenantRequiredException : InvalidOperationException</c> - so a caller writing the
/// obvious <c>catch (InvalidOperationException)</c> treated a hold that was <em>never written</em> as one
/// already on file. A legal hold blocks erasure, so a dropped one does not fail safe: the next erasure
/// runs and reports success over data a court order says to keep.
/// </para>
/// <para>
/// <b>The identifier scope arms are the ones to read first, because they lock a decision that looks like
/// a bug.</b> A hold identifier is the sole primary key of the legal-hold table in every shipped
/// relational provider, so it is unique across the ESTATE, not within a tenant. That produces a pair of
/// answers that reads as contradictory and is not: a second tenant re-using an identifier is refused,
/// while its read of that same identifier reports nothing there. Making this store accept the second
/// write - by composing the tenant into its dictionary key - would look like isolation and would in fact
/// make it accept a hold that PostgreSQL and SQL Server reject on their primary key. A consumer who
/// developed against the in-memory store would then lose the second hold on its first day in production,
/// silently, which is the harm the isolation was meant to prevent. The arms below pin the behaviour to
/// the direction the relational providers actually implement.
/// </para>
/// <para>
/// Every arm that matters runs with a RESOLVED ambient tenant under <c>RequireTenant</c>. Constructed
/// unscoped, the ambient term short-circuits and the store never exercises a tenant decision at all.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class InMemoryLegalHoldStoreDuplicateSignalShould
{
	private const string TenantA = "tenant-a";
	private const string TenantB = "tenant-b";

	/// <summary>
	/// An <see cref="ITenantContext"/> whose ambient tenant can be moved between phases of an arm, so one
	/// store instance can be driven as tenant A and then as tenant B - the shape a real multi-tenant host
	/// produces, where both tenants address the same store.
	/// </summary>
	private sealed class MovableTenantContext : ITenantContext
	{
		public string? TenantId { get; set; }

		public bool HasTenant => !string.IsNullOrEmpty(TenantId);
	}

	private static InMemoryLegalHoldStore MultiTenantStore(MovableTenantContext context) =>
		new(context, Options.Create(new TenantContextOptions { RequireTenant = true }));

	private static LegalHold Hold(Guid holdId) => new()
	{
		HoldId = holdId,
		DataSubjectIdHash = "hash-subject-1",
		IdType = DataSubjectIdType.UserId,
		Basis = LegalHoldBasis.LitigationHold,
		CaseReference = "CASE-001",
		Description = "Test hold",
		IsActive = true,
		CreatedBy = "admin",
		CreatedAt = DateTimeOffset.UtcNow
	};

	// ---------- SAFETY: the duplicate condition has its own type ----------

	[Fact]
	public async Task Raise_the_duplicate_type_when_the_ambient_tenant_re_files_its_own_hold_id()
	{
		var context = new MovableTenantContext { TenantId = TenantA };
		var store = MultiTenantStore(context);
		var holdId = Guid.NewGuid();
		await store.SaveHoldAsync(Hold(holdId), CancellationToken.None).ConfigureAwait(false);

		var thrown = await Should.ThrowAsync<DuplicateLegalHoldException>(
			() => store.SaveHoldAsync(Hold(holdId), CancellationToken.None)).ConfigureAwait(false);

		thrown.HoldId.ShouldBe(holdId);
	}

	[Fact]
	public async Task Not_raise_the_duplicate_type_when_no_ambient_tenant_was_resolved()
	{
		// The whole point of the dedicated type. This store is empty, so there is no duplicate to find;
		// the failure is that multi-tenancy is active with nothing resolved, and the hold is not written.
		// Before the duplicate condition had a type of its own, a caller could not tell this apart from
		// "already on file" and would drop the hold.
		var store = MultiTenantStore(new MovableTenantContext { TenantId = null });

		var thrown = await Should.ThrowAsync<TenantRequiredException>(
			() => store.SaveHoldAsync(Hold(Guid.NewGuid()), CancellationToken.None)).ConfigureAwait(false);

		_ = thrown.ShouldBeAssignableTo<InvalidOperationException>();
		thrown.ShouldNotBeOfType<DuplicateLegalHoldException>();
	}

	// ---------- LIVENESS: a store that throws on every insert fails these ----------

	[Fact]
	public async Task Store_a_fresh_hold_id_and_return_it_to_the_tenant_that_filed_it()
	{
		var context = new MovableTenantContext { TenantId = TenantA };
		var store = MultiTenantStore(context);
		var holdId = Guid.NewGuid();

		await store.SaveHoldAsync(Hold(holdId), CancellationToken.None).ConfigureAwait(false);

		var read = await store.GetHoldAsync(holdId, CancellationToken.None).ConfigureAwait(false);
		read.ShouldNotBeNull();
		read.HoldId.ShouldBe(holdId);
		read.TenantId.ShouldBe(TenantA);
	}

	[Fact]
	public async Task Let_a_second_tenant_file_a_hold_under_a_different_id()
	{
		// The liveness partner of the identifier-scope arm below: the estate-wide identifier namespace
		// must refuse only a re-used identifier, never a second tenant as such. A store that rejected
		// tenant B outright would satisfy the safety arm and be useless.
		var context = new MovableTenantContext { TenantId = TenantA };
		var store = MultiTenantStore(context);
		await store.SaveHoldAsync(Hold(Guid.NewGuid()), CancellationToken.None).ConfigureAwait(false);

		context.TenantId = TenantB;
		var tenantBHoldId = Guid.NewGuid();
		await store.SaveHoldAsync(Hold(tenantBHoldId), CancellationToken.None).ConfigureAwait(false);

		var read = await store.GetHoldAsync(tenantBHoldId, CancellationToken.None).ConfigureAwait(false);
		read.ShouldNotBeNull();
		read.TenantId.ShouldBe(TenantB);
	}

	// ---------- The identifier namespace is estate-wide, and both answers are true ----------

	[Fact]
	public async Task Refuse_a_second_tenants_re_use_of_a_hold_id_the_way_the_relational_providers_do()
	{
		// hold_id is the SOLE primary key of the legal-hold table on PostgreSQL and SQL Server, so a
		// second tenant re-using it is a unique-key violation there. This store must agree, or a consumer
		// who develops against it loses a preservation order the first time it runs on a real database.
		var context = new MovableTenantContext { TenantId = TenantA };
		var store = MultiTenantStore(context);
		var sharedHoldId = Guid.NewGuid();
		await store.SaveHoldAsync(Hold(sharedHoldId), CancellationToken.None).ConfigureAwait(false);

		context.TenantId = TenantB;

		var thrown = await Should.ThrowAsync<DuplicateLegalHoldException>(
			() => store.SaveHoldAsync(Hold(sharedHoldId), CancellationToken.None)).ConfigureAwait(false);

		thrown.HoldId.ShouldBe(sharedHoldId);
	}

	[Fact]
	public async Task Report_a_foreign_tenants_hold_as_absent_even_though_its_id_is_taken()
	{
		// The other half of the pair, asserted together so the contract is legible: the identifier is
		// refused on write and reports as absent on read. Both are true; neither is a defect. The read
		// confinement is the isolation, and adding the tenant to the KEY to "fix" the write would destroy
		// the agreement with the relational primary key without adding any isolation the read lacks.
		var context = new MovableTenantContext { TenantId = TenantA };
		var store = MultiTenantStore(context);
		var tenantAHoldId = Guid.NewGuid();
		await store.SaveHoldAsync(Hold(tenantAHoldId), CancellationToken.None).ConfigureAwait(false);

		context.TenantId = TenantB;

		var read = await store.GetHoldAsync(tenantAHoldId, CancellationToken.None).ConfigureAwait(false);
		read.ShouldBeNull();
	}
}

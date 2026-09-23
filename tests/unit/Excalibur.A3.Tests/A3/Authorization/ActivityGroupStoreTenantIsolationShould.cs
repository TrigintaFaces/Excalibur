// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.A3.Authorization;
using Excalibur.A3.Authorization.Stores.InMemory;

namespace Excalibur.Tests.A3.Authorization;

/// <summary>
/// Independent locks on the activity-group STORE's tenant isolation, authored separately from the
/// authorization-policy fix and deliberately NOT re-testing the wildcard decision path.
/// </summary>
/// <remarks>
/// <para>
/// The wildcard escalation is locked elsewhere, by the author of that fix. These arms exist because a
/// fixer-authored lock proves the fix WORKS and cannot prove the fix is COMPLETE: it tests what its
/// author thought of. All three arms below target exposures on the same contract that the wildcard
/// repair does not reach, so they are non-duplicative by construction rather than by intention.
/// </para>
/// <para>
/// <b>Every arm asserts at the STORE contract, never at a private field.</b> A store is tenant-isolated
/// only if its published surface behaves that way for an ordinary caller; reaching past the contract to
/// inspect internals would pass on an implementation whose public behaviour is still wrong.
/// </para>
/// <para>
/// <b>The three targets are deliberately different in kind.</b> The first is RED today and names a
/// defect with no fix yet. The second is green and binds a guarantee the type publishes about itself.
/// The third hands construction to the framework, because an arm that builds its own input cannot
/// observe the real one being absent — which is how this whole class of defect stayed green.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "A3")]
[Trait("Feature", "Authorization")]
public sealed class ActivityGroupStoreTenantIsolationShould
{
	private const string TenantA = "tenant-a";
	private const string TenantB = "tenant-b";
	private const string SharedGroupName = "Finance";
	private const string ActivityName = "Read";

	/// <summary>
	/// The delete-all contract cannot be scoped to a tenant, so one tenant's maintenance erases every
	/// other tenant's groups.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>This arm asserts the SPECIFICATION and is expected RED until the contract carries a tenant.</b>
	/// It is not written against the estate-wide member: <c>ReplaceAllActivityGroupsAsync</c> takes no
	/// tenant, so there is no tenant for an implementation to honour and no implementation of that
	/// signature can pass. That is the finding, not a defect in the arm — an arm asserting
	/// today's wipe-everything behaviour would be a characterization lock that could never go red.
	/// </para>
	/// <para>
	/// <b>Why this is not hypothetical.</b> The shipped caller is a synchronise-from-remote routine that
	/// deletes every row and then re-creates only what the remote payload contained. A payload covering
	/// one tenant therefore erases every other tenant's groups permanently, with no wildcard, no race and
	/// no privileged actor involved. The relational providers compose the same statement with no WHERE
	/// clause, so the exposure is the contract's, not one provider's.
	/// </para>
	/// <para>
	/// <b>Liveness first.</b> The pre-assertion below is what stops this arm passing vacuously against a
	/// store that never created anything: a store that holds nothing satisfies "the other tenant's group
	/// was not erased" perfectly.
	/// </para>
	/// </remarks>
	[Fact]
	public async Task NotEraseAnotherTenantsGroupWhenOneTenantsGroupsAreDeleted()
	{
		// Arrange
		var store = new InMemoryActivityGroupStore();

		_ = await store.CreateActivityGroupAsync(TenantA, SharedGroupName, ActivityName, CancellationToken.None);
		_ = await store.CreateActivityGroupAsync(TenantB, SharedGroupName, ActivityName, CancellationToken.None);

		var tenantBKey = SegmentedKey.Compose(TenantB, SharedGroupName);

		// LIVENESS. Without this the arm would pass on a store that never stored either group.
		var seeded = await store.FindActivityGroupsAsync(TenantB, CancellationToken.None);
		seeded.ShouldContainKey(
			tenantBKey,
			"the arm cannot test erasure of a group the store never held");

		// Act -- tenant A removes ITS OWN groups, by name, with the tenant at the call site.
		var removed = await store.DeleteActivityGroupsForTenantAsync(TenantA, CancellationToken.None);

		// LIVENESS. A delete that removed nothing satisfies every confinement assertion below perfectly,
		// and is the cheapest way to pass this arm while doing nothing at all.
		removed.ShouldBe(
			1,
			"the scoped delete reported no rows removed, so it is inert -- and an inert delete confines "
			+ "perfectly while leaving the caller's own groups in place, which is not the contract");
		(await store.FindActivityGroupsAsync(TenantA, CancellationToken.None)).ShouldBeEmpty(
			"tenant A asked for its own groups to be removed and they are still there");

		// Assert -- SAFETY. Tenant B never asked for anything to be deleted.
		var remaining = await store.FindActivityGroupsAsync(TenantB, CancellationToken.None);
		remaining.ShouldContainKey(
			tenantBKey,
			"a delete issued for ONE tenant removed a DIFFERENT tenant's activity groups. The scoped member "
			+ "exists precisely so a consumer can clear its own groups without touching the estate; if it "
			+ "cannot confine itself there is no safe way to remove anything.");
	}

	/// <summary>
	/// The estate-wide replace really is estate-wide, and that is the specified contract rather than a leak.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>This arm exists so the behaviour is not re-filed as a defect.</b> An earlier arm here asserted the
	/// opposite — that a delete-all must not reach another tenant — and it was RED for as long as it existed,
	/// because the member does what its name says. The requirements owner ruled the estate-wide operation is
	/// a dangerous but HONEST API and stays: the sync contract is a full refresh, which legitimately needs to
	/// clear the estate before repopulating it.
	/// </para>
	/// <para>
	/// <b>What made it safe was not removing this member but ADDING its scoped sibling</b>, so a consumer can
	/// express "just my tenant" and the estate-wide case has to be named at the call site. The two are
	/// separate operations rather than one nullable parameter, so the catastrophic radius is not the value
	/// you get by forgetting an argument.
	/// </para>
	/// </remarks>
	[Fact]
	public async Task ReplaceEveryTenantsGroupsWhenTheEstateWideReplaceIsNamedExplicitly()
	{
		// Arrange
		const string TenantC = "tenant-c";
		var store = new InMemoryActivityGroupStore();

		_ = await store.CreateActivityGroupAsync(TenantA, SharedGroupName, ActivityName, CancellationToken.None);
		_ = await store.CreateActivityGroupAsync(TenantB, SharedGroupName, ActivityName, CancellationToken.None);

		// Act -- a catalogue that names only a third tenant.
		var previous = await store.ReplaceAllActivityGroupsAsync(
			new ActivityGroupCatalogue([new ActivityGroupEntry(TenantC, SharedGroupName, ActivityName)]),
			CancellationToken.None);

		// Assert -- it reports the radius it actually took, which is what makes a variable-radius operation
		// honest rather than merely destructive.
		previous.ShouldContain(TenantA);
		previous.ShouldContain(TenantB);

		// A tenant absent from the catalogue has no groups afterwards...
		(await store.FindActivityGroupsAsync(TenantA, CancellationToken.None)).ShouldBeEmpty();
		(await store.FindActivityGroupsAsync(TenantB, CancellationToken.None)).ShouldBeEmpty();

		// ...and LIVENESS: the catalogue it was given is what remains.
		(await store.FindActivityGroupsAsync(TenantC, CancellationToken.None)).ShouldHaveSingleItem();
	}

	/// <summary>
	/// Two tenants may hold a group of the same name, and the second must not silently inherit the first
	/// tenant's row.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>This arm is GREEN today and binds a published guarantee.</b> The store's own documentation
	/// states that every key carries the tenant, composed so that no two (tenant, name) pairs can produce
	/// the same key. That sentence is a promise to a consumer reading the type, and nothing was holding
	/// it: the guarantee and its enforcement were one edit apart.
	/// </para>
	/// <para>
	/// <b>Mutant:</b> drop the tenant from the key composition in <c>BuildKey</c>. The second create then
	/// collides, the atomic add reports zero rows written, tenant B silently keeps tenant A's row
	/// including tenant A's tenant identifier, and the find below returns one fused entry instead of two.
	/// Both assertions go red on that single-token change, which is what makes this a lock rather than a
	/// restatement.
	/// </para>
	/// <para>
	/// <b>The count assertion is the load-bearing one.</b> Asserting only that tenant B's key is present
	/// would pass against an implementation that had quietly merged the two groups, because the merged
	/// entry is reachable under a key that still looks right to a reader.
	/// </para>
	/// </remarks>
	[Fact]
	public async Task GiveEachTenantItsOwnRowWhenTwoTenantsCreateAGroupOfTheSameName()
	{
		// Arrange
		var store = new InMemoryActivityGroupStore();

		// Act
		var firstCreate = await store.CreateActivityGroupAsync(
			TenantA, SharedGroupName, ActivityName, CancellationToken.None);
		var secondCreate = await store.CreateActivityGroupAsync(
			TenantB, SharedGroupName, ActivityName, CancellationToken.None);

		// Assert -- the second create must genuinely write, not be absorbed as a duplicate.
		firstCreate.ShouldBe(1, "the first tenant's group is the arm's premise");
		secondCreate.ShouldBe(
			1,
			"the second tenant's create reported no row written, so its group was absorbed into the first "
			+ "tenant's entry. A caller that creates a group and is told nothing happened has had its "
			+ "record silently replaced by another tenant's.");

		// Assert -- two DISTINCT entries, each visible only to the tenant that owns it. The read takes the
		// tenant now, so the isolation property is asserted DIRECTLY rather than inferred from a count of
		// a cross-tenant result: a count of two says the entries did not fuse, and says nothing at all
		// about who can read them.
		var tenantAGroups = await store.FindActivityGroupsAsync(TenantA, CancellationToken.None);
		var tenantBGroups = await store.FindActivityGroupsAsync(TenantB, CancellationToken.None);

		// LIVENESS. Each tenant still reads back its OWN group -- without these, a store that returned
		// nothing to anybody would satisfy both confinement assertions below perfectly.
		tenantAGroups.ShouldContainKey(
			SegmentedKey.Compose(TenantA, SharedGroupName),
			"the first tenant's group must remain addressable under its own tenant");
		tenantBGroups.ShouldContainKey(
			SegmentedKey.Compose(TenantB, SharedGroupName),
			"the second tenant's group must be addressable under ITS tenant, not the first tenant's");

		// SAFETY. Neither tenant's read observes the other's catalogue at all.
		tenantAGroups.ShouldNotContainKey(
			SegmentedKey.Compose(TenantB, SharedGroupName),
			"one tenant's read returned a DIFFERENT tenant's group. Composing the key makes the foreign "
			+ "group unaddressable, which is not the same as unreadable -- the caller still receives the "
			+ "whole estate's catalogue and can enumerate every tenant's group names and activities.");
		tenantBGroups.ShouldNotContainKey(
			SegmentedKey.Compose(TenantA, SharedGroupName),
			"one tenant's read returned a DIFFERENT tenant's group.");

		tenantAGroups.Count.ShouldBe(
			1,
			"two tenants' same-named groups collapsed into one entry, or one tenant's read spanned both. "
			+ "A fused entry carries both tenants' activities, and the authorization decision path reads "
			+ "that membership as a grant -- which is the shape of a cross-tenant escalation, arriving "
			+ "through the store rather than through the decision path.");
	}

	/// <summary>
	/// The framework's own registration must produce a store that actually works — an inert provider is
	/// a RED, never a green.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>This is the arm the other two structurally cannot be.</b> Both arms above construct the store
	/// directly, and so does every arm on the decision path: <b>an arm that builds its own input can
	/// never observe the real one being absent.</b> That is not a lapse in those arms, it is what they
	/// are — and it is why this defect survived so long behind a green suite. On the relational providers
	/// the whole group path was inert, so every confinement assertion passed while authorizing nothing.
	/// </para>
	/// <para>
	/// So this arm hands the construction to the FRAMEWORK and asserts the resolved store behaves. It
	/// goes red on three distinct failures that a self-constructed arm reports as success: the
	/// registration is missing, the resolved store accepts a write and cannot read it back, or the write
	/// is silently discarded. A store that does nothing satisfies every safety assertion ever written
	/// about it.
	/// </para>
	/// <para>
	/// <b>Deliberately the round trip, not the registration.</b> Asserting that a descriptor exists would
	/// pass against a registered type whose methods are no-ops, which is the advertised-but-inert shape
	/// this arm exists to catch. The write must be observable through the same contract that reads it.
	/// </para>
	/// </remarks>
	[Fact]
	public async Task ResolveAnActivityGroupStoreThatActuallyWorksFromTheFrameworksOwnRegistration()
	{
		// Arrange -- the FRAMEWORK builds the store. This test deliberately does not.
		var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
		_ = services.AddExcaliburA3Core();
		await using var provider = services.BuildServiceProvider();

		// Act
		var store = provider.GetService<IActivityGroupStore>();

		// Assert -- the seam exists at all.
		_ = store.ShouldNotBeNull(
			"the framework's own registration produced no activity-group store, so every arm that "
			+ "constructs one itself is testing a type the host would never resolve");

		var written = await store.CreateActivityGroupAsync(
			TenantA, SharedGroupName, ActivityName, CancellationToken.None);

		written.ShouldBe(
			1,
			"the resolved store reported no row written for a group that did not previously exist, so the "
			+ "provider the framework registers is inert on its write path");

		// Assert -- and the write is OBSERVABLE through the read contract. This is what separates a
		// working provider from one that accepts everything and stores nothing.
		var groups = await store.FindActivityGroupsAsync(TenantA, CancellationToken.None);

		groups.ShouldContainKey(
			SegmentedKey.Compose(TenantA, SharedGroupName),
			"the resolved store accepted a write and could not read it back. An inert provider passes "
			+ "every tenant-confinement assertion perfectly, because it authorizes nothing at all -- which "
			+ "is exactly how this defect stayed green on the relational providers.");
	}
}

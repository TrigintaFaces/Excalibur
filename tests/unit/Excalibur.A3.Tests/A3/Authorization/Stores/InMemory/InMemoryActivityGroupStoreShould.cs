// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.A3.Authorization;
using Excalibur.Dispatch;
using Excalibur.A3.Authorization.Stores.InMemory;

using Tests.Shared.Infrastructure;

namespace Excalibur.Tests.A3.Authorization.Stores.InMemory;

/// <summary>
/// Unit tests for <see cref="InMemoryActivityGroupStore"/>.
/// Covers IActivityGroupStore contract: CRUD, grouping, GetService, concurrency.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "A3")]
public sealed class InMemoryActivityGroupStoreShould : UnitTestBase
{
	private readonly InMemoryActivityGroupStore _sut = new();
	private readonly CancellationToken _ct = CancellationToken.None;

	#region CreateActivityGroupAsync

	[Fact]
	public async Task CreateActivityGroup_NewEntry_ReturnsOne()
	{
		// Act
		var affected = await _sut.CreateActivityGroupAsync("tenant-1", "Orders", "View", _ct);

		// Assert
		affected.ShouldBe(1);
	}

	[Fact]
	public async Task CreateActivityGroup_DuplicateEntry_ReturnsZero()
	{
		// Arrange
		await _sut.CreateActivityGroupAsync("tenant-1", "Orders", "View", _ct);

		// Act -- same name+activity pair
		var affected = await _sut.CreateActivityGroupAsync("tenant-1", "Orders", "View", _ct);

		// Assert
		affected.ShouldBe(0);
	}

	[Fact]
	public async Task CreateActivityGroup_SameGroupDifferentActivity_ReturnOne()
	{
		// Arrange
		await _sut.CreateActivityGroupAsync("tenant-1", "Orders", "View", _ct);

		// Act
		var affected = await _sut.CreateActivityGroupAsync("tenant-1", "Orders", "Edit", _ct);

		// Assert
		affected.ShouldBe(1);
	}

	/// <summary>
	/// A blank tenant is REFUSED. This arm previously asserted that it succeeded, which certified
	/// behaviour the contract forbids: IActivityGroupStore documents tenantId as "Required; must be
	/// non-empty". An entry written without a tenant is addressable by no tenant-scoped read, and the
	/// untenanted partition is a declared value rather than the absence of one.
	/// </summary>
	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public async Task CreateActivityGroup_BlankTenantId_IsRefused(string? tenantId)
	{
		_ = await Should.ThrowAsync<ArgumentException>(
			() => _sut.CreateActivityGroupAsync(tenantId!, "Orders", "View", _ct));

		(await _sut.ActivityGroupExistsAsync("tenant-1", "Orders", _ct)).ShouldBeFalse(
			"a refused write must not leave the group behind");
	}

	#endregion

	#region ActivityGroupExistsAsync

	/// <summary>
	/// SAFETY: existence is answered for ONE tenant, never for the estate.
	/// </summary>
	/// <remarks>
	/// The precondition is only that two tenants chose the same group name — no race, no privileged
	/// actor. Before the tenant term existed, the lookup matched on the bare name and reported tenant A's
	/// group as tenant B's: a consumer using this as a create-if-not-exists guard would skip creating a
	/// group it does not have, and a consumer using it as a permission probe would be told yes on a
	/// catalogue it cannot see.
	/// </remarks>
	[Fact]
	public async Task NotReportAnotherTenantsGroupAsThisTenants()
	{
		await _sut.CreateActivityGroupAsync("tenant-a", "Support", "ViewTicket", _ct);

		(await _sut.ActivityGroupExistsAsync("tenant-b", "Support", _ct)).ShouldBeFalse(
			"tenant-b has no group called Support; only tenant-a does");
	}

	/// <summary>
	/// LIVENESS: the tenant that DOES own the group still observes it.
	/// </summary>
	/// <remarks>
	/// Without this, the arm above is satisfied by an implementation that answers false to everything.
	/// </remarks>
	[Fact]
	public async Task StillReportAGroupToTheTenantThatOwnsIt()
	{
		await _sut.CreateActivityGroupAsync("tenant-a", "Support", "ViewTicket", _ct);

		(await _sut.ActivityGroupExistsAsync("tenant-a", "Support", _ct)).ShouldBeTrue();
	}

	[Fact]
	public async Task ActivityGroupExists_WhenPresent_ReturnsTrue()
	{
		// Arrange
		await _sut.CreateActivityGroupAsync("tenant-1", "Orders", "View", _ct);

		// Act & Assert
		(await _sut.ActivityGroupExistsAsync("tenant-1", "Orders", _ct)).ShouldBeTrue();
	}

	[Fact]
	public async Task ActivityGroupExists_WhenAbsent_ReturnsFalse()
	{
		// Act & Assert
		(await _sut.ActivityGroupExistsAsync("tenant-1", "Nonexistent", _ct)).ShouldBeFalse();
	}

	[Fact]
	public async Task ActivityGroupExists_EmptyStore_ReturnsFalse()
	{
		// Act & Assert
		(await _sut.ActivityGroupExistsAsync("tenant-1", "Orders", _ct)).ShouldBeFalse();
	}

	[Fact]
	public async Task ActivityGroupExists_CaseSensitive()
	{
		// Arrange
		await _sut.CreateActivityGroupAsync("t", "Orders", "View", _ct);

		// Act & Assert -- exact match required
		(await _sut.ActivityGroupExistsAsync("t", "Orders", _ct)).ShouldBeTrue();
		(await _sut.ActivityGroupExistsAsync("t", "orders", _ct)).ShouldBeFalse();
		(await _sut.ActivityGroupExistsAsync("t", "ORDERS", _ct)).ShouldBeFalse();
	}

	#endregion

	#region FindActivityGroupsAsync

	[Fact]
	public async Task FindActivityGroups_ReturnsGroupedByName()
	{
		// Arrange
		await _sut.CreateActivityGroupAsync("t", "Orders", "View", _ct);
		await _sut.CreateActivityGroupAsync("t", "Orders", "Edit", _ct);
		await _sut.CreateActivityGroupAsync("t", "Products", "View", _ct);

		// Act
		var result = await _sut.FindActivityGroupsAsync("t", _ct);

		// Assert
		result.Count.ShouldBe(2);
		result.ShouldContainKey(SegmentedKey.Compose("t", "Orders"));
		result.ShouldContainKey(SegmentedKey.Compose("t", "Products"));

		var ordersActivities = result[SegmentedKey.Compose("t", "Orders")] as List<string>;
		ordersActivities.ShouldNotBeNull();
		ordersActivities.Count.ShouldBe(2);
		ordersActivities.ShouldContain("View");
		ordersActivities.ShouldContain("Edit");
	}

	/// <summary>
	/// THE ESCALATION ARM. Two tenants, the same group name, different activities.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The precondition for the escalation is ONLY that two tenants pick the same group name. No race,
	/// no privileged actor, no crafted input. Before the fix, entries were keyed on (name, activity) and
	/// grouped on name alone, so the two tenants' activities merged into one set and a member of tenant
	/// A's "Support" group was authorized for an activity only tenant B's catalogue confers.
	/// </para>
	/// <para>
	/// <b>Why the sibling grouping test does not cover this, and why it must not be relied on.</b>
	/// <c>FindActivityGroups_ReturnsGroupedByName</c> uses a SINGLE tenant. It binds "the key is
	/// composed" - it goes red against the old shape because the key changes - but it says nothing about
	/// ISOLATION. A store that composes the key and still merges two tenants' activities somewhere else
	/// passes it. The property that matters is not the key's spelling; it is that tenant A cannot
	/// observe tenant B's activity.
	/// </para>
	/// <para>
	/// <b>This is a store-level arm on purpose.</b> The authorization cache keys activity groups on a
	/// constant with no tenant term, so a test driven through the policy would confound the store's
	/// behaviour with the cache's. Nothing here touches the cache, so a failure here is the store.
	/// </para>
	/// <para>
	/// <b>Scope, stated so a green is not over-read:</b> this binds the READ/merge half only. The
	/// estate-wide <c>ReplaceAllActivityGroupsAsync</c> is bound by its own region below.
	/// </para>
	/// </remarks>
	[Fact]
	public async Task FindActivityGroups_TwoTenantsShareAGroupName_DoesNotMergeTheirActivities()
	{
		// Arrange - tenant A's "Support" group confers a read activity.
		_ = await _sut.CreateActivityGroupAsync("tenant-a", "Support", "ViewTicket", _ct);

		// Tenant B names its group the same and confers a materially more powerful activity.
		var tenantBCreated = await _sut.CreateActivityGroupAsync("tenant-b", "Support", "IssueRefund", _ct);

		// LIVENESS, and it is the first thing that broke: with the tenant absent from the key this
		// returned 0 - TryAdd saw a duplicate - so tenant B's group was silently never created and
		// tenant A's row was kept, carrying tenant A's TenantId.
		tenantBCreated.ShouldBe(
			1,
			"tenant B's group was not created at all. Two tenants choosing the same group name is not a "
			+ "duplicate, and silently discarding the second one leaves B's grant attributed to A.");

		// Act -- each tenant's own view. The read takes the tenant now, so a merge can only reach a tenant
		// by surfacing INSIDE that tenant's own result.
		var result = await _sut.FindActivityGroupsAsync("tenant-a", _ct);
		var tenantBResult = await _sut.FindActivityGroupsAsync("tenant-b", _ct);

		// SAFETY, ASSERTED WITHOUT REFERENCE TO THE KEY SPELLING, AND FIRST ON PURPOSE.
		//
		// Every key-shaped assertion below would fail under the old implementation simply because the
		// key changes from "Support" to "tenant-a:Support". If those ran first, this arm would redden on
		// the KEY SHAPE and tell us nothing about whether the MERGE was fixed - which is precisely the
		// weakness that lets the sibling grouping test pass while the escalation is live. So the merge
		// is refuted here, in a form that holds however the dictionary is keyed.
		foreach (var (groupKey, groupValue) in result.Concat(tenantBResult))
		{
			var activities = groupValue as List<string>;
			activities.ShouldNotBeNull();

			(activities.Contains("ViewTicket", StringComparer.Ordinal)
				&& activities.Contains("IssueRefund", StringComparer.Ordinal)).ShouldBeFalse(
				$"CROSS-TENANT PRIVILEGE ESCALATION: the group '{groupKey}' carries BOTH tenants' "
				+ "activities in one set, so a member of one tenant's group is authorized for an "
				+ "activity only the other tenant's catalogue confers. The entire precondition is that "
				+ "both tenants picked the same group name.");
		}

		var tenantAKey = SegmentedKey.Compose("tenant-a", "Support");
		var tenantBKey = SegmentedKey.Compose("tenant-b", "Support");

		// LIVENESS - each tenant still observes its own group, in its OWN result. Without these two, a
		// store that returned nothing to anybody would satisfy every safety assertion below.
		result.ShouldContainKey(tenantAKey, "tenant A can no longer see its own group");
		tenantBResult.ShouldContainKey(tenantBKey, "tenant B can no longer see its own group");

		// SAFETY - and neither tenant's read reaches the other's catalogue at all. Composing the key makes
		// a foreign group unaddressable, which is NOT the same as unreadable: before the read took a tenant,
		// every caller received the whole estate and could enumerate it.
		result.ShouldNotContainKey(tenantBKey, "tenant A's read returned tenant B's group");
		tenantBResult.ShouldNotContainKey(tenantAKey, "tenant B's read returned tenant A's group");

		var tenantAActivities = result[tenantAKey] as List<string>;
		var tenantBActivities = tenantBResult[tenantBKey] as List<string>;
		tenantAActivities.ShouldNotBeNull();
		tenantBActivities.ShouldNotBeNull();

		tenantAActivities.ShouldContain("ViewTicket", "tenant A lost its own activity");
		tenantBActivities.ShouldContain("IssueRefund", "tenant B lost its own activity");

		// SAFETY - the escalation itself, in both directions.
		tenantAActivities.ShouldNotContain(
			"IssueRefund",
			"CROSS-TENANT PRIVILEGE ESCALATION: a member of tenant A's group is authorized for an "
			+ "activity that only tenant B's catalogue confers, because the two tenants' groups were "
			+ "merged on name alone. The entire precondition is that both tenants picked the same name.");

		tenantBActivities.ShouldNotContain(
			"ViewTicket",
			"the merge is symmetric - tenant B observes an activity only tenant A conferred. Asserted in "
			+ "both directions because a one-directional check passes a store that merges into whichever "
			+ "group it happened to enumerate second.");
	}

	/// <summary>
	/// The IDENTITY half: two tenants conferring the SAME activity under the SAME group name.
	/// </summary>
	/// <remarks>
	/// <para>
	/// This exists because the escalation arm above does NOT cover it, and I only learned that by
	/// severing the two halves of the fix independently. That arm gives the two tenants DIFFERENT
	/// activities, so the entry keys differ and never collide - reverting the key to omit the tenant
	/// leaves it green. The merge and the identity are two defects at one site, and an arm for one is
	/// not an arm for the other.
	/// </para>
	/// <para>
	/// With the tenant absent from the entry key, the second tenant's registration collides with the
	/// first, <c>TryAdd</c> reports false, and the store returns 0 having stored nothing - while the
	/// surviving row carries the FIRST tenant's identity. The second tenant's grant is silently
	/// attributed to the first, which is a write-side escalation rather than a read-side one.
	/// </para>
	/// </remarks>
	[Fact]
	public async Task CreateActivityGroup_TwoTenantsSameGroupAndActivity_AreDistinctRegistrations()
	{
		var tenantACreated = await _sut.CreateActivityGroupAsync("tenant-a", "Support", "ViewTicket", _ct);
		var tenantBCreated = await _sut.CreateActivityGroupAsync("tenant-b", "Support", "ViewTicket", _ct);

		tenantACreated.ShouldBe(1, "tenant A's own registration did not take effect");

		tenantBCreated.ShouldBe(
			1,
			"tenant B's registration was silently discarded as a duplicate of tenant A's. Two tenants "
			+ "conferring the same activity under the same group name are two distinct grants, not one - "
			+ "the tenant is part of the identity, not a field beside it. The row that survives carries "
			+ "tenant A's identity, so tenant B's grant is recorded against the wrong tenant.");

		// LIVENESS - both registrations are observable afterwards, so this cannot be satisfied by a
		// store that reports success and stores nothing.
		(await _sut.FindActivityGroupsAsync("tenant-a", _ct))
			.ShouldContainKey(SegmentedKey.Compose("tenant-a", "Support"));
		(await _sut.FindActivityGroupsAsync("tenant-b", _ct))
			.ShouldContainKey(SegmentedKey.Compose("tenant-b", "Support"));
	}

	[Fact]
	public async Task FindActivityGroups_EmptyStore_ReturnsEmptyDictionary()
	{
		// Act
		var result = await _sut.FindActivityGroupsAsync("t", _ct);

		// Assert
		result.ShouldBeEmpty();
	}

	[Fact]
	public async Task FindActivityGroups_SingleEntry_ReturnsSingleGroup()
	{
		// Arrange
		await _sut.CreateActivityGroupAsync("t", "Admin", "ManageUsers", _ct);

		// Act
		var result = await _sut.FindActivityGroupsAsync("t", _ct);

		// Assert
		result.Count.ShouldBe(1);
		result.ShouldContainKey(SegmentedKey.Compose("t", "Admin"));
	}

	#endregion

	#region ReplaceAllActivityGroupsAsync

	[Fact]
	public async Task ReplaceAllActivityGroups_ReportsThePreviousTenants()
	{
		// Arrange
		await _sut.CreateActivityGroupAsync("t", "Orders", "View", _ct);
		await _sut.CreateActivityGroupAsync("t", "Orders", "Edit", _ct);
		await _sut.CreateActivityGroupAsync("t", "Products", "View", _ct);

		// Act
		var previous = await _sut.ReplaceAllActivityGroupsAsync(Catalogue(("u", "Orders", "View")), _ct);

		// Assert -- the tenants that held groups, deduplicated. Three rows, one tenant: a caller
		// invalidating per-tenant derived state needs the partitions, and a row count cannot name them.
		previous.ShouldHaveSingleItem().ShouldBe("t");
		(await _sut.FindActivityGroupsAsync("t", _ct)).ShouldBeEmpty();
	}

	[Fact]
	public async Task ReplaceAllActivityGroups_EmptyStore_ReportsNoTenants()
	{
		// Act
		var previous = await _sut.ReplaceAllActivityGroupsAsync(Catalogue(("t", "Orders", "View")), _ct);

		// Assert -- nothing was there before, so there is nothing for a caller to invalidate.
		previous.ShouldBeEmpty();
	}

	/// <summary>
	/// Every tenant that held groups is reported, not just the first one found.
	/// </summary>
	/// <remarks>
	/// This is the arm that makes the return value worth having. A store reporting one arbitrary tenant
	/// satisfies the single-tenant arm above perfectly, and the caller then leaves every other tenant's
	/// derived authorization state pointing at groups that no longer exist -- an over-grant, for the
	/// lifetime of that state, that nothing surfaces.
	/// </remarks>
	[Fact]
	public async Task ReplaceAllActivityGroups_ReportsEveryTenantNotJustOne()
	{
		// Arrange
		_ = await _sut.CreateActivityGroupAsync("tenant-a", "Support", "ViewTicket", _ct);
		_ = await _sut.CreateActivityGroupAsync("tenant-b", "Support", "IssueRefund", _ct);
		_ = await _sut.CreateActivityGroupAsync("tenant-b", "Billing", "Refund", _ct);

		// Act
		var previous = await _sut.ReplaceAllActivityGroupsAsync(Catalogue(("tenant-c", "Support", "ViewTicket")), _ct);

		// Assert -- SAFETY: no tenant that held groups is omitted.
		previous.ShouldContain("tenant-a");
		previous.ShouldContain("tenant-b");

		// LIVENESS: and it is a DEDUPLICATED tenant set, not one entry per row.
		previous.Count.ShouldBe(2);
	}

	[Fact]
	public async Task ReplaceAllActivityGroups_LeavesExactlyTheCatalogue()
	{
		// Arrange
		await _sut.CreateActivityGroupAsync("t", "Orders", "View", _ct);

		// Act
		_ = await _sut.ReplaceAllActivityGroupsAsync(Catalogue(("t", "Products", "Edit"), ("t", "Products", "View")), _ct);

		// Assert -- the old group is gone, and the new one carries exactly the catalogue's activities.
		var groups = await _sut.FindActivityGroupsAsync("t", _ct);
		groups.ShouldHaveSingleItem();
		groups.Values.Single().ShouldBe(["Edit", "View"], ignoreOrder: true);
	}

	[Fact]
	public async Task ReplaceAllActivityGroups_StoreIsReusableAfterReplace()
	{
		// Arrange
		await _sut.CreateActivityGroupAsync("t", "Orders", "View", _ct);
		_ = await _sut.ReplaceAllActivityGroupsAsync(Catalogue(("t", "Orders", "Edit")), _ct);

		// Act
		var affected = await _sut.CreateActivityGroupAsync("t", "Products", "Edit", _ct);

		// Assert
		affected.ShouldBe(1);
	}

	/// <summary>
	/// A concurrent reader observes either the whole previous catalogue or the whole new one, never a mixture.
	/// </summary>
	/// <remarks>
	/// Clearing the store and refilling it passes every other arm in this region, and lets a reader between the
	/// clear and the last insert see an empty or partial catalogue -- which the authorization path reads as the
	/// tenant having fewer groups than it does. Two whole catalogues alternate while readers run; every read
	/// must equal one of them exactly.
	/// </remarks>
	[Fact]
	public async Task ReplaceAllActivityGroups_NeverShowsAReaderAPartialCatalogue()
	{
		const int Size = 200;
		var first = new ActivityGroupCatalogue(Enumerable.Range(0, Size).Select(i => new ActivityGroupEntry("t", "First", $"a{i}")));
		var second = new ActivityGroupCatalogue(Enumerable.Range(0, Size).Select(i => new ActivityGroupEntry("t", "Second", $"a{i}")));
		_ = await _sut.ReplaceAllActivityGroupsAsync(first, _ct);

		using var stop = new CancellationTokenSource();
		var torn = 0;
		var reads = 0;
		var live = 0;

		const int Readers = 4;

		var readers = Enumerable.Range(0, Readers).Select(_ => Task.Run(
			async () =>
			{
				var counted = false;

				while (!stop.IsCancellationRequested)
				{
					var groups = await _sut.FindActivityGroupsAsync("t", _ct);
					_ = Interlocked.Increment(ref reads);

					if (groups.Count != 1 || groups.Values.Single().Count != Size)
					{
						_ = Interlocked.Increment(ref torn);
					}

					if (!counted)
					{
						counted = true;
						_ = Interlocked.Increment(ref live);
					}

					// The in-memory read completes synchronously, so without this the loop never yields and
					// four of these monopolise a small runner -- starving the writer whose replacements are
					// the thing being observed.
					await Task.Yield();
				}
			},
			_ct)).ToArray();

		// Task.Run QUEUES a reader; it does not start one. On a constrained runner the writer below can
		// finish all 200 replacements and cancel before any reader is dequeued -- every reader then sees
		// cancellation on its first loop check and exits having read nothing, and the arm fails on
		// "reads == 0". That is the assertion behaving correctly and the test observing nothing, which is
		// a scheduling artifact rather than a defect in the store. So the writer does not start until
		// every reader has completed a read and is demonstrably in its loop.
		var allLive = await WaitHelpers.WaitUntilAsync(
			() => Volatile.Read(ref live) == Readers,
			TimeSpan.FromSeconds(30));

		allLive.ShouldBeTrue(
			$"only {Volatile.Read(ref live)} of {Readers} readers reached their first read within 30s, so the "
			+ "tearing window was never actually observed");

		for (var i = 0; i < 200; i++)
		{
			_ = await _sut.ReplaceAllActivityGroupsAsync(i % 2 == 0 ? second : first, _ct);
		}

		await stop.CancelAsync();
		await Task.WhenAll(readers);

		reads.ShouldBeGreaterThan(0, "the readers never ran, so nothing was observed");
		torn.ShouldBe(0, $"{torn} of {reads} reads observed a partial catalogue");
	}

	private static ActivityGroupCatalogue Catalogue(params (string Tenant, string Name, string Activity)[] entries) =>
		new(entries.Select(static e => new ActivityGroupEntry(e.Tenant, e.Name, e.Activity)));

	#endregion

	#region GetService

	/// <summary>
	/// The capability probe answers for what this store IS, and for nothing else.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>Reached through <see cref="IServiceProvider"/>, because that is where the member lives now.</b>
	/// The store inherits the BCL interface rather than hand-declaring a look-alike of it, so a consumer
	/// can hand it to anything that accepts an <see cref="IServiceProvider"/> — which is precisely what a
	/// caller does here.
	/// </para>
	/// <para>
	/// <b>The positive half is the one that was broken and the one this arm previously lacked.</b> Every
	/// store overrode the probe with an unconditional <c>return null</c>, contradicting the contract's own
	/// default — so a caller asking the store for the capability it demonstrably implements got nothing.
	/// An arm that only asserts what the probe REFUSES is satisfied by a probe that refuses everything,
	/// which is exactly the defect that shipped.
	/// </para>
	/// </remarks>
	[Fact]
	public void GetService_AnswersForItsOwnContract_AndNothingElse()
	{
		var probe = (IServiceProvider)_sut;

		// LIVENESS -- it answers for the contract it implements.
		probe.GetService(typeof(IActivityGroupStore)).ShouldBeSameAs(
			_sut,
			"the store refused the capability it itself implements, so a consumer treating it as an "
			+ "IServiceProvider cannot resolve it at all");

		// SAFETY -- and it claims nothing it does not implement.
		probe.GetService(typeof(IActivityGroupGrantStore)).ShouldBeNull();
		probe.GetService(typeof(IGrantStore)).ShouldBeNull();
		probe.GetService(typeof(IGrantQueryStore)).ShouldBeNull();
		probe.GetService(typeof(string)).ShouldBeNull();
	}

	[Fact]
	public void GetService_NullType_ThrowsArgumentNullException()
	{
		// Act & Assert
		// Through the interface: the member is inherited from IServiceProvider, not declared here. The
		// default implementation keeps the null guard, so the contract is unchanged.
		_ = Should.Throw<ArgumentNullException>(() => ((IServiceProvider)_sut).GetService(null!));
	}

	#endregion

	#region Concurrency

	[Fact]
	public async Task ConcurrentCreateAndRead_IsThreadSafe()
	{
		// Arrange
		const int count = 100;
		var tasks = new List<Task>(count * 2);

		for (var i = 0; i < count; i++)
		{
			var name = $"Group-{i}";
			tasks.Add(_sut.CreateActivityGroupAsync("t", name, $"Activity-{i}", _ct));
			tasks.Add(_sut.ActivityGroupExistsAsync("t", name, _ct));
		}

		// Act & Assert -- should not throw
		await Task.WhenAll(tasks);

		// Verify all writes landed
		for (var i = 0; i < count; i++)
		{
			(await _sut.ActivityGroupExistsAsync("t", $"Group-{i}", _ct)).ShouldBeTrue();
		}
	}

	[Fact]
	public async Task ConcurrentCreateAndReplaceAll_IsThreadSafe()
	{
		// Arrange -- pre-populate
		for (var i = 0; i < 50; i++)
		{
			await _sut.CreateActivityGroupAsync("t", $"Group-{i}", $"Act-{i}", _ct);
		}

		// Act -- concurrent create + replace interleaved
		var tasks = new List<Task>();
		for (var i = 50; i < 100; i++)
		{
			tasks.Add(_sut.CreateActivityGroupAsync("t", $"Group-{i}", $"Act-{i}", _ct));
		}

		tasks.Add(_sut.ReplaceAllActivityGroupsAsync(Catalogue(("t", "Replaced", "Act")), _ct));

		// Assert -- should not throw (final state is non-deterministic but no exceptions)
		await Task.WhenAll(tasks);
	}

	#endregion

	#region Edge Cases

	/// <summary>
	/// An empty NAME and ACTIVITY are still tolerated -- the contract states no precondition on either,
	/// and this arm exists to pin that tolerance. Only the TENANT is required to be non-empty, so the
	/// tenant is supplied here and the blank-tenant case is covered by its own arm above. Asserting the
	/// whole call succeeded with every argument blank would have re-certified the tenant defect under a
	/// different name.
	/// </summary>
	[Fact]
	public async Task CreateActivityGroup_EmptyNameAndActivity_StillSucceeds()
	{
		var affected = await _sut.CreateActivityGroupAsync("t", "", "", _ct);

		affected.ShouldBe(1);
		(await _sut.ActivityGroupExistsAsync("t", "", _ct)).ShouldBeTrue();
	}

	[Fact]
	public async Task FindActivityGroups_ForATenantTheReplaceDropped_ReturnsEmpty()
	{
		// Arrange
		await _sut.CreateActivityGroupAsync("t", "Orders", "View", _ct);
		_ = await _sut.ReplaceAllActivityGroupsAsync(Catalogue(("other", "Orders", "View")), _ct);

		// Act
		var result = await _sut.FindActivityGroupsAsync("t", _ct);

		// Assert
		result.ShouldBeEmpty();
	}

	#endregion
}

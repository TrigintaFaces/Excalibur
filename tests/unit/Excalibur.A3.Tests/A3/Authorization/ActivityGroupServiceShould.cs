// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Net;
using System.Text;

using Excalibur.A3.Authentication;
using Excalibur.A3.Authorization;
using Excalibur.A3.Authorization.Grants;
using Excalibur.Data;
using Excalibur.Dispatch;
using Excalibur.Domain;

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.Tests.A3.Authorization;

/// <summary>
/// Locks the sync payload parsing. These read a real JSON body through the service's own
/// deserialization path, so they fail if that path stops producing populated objects — the failure
/// mode a compile-time-only check cannot see.
/// </summary>
// ApplicationContext.Init clears process-wide state, so every class that calls it shares one
// collection. Without this the class ran in parallel with the others and wiped the keys they had
// just installed, which surfaces as an InvalidConfigurationException in whichever class lost the race.
[Collection("ApplicationContext")]
[Trait("Category", "Unit")]
[Trait("Component", "A3")]
public sealed class ActivityGroupServiceShould : IDisposable
{
	private const string ActivityGroupsPayload = """
		[
		  {
		    "TenantId": "acme",
		    "Name": "Billing",
		    "Description": "Billing operations",
		    "Activities": [
		      { "ApplicationName": "Portal", "ActivityName": "Invoice.Read" }
		    ]
		  }
		]
		""";

	private const string GrantsPayload = """
		[
		  {
		    "TenantId": "acme",
		    "ActivityGroupName": "Billing",
		    "ExpiresOn": "2030-01-02T03:04:05+00:00",
		    "UserId": "user-1"
		  }
		]
		""";

	private readonly IActivityGroupStore _groupStore = A.Fake<IActivityGroupStore>();

	// Carries the atomic-replace capability, like the SQL Server, PostgreSQL and in-memory stores. The arms
	// that exercise a store WITHOUT it use _nonAtomicGrantStore below.
	private readonly IActivityGroupGrantStore _grantStore =
		A.Fake<IActivityGroupGrantStore>(options => options.Implements<IActivityGroupGrantReplacement>());

	private readonly IActivityGroupGrantStore _nonAtomicGrantStore = A.Fake<IActivityGroupGrantStore>();

	private readonly IDistributedCache _cache = A.Fake<IDistributedCache>();

	public ActivityGroupServiceShould() =>
		ApplicationContext.Init(new Dictionary<string, string?>
		{
			["ApplicationName"] = "TestApp",
		});

	public void Dispose() => ApplicationContext.Reset();

	[Fact]
	public async Task Populate_every_activity_group_field_from_the_sync_payload()
	{
		var sut = CreateService(ActivityGroupsPayload);

		await sut.SyncActivityGroupsAsync(CancellationToken.None);

		A.CallTo(() => _groupStore.ReplaceAllActivityGroupsAsync(
				A<ActivityGroupCatalogue>.That.Matches(c =>
					c.Entries.Count == 1 && c.Entries[0] == new ActivityGroupEntry("acme", "Billing", "Invoice.Read")),
				A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task Populate_every_grant_field_from_the_sync_payload()
	{
		var sut = CreateService(GrantsPayload);

		await sut.SyncActivityGroupGrantsAsync("user-1", CancellationToken.None);

		A.CallTo(() => ((IActivityGroupGrantReplacement)_grantStore).ReplaceActivityGroupGrantsForUserAsync(
				"user-1",
				GrantType.ActivityGroup,
				A<ActivityGroupGrantSnapshot>.That.Matches(s =>
					s.Entries.Count == 1
					&& s.Entries[0].UserId == "user-1"
					&& s.Entries[0].TenantId == "acme"
					&& s.Entries[0].GrantType == GrantType.ActivityGroup
					&& s.Entries[0].Qualifier == "Billing"
					&& s.Entries[0].ExpiresOn == new DateTimeOffset(2030, 1, 2, 3, 4, 5, TimeSpan.Zero)),
				A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();

		// One atomic replace, and no per-row writes beside it: a row written outside the replace is a row a
		// concurrent reader could see without the rest of the user's grants.
		A.CallTo(() => _grantStore.InsertActivityGroupGrantAsync(
				A<string>._, A<string>._, A<string>._, A<string>._, A<string>._, A<DateTimeOffset?>._, A<string>._,
				A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	/// <summary>
	/// A store WITHOUT the atomic replace, under the option that accepts the window, still applies the whole
	/// payload through the delete-then-insert path.
	/// </summary>
	/// <remarks>
	/// This is the arm that keeps <see cref="GrantSyncAtomicity.BestEffort"/> honest. Without it, the option
	/// could refuse everything — the safety arm below would still pass — and the four document-database
	/// providers would be left with no working sync at all, which is the outcome the option exists to avoid.
	/// </remarks>
	[Fact]
	public async Task Populate_every_grant_field_through_the_non_atomic_path_when_the_window_is_accepted()
	{
		var sut = CreateService(GrantsPayload, _nonAtomicGrantStore, GrantSyncAtomicity.BestEffort);

		await sut.SyncActivityGroupGrantsAsync("user-1", CancellationToken.None);

		A.CallTo(() => _nonAtomicGrantStore.DeleteActivityGroupGrantsByUserIdAsync(
				"user-1", GrantType.ActivityGroup, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
		A.CallTo(() => _nonAtomicGrantStore.InsertActivityGroupGrantAsync(
				"user-1",
				"user-1",
				"acme",
				GrantType.ActivityGroup,
				"Billing",
				new DateTimeOffset(2030, 1, 2, 3, 4, 5, TimeSpan.Zero),
				A<string>._,
				A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	/// <summary>
	/// A store without the atomic replace is refused under the default option, and nothing is deleted.
	/// </summary>
	[Fact]
	public async Task Refuse_the_grant_sync_on_a_non_atomic_store_when_the_option_requires_atomicity()
	{
		var sut = CreateService(GrantsPayload, _nonAtomicGrantStore, GrantSyncAtomicity.Required);

		var failure = await Should.ThrowAsync<InvalidOperationException>(
			() => sut.SyncActivityGroupGrantsAsync("user-1", CancellationToken.None));

		failure.Message.ShouldContain(nameof(GrantSyncAtomicity.BestEffort));
		A.CallTo(() => _nonAtomicGrantStore.DeleteActivityGroupGrantsByUserIdAsync(
				A<string>._, A<string>._, A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	/// <summary>
	/// A per-user sync whose authority returns an EMPTY list revokes every activity-group grant that user held.
	/// </summary>
	/// <remarks>
	/// This is the one place an empty payload is applied rather than refused, and the asymmetry is the
	/// requirement: a user who now belongs to no group must lose the grants they held, or a revocation the
	/// authority performed never takes effect here.
	/// </remarks>
	[Fact]
	public async Task Apply_an_empty_per_user_grant_payload_as_a_revocation()
	{
		var sut = CreateService("[]");

		await sut.SyncActivityGroupGrantsAsync("user-1", CancellationToken.None);

		A.CallTo(() => ((IActivityGroupGrantReplacement)_grantStore).ReplaceActivityGroupGrantsForUserAsync(
				"user-1",
				GrantType.ActivityGroup,
				A<ActivityGroupGrantSnapshot>.That.Matches(s => s.Entries.Count == 0),
				A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	/// <summary>
	/// A per-user sync whose body did not deserialize to a list is a FAILED FETCH, and is refused rather than
	/// applied as "this user now holds nothing".
	/// </summary>
	/// <remarks>
	/// The arm above makes an empty list revoke, which is correct and is exactly what makes this one
	/// load-bearing: without it, any response the deserializer cannot read would silently revoke the user's
	/// access. Could-not-fetch must never be read as has-nothing.
	/// </remarks>
	[Fact]
	public async Task Refuse_a_per_user_grant_body_that_did_not_deserialize_to_a_list()
	{
		var sut = CreateService("null");

		_ = await Should.ThrowAsync<OperationFailedException>(
			() => sut.SyncActivityGroupGrantsAsync("user-1", CancellationToken.None));

		A.CallTo(() => ((IActivityGroupGrantReplacement)_grantStore).ReplaceActivityGroupGrantsForUserAsync(
				A<string>._, A<string>._, A<ActivityGroupGrantSnapshot>._, A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	/// <summary>
	/// The ESTATE-WIDE grant sync refuses an empty payload: it would revoke every user's activity-group grants
	/// at once, and an empty response cannot be told apart from a fetch that returned nothing.
	/// </summary>
	[Fact]
	public async Task Refuse_an_empty_estate_wide_grant_payload()
	{
		var sut = CreateService("[]");

		_ = await Should.ThrowAsync<OperationFailedException>(
			() => sut.SyncAllActivityGroupGrantsAsync(CancellationToken.None));

		A.CallTo(() => ((IActivityGroupGrantReplacement)_grantStore).ReplaceActivityGroupGrantsAsync(
				A<string>._, A<ActivityGroupGrantSnapshot>._, A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	/// <summary>
	/// A user the estate-wide replace reports as having held grants is invalidated, even though the payload no
	/// longer names them.
	/// </summary>
	/// <remarks>
	/// A user the payload dropped appears only in what the replace reports, from inside its transaction.
	/// Invalidating only the payload's users would leave the dropped user's cached grants conferring access
	/// the sync existed to revoke.
	/// </remarks>
	[Fact]
	public async Task Invalidate_a_user_the_estate_wide_grant_payload_dropped()
	{
		var sut = CreateService(GrantsPayload);

		// Configured AFTER the service is built: the builder installs a default empty reply for this call, and
		// in FakeItEasy the later rule wins.
		A.CallTo(() => ((IActivityGroupGrantReplacement)_grantStore).ReplaceActivityGroupGrantsAsync(
				A<string>._, A<ActivityGroupGrantSnapshot>._, A<CancellationToken>._))
			.Returns<IReadOnlyCollection<string>>(["dropped-user"]);

		await sut.SyncAllActivityGroupGrantsAsync(CancellationToken.None);

		A.CallTo(() => _cache.RemoveAsync(AuthorizationCacheKey.ForGrants("dropped-user"), A<CancellationToken>._))
			.MustHaveHappened();
		A.CallTo(() => _cache.RemoveAsync(AuthorizationCacheKey.ForGrants("user-1"), A<CancellationToken>._))
			.MustHaveHappened();
	}

	/// <summary>
	/// A payload that cannot be applied must leave the existing catalogue INTACT — validation precedes
	/// destruction.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>This arm used to assert only the throw, and that is why it certified the defect GREEN.</b> The
	/// sync deletes every tenant's activity groups FIRST and validates each entry only while repopulating,
	/// so a single entry with a null tenant empties the catalogue for EVERY tenant and then throws with
	/// nothing restored. The old assertion was satisfied by exactly that outcome: the exception it wanted
	/// is the one the wipe produces.
	/// </para>
	/// <para>
	/// A safety assertion of the form "the bad input is rejected" is satisfied by a component that rejects
	/// it AFTER destroying the data. The missing half is WHAT SURVIVED, and it is the only half that
	/// distinguishes a validating sync from a destructive one.
	/// </para>
	/// </remarks>
	[Fact]
	public async Task Reject_a_payload_entry_that_omits_the_tenant_WITHOUT_emptying_the_catalogue()
	{
		var sut = CreateService(ActivityGroupsPayload.Replace("\"TenantId\": \"acme\"", "\"TenantId\": null", StringComparison.Ordinal));

		_ = await Should.ThrowAsync<InvalidOperationException>(
			() => sut.SyncActivityGroupsAsync(CancellationToken.None));

		// THE ASSERTION THE ORIGINAL ARM LACKED. An unappliable payload must not have reached the store at all:
		// the replace makes the payload the whole catalogue, so reaching it with an incomplete payload would
		// replace every tenant's groups with less than the authority holds.
		A.CallTo(() => _groupStore.ReplaceAllActivityGroupsAsync(A<ActivityGroupCatalogue>._, A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	/// <summary>
	/// LIVENESS. The paired half: a VALID payload still replaces the catalogue. Without this, a sync that
	/// refused every payload — never deleting, never creating — would satisfy the safety arm above and be
	/// indistinguishable from a correct one.
	/// </summary>
	[Fact]
	public async Task Still_replace_the_catalogue_when_the_payload_is_valid()
	{
		var sut = CreateService(ActivityGroupsPayload);

		await sut.SyncActivityGroupsAsync(CancellationToken.None);

		// One atomic replace, and no per-row writes beside it: a row written outside the replace is a row a
		// concurrent reader could see without the rest of the catalogue.
		A.CallTo(() => _groupStore.ReplaceAllActivityGroupsAsync(A<ActivityGroupCatalogue>._, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
		A.CallTo(() => _groupStore.CreateActivityGroupAsync(
				A<string>._, A<string>._, A<string>._, A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	/// <summary>
	/// A payload entry with leading or trailing whitespace is refused before the catalogue is touched.
	/// </summary>
	/// <remarks>
	/// SQL Server ignores trailing spaces when it compares keys, so such an entry names a different group there
	/// than on the other providers. The same payload has to mean the same thing everywhere, so it is refused.
	/// </remarks>
	[Fact]
	public async Task Refuse_a_payload_entry_with_surrounding_whitespace_BEFORE_replacing_the_catalogue()
	{
		var sut = CreateService(ActivityGroupsPayload.Replace("\"Name\": \"Billing\"", "\"Name\": \"Billing \"", StringComparison.Ordinal));

		_ = await Should.ThrowAsync<ArgumentException>(
			() => sut.SyncActivityGroupsAsync(CancellationToken.None));

		A.CallTo(() => _groupStore.ReplaceAllActivityGroupsAsync(A<ActivityGroupCatalogue>._, A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	/// <summary>
	/// A tenant the replace reports as having held groups before is invalidated, even though the payload no
	/// longer names it.
	/// </summary>
	/// <remarks>
	/// A tenant the payload dropped appears only in what the replace reports. Invalidating only the payload's
	/// tenants would leave the dropped tenant's cached catalogue conferring activities that no longer exist.
	/// </remarks>
	[Fact]
	public async Task Invalidate_a_tenant_the_payload_dropped()
	{
		A.CallTo(() => _groupStore.ReplaceAllActivityGroupsAsync(A<ActivityGroupCatalogue>._, A<CancellationToken>._))
			.Returns<IReadOnlyCollection<string>>(["globex"]);
		var sut = CreateService(ActivityGroupsPayload);

		await sut.SyncActivityGroupsAsync(CancellationToken.None);

		A.CallTo(() => _cache.RemoveAsync(AuthorizationCacheKey.ForActivityGroups("globex"), A<CancellationToken>._))
			.MustHaveHappened();
		A.CallTo(() => _cache.RemoveAsync(AuthorizationCacheKey.ForActivityGroups("acme"), A<CancellationToken>._))
			.MustHaveHappened();
	}

	/// <summary>
	/// A grant row naming no tenant is refused BEFORE the user's existing grants are deleted.
	/// </summary>
	/// <remarks>
	/// The per-user grant sync deletes the user's activity-group grants and then re-inserts from the payload.
	/// Validating each row's tenant only while re-inserting means one tenantless row removes every grant the
	/// user held and then throws with nothing restored: the refusal is correct and the user has already lost
	/// their permissions. The safety half is WHAT SURVIVED, so the arm asserts the delete never ran.
	/// </remarks>
	[Fact]
	public async Task Refuse_a_grant_row_without_a_tenant_BEFORE_deleting_the_users_grants()
	{
		var sut = CreateService(GrantsPayload.Replace("\"TenantId\": \"acme\"", "\"TenantId\": null", StringComparison.Ordinal));

		_ = await Should.ThrowAsync<InvalidOperationException>(
			() => sut.SyncActivityGroupGrantsAsync("user-1", CancellationToken.None));

		A.CallTo(() => _grantStore.DeleteActivityGroupGrantsByUserIdAsync(A<string>._, A<string>._, A<CancellationToken>._))
			.MustNotHaveHappened();
		A.CallTo(() => ((IActivityGroupGrantReplacement)_grantStore).ReplaceActivityGroupGrantsForUserAsync(
				A<string>._, A<string>._, A<ActivityGroupGrantSnapshot>._, A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	/// <summary>
	/// The estate-wide grant sync refuses a tenantless row BEFORE deleting every user's grants.
	/// </summary>
	[Fact]
	public async Task Refuse_a_grant_row_without_a_tenant_BEFORE_deleting_every_users_grants()
	{
		var sut = CreateService(GrantsPayload.Replace("\"TenantId\": \"acme\"", "\"TenantId\": null", StringComparison.Ordinal));

		_ = await Should.ThrowAsync<InvalidOperationException>(
			() => sut.SyncAllActivityGroupGrantsAsync(CancellationToken.None));

		A.CallTo(() => _grantStore.DeleteAllActivityGroupGrantsAsync(A<string>._, A<CancellationToken>._))
			.MustNotHaveHappened();
		A.CallTo(() => ((IActivityGroupGrantReplacement)_grantStore).ReplaceActivityGroupGrantsAsync(
				A<string>._, A<ActivityGroupGrantSnapshot>._, A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	/// <summary>
	/// A group sync that fails part-way still invalidates the cached catalogue it was replacing.
	/// </summary>
	/// <remarks>
	/// A replace whose outcome is unknown -- it failed, or its reply was lost -- may or may not have changed
	/// the catalogue, so the cached catalogue of every tenant the payload names is invalidated on that path
	/// too. Clearing the cache only on success would leave the entry serving whatever the replace removed.
	/// </remarks>
	[Fact]
	public async Task Invalidate_the_cached_catalogue_even_when_the_group_sync_fails_part_way()
	{
		A.CallTo(() => _groupStore.ReplaceAllActivityGroupsAsync(A<ActivityGroupCatalogue>._, A<CancellationToken>._))
			.ThrowsAsync(new InvalidOperationException("the store failed while replacing the catalogue"));
		var sut = CreateService(ActivityGroupsPayload);

		_ = await Should.ThrowAsync<InvalidOperationException>(
			() => sut.SyncActivityGroupsAsync(CancellationToken.None));

		A.CallTo(() => _cache.RemoveAsync(AuthorizationCacheKey.ForActivityGroups("acme"), A<CancellationToken>._))
			.MustHaveHappened();
	}

	/// <summary>
	/// A grant sync that fails part-way still invalidates the user's cached grants.
	/// </summary>
	[Fact]
	public async Task Invalidate_the_cached_grants_even_when_the_grant_sync_fails_part_way()
	{
		var sut = CreateService(GrantsPayload);

		// Configured AFTER the service is built, for the reason given on the dropped-user arm above.
		A.CallTo(() => ((IActivityGroupGrantReplacement)_grantStore).ReplaceActivityGroupGrantsForUserAsync(
				A<string>._, A<string>._, A<ActivityGroupGrantSnapshot>._, A<CancellationToken>._))
			.ThrowsAsync(new InvalidOperationException("the store failed while replacing the user's grants"));

		_ = await Should.ThrowAsync<InvalidOperationException>(
			() => sut.SyncActivityGroupGrantsAsync("user-1", CancellationToken.None));

		A.CallTo(() => _cache.RemoveAsync(AuthorizationCacheKey.ForGrants("user-1"), A<CancellationToken>._))
			.MustHaveHappened();
	}

	private ActivityGroupService CreateService(string body) =>
		CreateService(body, _grantStore, GrantSyncAtomicity.Required);

	private ActivityGroupService CreateService(
		string body,
		IActivityGroupGrantStore grantStore,
		GrantSyncAtomicity atomicity)
	{
		var httpClient = new HttpClient(new StubHandler(body)) { BaseAddress = new Uri("https://authorization.test/") };
		var correlationId = A.Fake<ICorrelationId>();
		var token = A.Fake<IAuthenticationToken>();

		A.CallTo(() => grantStore.GetDistinctActivityGroupGrantUserIdsAsync(A<string>._, A<CancellationToken>._))
			.Returns<IReadOnlyList<string>>([]);

		if (grantStore is IActivityGroupGrantReplacement replacement)
		{
			A.CallTo(() => replacement.ReplaceActivityGroupGrantsAsync(
					A<string>._, A<ActivityGroupGrantSnapshot>._, A<CancellationToken>._))
				.Returns<IReadOnlyCollection<string>>([]);
			A.CallTo(() => replacement.ReplaceActivityGroupGrantsForUserAsync(
					A<string>._, A<string>._, A<ActivityGroupGrantSnapshot>._, A<CancellationToken>._))
				.Returns<IReadOnlyCollection<string>>([]);
		}

		return new ActivityGroupService(
			httpClient,
			correlationId,
			token,
			_groupStore,
			grantStore,
			Options.Create(new ActivityGroupSyncOptions { GrantSyncAtomicity = atomicity }),
			NullLogger<ActivityGroupService>.Instance,
			_cache);
	}

	private sealed class StubHandler(string body) : HttpMessageHandler
	{
		protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
			Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
			{
				Content = new StringContent(body, Encoding.UTF8, "application/json"),
			});
	}
}

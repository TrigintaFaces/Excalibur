// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.A3.Authorization;
using Excalibur.A3.Authorization.Grants;
using Excalibur.A3.Authorization.Stores.InMemory;

namespace Excalibur.Tests.A3.Authorization.Stores;

/// <summary>
/// Locks the activity-group grant replace against the in-memory store: what it revokes, what it leaves alone,
/// and what it refuses before removing anything.
/// </summary>
/// <remarks>
/// Run against the real store rather than a fake, because every property here is a property of the store's own
/// behaviour. The two database stores carry the same contract and are locked against real containers.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "A3")]
public sealed class ActivityGroupGrantReplacementShould
{
	private const string Other = "OtherType";

	private readonly InMemoryGrantStore _store = new();

	/// <summary>
	/// SAFETY and LIVENESS: a grant the snapshot omits is revoked, and one it carries is present afterwards.
	/// </summary>
	[Fact]
	public async Task Revoke_a_grant_the_estate_snapshot_omits_and_keep_the_ones_it_carries()
	{
		var ct = TestContext.Current.CancellationToken;
		await SeedAsync("user-1", "Billing", ct);
		await SeedAsync("user-2", "Support", ct);

		var previous = await ((IActivityGroupGrantReplacement)_store).ReplaceActivityGroupGrantsAsync(
			GrantType.ActivityGroup,
			new ActivityGroupGrantSnapshot([Entry("user-2", "Support")]),
			ct);

		previous.OrderBy(static u => u, StringComparer.Ordinal).ToArray().ShouldBe(["user-1", "user-2"]);
		(await _store.GetAllGrantsAsync("user-1", includeExpired: true, ct))
			.ShouldBeEmpty("a user the snapshot does not name holds no grants of that type afterwards");
		(await _store.GetAllGrantsAsync("user-2", includeExpired: true, ct)).ShouldHaveSingleItem()
			.Qualifier.ShouldBe("Support");
	}

	/// <summary>
	/// SAFETY: an empty ESTATE-WIDE snapshot is refused, and nothing is removed.
	/// </summary>
	/// <remarks>
	/// It would revoke every user's grants of that type, and an empty result from an authority cannot be told
	/// apart from a fetch that returned nothing.
	/// </remarks>
	[Fact]
	public async Task Refuse_an_empty_estate_snapshot_without_removing_anything()
	{
		var ct = TestContext.Current.CancellationToken;
		await SeedAsync("user-1", "Billing", ct);

		_ = await Should.ThrowAsync<ArgumentException>(
			() => ((IActivityGroupGrantReplacement)_store).ReplaceActivityGroupGrantsAsync(
				GrantType.ActivityGroup, new ActivityGroupGrantSnapshot([]), ct));

		(await _store.GetAllGrantsAsync("user-1", includeExpired: true, ct)).ShouldHaveSingleItem();
	}

	/// <summary>
	/// The PER-USER replace ACCEPTS an empty snapshot, revokes exactly that user's grants of the type, and
	/// touches no other user's rows.
	/// </summary>
	/// <remarks>
	/// The asymmetry with the estate-wide member above is the requirement, not an oversight: a user who now
	/// holds no activity-group grants is an ordinary state and every grant they held must go. Refusing it here
	/// would keep revoked access alive.
	/// </remarks>
	[Fact]
	public async Task Accept_an_empty_per_user_snapshot_and_revoke_only_that_users_grants()
	{
		var ct = TestContext.Current.CancellationToken;
		await SeedAsync("user-1", "Billing", ct);
		await SeedAsync("user-1", "Support", ct);
		await SeedAsync("user-2", "Billing", ct);

		var previous = await ((IActivityGroupGrantReplacement)_store).ReplaceActivityGroupGrantsForUserAsync(
			"user-1", GrantType.ActivityGroup, new ActivityGroupGrantSnapshot([]), ct);

		previous.ShouldBe(["user-1"]);
		(await _store.GetAllGrantsAsync("user-1", includeExpired: true, ct))
			.ShouldBeEmpty("an empty per-user snapshot revokes every grant of that type the user held");

		// LIVENESS on the other side of the same act: a replace that removed everything would also satisfy the
		// assertion above.
		(await _store.GetAllGrantsAsync("user-2", includeExpired: true, ct)).ShouldHaveSingleItem()
			.Qualifier.ShouldBe("Billing");
	}

	/// <summary>
	/// SAFETY: a snapshot mixing grant types is refused before anything is deleted.
	/// </summary>
	/// <remarks>
	/// The replace removes only the rows carrying its own grant type, so a row of another type would be written
	/// without the rows it replaces having been removed — two replaces half-applied.
	/// </remarks>
	[Fact]
	public async Task Refuse_a_snapshot_mixing_grant_types_before_deleting_anything()
	{
		var ct = TestContext.Current.CancellationToken;
		await SeedAsync("user-1", "Billing", ct);

		var mixed = new ActivityGroupGrantSnapshot(
		[
			Entry("user-1", "Billing"),
			Entry("user-1", "Elsewhere", Other),
		]);

		_ = await Should.ThrowAsync<ArgumentException>(
			() => ((IActivityGroupGrantReplacement)_store).ReplaceActivityGroupGrantsAsync(
				GrantType.ActivityGroup, mixed, ct));

		(await _store.GetAllGrantsAsync("user-1", includeExpired: true, ct)).ShouldHaveSingleItem()
			.Qualifier.ShouldBe("Billing", customMessage: "a refused snapshot must not have deleted anything");
	}

	/// <summary>
	/// SAFETY: a per-user snapshot carrying another user's grant is refused before anything is deleted.
	/// </summary>
	[Fact]
	public async Task Refuse_a_per_user_snapshot_carrying_another_users_grant_before_deleting_anything()
	{
		var ct = TestContext.Current.CancellationToken;
		await SeedAsync("user-1", "Billing", ct);

		_ = await Should.ThrowAsync<ArgumentException>(
			() => ((IActivityGroupGrantReplacement)_store).ReplaceActivityGroupGrantsForUserAsync(
				"user-1",
				GrantType.ActivityGroup,
				new ActivityGroupGrantSnapshot([Entry("user-2", "Billing")]),
				ct));

		(await _store.GetAllGrantsAsync("user-1", includeExpired: true, ct)).ShouldHaveSingleItem();
	}

	/// <summary>
	/// SAFETY: an estate-wide replace of one grant type leaves grants of every other type alone.
	/// </summary>
	[Fact]
	public async Task Leave_grants_of_another_type_alone()
	{
		var ct = TestContext.Current.CancellationToken;
		await SeedAsync("user-1", "Billing", ct);
		await SeedAsync("user-1", "Elsewhere", ct, Other);

		_ = await ((IActivityGroupGrantReplacement)_store).ReplaceActivityGroupGrantsAsync(
			GrantType.ActivityGroup,
			new ActivityGroupGrantSnapshot([Entry("user-1", "Support")]),
			ct);

		var held = await _store.GetAllGrantsAsync("user-1", includeExpired: true, ct);
		held.Select(static g => g.Qualifier).OrderBy(static q => q, StringComparer.Ordinal).ToArray()
			.ShouldBe(["Elsewhere", "Support"]);
	}

	/// <summary>
	/// A snapshot term with leading or trailing whitespace is refused at construction, so no store ever sees it.
	/// </summary>
	/// <remarks>
	/// SQL Server ignores trailing spaces when it compares keys, so such a value identifies a different grant
	/// there than on the other providers.
	/// </remarks>
	[Fact]
	public void Refuse_a_snapshot_term_with_surrounding_whitespace() =>
		Should.Throw<ArgumentException>(() => new ActivityGroupGrantSnapshot([Entry("user-1", "Billing ")]));

	/// <summary>
	/// A repeated grant is kept once rather than refused, so a snapshot is a set on every provider.
	/// </summary>
	[Fact]
	public void Keep_a_repeated_grant_once() =>
		new ActivityGroupGrantSnapshot([Entry("user-1", "Billing"), Entry("user-1", "Billing")])
			.Entries.ShouldHaveSingleItem();

	private static ActivityGroupGrantEntry Entry(
		string userId,
		string qualifier,
		string grantType = "") =>
		new(
			userId,
			userId,
			"acme",
			grantType.Length == 0 ? GrantType.ActivityGroup : grantType,
			qualifier,
			null,
			"granter");

	private Task SeedAsync(string userId, string qualifier, CancellationToken cancellationToken) =>
		SeedAsync(userId, qualifier, cancellationToken, GrantType.ActivityGroup);

	private Task<int> SeedAsync(
		string userId,
		string qualifier,
		CancellationToken cancellationToken,
		string grantType) =>
		((IActivityGroupGrantStore)_store).InsertActivityGroupGrantAsync(
			userId, userId, "acme", grantType, qualifier, null, "granter", cancellationToken);
}

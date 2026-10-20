// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.A3.Authorization;
using Excalibur.A3.Authorization.Stores.InMemory;

namespace Excalibur.Tests.A3.Authorization.Stores.InMemory;

[Trait("Category", "Unit")]
[Trait("Component", "A3")]
[Trait("Pattern", "Concurrency")]
public sealed class AtomicGrantRevocationShould
{
	private readonly InMemoryGrantStore _store = new();
	private static Grant Grant(string user = "u", string tenant = "a", string type = "role", string qualifier = "q") =>
		new(user, null, tenant, type, qualifier, null, "issuer", DateTimeOffset.UtcNow);
	private static GrantRevocationRequest Request(Guid? id = null, GrantRevocationScope? scope = null, string user = "u") =>
		new(id ?? Guid.NewGuid(), user, scope ?? GrantRevocationScope.ForTenant("a"), "administrator");

	[Fact]
	public async Task RevokeEveryTypeInExactlyOneTenantAndKeepOtherSubjects()
	{
		foreach (var grant in new[] { Grant(), Grant(type: "ActivityGroup"), Grant(tenant: "b"), Grant(user: "other") })
		{
			await _store.SaveGrantAsync(grant, CancellationToken.None);
		}
		var before = await _store.ReadUserSnapshotAsync("u", true, CancellationToken.None);
		var result = await _store.RevokeAllAsync(Request(), CancellationToken.None);
		result.RevokedCount.ShouldBe(2);
		result.Revision.ShouldBe(before.Revision + 1);
		var after = await _store.ReadUserSnapshotAsync("u", true, CancellationToken.None);
		after.Revision.ShouldBe(result.Revision);
		after.Grants.ShouldHaveSingleItem().TenantId.ShouldBe("b");
		(await _store.GetAllGrantsAsync("other", true, CancellationToken.None)).ShouldHaveSingleItem();
		before.Grants.Count.ShouldBe(3);
	}

	[Fact]
	public async Task RevokeAllTenantsOnlyWhenExplicitlyRequested()
	{
		await _store.SaveGrantAsync(Grant(), CancellationToken.None);
		await _store.SaveGrantAsync(Grant(tenant: "b", type: "ActivityGroup"), CancellationToken.None);
		(await _store.RevokeAllAsync(Request(scope: GrantRevocationScope.AllTenants), CancellationToken.None))
			.RevokedCount.ShouldBe(2);
		(await _store.ReadUserSnapshotAsync("u", true, CancellationToken.None)).Grants.ShouldBeEmpty();
	}

	[Fact]
	public async Task ResolveLostResponseRetryWithoutRevokingLaterGrant()
	{
		await _store.SaveGrantAsync(Grant(), CancellationToken.None);
		var request = Request();
		var committed = await _store.RevokeAllAsync(request, CancellationToken.None);
		await _store.SaveGrantAsync(Grant(qualifier: "later"), CancellationToken.None);
		var revision = await _store.ReadUserRevisionAsync("u", CancellationToken.None);
		(await _store.RevokeAllAsync(request, CancellationToken.None)).ShouldBe(committed);
		(await _store.ReadUserRevisionAsync("u", CancellationToken.None)).ShouldBe(revision);
		(await _store.GetAllGrantsAsync("u", true, CancellationToken.None)).ShouldHaveSingleItem().Qualifier.ShouldBe("later");
		await Should.ThrowAsync<InvalidOperationException>(() => _store.RevokeAllAsync(
			Request(request.OperationId, GrantRevocationScope.AllTenants), CancellationToken.None));
		(await _store.ReadUserRevisionAsync("u", CancellationToken.None)).ShouldBe(revision);
	}

	[Fact]
	public async Task KeepOperationIdentifiersIndependentAcrossSubjects()
	{
		var id = Guid.NewGuid();
		await _store.SaveGrantAsync(Grant(), CancellationToken.None);
		await _store.SaveGrantAsync(Grant(user: "other"), CancellationToken.None);
		(await _store.RevokeAllAsync(Request(id), CancellationToken.None)).RevokedCount.ShouldBe(1);
		(await _store.RevokeAllAsync(Request(id, user: "other"), CancellationToken.None)).RevokedCount.ShouldBe(1);
	}

	[Fact]
	public async Task RefuseCancellationBeforeMutationAndLeaveIdentifierRetryable()
	{
		await _store.SaveGrantAsync(Grant(), CancellationToken.None);
		var before = await _store.ReadUserSnapshotAsync("u", true, CancellationToken.None);
		var request = Request();
		using var canceled = new CancellationTokenSource();
		await canceled.CancelAsync();
		await Should.ThrowAsync<OperationCanceledException>(() => _store.RevokeAllAsync(request, canceled.Token));
		(await _store.ReadUserRevisionAsync("u", CancellationToken.None)).ShouldBe(before.Revision);
		(await _store.GetAllGrantsAsync("u", true, CancellationToken.None)).ShouldHaveSingleItem();
		(await _store.RevokeAllAsync(request, CancellationToken.None)).RevokedCount.ShouldBe(1);
	}

	[Fact]
	public async Task PairSnapshotsWithAtomicRevisionUnderCompetingWriters()
	{
		const int count = 128;
		var writers = Enumerable.Range(0, count).Select(i => Task.Run(async () =>
			await _store.SaveGrantAsync(Grant(qualifier: i.ToString(System.Globalization.CultureInfo.InvariantCulture)), CancellationToken.None))).ToArray();
		var completion = Task.WhenAll(writers);
		while (!completion.IsCompleted)
		{
			var snapshot = await _store.ReadUserSnapshotAsync("u", true, CancellationToken.None);
			snapshot.Revision.ShouldBe(snapshot.Grants.Count);
		}
		await completion;
		var final = await _store.ReadUserSnapshotAsync("u", true, CancellationToken.None);
		final.Revision.ShouldBe(count);
		final.Grants.Count.ShouldBe(count);
	}

	[Fact]
	public async Task AdvanceRevisionsForActivityGroupWritesAndReplacementIncludingInsertedUsers()
	{
		await _store.InsertActivityGroupGrantAsync("u", "User", "a", "ActivityGroup", "old", null, "issuer", CancellationToken.None);
		(await _store.ReadUserRevisionAsync("u", CancellationToken.None)).ShouldBe(1);
		await _store.ReplaceActivityGroupGrantsAsync("ActivityGroup", new ActivityGroupGrantSnapshot([
			new ActivityGroupGrantEntry("other", "Other", "a", "ActivityGroup", "new", null, "issuer")]), CancellationToken.None);
		(await _store.ReadUserRevisionAsync("u", CancellationToken.None)).ShouldBe(2);
		(await _store.ReadUserRevisionAsync("other", CancellationToken.None)).ShouldBe(1);
		await _store.DeleteAllActivityGroupGrantsAsync("ActivityGroup", CancellationToken.None);
		(await _store.ReadUserRevisionAsync("other", CancellationToken.None)).ShouldBe(2);
	}

	[Fact]
	public void OwnSnapshotCollectionAndRejectForeignSubject()
	{
		var grants = new List<Grant> { Grant() };
		var snapshot = new UserGrantSnapshot("u", 1, grants);
		grants.Clear();
		snapshot.Grants.ShouldHaveSingleItem();
		Should.Throw<ArgumentException>(() => new UserGrantSnapshot("other", 1, snapshot.Grants));
		Should.Throw<ArgumentException>(() => GrantRevocationScope.ForTenant(" "));
	}
}

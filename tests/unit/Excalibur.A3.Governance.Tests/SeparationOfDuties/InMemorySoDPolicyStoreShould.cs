// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.A3.Governance.SeparationOfDuties;
using Excalibur.A3.Governance.Stores.InMemory;

namespace Excalibur.A3.Governance.Tests.SeparationOfDuties;

/// <summary>
/// Unit tests for <see cref="InMemorySoDPolicyStore"/>: CRUD, concurrent access,
/// and GetService escape hatch.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class InMemorySoDPolicyStoreShould : UnitTestBase
{
	private readonly InMemorySoDPolicyStore _store = new();

	private static SoDPolicy CreatePolicy(
		string id = "policy-1",
		string name = "No Admin+Finance") =>
		new(id, name, null, SoDSeverity.Violation, SoDPolicyScope.Role,
			["Admin", "Finance"], null, "admin");

	#region SavePolicyAsync / GetPolicyAsync

	[Fact]
	public async Task SaveAndRetrievePolicy()
	{
		var policy = CreatePolicy();
		await _store.SavePolicyAsync(policy, CancellationToken.None);

		var result = await _store.GetPolicyAsync("policy-1", CancellationToken.None);
		result.ShouldNotBeNull();
		result.PolicyId.ShouldBe("policy-1");
		result.Name.ShouldBe("No Admin+Finance");
		result.ConflictingItems.Count.ShouldBe(2);
	}

	[Fact]
	public async Task UpsertPolicy()
	{
		await _store.SavePolicyAsync(CreatePolicy(name: "Original"), CancellationToken.None);
		await _store.SavePolicyAsync(CreatePolicy(name: "Updated"), CancellationToken.None);

		var result = await _store.GetPolicyAsync("policy-1", CancellationToken.None);
		result!.Name.ShouldBe("Updated");
	}

	[Fact]
	public async Task ReturnNull_WhenPolicyNotFound()
	{
		var result = await _store.GetPolicyAsync("nonexistent", CancellationToken.None);
		result.ShouldBeNull();
	}

	[Fact]
	public async Task ThrowOnSave_WhenNull()
	{
		await Should.ThrowAsync<ArgumentNullException>(() =>
			_store.SavePolicyAsync(null!, CancellationToken.None));
	}

	#endregion

	#region GetAllPoliciesAsync

	[Fact]
	public async Task ReturnAllPolicies()
	{
		await _store.SavePolicyAsync(CreatePolicy("p1"), CancellationToken.None);
		await _store.SavePolicyAsync(CreatePolicy("p2"), CancellationToken.None);

		var result = await _store.GetAllPoliciesAsync(CancellationToken.None);
		result.Count.ShouldBe(2);
	}

	[Fact]
	public async Task ReturnEmptyList_WhenStoreIsEmpty()
	{
		var result = await _store.GetAllPoliciesAsync(CancellationToken.None);
		result.ShouldBeEmpty();
	}

	#endregion

	#region DeletePolicyAsync

	[Fact]
	public async Task DeleteExistingPolicy()
	{
		await _store.SavePolicyAsync(CreatePolicy(), CancellationToken.None);

		var deleted = await _store.DeletePolicyAsync("policy-1", CancellationToken.None);
		deleted.ShouldBeTrue();

		var result = await _store.GetPolicyAsync("policy-1", CancellationToken.None);
		result.ShouldBeNull();
	}

	[Fact]
	public async Task ReturnFalse_WhenDeletingNonexistent()
	{
		var deleted = await _store.DeletePolicyAsync("nonexistent", CancellationToken.None);
		deleted.ShouldBeFalse();
	}

	#endregion

	#region GetService

	[Fact]
	public void ReturnNull_FromGetService()
	{
		_store.GetService(typeof(string)).ShouldBeNull();
	}

	[Fact]
	public void ThrowOnGetService_WhenNull()
	{
		Should.Throw<ArgumentNullException>(() => _store.GetService(null!));
	}

	#endregion

	#region Concurrent Access

	[Fact]
	public async Task HandleConcurrentSaves()
	{
		var tasks = Enumerable.Range(0, 50)
			.Select(i => _store.SavePolicyAsync(CreatePolicy($"p-{i}"), CancellationToken.None));

		await Task.WhenAll(tasks);

		var all = await _store.GetAllPoliciesAsync(CancellationToken.None);
		all.Count.ShouldBe(50);
	}

	#endregion
}

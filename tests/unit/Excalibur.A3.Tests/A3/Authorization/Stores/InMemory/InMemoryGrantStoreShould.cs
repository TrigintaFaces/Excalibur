// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.A3.Abstractions.Authorization;
using Excalibur.A3.Authorization.Stores.InMemory;

namespace Excalibur.Tests.A3.Authorization.Stores.InMemory;

/// <summary>
/// Unit tests for <see cref="InMemoryGrantStore"/>.
/// Covers IGrantStore, IGrantQueryStore, and IActivityGroupGrantStore contracts.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "A3")]
public sealed class InMemoryGrantStoreShould : UnitTestBase
{
	private readonly InMemoryGrantStore _sut = new();
	private readonly CancellationToken _ct = CancellationToken.None;
	private readonly DateTimeOffset _now = DateTimeOffset.UtcNow;

	private Grant CreateGrant(
		string userId = "user-1",
		string? tenantId = "tenant-1",
		string grantType = "role",
		string qualifier = "admin",
		string? fullName = null,
		DateTimeOffset? expiresOn = null,
		string grantedBy = "system") =>
		new(userId, fullName, tenantId, grantType, qualifier, expiresOn, grantedBy, _now);

	#region IGrantStore -- SaveGrantAsync

	[Fact]
	public async Task SaveGrant_AndRetrieveIt()
	{
		// Arrange
		var grant = CreateGrant();

		// Act
		var affected = await _sut.SaveGrantAsync(grant, _ct);
		var result = await _sut.GetGrantAsync("user-1", "tenant-1", "role", "admin", _ct);

		// Assert
		affected.ShouldBe(1);
		result.ShouldNotBeNull();
		result.UserId.ShouldBe("user-1");
		result.TenantId.ShouldBe("tenant-1");
		result.GrantType.ShouldBe("role");
		result.Qualifier.ShouldBe("admin");
	}

	[Fact]
	public async Task SaveGrant_Upsert_OverwritesExisting()
	{
		// Arrange
		var original = CreateGrant(grantedBy: "admin-1");
		var updated = CreateGrant(grantedBy: "admin-2");

		// Act
		await _sut.SaveGrantAsync(original, _ct);
		await _sut.SaveGrantAsync(updated, _ct);
		var result = await _sut.GetGrantAsync("user-1", "tenant-1", "role", "admin", _ct);

		// Assert
		result.ShouldNotBeNull();
		result.GrantedBy.ShouldBe("admin-2");
	}

	[Fact]
	public async Task SaveGrant_NullGrant_ThrowsArgumentNullException()
	{
		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(
			() => _sut.SaveGrantAsync(null!, _ct));
	}

	[Fact]
	public async Task SaveGrant_NullTenantId_UsesEmptyStringInKey()
	{
		// Arrange
		var grant = CreateGrant(tenantId: null);

		// Act
		await _sut.SaveGrantAsync(grant, _ct);

		// TenantId is null but key uses empty string -- retrieve with empty string
		var result = await _sut.GetGrantAsync("user-1", string.Empty, "role", "admin", _ct);

		// Assert
		result.ShouldNotBeNull();
		result.TenantId.ShouldBeNull();
	}

	#endregion

	#region IGrantStore -- GetGrantAsync

	[Fact]
	public async Task GetGrant_NotFound_ReturnsNull()
	{
		// Act
		var result = await _sut.GetGrantAsync("nonexistent", "t", "role", "admin", _ct);

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public async Task GetGrant_EmptyStore_ReturnsNull()
	{
		// Act
		var result = await _sut.GetGrantAsync("user-1", "tenant-1", "role", "admin", _ct);

		// Assert
		result.ShouldBeNull();
	}

	#endregion

	#region IGrantStore -- GetAllGrantsAsync

	[Fact]
	public async Task GetAllGrants_ReturnsAllForUser()
	{
		// Arrange
		await _sut.SaveGrantAsync(CreateGrant(qualifier: "admin"), _ct);
		await _sut.SaveGrantAsync(CreateGrant(qualifier: "editor"), _ct);
		await _sut.SaveGrantAsync(CreateGrant(userId: "user-2", qualifier: "viewer"), _ct);

		// Act
		var results = await _sut.GetAllGrantsAsync("user-1", _ct);

		// Assert
		results.Count.ShouldBe(2);
		results.ShouldAllBe(g => g.UserId == "user-1");
	}

	[Fact]
	public async Task GetAllGrants_NoGrants_ReturnsEmptyList()
	{
		// Act
		var results = await _sut.GetAllGrantsAsync("user-1", _ct);

		// Assert
		results.ShouldBeEmpty();
	}

	#endregion

	#region IGrantStore -- DeleteGrantAsync

	[Fact]
	public async Task DeleteGrant_Existing_ReturnsOne()
	{
		// Arrange
		await _sut.SaveGrantAsync(CreateGrant(), _ct);

		// Act
		var affected = await _sut.DeleteGrantAsync("user-1", "tenant-1", "role", "admin", null, null, _ct);

		// Assert
		affected.ShouldBe(1);
		(await _sut.GetGrantAsync("user-1", "tenant-1", "role", "admin", _ct)).ShouldBeNull();
	}

	[Fact]
	public async Task DeleteGrant_NonExisting_ReturnsZero()
	{
		// Act
		var affected = await _sut.DeleteGrantAsync("user-1", "tenant-1", "role", "admin", null, null, _ct);

		// Assert
		affected.ShouldBe(0);
	}

	[Fact]
	public async Task DeleteGrant_DoesNotAffectOtherGrants()
	{
		// Arrange
		await _sut.SaveGrantAsync(CreateGrant(qualifier: "admin"), _ct);
		await _sut.SaveGrantAsync(CreateGrant(qualifier: "editor"), _ct);

		// Act
		await _sut.DeleteGrantAsync("user-1", "tenant-1", "role", "admin", "revoker", DateTimeOffset.UtcNow, _ct);

		// Assert
		(await _sut.GetGrantAsync("user-1", "tenant-1", "role", "editor", _ct)).ShouldNotBeNull();
	}

	#endregion

	#region IGrantStore -- GrantExistsAsync

	[Fact]
	public async Task GrantExists_WhenPresent_ReturnsTrue()
	{
		// Arrange
		await _sut.SaveGrantAsync(CreateGrant(), _ct);

		// Act & Assert
		(await _sut.GrantExistsAsync("user-1", "tenant-1", "role", "admin", _ct)).ShouldBeTrue();
	}

	[Fact]
	public async Task GrantExists_WhenAbsent_ReturnsFalse()
	{
		// Act & Assert
		(await _sut.GrantExistsAsync("user-1", "tenant-1", "role", "admin", _ct)).ShouldBeFalse();
	}

	[Fact]
	public async Task GrantExists_AfterDelete_ReturnsFalse()
	{
		// Arrange
		await _sut.SaveGrantAsync(CreateGrant(), _ct);
		await _sut.DeleteGrantAsync("user-1", "tenant-1", "role", "admin", null, null, _ct);

		// Act & Assert
		(await _sut.GrantExistsAsync("user-1", "tenant-1", "role", "admin", _ct)).ShouldBeFalse();
	}

	#endregion

	#region IGrantStore -- GetService

	[Fact]
	public void GetService_IGrantQueryStore_ReturnsSelf()
	{
		// Act
		var service = _sut.GetService(typeof(IGrantQueryStore));

		// Assert
		service.ShouldNotBeNull();
		service.ShouldBeOfType<InMemoryGrantStore>();
		service.ShouldBeSameAs(_sut);
	}

	[Fact]
	public void GetService_IActivityGroupGrantStore_ReturnsSelf()
	{
		// Act
		var service = _sut.GetService(typeof(IActivityGroupGrantStore));

		// Assert
		service.ShouldNotBeNull();
		service.ShouldBeOfType<InMemoryGrantStore>();
		service.ShouldBeSameAs(_sut);
	}

	[Fact]
	public void GetService_UnsupportedType_ReturnsNull()
	{
		// Act & Assert
		_sut.GetService(typeof(string)).ShouldBeNull();
		_sut.GetService(typeof(IGrantStore)).ShouldBeNull();
		_sut.GetService(typeof(IActivityGroupStore)).ShouldBeNull();
	}

	[Fact]
	public void GetService_NullType_ThrowsArgumentNullException()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() => _sut.GetService(null!));
	}

	#endregion

	#region IGrantQueryStore -- GetMatchingGrantsAsync

	[Fact]
	public async Task GetMatchingGrants_WithUserId_FiltersCorrectly()
	{
		// Arrange
		await _sut.SaveGrantAsync(CreateGrant(userId: "user-1", qualifier: "admin"), _ct);
		await _sut.SaveGrantAsync(CreateGrant(userId: "user-2", qualifier: "admin"), _ct);

		// Act
		var results = await _sut.GetMatchingGrantsAsync("user-1", "tenant-1", "role", "admin", _ct);

		// Assert
		results.Count.ShouldBe(1);
		results[0].UserId.ShouldBe("user-1");
	}

	[Fact]
	public async Task GetMatchingGrants_NullUserId_ReturnsAllMatching()
	{
		// Arrange
		await _sut.SaveGrantAsync(CreateGrant(userId: "user-1", qualifier: "admin"), _ct);
		await _sut.SaveGrantAsync(CreateGrant(userId: "user-2", qualifier: "admin"), _ct);
		await _sut.SaveGrantAsync(CreateGrant(userId: "user-3", qualifier: "editor"), _ct);

		// Act
		var results = await _sut.GetMatchingGrantsAsync(null, "tenant-1", "role", "admin", _ct);

		// Assert
		results.Count.ShouldBe(2);
	}

	[Fact]
	public async Task GetMatchingGrants_NoMatch_ReturnsEmptyList()
	{
		// Arrange
		await _sut.SaveGrantAsync(CreateGrant(), _ct);

		// Act
		var results = await _sut.GetMatchingGrantsAsync("user-1", "tenant-1", "role", "nonexistent", _ct);

		// Assert
		results.ShouldBeEmpty();
	}

	#endregion

	#region IGrantQueryStore -- FindUserGrantsAsync

	[Fact]
	public async Task FindUserGrants_ReturnsKeyedDictionary()
	{
		// Arrange
		await _sut.SaveGrantAsync(CreateGrant(qualifier: "admin"), _ct);
		await _sut.SaveGrantAsync(CreateGrant(qualifier: "editor"), _ct);

		// Act
		var results = await _sut.FindUserGrantsAsync("user-1", _ct);

		// Assert
		results.Count.ShouldBe(2);
		results.Values.ShouldAllBe(v => v is Grant);
	}

	[Fact]
	public async Task FindUserGrants_NoGrants_ReturnsEmptyDictionary()
	{
		// Act
		var results = await _sut.FindUserGrantsAsync("user-1", _ct);

		// Assert
		results.ShouldBeEmpty();
	}

	[Fact]
	public async Task FindUserGrants_DoesNotReturnOtherUsers()
	{
		// Arrange
		await _sut.SaveGrantAsync(CreateGrant(userId: "user-1"), _ct);
		await _sut.SaveGrantAsync(CreateGrant(userId: "user-2"), _ct);

		// Act
		var results = await _sut.FindUserGrantsAsync("user-1", _ct);

		// Assert
		results.Count.ShouldBe(1);
	}

	#endregion

	#region IActivityGroupGrantStore -- InsertActivityGroupGrantAsync

	[Fact]
	public async Task InsertActivityGroupGrant_CreatesGrantEntry()
	{
		// Act
		var affected = await _sut.InsertActivityGroupGrantAsync(
			"user-1", "John Doe", "tenant-1", "activity-group", "orders", null, "admin", _ct);

		// Assert
		affected.ShouldBe(1);
		var grant = await _sut.GetGrantAsync("user-1", "tenant-1", "activity-group", "orders", _ct);
		grant.ShouldNotBeNull();
		grant.FullName.ShouldBe("John Doe");
		grant.GrantedBy.ShouldBe("admin");
	}

	[Fact]
	public async Task InsertActivityGroupGrant_NullTenantId_UsesEmptyKey()
	{
		// Act
		await _sut.InsertActivityGroupGrantAsync(
			"user-1", "John", null, "activity-group", "orders", null, "admin", _ct);

		// Assert
		var grant = await _sut.GetGrantAsync("user-1", string.Empty, "activity-group", "orders", _ct);
		grant.ShouldNotBeNull();
	}

	#endregion

	#region IActivityGroupGrantStore -- DeleteActivityGroupGrantsByUserIdAsync

	[Fact]
	public async Task DeleteActivityGroupGrantsByUserId_RemovesMatchingGrants()
	{
		// Arrange
		await _sut.InsertActivityGroupGrantAsync("user-1", "U1", "t", "ag", "q1", null, "sys", _ct);
		await _sut.InsertActivityGroupGrantAsync("user-1", "U1", "t", "ag", "q2", null, "sys", _ct);
		await _sut.InsertActivityGroupGrantAsync("user-2", "U2", "t", "ag", "q1", null, "sys", _ct);

		// Act
		var removed = await _sut.DeleteActivityGroupGrantsByUserIdAsync("user-1", "ag", _ct);

		// Assert
		removed.ShouldBe(2);
		(await _sut.GetAllGrantsAsync("user-1", _ct)).ShouldBeEmpty();
		(await _sut.GetAllGrantsAsync("user-2", _ct)).Count.ShouldBe(1);
	}

	[Fact]
	public async Task DeleteActivityGroupGrantsByUserId_NoMatch_ReturnsZero()
	{
		// Act
		var removed = await _sut.DeleteActivityGroupGrantsByUserIdAsync("user-1", "ag", _ct);

		// Assert
		removed.ShouldBe(0);
	}

	[Fact]
	public async Task DeleteActivityGroupGrantsByUserId_OnlyMatchesGrantType()
	{
		// Arrange -- different grant types
		await _sut.InsertActivityGroupGrantAsync("user-1", "U1", "t", "ag", "q1", null, "sys", _ct);
		await _sut.InsertActivityGroupGrantAsync("user-1", "U1", "t", "role", "q1", null, "sys", _ct);

		// Act
		var removed = await _sut.DeleteActivityGroupGrantsByUserIdAsync("user-1", "ag", _ct);

		// Assert
		removed.ShouldBe(1);
		(await _sut.GetAllGrantsAsync("user-1", _ct)).Count.ShouldBe(1);
	}

	#endregion

	#region IActivityGroupGrantStore -- DeleteAllActivityGroupGrantsAsync

	[Fact]
	public async Task DeleteAllActivityGroupGrants_RemovesAllMatchingGrantType()
	{
		// Arrange
		await _sut.InsertActivityGroupGrantAsync("user-1", "U1", "t", "ag", "q1", null, "sys", _ct);
		await _sut.InsertActivityGroupGrantAsync("user-2", "U2", "t", "ag", "q2", null, "sys", _ct);
		await _sut.InsertActivityGroupGrantAsync("user-3", "U3", "t", "role", "q3", null, "sys", _ct);

		// Act
		var removed = await _sut.DeleteAllActivityGroupGrantsAsync("ag", _ct);

		// Assert
		removed.ShouldBe(2);
		(await _sut.GetAllGrantsAsync("user-3", _ct)).Count.ShouldBe(1);
	}

	[Fact]
	public async Task DeleteAllActivityGroupGrants_EmptyStore_ReturnsZero()
	{
		// Act
		var removed = await _sut.DeleteAllActivityGroupGrantsAsync("ag", _ct);

		// Assert
		removed.ShouldBe(0);
	}

	#endregion

	#region IActivityGroupGrantStore -- GetDistinctActivityGroupGrantUserIdsAsync

	[Fact]
	public async Task GetDistinctUserIds_ReturnsDistinctValues()
	{
		// Arrange -- user-1 has two grants of same type
		await _sut.InsertActivityGroupGrantAsync("user-1", "U1", "t", "ag", "q1", null, "sys", _ct);
		await _sut.InsertActivityGroupGrantAsync("user-1", "U1", "t", "ag", "q2", null, "sys", _ct);
		await _sut.InsertActivityGroupGrantAsync("user-2", "U2", "t", "ag", "q3", null, "sys", _ct);

		// Act
		var userIds = await _sut.GetDistinctActivityGroupGrantUserIdsAsync("ag", _ct);

		// Assert
		userIds.Count.ShouldBe(2);
		userIds.ShouldContain("user-1");
		userIds.ShouldContain("user-2");
	}

	[Fact]
	public async Task GetDistinctUserIds_EmptyStore_ReturnsEmptyList()
	{
		// Act
		var userIds = await _sut.GetDistinctActivityGroupGrantUserIdsAsync("ag", _ct);

		// Assert
		userIds.ShouldBeEmpty();
	}

	[Fact]
	public async Task GetDistinctUserIds_FiltersOnGrantType()
	{
		// Arrange
		await _sut.InsertActivityGroupGrantAsync("user-1", "U1", "t", "ag", "q1", null, "sys", _ct);
		await _sut.InsertActivityGroupGrantAsync("user-2", "U2", "t", "role", "q1", null, "sys", _ct);

		// Act
		var userIds = await _sut.GetDistinctActivityGroupGrantUserIdsAsync("ag", _ct);

		// Assert
		userIds.Count.ShouldBe(1);
		userIds.ShouldContain("user-1");
	}

	#endregion

	#region Concurrency

	[Fact]
	public async Task ConcurrentSaveAndRead_IsThreadSafe()
	{
		// Arrange -- 100 concurrent writers + readers
		const int count = 100;
		var tasks = new List<Task>(count * 2);

		for (var i = 0; i < count; i++)
		{
			var userId = $"user-{i}";
			tasks.Add(_sut.SaveGrantAsync(CreateGrant(userId: userId, qualifier: $"q-{i}"), _ct));
			tasks.Add(_sut.GetAllGrantsAsync(userId, _ct));
		}

		// Act & Assert -- should not throw
		await Task.WhenAll(tasks);

		// Verify all writes landed
		for (var i = 0; i < count; i++)
		{
			(await _sut.GrantExistsAsync($"user-{i}", "tenant-1", "role", $"q-{i}", _ct)).ShouldBeTrue();
		}
	}

	[Fact]
	public async Task ConcurrentSaveAndDelete_IsThreadSafe()
	{
		// Arrange -- pre-populate
		const int count = 100;
		for (var i = 0; i < count; i++)
		{
			await _sut.SaveGrantAsync(CreateGrant(userId: $"user-{i}", qualifier: $"q-{i}"), _ct);
		}

		// Act -- concurrent delete + save interleaved
		var tasks = new List<Task>(count);
		for (var i = 0; i < count; i++)
		{
			if (i % 2 == 0)
			{
				tasks.Add(_sut.DeleteGrantAsync($"user-{i}", "tenant-1", "role", $"q-{i}", null, null, _ct));
			}
			else
			{
				tasks.Add(_sut.SaveGrantAsync(
					CreateGrant(userId: $"user-{i}", qualifier: $"q-{i}", grantedBy: "updated"), _ct));
			}
		}

		// Assert -- should not throw
		await Task.WhenAll(tasks);
	}

	#endregion

	#region Composite Key Isolation

	[Fact]
	public async Task CompositeKey_DifferentTenants_AreSeparateGrants()
	{
		// Arrange
		await _sut.SaveGrantAsync(CreateGrant(tenantId: "tenant-A"), _ct);
		await _sut.SaveGrantAsync(CreateGrant(tenantId: "tenant-B"), _ct);

		// Act
		var grantA = await _sut.GetGrantAsync("user-1", "tenant-A", "role", "admin", _ct);
		var grantB = await _sut.GetGrantAsync("user-1", "tenant-B", "role", "admin", _ct);

		// Assert
		grantA.ShouldNotBeNull();
		grantB.ShouldNotBeNull();
		grantA.TenantId.ShouldBe("tenant-A");
		grantB.TenantId.ShouldBe("tenant-B");
	}

	[Fact]
	public async Task CompositeKey_DifferentGrantTypes_AreSeparateGrants()
	{
		// Arrange
		await _sut.SaveGrantAsync(CreateGrant(grantType: "role"), _ct);
		await _sut.SaveGrantAsync(CreateGrant(grantType: "permission"), _ct);

		// Act
		var role = await _sut.GetGrantAsync("user-1", "tenant-1", "role", "admin", _ct);
		var permission = await _sut.GetGrantAsync("user-1", "tenant-1", "permission", "admin", _ct);

		// Assert
		role.ShouldNotBeNull();
		permission.ShouldNotBeNull();
	}

	[Fact]
	public async Task CompositeKey_DifferentQualifiers_AreSeparateGrants()
	{
		// Arrange
		await _sut.SaveGrantAsync(CreateGrant(qualifier: "admin"), _ct);
		await _sut.SaveGrantAsync(CreateGrant(qualifier: "viewer"), _ct);

		// Act
		var admin = await _sut.GetGrantAsync("user-1", "tenant-1", "role", "admin", _ct);
		var viewer = await _sut.GetGrantAsync("user-1", "tenant-1", "role", "viewer", _ct);

		// Assert
		admin.ShouldNotBeNull();
		viewer.ShouldNotBeNull();
	}

	#endregion
}

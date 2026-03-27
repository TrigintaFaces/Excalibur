// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.A3.Abstractions.Authorization;
using Excalibur.A3.Authorization.Stores.InMemory;

namespace Excalibur.Tests.A3.Authorization.Stores.InMemory;

/// <summary>
/// Unit tests for <see cref="InMemoryRoleStore"/>.
/// Covers IRoleStore contract: CRUD, tenant filtering, GetService, concurrency.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "A3")]
public sealed class InMemoryRoleStoreShould : UnitTestBase
{
	private readonly InMemoryRoleStore _sut = new();
	private readonly CancellationToken _ct = CancellationToken.None;

	private static RoleSummary CreateSummary(
		string roleId = "role-1",
		string name = "Admin",
		string? tenantId = "tenant-1",
		RoleState state = RoleState.Active) =>
		new(roleId, name, null, tenantId, ["GroupA"], [], state, DateTimeOffset.UtcNow);

	#region SaveRoleAsync

	[Fact]
	public async Task SaveRole_AndRetrieveIt()
	{
		var role = CreateSummary();
		await _sut.SaveRoleAsync(role, _ct);
		var result = await _sut.GetRoleAsync("role-1", _ct);

		result.ShouldNotBeNull();
		result.RoleId.ShouldBe("role-1");
		result.Name.ShouldBe("Admin");
	}

	[Fact]
	public async Task SaveRole_Upsert_OverwritesExisting()
	{
		await _sut.SaveRoleAsync(CreateSummary(name: "OldName"), _ct);
		await _sut.SaveRoleAsync(CreateSummary(name: "NewName"), _ct);
		var result = await _sut.GetRoleAsync("role-1", _ct);
		result!.Name.ShouldBe("NewName");
	}

	[Fact]
	public async Task SaveRole_NullRole_ThrowsArgumentNullException()
	{
		await Should.ThrowAsync<ArgumentNullException>(() => _sut.SaveRoleAsync(null!, _ct));
	}

	#endregion

	#region GetRoleAsync

	[Fact]
	public async Task GetRole_NotFound_ReturnsNull()
	{
		(await _sut.GetRoleAsync("nonexistent", _ct)).ShouldBeNull();
	}

	[Fact]
	public async Task GetRole_EmptyStore_ReturnsNull()
	{
		(await _sut.GetRoleAsync("role-1", _ct)).ShouldBeNull();
	}

	#endregion

	#region GetRolesAsync

	[Fact]
	public async Task GetRoles_NullTenantId_ReturnsAll()
	{
		await _sut.SaveRoleAsync(CreateSummary("role-1", tenantId: "t1"), _ct);
		await _sut.SaveRoleAsync(CreateSummary("role-2", tenantId: "t2"), _ct);
		(await _sut.GetRolesAsync(null, _ct)).Count.ShouldBe(2);
	}

	[Fact]
	public async Task GetRoles_WithTenantId_FiltersCorrectly()
	{
		await _sut.SaveRoleAsync(CreateSummary("role-1", tenantId: "t1"), _ct);
		await _sut.SaveRoleAsync(CreateSummary("role-2", tenantId: "t2"), _ct);
		await _sut.SaveRoleAsync(CreateSummary("role-3", tenantId: "t1"), _ct);

		var results = await _sut.GetRolesAsync("t1", _ct);
		results.Count.ShouldBe(2);
		results.ShouldAllBe(r => r.TenantId == "t1");
	}

	[Fact]
	public async Task GetRoles_EmptyStore_ReturnsEmptyList()
	{
		(await _sut.GetRolesAsync(null, _ct)).ShouldBeEmpty();
	}

	#endregion

	#region DeleteRoleAsync

	[Fact]
	public async Task DeleteRole_Existing_ReturnsTrue()
	{
		await _sut.SaveRoleAsync(CreateSummary(), _ct);
		(await _sut.DeleteRoleAsync("role-1", _ct)).ShouldBeTrue();
		(await _sut.GetRoleAsync("role-1", _ct)).ShouldBeNull();
	}

	[Fact]
	public async Task DeleteRole_NonExisting_ReturnsFalse()
	{
		(await _sut.DeleteRoleAsync("nonexistent", _ct)).ShouldBeFalse();
	}

	[Fact]
	public async Task DeleteRole_DoesNotAffectOtherRoles()
	{
		await _sut.SaveRoleAsync(CreateSummary("role-1"), _ct);
		await _sut.SaveRoleAsync(CreateSummary("role-2"), _ct);
		await _sut.DeleteRoleAsync("role-1", _ct);
		(await _sut.GetRoleAsync("role-2", _ct)).ShouldNotBeNull();
	}

	#endregion

	#region GetService

	[Fact]
	public void GetService_AnyType_ReturnsNull()
	{
		_sut.GetService(typeof(IRoleStore)).ShouldBeNull();
		_sut.GetService(typeof(string)).ShouldBeNull();
	}

	[Fact]
	public void GetService_NullType_ThrowsArgumentNullException()
	{
		Should.Throw<ArgumentNullException>(() => _sut.GetService(null!));
	}

	#endregion

	#region Concurrency

	[Fact]
	public async Task ConcurrentSaveAndRead_IsThreadSafe()
	{
		const int count = 100;
		var tasks = new List<Task>(count * 2);
		for (var i = 0; i < count; i++)
		{
			var id = $"role-{i}";
			tasks.Add(_sut.SaveRoleAsync(CreateSummary(id, name: $"Role{i}"), _ct));
			tasks.Add(_sut.GetRoleAsync(id, _ct));
		}

		await Task.WhenAll(tasks);
		(await _sut.GetRolesAsync(null, _ct)).Count.ShouldBe(count);
	}

	[Fact]
	public async Task ConcurrentSaveAndDelete_IsThreadSafe()
	{
		const int count = 100;
		for (var i = 0; i < count; i++)
			await _sut.SaveRoleAsync(CreateSummary($"role-{i}"), _ct);

		var tasks = new List<Task>(count);
		for (var i = 0; i < count; i++)
		{
			if (i % 2 == 0)
				tasks.Add(_sut.DeleteRoleAsync($"role-{i}", _ct));
			else
				tasks.Add(_sut.SaveRoleAsync(CreateSummary($"role-{i}", name: "Updated"), _ct));
		}

		await Task.WhenAll(tasks);
	}

	#endregion
}

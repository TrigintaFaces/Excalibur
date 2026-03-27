// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.A3.Abstractions.Authorization;
using Excalibur.A3.Authorization.Stores.InMemory;

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

	[Fact]
	public async Task CreateActivityGroup_NullTenantId_Succeeds()
	{
		// Act
		var affected = await _sut.CreateActivityGroupAsync(null, "Orders", "View", _ct);

		// Assert
		affected.ShouldBe(1);
		(await _sut.ActivityGroupExistsAsync("Orders", _ct)).ShouldBeTrue();
	}

	#endregion

	#region ActivityGroupExistsAsync

	[Fact]
	public async Task ActivityGroupExists_WhenPresent_ReturnsTrue()
	{
		// Arrange
		await _sut.CreateActivityGroupAsync("tenant-1", "Orders", "View", _ct);

		// Act & Assert
		(await _sut.ActivityGroupExistsAsync("Orders", _ct)).ShouldBeTrue();
	}

	[Fact]
	public async Task ActivityGroupExists_WhenAbsent_ReturnsFalse()
	{
		// Act & Assert
		(await _sut.ActivityGroupExistsAsync("Nonexistent", _ct)).ShouldBeFalse();
	}

	[Fact]
	public async Task ActivityGroupExists_EmptyStore_ReturnsFalse()
	{
		// Act & Assert
		(await _sut.ActivityGroupExistsAsync("Orders", _ct)).ShouldBeFalse();
	}

	[Fact]
	public async Task ActivityGroupExists_CaseSensitive()
	{
		// Arrange
		await _sut.CreateActivityGroupAsync("t", "Orders", "View", _ct);

		// Act & Assert -- exact match required
		(await _sut.ActivityGroupExistsAsync("Orders", _ct)).ShouldBeTrue();
		(await _sut.ActivityGroupExistsAsync("orders", _ct)).ShouldBeFalse();
		(await _sut.ActivityGroupExistsAsync("ORDERS", _ct)).ShouldBeFalse();
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
		var result = await _sut.FindActivityGroupsAsync(_ct);

		// Assert
		result.Count.ShouldBe(2);
		result.ShouldContainKey("Orders");
		result.ShouldContainKey("Products");

		var ordersActivities = result["Orders"] as List<string>;
		ordersActivities.ShouldNotBeNull();
		ordersActivities.Count.ShouldBe(2);
		ordersActivities.ShouldContain("View");
		ordersActivities.ShouldContain("Edit");
	}

	[Fact]
	public async Task FindActivityGroups_EmptyStore_ReturnsEmptyDictionary()
	{
		// Act
		var result = await _sut.FindActivityGroupsAsync(_ct);

		// Assert
		result.ShouldBeEmpty();
	}

	[Fact]
	public async Task FindActivityGroups_SingleEntry_ReturnsSingleGroup()
	{
		// Arrange
		await _sut.CreateActivityGroupAsync("t", "Admin", "ManageUsers", _ct);

		// Act
		var result = await _sut.FindActivityGroupsAsync(_ct);

		// Assert
		result.Count.ShouldBe(1);
		result.ShouldContainKey("Admin");
	}

	#endregion

	#region DeleteAllActivityGroupsAsync

	[Fact]
	public async Task DeleteAllActivityGroups_ReturnsCount()
	{
		// Arrange
		await _sut.CreateActivityGroupAsync("t", "Orders", "View", _ct);
		await _sut.CreateActivityGroupAsync("t", "Orders", "Edit", _ct);
		await _sut.CreateActivityGroupAsync("t", "Products", "View", _ct);

		// Act
		var removed = await _sut.DeleteAllActivityGroupsAsync(_ct);

		// Assert
		removed.ShouldBe(3);
		(await _sut.FindActivityGroupsAsync(_ct)).ShouldBeEmpty();
	}

	[Fact]
	public async Task DeleteAllActivityGroups_EmptyStore_ReturnsZero()
	{
		// Act
		var removed = await _sut.DeleteAllActivityGroupsAsync(_ct);

		// Assert
		removed.ShouldBe(0);
	}

	[Fact]
	public async Task DeleteAllActivityGroups_StoreIsReusableAfterDelete()
	{
		// Arrange
		await _sut.CreateActivityGroupAsync("t", "Orders", "View", _ct);
		await _sut.DeleteAllActivityGroupsAsync(_ct);

		// Act -- add new entries after clearing
		var affected = await _sut.CreateActivityGroupAsync("t", "Products", "Edit", _ct);

		// Assert
		affected.ShouldBe(1);
		(await _sut.ActivityGroupExistsAsync("Products", _ct)).ShouldBeTrue();
	}

	#endregion

	#region GetService

	[Fact]
	public void GetService_AnyType_ReturnsNull()
	{
		// Activity group store has no ISP sub-interfaces
		_sut.GetService(typeof(IActivityGroupGrantStore)).ShouldBeNull();
		_sut.GetService(typeof(IGrantStore)).ShouldBeNull();
		_sut.GetService(typeof(IGrantQueryStore)).ShouldBeNull();
		_sut.GetService(typeof(string)).ShouldBeNull();
	}

	[Fact]
	public void GetService_NullType_ThrowsArgumentNullException()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() => _sut.GetService(null!));
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
			tasks.Add(_sut.ActivityGroupExistsAsync(name, _ct));
		}

		// Act & Assert -- should not throw
		await Task.WhenAll(tasks);

		// Verify all writes landed
		for (var i = 0; i < count; i++)
		{
			(await _sut.ActivityGroupExistsAsync($"Group-{i}", _ct)).ShouldBeTrue();
		}
	}

	[Fact]
	public async Task ConcurrentCreateAndDeleteAll_IsThreadSafe()
	{
		// Arrange -- pre-populate
		for (var i = 0; i < 50; i++)
		{
			await _sut.CreateActivityGroupAsync("t", $"Group-{i}", $"Act-{i}", _ct);
		}

		// Act -- concurrent create + deleteAll interleaved
		var tasks = new List<Task>();
		for (var i = 50; i < 100; i++)
		{
			tasks.Add(_sut.CreateActivityGroupAsync("t", $"Group-{i}", $"Act-{i}", _ct));
		}

		tasks.Add(_sut.DeleteAllActivityGroupsAsync(_ct));

		// Assert -- should not throw (final state is non-deterministic but no exceptions)
		await Task.WhenAll(tasks);
	}

	#endregion

	#region Edge Cases

	[Fact]
	public async Task CreateActivityGroup_EmptyStrings_Succeeds()
	{
		// Act
		var affected = await _sut.CreateActivityGroupAsync("", "", "", _ct);

		// Assert
		affected.ShouldBe(1);
		(await _sut.ActivityGroupExistsAsync("", _ct)).ShouldBeTrue();
	}

	[Fact]
	public async Task FindActivityGroups_AfterDeleteAll_ReturnsEmpty()
	{
		// Arrange
		await _sut.CreateActivityGroupAsync("t", "Orders", "View", _ct);
		await _sut.DeleteAllActivityGroupsAsync(_ct);

		// Act
		var result = await _sut.FindActivityGroupsAsync(_ct);

		// Assert
		result.ShouldBeEmpty();
	}

	#endregion
}

// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.A3.Abstractions.Authorization;
using Excalibur.A3.Authorization.Roles;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Tests.A3.Authorization.Roles;

/// <summary>
/// Unit tests for <see cref="RolePermissionResolver"/>: role-to-activity resolution,
/// caching behavior, and handling of unknown/inactive roles.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "A3")]
public sealed class RolePermissionResolverShould : UnitTestBase
{
	private readonly IRoleStore _roleStore = A.Fake<IRoleStore>();
	private readonly IActivityGroupStore _activityGroupStore = A.Fake<IActivityGroupStore>();
	private readonly RolePermissionResolver _sut;

	public RolePermissionResolverShould()
	{
		var roleOptions = Microsoft.Extensions.Options.Options.Create(new RoleOptions
		{
			PermissionCacheDurationSeconds = 300,
		});
		_sut = new RolePermissionResolver(_roleStore, _activityGroupStore, roleOptions, NullLogger<RolePermissionResolver>.Instance);
	}

	private static RoleSummary MakeRole(
		string name,
		IReadOnlyList<string> activityGroupNames,
		IReadOnlyList<string> activityNames,
		RoleState state = RoleState.Active,
		string? parentRoleName = null) =>
		new(name, name, null, null, activityGroupNames, activityNames, state, DateTimeOffset.UtcNow, parentRoleName);

	#region Resolution

	[Fact]
	public async Task ResolveDirectActivityNames()
	{
		A.CallTo(() => _roleStore.GetRoleAsync("Admin", A<CancellationToken>._))
			.Returns(MakeRole("Admin", [], ["ExportData", "ImportData"]));

		var result = await _sut.ResolveRolePermissionsAsync("Admin", CancellationToken.None);

		result.Count.ShouldBe(2);
		result.ShouldContain("ExportData");
		result.ShouldContain("ImportData");
	}

	[Fact]
	public async Task ResolveActivityGroupsToActivities()
	{
		A.CallTo(() => _roleStore.GetRoleAsync("Admin", A<CancellationToken>._))
			.Returns(MakeRole("Admin", ["Finance"], []));

		var groups = new Dictionary<string, object>
		{
			["Finance"] = new List<string> { "CreatePayment", "ApprovePayment" },
		};
		A.CallTo(() => _activityGroupStore.FindActivityGroupsAsync(A<CancellationToken>._))
			.Returns(groups);

		var result = await _sut.ResolveRolePermissionsAsync("Admin", CancellationToken.None);

		result.Count.ShouldBe(2);
		result.ShouldContain("CreatePayment");
		result.ShouldContain("ApprovePayment");
	}

	[Fact]
	public async Task UnionDirectAndGroupActivities()
	{
		A.CallTo(() => _roleStore.GetRoleAsync("Admin", A<CancellationToken>._))
			.Returns(MakeRole("Admin", ["Finance"], ["ExportData"]));

		var groups = new Dictionary<string, object>
		{
			["Finance"] = new List<string> { "CreatePayment" },
		};
		A.CallTo(() => _activityGroupStore.FindActivityGroupsAsync(A<CancellationToken>._))
			.Returns(groups);

		var result = await _sut.ResolveRolePermissionsAsync("Admin", CancellationToken.None);

		result.Count.ShouldBe(2);
		result.ShouldContain("ExportData");
		result.ShouldContain("CreatePayment");
	}

	[Fact]
	public async Task ReturnEmpty_WhenRoleNotFound()
	{
		A.CallTo(() => _roleStore.GetRoleAsync("Missing", A<CancellationToken>._))
			.Returns((RoleSummary?)null);

		var result = await _sut.ResolveRolePermissionsAsync("Missing", CancellationToken.None);
		result.ShouldBeEmpty();
	}

	[Fact]
	public async Task ReturnEmpty_WhenRoleIsInactive()
	{
		A.CallTo(() => _roleStore.GetRoleAsync("Inactive", A<CancellationToken>._))
			.Returns(MakeRole("Inactive", ["Finance"], ["Export"], state: RoleState.Inactive));

		var result = await _sut.ResolveRolePermissionsAsync("Inactive", CancellationToken.None);
		result.ShouldBeEmpty();
	}

	[Fact]
	public async Task ReturnEmpty_WhenRoleIsDeprecated()
	{
		A.CallTo(() => _roleStore.GetRoleAsync("Old", A<CancellationToken>._))
			.Returns(MakeRole("Old", [], ["X"], state: RoleState.Deprecated));

		var result = await _sut.ResolveRolePermissionsAsync("Old", CancellationToken.None);
		result.ShouldBeEmpty();
	}

	[Fact]
	public async Task ReturnEmpty_WhenRoleHasNoActivities()
	{
		A.CallTo(() => _roleStore.GetRoleAsync("Empty", A<CancellationToken>._))
			.Returns(MakeRole("Empty", [], []));

		var result = await _sut.ResolveRolePermissionsAsync("Empty", CancellationToken.None);
		result.ShouldBeEmpty();
	}

	#endregion

	#region Caching

	[Fact]
	public async Task CacheResolvedPermissions()
	{
		A.CallTo(() => _roleStore.GetRoleAsync("Admin", A<CancellationToken>._))
			.Returns(MakeRole("Admin", [], ["ExportData"]));

		// First call
		await _sut.ResolveRolePermissionsAsync("Admin", CancellationToken.None);
		// Second call -- should use cache
		var result = await _sut.ResolveRolePermissionsAsync("Admin", CancellationToken.None);

		result.ShouldContain("ExportData");
		// Role store should only be called once (cached on second call)
		A.CallTo(() => _roleStore.GetRoleAsync("Admin", A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task SkipCache_WhenCacheDurationIsZero()
	{
		var roleOptions = Microsoft.Extensions.Options.Options.Create(new RoleOptions
		{
			PermissionCacheDurationSeconds = 0,
		});
		var resolver = new RolePermissionResolver(_roleStore, _activityGroupStore, roleOptions, NullLogger<RolePermissionResolver>.Instance);

		A.CallTo(() => _roleStore.GetRoleAsync("Admin", A<CancellationToken>._))
			.Returns(MakeRole("Admin", [], ["ExportData"]));

		await resolver.ResolveRolePermissionsAsync("Admin", CancellationToken.None);
		await resolver.ResolveRolePermissionsAsync("Admin", CancellationToken.None);

		// Should call store twice since caching is disabled
		A.CallTo(() => _roleStore.GetRoleAsync("Admin", A<CancellationToken>._))
			.MustHaveHappenedTwiceExactly();
	}

	#endregion

	#region Hierarchy Traversal (Sprint 713)

	[Fact]
	public async Task ResolvePermissions_FromParentRole()
	{
		// Arrange -- Child inherits from Parent
		A.CallTo(() => _roleStore.GetRoleAsync("Child", A<CancellationToken>._))
			.Returns(MakeRole("Child", [], ["ChildActivity"], parentRoleName: "Parent"));
		A.CallTo(() => _roleStore.GetRoleAsync("Parent", A<CancellationToken>._))
			.Returns(MakeRole("Parent", [], ["ParentActivity"]));

		// Act
		var result = await _sut.ResolveRolePermissionsAsync("Child", CancellationToken.None);

		// Assert -- union of child + parent
		result.Count.ShouldBe(2);
		result.ShouldContain("ChildActivity");
		result.ShouldContain("ParentActivity");
	}

	[Fact]
	public async Task ResolvePermissions_ThreeLevelsDeep()
	{
		// Arrange -- Grandchild -> Child -> Parent
		A.CallTo(() => _roleStore.GetRoleAsync("Grandchild", A<CancellationToken>._))
			.Returns(MakeRole("Grandchild", [], ["GcActivity"], parentRoleName: "Child"));
		A.CallTo(() => _roleStore.GetRoleAsync("Child", A<CancellationToken>._))
			.Returns(MakeRole("Child", [], ["ChildActivity"], parentRoleName: "Parent"));
		A.CallTo(() => _roleStore.GetRoleAsync("Parent", A<CancellationToken>._))
			.Returns(MakeRole("Parent", [], ["ParentActivity"]));

		// Act
		var result = await _sut.ResolveRolePermissionsAsync("Grandchild", CancellationToken.None);

		// Assert -- union of all 3 levels
		result.Count.ShouldBe(3);
		result.ShouldContain("GcActivity");
		result.ShouldContain("ChildActivity");
		result.ShouldContain("ParentActivity");
	}

	[Fact]
	public async Task DetectCycle_AndStopTraversal()
	{
		// Arrange -- A -> B -> A (cycle)
		A.CallTo(() => _roleStore.GetRoleAsync("RoleA", A<CancellationToken>._))
			.Returns(MakeRole("RoleA", [], ["ActivityA"], parentRoleName: "RoleB"));
		A.CallTo(() => _roleStore.GetRoleAsync("RoleB", A<CancellationToken>._))
			.Returns(MakeRole("RoleB", [], ["ActivityB"], parentRoleName: "RoleA"));

		// Act -- should not infinite loop
		var result = await _sut.ResolveRolePermissionsAsync("RoleA", CancellationToken.None);

		// Assert -- visits both once, stops on cycle
		result.ShouldContain("ActivityA");
		result.ShouldContain("ActivityB");
		result.Count.ShouldBe(2);
	}

	[Fact]
	public async Task EnforceMaxHierarchyDepth()
	{
		// Arrange -- MaxHierarchyDepth = 2; chain is 4 levels deep
		var roleOptions = Microsoft.Extensions.Options.Options.Create(new RoleOptions
		{
			PermissionCacheDurationSeconds = 0,
			MaxHierarchyDepth = 2,
		});
		var resolver = new RolePermissionResolver(_roleStore, _activityGroupStore, roleOptions,
			NullLogger<RolePermissionResolver>.Instance);

		A.CallTo(() => _roleStore.GetRoleAsync("Level0", A<CancellationToken>._))
			.Returns(MakeRole("Level0", [], ["Act0"], parentRoleName: "Level1"));
		A.CallTo(() => _roleStore.GetRoleAsync("Level1", A<CancellationToken>._))
			.Returns(MakeRole("Level1", [], ["Act1"], parentRoleName: "Level2"));
		A.CallTo(() => _roleStore.GetRoleAsync("Level2", A<CancellationToken>._))
			.Returns(MakeRole("Level2", [], ["Act2"], parentRoleName: "Level3"));
		A.CallTo(() => _roleStore.GetRoleAsync("Level3", A<CancellationToken>._))
			.Returns(MakeRole("Level3", [], ["Act3"]));

		// Act
		var result = await resolver.ResolveRolePermissionsAsync("Level0", CancellationToken.None);

		// Assert -- only Level0 (depth=0), Level1 (depth=1), Level2 (depth=2) resolved
		result.ShouldContain("Act0");
		result.ShouldContain("Act1");
		result.ShouldContain("Act2");
		result.ShouldNotContain("Act3"); // Beyond MaxHierarchyDepth
	}

	[Fact]
	public async Task StopTraversal_WhenParentIsInactive()
	{
		// Arrange -- Child -> InactiveParent -> Grandparent
		A.CallTo(() => _roleStore.GetRoleAsync("Child", A<CancellationToken>._))
			.Returns(MakeRole("Child", [], ["ChildActivity"], parentRoleName: "InactiveParent"));
		A.CallTo(() => _roleStore.GetRoleAsync("InactiveParent", A<CancellationToken>._))
			.Returns(MakeRole("InactiveParent", [], ["ParentActivity"], state: RoleState.Inactive, parentRoleName: "Grandparent"));
		A.CallTo(() => _roleStore.GetRoleAsync("Grandparent", A<CancellationToken>._))
			.Returns(MakeRole("Grandparent", [], ["GrandparentActivity"]));

		// Act
		var result = await _sut.ResolveRolePermissionsAsync("Child", CancellationToken.None);

		// Assert -- stops at inactive parent, grandparent not reached
		result.Count.ShouldBe(1);
		result.ShouldContain("ChildActivity");
		result.ShouldNotContain("ParentActivity");
		result.ShouldNotContain("GrandparentActivity");
	}

	[Fact]
	public async Task StopTraversal_WhenParentNotFound()
	{
		// Arrange -- Child -> Missing parent
		A.CallTo(() => _roleStore.GetRoleAsync("Child", A<CancellationToken>._))
			.Returns(MakeRole("Child", [], ["ChildActivity"], parentRoleName: "Missing"));
		A.CallTo(() => _roleStore.GetRoleAsync("Missing", A<CancellationToken>._))
			.Returns((RoleSummary?)null);

		// Act
		var result = await _sut.ResolveRolePermissionsAsync("Child", CancellationToken.None);

		// Assert -- only child activities
		result.Count.ShouldBe(1);
		result.ShouldContain("ChildActivity");
	}

	[Fact]
	public async Task ReturnOnlyDirectActivities_WhenNoParent()
	{
		// Arrange -- root role with no parent
		A.CallTo(() => _roleStore.GetRoleAsync("Root", A<CancellationToken>._))
			.Returns(MakeRole("Root", [], ["RootActivity"]));

		var result = await _sut.ResolveRolePermissionsAsync("Root", CancellationToken.None);

		result.Count.ShouldBe(1);
		result.ShouldContain("RootActivity");
	}

	[Fact]
	public async Task UnionParentGroupsWithChildDirectActivities()
	{
		// Arrange -- Parent has activity groups, Child has direct activities
		A.CallTo(() => _roleStore.GetRoleAsync("Child", A<CancellationToken>._))
			.Returns(MakeRole("Child", [], ["DirectChildAct"], parentRoleName: "Parent"));
		A.CallTo(() => _roleStore.GetRoleAsync("Parent", A<CancellationToken>._))
			.Returns(MakeRole("Parent", ["Finance"], []));

		var groups = new Dictionary<string, object>
		{
			["Finance"] = new List<string> { "CreatePayment", "ApprovePayment" },
		};
		A.CallTo(() => _activityGroupStore.FindActivityGroupsAsync(A<CancellationToken>._))
			.Returns(groups);

		// Act
		var result = await _sut.ResolveRolePermissionsAsync("Child", CancellationToken.None);

		// Assert -- child direct + parent groups expanded
		result.Count.ShouldBe(3);
		result.ShouldContain("DirectChildAct");
		result.ShouldContain("CreatePayment");
		result.ShouldContain("ApprovePayment");
	}

	#endregion
}

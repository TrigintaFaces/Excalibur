// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.A3.Abstractions.Authorization;
using Excalibur.A3.Authorization.Roles;
using Excalibur.A3.Authorization.Roles.Events;

namespace Excalibur.Tests.A3.Authorization.Roles;

/// <summary>
/// Unit tests for <see cref="Role"/> aggregate: creation, state machine, mutations,
/// domain events, and event replay via <see cref="Role.FromEvents"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "A3")]
public sealed class RoleShould : UnitTestBase
{
	private static readonly IReadOnlyList<string> DefaultActivityGroups = ["Orders.View", "Orders.Edit"];

	#region Creation

	[Fact]
	public void SetProperties_WhenCreated()
	{
		// Act
		var role = new Role("role-1", "Admin", "Administrator", "tenant-1", DefaultActivityGroups, null, "creator");

		// Assert
		role.Id.ShouldBe("role-1");
		role.Name.ShouldBe("Admin");
		role.Description.ShouldBe("Administrator");
		role.TenantId.ShouldBe("tenant-1");
		role.ActivityGroupNames.Count.ShouldBe(2);
		role.ActivityGroupNames.ShouldContain("Orders.View");
		role.ActivityGroupNames.ShouldContain("Orders.Edit");
		role.State.ShouldBe(RoleState.Active);
		role.CreatedAt.ShouldNotBe(default);
	}

	[Fact]
	public void AllowNullOptionalParameters()
	{
		// Act
		var role = new Role("role-1", "Reader", null, null, [], null, "creator");

		// Assert
		role.Description.ShouldBeNull();
		role.TenantId.ShouldBeNull();
		role.ActivityGroupNames.ShouldBeEmpty();
	}

	[Fact]
	public void RaiseDomainEvent_WhenCreated()
	{
		// Act
		var role = new Role("role-1", "Admin", "Desc", "t1", DefaultActivityGroups, null, "creator");

		// Assert
		var events = role.GetUncommittedEvents().ToList();
		events.Count.ShouldBe(1);
		var created = events[0].ShouldBeOfType<RoleCreated>();
		created.RoleId.ShouldBe("role-1");
		created.Name.ShouldBe("Admin");
		created.Description.ShouldBe("Desc");
		created.TenantId.ShouldBe("t1");
		created.ActivityGroupNames.Count.ShouldBe(2);
		created.CreatedBy.ShouldBe("creator");
		created.EventType.ShouldBe("RoleCreated");
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	public void ThrowOnNullOrEmptyRoleId(string? roleId)
	{
		Should.Throw<ArgumentException>(() =>
			new Role(roleId!, "Name", null, null, [], null, "creator"));
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	public void ThrowOnNullOrEmptyName(string? name)
	{
		Should.Throw<ArgumentException>(() =>
			new Role("role-1", name!, null, null, [], null, "creator"));
	}

	[Fact]
	public void ThrowOnNullActivityGroupNames()
	{
		Should.Throw<ArgumentNullException>(() =>
			new Role("role-1", "Name", null, null, null!, null, "creator"));
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	public void ThrowOnNullOrEmptyCreatedBy(string? createdBy)
	{
		Should.Throw<ArgumentException>(() =>
			new Role("role-1", "Name", null, null, [], null, createdBy!));
	}

	#endregion

	#region Modify

	[Fact]
	public void UpdateNameAndDescription_WhenModified()
	{
		// Arrange
		var role = new Role("role-1", "OldName", "OldDesc", null, [], null, "creator");

		// Act
		role.Modify("NewName", "NewDesc");

		// Assert
		role.Name.ShouldBe("NewName");
		role.Description.ShouldBe("NewDesc");
	}

	[Fact]
	public void RaiseRoleModifiedEvent()
	{
		// Arrange
		var role = new Role("role-1", "Admin", null, null, [], null, "creator");

		// Act
		role.Modify("SuperAdmin", "Updated description");

		// Assert
		var events = role.GetUncommittedEvents().ToList();
		events.Count.ShouldBe(2); // RoleCreated + RoleModified
		var modified = events[1].ShouldBeOfType<RoleModified>();
		modified.RoleId.ShouldBe("role-1");
		modified.Name.ShouldBe("SuperAdmin");
		modified.Description.ShouldBe("Updated description");
	}

	[Fact]
	public void ThrowOnModify_WhenDeprecated()
	{
		// Arrange
		var role = new Role("role-1", "Admin", null, null, [], null, "creator");
		role.Deprecate("End of life");

		// Act & Assert
		Should.Throw<InvalidOperationException>(() => role.Modify("New", null))
			.Message.ShouldContain("deprecated");
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	public void ThrowOnModify_WithNullOrEmptyName(string? name)
	{
		var role = new Role("role-1", "Admin", null, null, [], null, "creator");
		Should.Throw<ArgumentException>(() => role.Modify(name!, null));
	}

	#endregion

	#region ChangeActivityGroups

	[Fact]
	public void ReplaceActivityGroups_WhenChanged()
	{
		// Arrange
		var role = new Role("role-1", "Admin", null, null, ["OldGroup"], null, "creator");
		var newGroups = new List<string> { "GroupA", "GroupB", "GroupC" };

		// Act
		role.ChangeActivityGroups(newGroups);

		// Assert
		role.ActivityGroupNames.Count.ShouldBe(3);
		role.ActivityGroupNames.ShouldContain("GroupA");
		role.ActivityGroupNames.ShouldNotContain("OldGroup");
	}

	[Fact]
	public void RaiseRoleActivityGroupsChangedEvent()
	{
		// Arrange
		var role = new Role("role-1", "Admin", null, null, [], null, "creator");

		// Act
		role.ChangeActivityGroups(["X", "Y"]);

		// Assert
		var events = role.GetUncommittedEvents().ToList();
		var changed = events[1].ShouldBeOfType<RoleActivityGroupsChanged>();
		changed.RoleId.ShouldBe("role-1");
		changed.ActivityGroupNames.Count.ShouldBe(2);
	}

	[Fact]
	public void ThrowOnChangeActivityGroups_WhenDeprecated()
	{
		var role = new Role("role-1", "Admin", null, null, [], null, "creator");
		role.Deprecate();

		Should.Throw<InvalidOperationException>(() => role.ChangeActivityGroups(["X"]));
	}

	[Fact]
	public void ThrowOnChangeActivityGroups_WithNull()
	{
		var role = new Role("role-1", "Admin", null, null, [], null, "creator");
		Should.Throw<ArgumentNullException>(() => role.ChangeActivityGroups(null!));
	}

	#endregion

	#region ChangeActivities

	[Fact]
	public void ReplaceActivityNames_WhenChanged()
	{
		var role = new Role("role-1", "Admin", null, null, [], ["OldActivity"], "creator");

		role.ChangeActivities(["ActA", "ActB"]);

		role.ActivityNames.Count.ShouldBe(2);
		role.ActivityNames.ShouldContain("ActA");
		role.ActivityNames.ShouldContain("ActB");
		role.ActivityNames.ShouldNotContain("OldActivity");
	}

	[Fact]
	public void RaiseRoleActivitiesChangedEvent()
	{
		var role = new Role("role-1", "Admin", null, null, [], null, "creator");

		role.ChangeActivities(["X", "Y"]);

		var events = role.GetUncommittedEvents().ToList();
		var changed = events[1].ShouldBeOfType<RoleActivitiesChanged>();
		changed.RoleId.ShouldBe("role-1");
		changed.ActivityNames.Count.ShouldBe(2);
	}

	[Fact]
	public void ThrowOnChangeActivities_WhenDeprecated()
	{
		var role = new Role("role-1", "Admin", null, null, [], null, "creator");
		role.Deprecate();

		Should.Throw<InvalidOperationException>(() => role.ChangeActivities(["X"]));
	}

	[Fact]
	public void ThrowOnChangeActivities_WithNull()
	{
		var role = new Role("role-1", "Admin", null, null, [], null, "creator");
		Should.Throw<ArgumentNullException>(() => role.ChangeActivities(null!));
	}

	#endregion

	#region ActivityNames in Creation

	[Fact]
	public void SetActivityNames_WhenCreatedWithValues()
	{
		var role = new Role("role-1", "Admin", null, null, ["GroupA"], ["ExportData", "ImportData"], "creator");

		role.ActivityNames.Count.ShouldBe(2);
		role.ActivityNames.ShouldContain("ExportData");
		role.ActivityNames.ShouldContain("ImportData");
	}

	[Fact]
	public void DefaultActivityNames_ToEmpty_WhenNull()
	{
		var role = new Role("role-1", "Admin", null, null, [], null, "creator");

		role.ActivityNames.ShouldBeEmpty();
	}

	[Fact]
	public void IncludeActivityNames_InRoleCreatedEvent()
	{
		var role = new Role("role-1", "Admin", null, null, [], ["Act1"], "creator");

		var created = role.GetUncommittedEvents().ToList()[0].ShouldBeOfType<RoleCreated>();
		created.ActivityNames.Count.ShouldBe(1);
		created.ActivityNames.ShouldContain("Act1");
	}

	[Fact]
	public void IncludeActivityNames_InToSummary()
	{
		var role = new Role("role-1", "Admin", null, null, ["GroupA"], ["ExportData"], "creator");
		var summary = role.ToSummary();

		summary.ActivityNames.Count.ShouldBe(1);
		summary.ActivityNames.ShouldContain("ExportData");
	}

	[Fact]
	public void ReplayActivityNames_ViaFromEvents()
	{
		var original = new Role("role-1", "Admin", null, null, [], ["Act1"], "creator");
		original.ChangeActivities(["Act2", "Act3"]);

		var rebuilt = Role.FromEvents("role-1", original.GetUncommittedEvents());

		rebuilt.ActivityNames.Count.ShouldBe(2);
		rebuilt.ActivityNames.ShouldContain("Act2");
		rebuilt.ActivityNames.ShouldContain("Act3");
	}

	#endregion

	#region State Machine

	[Fact]
	public void Deactivate_FromActive()
	{
		var role = new Role("role-1", "Admin", null, null, [], null, "creator");
		role.Deactivate("Maintenance");
		role.State.ShouldBe(RoleState.Inactive);
	}

	[Fact]
	public void Activate_FromInactive()
	{
		var role = new Role("role-1", "Admin", null, null, [], null, "creator");
		role.Deactivate();
		role.Activate();
		role.State.ShouldBe(RoleState.Active);
	}

	[Fact]
	public void Deprecate_FromActive()
	{
		var role = new Role("role-1", "Admin", null, null, [], null, "creator");
		role.Deprecate("End of life");
		role.State.ShouldBe(RoleState.Deprecated);
	}

	[Fact]
	public void Deprecate_FromInactive()
	{
		var role = new Role("role-1", "Admin", null, null, [], null, "creator");
		role.Deactivate();
		role.Deprecate();
		role.State.ShouldBe(RoleState.Deprecated);
	}

	[Fact]
	public void ThrowOnDeactivate_WhenDeprecated()
	{
		var role = new Role("role-1", "Admin", null, null, [], null, "creator");
		role.Deprecate();
		Should.Throw<InvalidOperationException>(() => role.Deactivate());
	}

	[Fact]
	public void ThrowOnActivate_WhenDeprecated()
	{
		var role = new Role("role-1", "Admin", null, null, [], null, "creator");
		role.Deprecate();
		Should.Throw<InvalidOperationException>(() => role.Activate());
	}

	[Fact]
	public void ThrowOnDeprecate_WhenAlreadyDeprecated()
	{
		var role = new Role("role-1", "Admin", null, null, [], null, "creator");
		role.Deprecate();
		Should.Throw<InvalidOperationException>(() => role.Deprecate());
	}

	[Fact]
	public void RaiseRoleStateChangedEvents()
	{
		var role = new Role("role-1", "Admin", null, null, [], null, "creator");
		role.Deactivate("reason");
		role.Activate();
		role.Deprecate("final");

		var events = role.GetUncommittedEvents().ToList();
		events.Count.ShouldBe(4);

		var deactivated = events[1].ShouldBeOfType<RoleStateChanged>();
		deactivated.NewState.ShouldBe(RoleState.Inactive);
		deactivated.Reason.ShouldBe("reason");

		var activated = events[2].ShouldBeOfType<RoleStateChanged>();
		activated.NewState.ShouldBe(RoleState.Active);

		var deprecated = events[3].ShouldBeOfType<RoleStateChanged>();
		deprecated.NewState.ShouldBe(RoleState.Deprecated);
		deprecated.Reason.ShouldBe("final");
	}

	#endregion

	#region Event Replay (FromEvents)

	[Fact]
	public void RebuildFromEvents_ViaFromEvents()
	{
		var original = new Role("role-1", "Admin", "Desc", "t1", ["GroupA"], null, "creator");
		original.Modify("SuperAdmin", "Updated");
		original.ChangeActivityGroups(["GroupB", "GroupC"]);
		original.Deactivate("Maintenance");

		var events = original.GetUncommittedEvents().ToList();
		var rebuilt = Role.FromEvents("role-1", events);

		rebuilt.Id.ShouldBe("role-1");
		rebuilt.Name.ShouldBe("SuperAdmin");
		rebuilt.Description.ShouldBe("Updated");
		rebuilt.TenantId.ShouldBe("t1");
		rebuilt.ActivityGroupNames.Count.ShouldBe(2);
		rebuilt.ActivityGroupNames.ShouldContain("GroupB");
		rebuilt.ActivityGroupNames.ShouldContain("GroupC");
		rebuilt.State.ShouldBe(RoleState.Inactive);
	}

	[Fact]
	public void CreateFactory_ReturnsEmptyAggregate()
	{
		var role = Role.Create("role-1");
		role.Id.ShouldBe("role-1");
		role.Name.ShouldBe(string.Empty);
		role.State.ShouldBe(RoleState.Active);
	}

	#endregion

	#region ToSummary

	[Fact]
	public void ConvertToSummary()
	{
		var role = new Role("role-1", "Admin", "Desc", "t1", ["GroupA"], null, "creator");
		var summary = role.ToSummary();

		summary.RoleId.ShouldBe("role-1");
		summary.Name.ShouldBe("Admin");
		summary.Description.ShouldBe("Desc");
		summary.TenantId.ShouldBe("t1");
		summary.ActivityGroupNames.Count.ShouldBe(1);
		summary.State.ShouldBe(RoleState.Active);
		summary.CreatedAt.ShouldNotBe(default);
	}

	#endregion
}

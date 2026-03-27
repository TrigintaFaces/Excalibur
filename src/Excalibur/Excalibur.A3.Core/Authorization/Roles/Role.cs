// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.A3.Abstractions.Authorization;
using Excalibur.A3.Authorization.Roles.Events;
using Excalibur.Dispatch.Abstractions;
using Excalibur.Domain.Model;

namespace Excalibur.A3.Authorization.Roles;

/// <summary>
/// Event-sourced aggregate representing a named role that maps to one or more activity groups.
/// </summary>
/// <remarks>
/// <para>
/// Roles are the unit of assignment for users. A role assignment is represented as a
/// <c>Grant</c> with <c>GrantType = "Role"</c> and <c>Qualifier = roleName</c>,
/// reusing the existing grant infrastructure.
/// </para>
/// <para>
/// State transitions: <c>Active</c> -> <c>Inactive</c> (reversible) -> <c>Deprecated</c> (one-way).
/// </para>
/// </remarks>
internal sealed class Role : AggregateRoot, IAggregateRoot<Role, string>
{
	private readonly List<string> _activityGroupNames = [];
	private readonly List<string> _activityNames = [];

	/// <summary>
	/// Private constructor for event replay via static factory methods.
	/// </summary>
	private Role()
	{
	}

	/// <summary>
	/// Creates a new role.
	/// </summary>
	/// <param name="roleId">Unique role identifier.</param>
	/// <param name="name">Display name.</param>
	/// <param name="description">Optional description.</param>
	/// <param name="tenantId">Optional tenant scope.</param>
	/// <param name="activityGroupNames">Activity groups this role maps to.</param>
	/// <param name="activityNames">Individual activities this role grants directly.</param>
	/// <param name="createdBy">The actor creating the role.</param>
	public Role(
		string roleId,
		string name,
		string? description,
		string? tenantId,
		IReadOnlyList<string> activityGroupNames,
		IReadOnlyList<string>? activityNames,
		string createdBy)
	{
		ArgumentException.ThrowIfNullOrEmpty(roleId);
		ArgumentException.ThrowIfNullOrEmpty(name);
		ArgumentNullException.ThrowIfNull(activityGroupNames);
		ArgumentException.ThrowIfNullOrEmpty(createdBy);

		RaiseEvent(new RoleCreated
		{
			RoleId = roleId,
			Name = name,
			Description = description,
			TenantId = tenantId,
			ActivityGroupNames = activityGroupNames,
			ActivityNames = activityNames ?? [],
			CreatedBy = createdBy,
		});
	}

	/// <summary>
	/// Gets the role display name.
	/// </summary>
	public string Name { get; private set; } = string.Empty;

	/// <summary>
	/// Gets the optional role description.
	/// </summary>
	public string? Description { get; private set; }

	/// <summary>
	/// Gets the optional tenant scope.
	/// </summary>
	public string? TenantId { get; private set; }

	/// <summary>
	/// Gets the activity groups this role maps to.
	/// </summary>
	public IReadOnlyList<string> ActivityGroupNames => _activityGroupNames.AsReadOnly();

	/// <summary>
	/// Gets the individual activities this role grants directly.
	/// </summary>
	public IReadOnlyList<string> ActivityNames => _activityNames.AsReadOnly();

	/// <summary>
	/// Gets the optional parent role name for hierarchy inheritance.
	/// </summary>
	public string? ParentRoleName { get; private set; }

	/// <summary>
	/// Gets the current lifecycle state.
	/// </summary>
	public RoleState State { get; private set; }

	/// <summary>
	/// Gets when the role was created.
	/// </summary>
	public DateTimeOffset CreatedAt { get; private set; }

	/// <summary>
	/// Creates a new Role instance with the specified identifier.
	/// </summary>
	/// <param name="id">The role identifier.</param>
	/// <returns>A new Role instance for event replay.</returns>
	public static Role Create(string id) => new() { Id = id };

	/// <summary>
	/// Rebuilds a Role from a stream of historical events.
	/// </summary>
	/// <param name="id">The role identifier.</param>
	/// <param name="events">The stream of events to apply.</param>
	/// <returns>The Role rebuilt from the events.</returns>
	public static Role FromEvents(string id, IEnumerable<IDomainEvent> events)
	{
		var role = new Role { Id = id };
		role.LoadFromHistory(events);
		return role;
	}

	/// <summary>
	/// Updates the role name and description.
	/// </summary>
	/// <param name="name">New display name.</param>
	/// <param name="description">New description.</param>
	public void Modify(string name, string? description)
	{
		ArgumentException.ThrowIfNullOrEmpty(name);
		EnsureNotDeprecated();

		RaiseEvent(new RoleModified
		{
			RoleId = Id,
			Name = name,
			Description = description,
		});
	}

	/// <summary>
	/// Updates the activity group mappings.
	/// </summary>
	/// <param name="activityGroupNames">New set of activity group names.</param>
	public void ChangeActivityGroups(IReadOnlyList<string> activityGroupNames)
	{
		ArgumentNullException.ThrowIfNull(activityGroupNames);
		EnsureNotDeprecated();

		RaiseEvent(new RoleActivityGroupsChanged
		{
			RoleId = Id,
			ActivityGroupNames = activityGroupNames,
		});
	}

	/// <summary>
	/// Updates the direct activity mappings.
	/// </summary>
	/// <param name="activityNames">New set of activity names.</param>
	public void ChangeActivities(IReadOnlyList<string> activityNames)
	{
		ArgumentNullException.ThrowIfNull(activityNames);
		EnsureNotDeprecated();

		RaiseEvent(new RoleActivitiesChanged
		{
			RoleId = Id,
			ActivityNames = activityNames,
		});
	}

	/// <summary>
	/// Deactivates the role. Can be reactivated later.
	/// </summary>
	/// <param name="reason">Optional reason for deactivation.</param>
	public void Deactivate(string? reason = null)
	{
		EnsureNotDeprecated();

		RaiseEvent(new RoleStateChanged
		{
			RoleId = Id,
			NewState = RoleState.Inactive,
			Reason = reason,
		});
	}

	/// <summary>
	/// Reactivates a previously deactivated role.
	/// </summary>
	public void Activate()
	{
		EnsureNotDeprecated();

		RaiseEvent(new RoleStateChanged
		{
			RoleId = Id,
			NewState = RoleState.Active,
		});
	}

	/// <summary>
	/// Changes the parent role for hierarchy inheritance.
	/// </summary>
	/// <param name="parentRoleName">The new parent role name, or <see langword="null"/> to make this a root role.</param>
	public void ChangeParent(string? parentRoleName)
	{
		EnsureNotDeprecated();

		RaiseEvent(new RoleParentChanged
		{
			RoleId = Id,
			ParentRoleName = parentRoleName,
		});
	}

	/// <summary>
	/// Permanently deprecates the role. Cannot be reversed.
	/// </summary>
	/// <param name="reason">Optional reason for deprecation.</param>
	public void Deprecate(string? reason = null)
	{
		EnsureNotDeprecated();

		RaiseEvent(new RoleStateChanged
		{
			RoleId = Id,
			NewState = RoleState.Deprecated,
			Reason = reason,
		});
	}

	/// <summary>
	/// Converts the aggregate to a read-model summary.
	/// </summary>
	internal RoleSummary ToSummary() => new(
		RoleId: Id,
		Name: Name,
		Description: Description,
		TenantId: TenantId,
		ActivityGroupNames: ActivityGroupNames,
		ActivityNames: ActivityNames,
		State: State,
		CreatedAt: CreatedAt,
		ParentRoleName: ParentRoleName);

	/// <inheritdoc />
	protected override void ApplyEventInternal(IDomainEvent @event)
	{
		switch (@event)
		{
			case RoleCreated e: Apply(e); break;
			case RoleModified e: Apply(e); break;
			case RoleActivityGroupsChanged e: Apply(e); break;
			case RoleActivitiesChanged e: Apply(e); break;
			case RoleParentChanged e: Apply(e); break;
			case RoleStateChanged e: Apply(e); break;
		}
	}

	private void Apply(RoleCreated e)
	{
		Id = e.RoleId;
		Name = e.Name;
		Description = e.Description;
		TenantId = e.TenantId;
		_activityGroupNames.Clear();
		_activityGroupNames.AddRange(e.ActivityGroupNames);
		_activityNames.Clear();
		_activityNames.AddRange(e.ActivityNames);
		ParentRoleName = e.ParentRoleName;
		State = RoleState.Active;
		CreatedAt = e.OccurredAt;
	}

	private void Apply(RoleModified e)
	{
		Name = e.Name;
		Description = e.Description;
	}

	private void Apply(RoleActivityGroupsChanged e)
	{
		_activityGroupNames.Clear();
		_activityGroupNames.AddRange(e.ActivityGroupNames);
	}

	private void Apply(RoleActivitiesChanged e)
	{
		_activityNames.Clear();
		_activityNames.AddRange(e.ActivityNames);
	}

	private void Apply(RoleParentChanged e)
	{
		ParentRoleName = e.ParentRoleName;
	}

	private void Apply(RoleStateChanged e)
	{
		State = e.NewState;
	}

	private void EnsureNotDeprecated()
	{
		if (State == RoleState.Deprecated)
		{
			throw new InvalidOperationException($"Role '{Id}' is deprecated and cannot be modified.");
		}
	}
}

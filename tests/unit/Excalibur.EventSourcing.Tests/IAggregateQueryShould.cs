// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;

using Excalibur.Domain.Model;
using Excalibur.EventSourcing.Abstractions;

using Shouldly;

using Xunit;

namespace Excalibur.EventSourcing.Tests;

/// <summary>
/// Unit tests for <see cref="IAggregateQuery{TAggregate}"/> marker interface.
/// </summary>
[Trait("Category", "Unit")]
public sealed class IAggregateQueryShould
{
	#region Test Aggregate

	internal enum UserStatus
	{ Active, Inactive, Suspended }

	internal enum UserRole
	{ User, Admin, SuperAdmin }

	internal sealed class UserAggregate : AggregateRoot
	{
		public UserAggregate()
		{ }

		public UserAggregate(string id) : base(id)
		{
		}

		public string Email { get; private set; } = string.Empty;
		public UserStatus Status { get; private set; } = UserStatus.Active;
		public UserRole Role { get; private set; } = UserRole.User;

		protected override void ApplyEventInternal(IDomainEvent @event)
		{
			// No-op for test purposes
		}
	}

	#endregion Test Aggregate

	#region Query Implementations

	/// <summary>
	/// Query implemented as a record (recommended pattern).
	/// </summary>
	internal sealed record GetUsersByStatusQuery(UserStatus Status) : IAggregateQuery<UserAggregate>;

	/// <summary>
	/// Query implemented as a class.
	/// </summary>
	internal sealed class GetUsersByRoleQuery : IAggregateQuery<UserAggregate>
	{
		public GetUsersByRoleQuery(UserRole role)
		{
			Role = role;
		}

		public UserRole Role { get; }
	}

	/// <summary>
	/// Query with multiple criteria.
	/// </summary>
	internal sealed record GetUsersByStatusAndRoleQuery(UserStatus Status, UserRole Role) : IAggregateQuery<UserAggregate>;

	/// <summary>
	/// Query with pagination parameters.
	/// </summary>
	internal sealed record GetUsersPagedQuery(int PageNumber, int PageSize) : IAggregateQuery<UserAggregate>;

	/// <summary>
	/// Query with optional filtering.
	/// </summary>
	internal sealed record SearchUsersQuery : IAggregateQuery<UserAggregate>
	{
		public string? EmailContains { get; init; }
		public UserStatus? Status { get; init; }
		public UserRole? Role { get; init; }
	}

	#endregion Query Implementations

	#region Record Implementation Tests

	[Fact]
	public void RecordQuery_ShouldBeCreatable()
	{
		// Arrange & Act
		var query = new GetUsersByStatusQuery(UserStatus.Active);

		// Assert
		_ = query.ShouldNotBeNull();
		query.Status.ShouldBe(UserStatus.Active);
	}

	[Fact]
	public void RecordQuery_ShouldBeAssignableToInterface()
	{
		// Arrange
		var query = new GetUsersByStatusQuery(UserStatus.Active);

		// Assert
		_ = query.ShouldBeAssignableTo<IAggregateQuery<UserAggregate>>();
	}

	[Fact]
	public void RecordQuery_ShouldSupportEquality()
	{
		// Arrange
		var query1 = new GetUsersByStatusQuery(UserStatus.Active);
		var query2 = new GetUsersByStatusQuery(UserStatus.Active);
		var query3 = new GetUsersByStatusQuery(UserStatus.Inactive);

		// Assert
		query1.ShouldBe(query2);
		query1.ShouldNotBe(query3);
	}

	[Fact]
	public void RecordQuery_ShouldSupportWithExpression()
	{
		// Arrange
		var original = new GetUsersByStatusQuery(UserStatus.Active);

		// Act
		var modified = original with { Status = UserStatus.Suspended };

		// Assert
		modified.Status.ShouldBe(UserStatus.Suspended);
		original.Status.ShouldBe(UserStatus.Active); // Original unchanged
	}

	#endregion Record Implementation Tests

	#region Class Implementation Tests

	[Fact]
	public void ClassQuery_ShouldBeCreatable()
	{
		// Arrange & Act
		var query = new GetUsersByRoleQuery(UserRole.Admin);

		// Assert
		_ = query.ShouldNotBeNull();
		query.Role.ShouldBe(UserRole.Admin);
	}

	[Fact]
	public void ClassQuery_ShouldBeAssignableToInterface()
	{
		// Arrange
		var query = new GetUsersByRoleQuery(UserRole.User);

		// Assert
		_ = query.ShouldBeAssignableTo<IAggregateQuery<UserAggregate>>();
	}

	#endregion Class Implementation Tests

	#region Multi-Criteria Query Tests

	[Fact]
	public void MultiCriteriaQuery_ShouldHoldAllCriteria()
	{
		// Arrange & Act
		var query = new GetUsersByStatusAndRoleQuery(UserStatus.Active, UserRole.Admin);

		// Assert
		query.Status.ShouldBe(UserStatus.Active);
		query.Role.ShouldBe(UserRole.Admin);
	}

	#endregion Multi-Criteria Query Tests

	#region Pagination Query Tests

	[Fact]
	public void PaginatedQuery_ShouldHoldPaginationParameters()
	{
		// Arrange & Act
		var query = new GetUsersPagedQuery(1, 20);

		// Assert
		query.PageNumber.ShouldBe(1);
		query.PageSize.ShouldBe(20);
	}

	#endregion Pagination Query Tests

	#region Optional Filtering Query Tests

	[Fact]
	public void OptionalFilterQuery_ShouldAllowNullCriteria()
	{
		// Arrange & Act
		var query = new SearchUsersQuery
		{
			EmailContains = "test@",
			Status = null,
			Role = UserRole.User
		};

		// Assert
		query.EmailContains.ShouldBe("test@");
		query.Status.ShouldBeNull();
		query.Role.ShouldBe(UserRole.User);
	}

	[Fact]
	public void OptionalFilterQuery_ShouldAllowAllNullCriteria()
	{
		// Arrange & Act
		var query = new SearchUsersQuery();

		// Assert
		query.EmailContains.ShouldBeNull();
		query.Status.ShouldBeNull();
		query.Role.ShouldBeNull();
	}

	#endregion Optional Filtering Query Tests

	#region Type Safety Tests

	[Fact]
	public void Query_ShouldBeTypeSafeToAggregate()
	{
		// This test validates compile-time type safety.
		// The generic constraint ensures queries are tied to specific aggregate types.

		// Arrange
		IAggregateQuery<UserAggregate> userQuery = new GetUsersByStatusQuery(UserStatus.Active);

		// Assert - The fact this compiles proves type safety
		_ = userQuery.ShouldNotBeNull();
	}

	[Fact]
	public void Query_CannotBeUsedWithWrongAggregateType()
	{
		// This is a compile-time check - if you try to use GetUsersByStatusQuery
		// with a different aggregate type, it won't compile.
		// The following would fail to compile:
		// IAggregateQuery<SomeOtherAggregate> wrongQuery = new GetUsersByStatusQuery(UserStatus.Active);

		// This test just documents the behavior
		var query = new GetUsersByStatusQuery(UserStatus.Active);
		_ = query.ShouldBeAssignableTo<IAggregateQuery<UserAggregate>>();
	}

	#endregion Type Safety Tests

	#region Interface Constraint Tests

	[Fact]
	public void IAggregateQuery_Constraint_RequiresClassAndIAggregate()
	{
		// The IAggregateQuery<T> has constraint: where T : class, IAggregateRoot
		// This test validates that UserAggregate satisfies both constraints

		// Arrange
		var aggregate = new UserAggregate("user-1");

		// Assert
		_ = aggregate.ShouldBeAssignableTo<IAggregateRoot>();
		typeof(UserAggregate).IsClass.ShouldBeTrue();
	}

	#endregion Interface Constraint Tests

	#region Marker Interface Behavior Tests

	[Fact]
	public void MarkerInterface_ShouldHaveNoMethods()
	{
		// Arrange
		var interfaceType = typeof(IAggregateQuery<>);

		// Act
		var methods = interfaceType.GetMethods();

		// Assert - Marker interfaces have no methods (only inherited from object)
		methods.ShouldBeEmpty();
	}

	[Fact]
	public void MarkerInterface_ShouldHaveNoProperties()
	{
		// Arrange
		var interfaceType = typeof(IAggregateQuery<>);

		// Act
		var properties = interfaceType.GetProperties();

		// Assert
		properties.ShouldBeEmpty();
	}

	#endregion Marker Interface Behavior Tests
}

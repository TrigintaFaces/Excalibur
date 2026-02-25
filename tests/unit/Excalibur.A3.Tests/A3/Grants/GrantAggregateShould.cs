using Excalibur.A3.Authorization.Events;
using Excalibur.A3.Authorization.Grants;
using Excalibur.Dispatch.Abstractions;
using Excalibur.Domain;

namespace Excalibur.Tests.A3.Grants;

[Collection("ApplicationContext")]
[Trait("Category", "Unit")]
[Trait("Component", "A3")]
public sealed class GrantAggregateShould
{
	#region Factory Method Tests

	[Fact]
	public void Create_returns_grant_with_specified_id()
	{
		// Arrange & Act
		var grant = Grant.Create("user-1:tenant-1:role:admin");

		// Assert
		grant.ShouldNotBeNull();
		grant.Id.ShouldBe("user-1:tenant-1:role:admin");
	}

	[Fact]
	public void FromEvents_rebuilds_grant_from_added_event()
	{
		// Arrange
		var grantedOn = DateTimeOffset.UtcNow.AddMinutes(-5);
		var addedEvent = new GrantAdded(
			"user-1", "John Doe", "TestApp", "tenant-1", "role", "admin",
			DateTimeOffset.UtcNow.AddDays(30), "admin-user", grantedOn);

		// Act
		var grant = Grant.FromEvents("user-1:tenant-1:role:admin", [addedEvent]);

		// Assert
		grant.ShouldNotBeNull();
		grant.UserId.ShouldBe("user-1");
		grant.FullName.ShouldBe("John Doe");
		grant.Scope.ShouldNotBeNull();
		grant.Scope!.TenantId.ShouldBe("tenant-1");
		grant.Scope.GrantType.ShouldBe("role");
		grant.Scope.Qualifier.ShouldBe("admin");
		grant.GrantedBy.ShouldBe("admin-user");
		grant.GrantedOn.ShouldBe(grantedOn);
		grant.ExpiresOn.ShouldNotBeNull();
	}

	[Fact]
	public void FromEvents_rebuilds_grant_with_revoke_event()
	{
		// Arrange
		var grantedOn = DateTimeOffset.UtcNow.AddMinutes(-10);
		var revokedOn = DateTimeOffset.UtcNow.AddMinutes(-2);

		var addedEvent = new GrantAdded(
			"user-1", "John Doe", "TestApp", "tenant-1", "role", "admin",
			null, "admin-user", grantedOn);

		var revokedEvent = new GrantRevoked(
			"user-1", "John Doe", "TestApp", "tenant-1", "role", "admin",
			null, "other-admin", revokedOn);

		// Act
		var grant = Grant.FromEvents("user-1:tenant-1:role:admin", [addedEvent, revokedEvent]);

		// Assert
		grant.RevokedBy.ShouldBe("other-admin");
		grant.RevokedOn.ShouldBe(revokedOn);
		grant.IsRevoked().ShouldBeTrue();
	}

	#endregion

	#region IsExpired Tests

	[Fact]
	public void IsExpired_returns_true_when_expires_on_is_in_the_past()
	{
		// Arrange
		var addedEvent = new GrantAdded(
			"user-1", "John Doe", "TestApp", "tenant-1", "role", "admin",
			DateTimeOffset.UtcNow.AddDays(-1), "admin-user", DateTimeOffset.UtcNow.AddDays(-5));

		var grant = Grant.FromEvents("user-1:tenant-1:role:admin", [addedEvent]);

		// Act & Assert
		grant.IsExpired().ShouldBeTrue();
	}

	[Fact]
	public void IsExpired_returns_false_when_expires_on_is_in_the_future()
	{
		// Arrange
		var addedEvent = new GrantAdded(
			"user-1", "John Doe", "TestApp", "tenant-1", "role", "admin",
			DateTimeOffset.UtcNow.AddDays(30), "admin-user", DateTimeOffset.UtcNow.AddDays(-1));

		var grant = Grant.FromEvents("user-1:tenant-1:role:admin", [addedEvent]);

		// Act & Assert
		grant.IsExpired().ShouldBeFalse();
	}

	[Fact]
	public void IsExpired_returns_false_when_expires_on_is_null()
	{
		// Arrange
		var addedEvent = new GrantAdded(
			"user-1", "John Doe", "TestApp", "tenant-1", "role", "admin",
			null, "admin-user", DateTimeOffset.UtcNow.AddDays(-1));

		var grant = Grant.FromEvents("user-1:tenant-1:role:admin", [addedEvent]);

		// Act & Assert
		grant.IsExpired().ShouldBeFalse();
	}

	#endregion

	#region IsRevoked Tests

	[Fact]
	public void IsRevoked_returns_false_when_not_revoked()
	{
		// Arrange
		var addedEvent = new GrantAdded(
			"user-1", "John Doe", "TestApp", "tenant-1", "role", "admin",
			null, "admin-user", DateTimeOffset.UtcNow);

		var grant = Grant.FromEvents("user-1:tenant-1:role:admin", [addedEvent]);

		// Act & Assert
		grant.IsRevoked().ShouldBeFalse();
	}

	[Fact]
	public void IsRevoked_returns_true_when_revoked()
	{
		// Arrange
		var addedEvent = new GrantAdded(
			"user-1", "John Doe", "TestApp", "tenant-1", "role", "admin",
			null, "admin-user", DateTimeOffset.UtcNow.AddMinutes(-5));

		var revokedEvent = new GrantRevoked(
			"user-1", "John Doe", "TestApp", "tenant-1", "role", "admin",
			null, "other-admin", DateTimeOffset.UtcNow);

		var grant = Grant.FromEvents("user-1:tenant-1:role:admin", [addedEvent, revokedEvent]);

		// Act & Assert
		grant.IsRevoked().ShouldBeTrue();
	}

	#endregion

	#region IsActive Tests

	[Fact]
	public void IsActive_returns_true_when_not_expired_and_not_revoked()
	{
		// Arrange
		var addedEvent = new GrantAdded(
			"user-1", "John Doe", "TestApp", "tenant-1", "role", "admin",
			DateTimeOffset.UtcNow.AddDays(30), "admin-user", DateTimeOffset.UtcNow);

		var grant = Grant.FromEvents("user-1:tenant-1:role:admin", [addedEvent]);

		// Act & Assert
		grant.IsActive().ShouldBeTrue();
	}

	[Fact]
	public void IsActive_returns_false_when_expired()
	{
		// Arrange
		var addedEvent = new GrantAdded(
			"user-1", "John Doe", "TestApp", "tenant-1", "role", "admin",
			DateTimeOffset.UtcNow.AddDays(-1), "admin-user", DateTimeOffset.UtcNow.AddDays(-5));

		var grant = Grant.FromEvents("user-1:tenant-1:role:admin", [addedEvent]);

		// Act & Assert
		grant.IsActive().ShouldBeFalse();
	}

	[Fact]
	public void IsActive_returns_false_when_revoked()
	{
		// Arrange
		var addedEvent = new GrantAdded(
			"user-1", "John Doe", "TestApp", "tenant-1", "role", "admin",
			null, "admin-user", DateTimeOffset.UtcNow.AddMinutes(-5));

		var revokedEvent = new GrantRevoked(
			"user-1", "John Doe", "TestApp", "tenant-1", "role", "admin",
			null, "other-admin", DateTimeOffset.UtcNow);

		var grant = Grant.FromEvents("user-1:tenant-1:role:admin", [addedEvent, revokedEvent]);

		// Act & Assert
		grant.IsActive().ShouldBeFalse();
	}

	[Fact]
	public void IsActive_returns_true_for_non_expiring_non_revoked_grant()
	{
		// Arrange
		var addedEvent = new GrantAdded(
			"user-1", "John Doe", "TestApp", "tenant-1", "role", "admin",
			null, "admin-user", DateTimeOffset.UtcNow);

		var grant = Grant.FromEvents("user-1:tenant-1:role:admin", [addedEvent]);

		// Act & Assert
		grant.IsActive().ShouldBeTrue();
	}

	#endregion

	#region Revoke Tests

	[Fact]
	public void Revoke_sets_revoked_by()
	{
		// Arrange -- Grant.Revoke() reads ApplicationContext.ApplicationName (static)
		ApplicationContext.Init(new Dictionary<string, string?>
		{
			["ApplicationName"] = "TestApp",
			["AuthorizationCacheKey"] = "test-cache",
		});

		var addedEvent = new GrantAdded(
			"user-1", "John Doe", "TestApp", "tenant-1", "role", "admin",
			null, "admin-user", DateTimeOffset.UtcNow);

		var grant = Grant.FromEvents("user-1:tenant-1:role:admin", [addedEvent]);

		// Act
		grant.Revoke("revoker-user");

		// Assert -- the revoke event should be applied
		grant.IsRevoked().ShouldBeTrue();
		grant.RevokedBy.ShouldBe("revoker-user");
		grant.RevokedOn.ShouldNotBeNull();
	}

	#endregion

	#region ApplyEventInternal -- Composite Id from GrantAdded

	[Fact]
	public void Set_composite_id_from_grant_added_event()
	{
		// Arrange
		var addedEvent = new GrantAdded(
			"user-42", "Jane Smith", "TestApp", "tenant-99", "permission", "write",
			null, "admin-user", DateTimeOffset.UtcNow);

		// Act
		var grant = Grant.FromEvents("initial-id", [addedEvent]);

		// Assert -- Id is set from event data, not the initial id
		grant.Id.ShouldBe("user-42:tenant-99:permission:write");
	}

	#endregion

	#region GrantAdded Event Properties

	[Fact]
	public void Apply_grant_added_event_correctly()
	{
		// Arrange
		var grantedOn = DateTimeOffset.UtcNow.AddMinutes(-10);
		var expiresOn = DateTimeOffset.UtcNow.AddDays(90);

		var addedEvent = new GrantAdded(
			"user-abc", "Alice Bob", "MyApp", "tenant-xyz", "activity-group", "orders",
			expiresOn, "superadmin", grantedOn);

		// Act
		var grant = Grant.FromEvents("some-id", [addedEvent]);

		// Assert
		grant.UserId.ShouldBe("user-abc");
		grant.FullName.ShouldBe("Alice Bob");
		grant.Scope.ShouldNotBeNull();
		grant.Scope!.TenantId.ShouldBe("tenant-xyz");
		grant.Scope.GrantType.ShouldBe("activity-group");
		grant.Scope.Qualifier.ShouldBe("orders");
		grant.ExpiresOn.ShouldBe(expiresOn);
		grant.GrantedBy.ShouldBe("superadmin");
		grant.GrantedOn.ShouldBe(grantedOn);
	}

	#endregion

	#region GrantRevoked Event Properties

	[Fact]
	public void Apply_grant_revoked_event_correctly()
	{
		// Arrange
		var revokedOn = DateTimeOffset.UtcNow.AddMinutes(-1);

		var addedEvent = new GrantAdded(
			"user-1", "John", "App", "t1", "role", "admin",
			null, "admin", DateTimeOffset.UtcNow.AddMinutes(-10));

		var revokedEvent = new GrantRevoked(
			"user-1", "John", "App", "t1", "role", "admin",
			null, "hr-admin", revokedOn);

		// Act
		var grant = Grant.FromEvents("user-1:t1:role:admin", [addedEvent, revokedEvent]);

		// Assert
		grant.RevokedBy.ShouldBe("hr-admin");
		grant.RevokedOn.ShouldBe(revokedOn);
	}

	#endregion
}

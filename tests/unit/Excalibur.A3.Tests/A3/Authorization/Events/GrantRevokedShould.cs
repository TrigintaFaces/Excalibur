using Excalibur.A3.Authorization.Events;
using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Tests.A3.Authorization.Events;

[Trait("Category", "Unit")]
[Trait("Component", "A3")]
public sealed class GrantRevokedShould
{
	[Fact]
	public void Initialize_with_all_properties()
	{
		// Arrange
		var userId = "user-123";
		var fullName = "John Doe";
		var applicationName = "TestApp";
		var tenantId = "tenant-456";
		var grantType = "ActivityGroup";
		var qualifier = "admin";
		var expiresOn = DateTimeOffset.UtcNow.AddDays(30);
		var revokedBy = "admin-user";
		var revokedOn = DateTimeOffset.UtcNow;

		// Act
		var grantRevoked = new GrantRevoked(userId, fullName, applicationName, tenantId, grantType, qualifier, expiresOn, revokedBy, revokedOn);

		// Assert
		grantRevoked.UserId.ShouldBe(userId);
		grantRevoked.FullName.ShouldBe(fullName);
		grantRevoked.ApplicationName.ShouldBe(applicationName);
		grantRevoked.TenantId.ShouldBe(tenantId);
		grantRevoked.GrantType.ShouldBe(grantType);
		grantRevoked.Qualifier.ShouldBe(qualifier);
		grantRevoked.ExpiresOn.ShouldBe(expiresOn);
		grantRevoked.RevokedBy.ShouldBe(revokedBy);
		grantRevoked.RevokedOn.ShouldBe(revokedOn);
	}

	[Fact]
	public void Accept_null_expires_on()
	{
		// Act
		var grantRevoked = new GrantRevoked("user", "name", "app", "tenant", "type", "qual", null, "admin", DateTimeOffset.UtcNow);

		// Assert
		grantRevoked.ExpiresOn.ShouldBeNull();
	}

	[Fact]
	public void Inherit_from_DomainEvent()
	{
		// Act
		var grantRevoked = new GrantRevoked("user", "name", "app", "tenant", "type", "qual", null, "admin", DateTimeOffset.UtcNow);

		// Assert
		grantRevoked.ShouldBeAssignableTo<DomainEvent>();
		grantRevoked.ShouldBeAssignableTo<IDomainEvent>();
		grantRevoked.EventId.ShouldNotBeNullOrWhiteSpace();
		grantRevoked.EventType.ShouldBe(nameof(GrantRevoked));
	}

	[Fact]
	public void Implement_IGrantRevoked()
	{
		// Act
		var grantRevoked = new GrantRevoked("user", "name", "app", "tenant", "type", "qual", null, "admin", DateTimeOffset.UtcNow);

		// Assert
		grantRevoked.ShouldBeAssignableTo<IGrantRevoked>();
	}

	[Fact]
	public void Generate_Valid_Uuid7_EventId()
	{
		// Act
		var grantRevoked = new GrantRevoked("user", "name", "app", "tenant", "type", "qual", null, "admin", DateTimeOffset.UtcNow);

		// Assert
		Guid.TryParse(grantRevoked.EventId, out _).ShouldBeTrue("EventId must be a valid UUID v7");
	}

	[Fact]
	public void Generate_Unique_EventIds()
	{
		// Act
		var event1 = new GrantRevoked("user", "name", "app", "tenant", "type", "qual", null, "admin", DateTimeOffset.UtcNow);
		var event2 = new GrantRevoked("user", "name", "app", "tenant", "type", "qual", null, "admin", DateTimeOffset.UtcNow);

		// Assert
		event1.EventId.ShouldNotBe(event2.EventId);
	}

	[Fact]
	public void Support_Fluent_Metadata_Api()
	{
		// Arrange
		var correlationId = Guid.NewGuid();
		var grantRevoked = new GrantRevoked("user", "name", "app", "tenant", "type", "qual", null, "admin", DateTimeOffset.UtcNow);

		// Act
		var withMeta = grantRevoked
			.WithCorrelationId(correlationId)
			.WithCausationId("parent-cmd-1")
			.WithMetadata("source", "test");

		// Assert
		withMeta.ShouldBeOfType<GrantRevoked>();
		var typed = (GrantRevoked)withMeta;
		typed.UserId.ShouldBe("user");
		typed.CorrelationId.ShouldBe(correlationId.ToString());
		typed.CausationId.ShouldBe("parent-cmd-1");
		typed.Metadata!["source"].ShouldBe("test");
	}

	[Fact]
	public void Default_AggregateId_To_Empty()
	{
		// Act
		var grantRevoked = new GrantRevoked("user-abc", "name", "app", "tenant", "type", "qual", null, "admin", DateTimeOffset.UtcNow);

		// Assert — GrantRevoked does not override AggregateId, uses DomainEvent default
		grantRevoked.AggregateId.ShouldBe(string.Empty);
	}
}

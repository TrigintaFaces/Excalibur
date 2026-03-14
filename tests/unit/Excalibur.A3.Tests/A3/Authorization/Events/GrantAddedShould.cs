using Excalibur.A3.Authorization.Events;
using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Tests.A3.Authorization.Events;

[Trait("Category", "Unit")]
[Trait("Component", "A3")]
public sealed class GrantAddedShould
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
		var grantedBy = "admin-user";
		var grantedOn = DateTimeOffset.UtcNow;

		// Act
		var grantAdded = new GrantAdded(userId, fullName, applicationName, tenantId, grantType, qualifier, expiresOn, grantedBy, grantedOn);

		// Assert
		grantAdded.UserId.ShouldBe(userId);
		grantAdded.FullName.ShouldBe(fullName);
		grantAdded.ApplicationName.ShouldBe(applicationName);
		grantAdded.TenantId.ShouldBe(tenantId);
		grantAdded.GrantType.ShouldBe(grantType);
		grantAdded.Qualifier.ShouldBe(qualifier);
		grantAdded.ExpiresOn.ShouldBe(expiresOn);
		grantAdded.GrantedBy.ShouldBe(grantedBy);
		grantAdded.GrantedOn.ShouldBe(grantedOn);
	}

	[Fact]
	public void Accept_null_expires_on()
	{
		// Act
		var grantAdded = new GrantAdded("user", "name", "app", "tenant", "type", "qual", null, "admin", DateTimeOffset.UtcNow);

		// Assert
		grantAdded.ExpiresOn.ShouldBeNull();
	}

	[Fact]
	public void Inherit_from_DomainEvent()
	{
		// Act
		var grantAdded = new GrantAdded("user", "name", "app", "tenant", "type", "qual", null, "admin", DateTimeOffset.UtcNow);

		// Assert
		grantAdded.ShouldBeAssignableTo<DomainEvent>();
		grantAdded.ShouldBeAssignableTo<IDomainEvent>();
		grantAdded.EventId.ShouldNotBeNullOrWhiteSpace();
		grantAdded.EventType.ShouldBe(nameof(GrantAdded));
	}

	[Fact]
	public void Implement_IGrantAdded()
	{
		// Act
		var grantAdded = new GrantAdded("user", "name", "app", "tenant", "type", "qual", null, "admin", DateTimeOffset.UtcNow);

		// Assert
		grantAdded.ShouldBeAssignableTo<IGrantAdded>();
	}

	[Fact]
	public void Generate_Valid_Uuid7_EventId()
	{
		// Act
		var grantAdded = new GrantAdded("user", "name", "app", "tenant", "type", "qual", null, "admin", DateTimeOffset.UtcNow);

		// Assert
		Guid.TryParse(grantAdded.EventId, out _).ShouldBeTrue("EventId must be a valid UUID v7");
	}

	[Fact]
	public void Generate_Unique_EventIds()
	{
		// Act
		var event1 = new GrantAdded("user", "name", "app", "tenant", "type", "qual", null, "admin", DateTimeOffset.UtcNow);
		var event2 = new GrantAdded("user", "name", "app", "tenant", "type", "qual", null, "admin", DateTimeOffset.UtcNow);

		// Assert
		event1.EventId.ShouldNotBe(event2.EventId);
	}

	[Fact]
	public void Support_Fluent_Metadata_Api()
	{
		// Arrange
		var correlationId = Guid.NewGuid();
		var grantAdded = new GrantAdded("user", "name", "app", "tenant", "type", "qual", null, "admin", DateTimeOffset.UtcNow);

		// Act
		var withMeta = grantAdded
			.WithCorrelationId(correlationId)
			.WithCausationId("parent-cmd-1")
			.WithMetadata("source", "test");

		// Assert
		withMeta.ShouldBeOfType<GrantAdded>();
		var typed = (GrantAdded)withMeta;
		typed.UserId.ShouldBe("user");
		typed.Metadata!["CorrelationId"].ShouldBe(correlationId.ToString());
		typed.Metadata["CausationId"].ShouldBe("parent-cmd-1");
		typed.Metadata["source"].ShouldBe("test");
	}

	[Fact]
	public void Default_AggregateId_To_Empty()
	{
		// Act
		var grantAdded = new GrantAdded("user-abc", "name", "app", "tenant", "type", "qual", null, "admin", DateTimeOffset.UtcNow);

		// Assert — GrantAdded does not override AggregateId, uses DomainEvent default
		grantAdded.AggregateId.ShouldBe(string.Empty);
	}
}

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
	public void Inherit_from_DomainEventBase()
	{
		// Act
		var grantAdded = new GrantAdded("user", "name", "app", "tenant", "type", "qual", null, "admin", DateTimeOffset.UtcNow);

		// Assert
		grantAdded.ShouldBeAssignableTo<IDomainEvent>();
		grantAdded.MessageId.ShouldNotBeNullOrWhiteSpace();
		grantAdded.Id.ShouldNotBe(Guid.Empty);
		grantAdded.Kind.ShouldBe(MessageKinds.Event);
	}

	[Fact]
	public void Implement_IGrantAdded()
	{
		// Act
		var grantAdded = new GrantAdded("user", "name", "app", "tenant", "type", "qual", null, "admin", DateTimeOffset.UtcNow);

		// Assert
		grantAdded.ShouldBeAssignableTo<IGrantAdded>();
	}
}

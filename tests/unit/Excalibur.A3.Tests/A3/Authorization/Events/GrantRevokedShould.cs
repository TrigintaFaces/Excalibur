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
	public void Inherit_from_DomainEventBase()
	{
		// Act
		var grantRevoked = new GrantRevoked("user", "name", "app", "tenant", "type", "qual", null, "admin", DateTimeOffset.UtcNow);

		// Assert
		grantRevoked.ShouldBeAssignableTo<IDomainEvent>();
		grantRevoked.MessageId.ShouldNotBeNullOrWhiteSpace();
		grantRevoked.Id.ShouldNotBe(Guid.Empty);
		grantRevoked.Kind.ShouldBe(MessageKinds.Event);
	}

	[Fact]
	public void Implement_IGrantRevoked()
	{
		// Act
		var grantRevoked = new GrantRevoked("user", "name", "app", "tenant", "type", "qual", null, "admin", DateTimeOffset.UtcNow);

		// Assert
		grantRevoked.ShouldBeAssignableTo<IGrantRevoked>();
	}
}

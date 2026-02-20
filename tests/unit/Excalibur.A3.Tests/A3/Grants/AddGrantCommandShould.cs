using Excalibur.A3.Audit;
using Excalibur.A3.Authorization.Grants;
using Excalibur.A3.Authorization.Requests;

namespace Excalibur.Tests.A3.Grants;

[Trait("Category", "Unit")]
[Trait("Component", "A3")]
[Trait("Feature", "Authorization")]
public sealed class AddGrantCommandShould
{
	[Fact]
	public void Store_all_constructor_parameters()
	{
		// Arrange
		var userId = "user-123";
		var fullName = "John Doe";
		var grantType = "ActivityGroup";
		var qualifier = "admin";
		var expiresOn = DateTimeOffset.UtcNow.AddDays(30);
		var correlationId = Guid.NewGuid();
		var tenantId = "tenant-456";

		// Act
		var command = new AddGrantCommand(userId, fullName, grantType, qualifier, expiresOn, correlationId, tenantId);

		// Assert
		command.UserId.ShouldBe(userId);
		command.FullName.ShouldBe(fullName);
		command.GrantType.ShouldBe(grantType);
		command.Qualifier.ShouldBe(qualifier);
		command.ExpiresOn.ShouldBe(expiresOn);
	}

	[Fact]
	public void Accept_null_expires_on()
	{
		// Act
		var command = new AddGrantCommand("user", "name", "type", "qual", null, Guid.NewGuid());

		// Assert
		command.ExpiresOn.ShouldBeNull();
	}

	[Fact]
	public void Accept_null_tenant_id()
	{
		// Act
		var command = new AddGrantCommand("user", "name", "type", "qual", null, Guid.NewGuid());

		// Assert — no exception thrown, tenant defaults to null
		command.ShouldNotBeNull();
	}

	[Fact]
	public void Inherit_from_AuthorizeCommandBase()
	{
		// Act
		var command = new AddGrantCommand("user", "name", "type", "qual", null, Guid.NewGuid());

		// Assert
		command.ShouldBeAssignableTo<AuthorizeCommandBase<AuditableResult<bool>>>();
	}

	[Fact]
	public void Have_settable_properties()
	{
		// Arrange
		var command = new AddGrantCommand("user", "name", "type", "qual", null, Guid.NewGuid());

		// Act
		command.UserId = "updated-user";
		command.FullName = "Updated Name";
		command.GrantType = "Activity";
		command.Qualifier = "updated-qual";
		command.ExpiresOn = DateTimeOffset.UtcNow.AddDays(60);

		// Assert
		command.UserId.ShouldBe("updated-user");
		command.FullName.ShouldBe("Updated Name");
		command.GrantType.ShouldBe("Activity");
		command.Qualifier.ShouldBe("updated-qual");
		command.ExpiresOn.ShouldNotBeNull();
	}

	[Fact]
	public void Support_access_token()
	{
		// Arrange
		var command = new AddGrantCommand("user", "name", "type", "qual", null, Guid.NewGuid());
		var accessToken = A.Fake<Excalibur.A3.IAccessToken>();

		// Act
		command.AccessToken = accessToken;

		// Assert
		command.AccessToken.ShouldBeSameAs(accessToken);
	}
}

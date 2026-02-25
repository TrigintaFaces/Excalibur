using Excalibur.A3.Audit;
using Excalibur.A3.Authorization.Grants;
using Excalibur.A3.Authorization.Requests;

namespace Excalibur.Tests.A3.Grants;

[Trait("Category", "Unit")]
[Trait("Component", "A3")]
[Trait("Feature", "Authorization")]
public sealed class RevokeAllGrantsCommandShould
{
	[Fact]
	public void Store_all_constructor_parameters()
	{
		// Arrange
		var userId = "user-123";
		var fullName = "John Doe";
		var correlationId = Guid.NewGuid();
		var tenantId = "tenant-456";

		// Act
		var command = new RevokeAllGrantsCommand(userId, fullName, correlationId, tenantId);

		// Assert
		command.UserId.ShouldBe(userId);
		command.FullName.ShouldBe(fullName);
	}

	[Fact]
	public void Accept_null_tenant_id()
	{
		// Act
		var command = new RevokeAllGrantsCommand("user", "name", Guid.NewGuid());

		// Assert
		command.ShouldNotBeNull();
	}

	[Fact]
	public void Inherit_from_AuthorizeCommandBase()
	{
		// Act
		var command = new RevokeAllGrantsCommand("user", "name", Guid.NewGuid());

		// Assert
		command.ShouldBeAssignableTo<AuthorizeCommandBase<AuditableResult<bool>>>();
	}

	[Fact]
	public void Have_settable_properties()
	{
		// Arrange
		var command = new RevokeAllGrantsCommand("user", "name", Guid.NewGuid());

		// Act
		command.UserId = "updated-user";
		command.FullName = "Updated Name";

		// Assert
		command.UserId.ShouldBe("updated-user");
		command.FullName.ShouldBe("Updated Name");
	}

	[Fact]
	public void Support_access_token()
	{
		// Arrange
		var command = new RevokeAllGrantsCommand("user", "name", Guid.NewGuid());
		var accessToken = A.Fake<Excalibur.A3.IAccessToken>();

		// Act
		command.AccessToken = accessToken;

		// Assert
		command.AccessToken.ShouldBeSameAs(accessToken);
	}
}

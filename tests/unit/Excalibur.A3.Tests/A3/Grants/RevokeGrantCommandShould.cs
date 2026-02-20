using Excalibur.A3.Audit;
using Excalibur.A3.Authorization.Grants;
using Excalibur.A3.Authorization.Requests;

namespace Excalibur.Tests.A3.Grants;

[Trait("Category", "Unit")]
[Trait("Component", "A3")]
[Trait("Feature", "Authorization")]
public sealed class RevokeGrantCommandShould
{
	[Fact]
	public void Store_all_constructor_parameters()
	{
		// Arrange
		var userId = "user-123";
		var grantType = "ActivityGroup";
		var qualifier = "admin";
		var correlationId = Guid.NewGuid();
		var tenantId = "tenant-456";

		// Act
		var command = new RevokeGrantCommand(userId, grantType, qualifier, correlationId, tenantId);

		// Assert
		command.UserId.ShouldBe(userId);
		command.GrantType.ShouldBe(grantType);
		command.Qualifier.ShouldBe(qualifier);
	}

	[Fact]
	public void Accept_null_tenant_id()
	{
		// Act
		var command = new RevokeGrantCommand("user", "type", "qual", Guid.NewGuid());

		// Assert
		command.ShouldNotBeNull();
	}

	[Fact]
	public void Inherit_from_AuthorizeCommandBase()
	{
		// Act
		var command = new RevokeGrantCommand("user", "type", "qual", Guid.NewGuid());

		// Assert
		command.ShouldBeAssignableTo<AuthorizeCommandBase<AuditableResult<bool>>>();
	}

	[Fact]
	public void Have_settable_properties()
	{
		// Arrange
		var command = new RevokeGrantCommand("user", "type", "qual", Guid.NewGuid());

		// Act
		command.UserId = "updated-user";
		command.GrantType = "Activity";
		command.Qualifier = "updated-qual";

		// Assert
		command.UserId.ShouldBe("updated-user");
		command.GrantType.ShouldBe("Activity");
		command.Qualifier.ShouldBe("updated-qual");
	}

	[Fact]
	public void Support_access_token()
	{
		// Arrange
		var command = new RevokeGrantCommand("user", "type", "qual", Guid.NewGuid());
		var accessToken = A.Fake<Excalibur.A3.IAccessToken>();

		// Act
		command.AccessToken = accessToken;

		// Assert
		command.AccessToken.ShouldBeSameAs(accessToken);
	}
}

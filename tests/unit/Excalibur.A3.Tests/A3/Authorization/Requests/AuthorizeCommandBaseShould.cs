using Excalibur.A3;
using Excalibur.A3.Authorization.Requests;
using Excalibur.Application.Requests;

namespace Excalibur.Tests.A3.Authorization.Requests;

[Trait("Category", "Unit")]
[Trait("Component", "A3")]
public sealed class AuthorizeCommandBaseShould
{
	[Fact]
	public void Initialize_with_correlation_id_and_tenant_id()
	{
		// Arrange
		var correlationId = Guid.NewGuid();

		// Act
		var command = new TestAuthorizeCommand(correlationId, "tenant-1");

		// Assert
		((IAmCorrelatable)command).CorrelationId.ShouldBe(correlationId);
		command.TenantId.ShouldBe("tenant-1");
	}

	[Fact]
	public void Initialize_with_null_tenant_id()
	{
		// Arrange & Act
		var command = new TestAuthorizeCommand(Guid.NewGuid());

		// Assert
		command.TenantId.ShouldNotBeNull();
	}

	[Fact]
	public void Have_null_access_token_by_default()
	{
		// Arrange & Act
		var command = new TestAuthorizeCommand();

		// Assert
		command.AccessToken.ShouldBeNull();
	}

	[Fact]
	public void Allow_setting_access_token()
	{
		// Arrange
		var command = new TestAuthorizeCommand();
		var token = A.Fake<IAccessToken>();

		// Act
		command.AccessToken = token;

		// Assert
		command.AccessToken.ShouldBeSameAs(token);
	}

	[Fact]
	public void Implement_IAuthorizeCommand()
	{
		// Arrange & Act
		var command = new TestAuthorizeCommand();

		// Assert
		command.ShouldBeAssignableTo<IAuthorizeCommand<string>>();
	}

	[Fact]
	public void Implement_IRequireAuthorization()
	{
		// Arrange & Act
		var command = new TestAuthorizeCommand();

		// Assert
		command.ShouldBeAssignableTo<Excalibur.A3.Authorization.IRequireAuthorization>();
	}

	private sealed class TestAuthorizeCommand : AuthorizeCommandBase<string>
	{
		public TestAuthorizeCommand() { }
		public TestAuthorizeCommand(Guid correlationId, string? tenantId = null)
			: base(correlationId, tenantId) { }
	}
}

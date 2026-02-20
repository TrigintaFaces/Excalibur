using Excalibur.A3;
using Excalibur.A3.Authorization;
using Excalibur.A3.Authorization.Requests;
using Excalibur.Application.Requests;

namespace Excalibur.Tests.A3.Authorization.Requests;

[Trait("Category", "Unit")]
[Trait("Component", "A3")]
public sealed class ResourceCommandBaseShould
{
	[Fact]
	public void Initialize_with_correlation_id_and_resource_id()
	{
		// Arrange
		var correlationId = Guid.NewGuid();

		// Act
		var command = new TestResourceCommand(correlationId, "resource-42");

		// Assert
		((IAmCorrelatable)command).CorrelationId.ShouldBe(correlationId);
		command.ResourceId.ShouldBe("resource-42");
	}

	[Fact]
	public void Initialize_with_tenant_id()
	{
		// Arrange
		var correlationId = Guid.NewGuid();

		// Act
		var command = new TestResourceCommand(correlationId, "res-1", "tenant-1");

		// Assert
		command.TenantId.ShouldBe("tenant-1");
	}

	[Fact]
	public void Return_resource_type_name()
	{
		// Arrange & Act
		var command = new TestResourceCommand(Guid.NewGuid(), "res-1");

		// Assert
		command.ResourceTypes.ShouldNotBeEmpty();
		command.ResourceTypes.ShouldContain("TestResource");
	}

	[Fact]
	public void Implement_IRequireActivityAuthorization()
	{
		// Arrange & Act
		var command = new TestResourceCommand();

		// Assert
		command.ShouldBeAssignableTo<IRequireActivityAuthorization>();
	}

	[Fact]
	public void Have_null_access_token_by_default()
	{
		// Arrange & Act
		var command = new TestResourceCommand();

		// Assert
		command.AccessToken.ShouldBeNull();
	}

	[Fact]
	public void Allow_setting_access_token()
	{
		// Arrange
		var command = new TestResourceCommand();
		var token = A.Fake<IAccessToken>();

		// Act
		command.AccessToken = token;

		// Assert
		command.AccessToken.ShouldBeSameAs(token);
	}

	private sealed class TestResource;

	private sealed class TestResourceCommand : ResourceCommandBase<TestResource, string>
	{
		public TestResourceCommand() { }
		public TestResourceCommand(Guid correlationId, string resourceId, string? tenantId = null)
			: base(correlationId, resourceId, tenantId) { }
	}
}

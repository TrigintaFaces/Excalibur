using Excalibur.A3;
using Excalibur.A3.Authorization.Requests;
using Excalibur.Application.Requests;

namespace Excalibur.Tests.A3.Authorization.Requests;

[Trait("Category", "Unit")]
[Trait("Component", "A3")]
public sealed class AuthorizeQueryShould
{
	[Fact]
	public void Initialize_with_correlation_id_and_tenant_id()
	{
		// Arrange
		var correlationId = Guid.NewGuid();

		// Act
		var query = new TestAuthorizeQuery(correlationId, "tenant-1");

		// Assert
		((IAmCorrelatable)query).CorrelationId.ShouldBe(correlationId);
		query.TenantId.ShouldBe("tenant-1");
	}

	[Fact]
	public void Initialize_with_null_tenant_id()
	{
		// Arrange & Act
		var query = new TestAuthorizeQuery(Guid.NewGuid());

		// Assert
		query.TenantId.ShouldNotBeNull();
	}

	[Fact]
	public void Have_null_access_token_by_default()
	{
		// Arrange & Act
		var query = new TestAuthorizeQuery();

		// Assert
		query.AccessToken.ShouldBeNull();
	}

	[Fact]
	public void Allow_setting_access_token()
	{
		// Arrange
		var query = new TestAuthorizeQuery();
		var token = A.Fake<IAccessToken>();

		// Act
		query.AccessToken = token;

		// Assert
		query.AccessToken.ShouldBeSameAs(token);
	}

	[Fact]
	public void Implement_IAuthorizeQuery()
	{
		// Arrange & Act
		var query = new TestAuthorizeQuery();

		// Assert
		query.ShouldBeAssignableTo<IAuthorizeQuery<string>>();
	}

	[Fact]
	public void Implement_IRequireAuthorization()
	{
		// Arrange & Act
		var query = new TestAuthorizeQuery();

		// Assert
		query.ShouldBeAssignableTo<Excalibur.A3.Authorization.IRequireAuthorization>();
	}

	private sealed class TestAuthorizeQuery : AuthorizeQuery<string>
	{
		public TestAuthorizeQuery() { }
		public TestAuthorizeQuery(Guid correlationId, string? tenantId = null)
			: base(correlationId, tenantId) { }
	}
}

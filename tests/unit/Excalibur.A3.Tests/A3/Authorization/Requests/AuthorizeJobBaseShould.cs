using Excalibur.A3;
using Excalibur.A3.Authorization.Requests;

namespace Excalibur.Tests.A3.Authorization.Requests;

[Trait("Category", "Unit")]
[Trait("Component", "A3")]
public sealed class AuthorizeJobBaseShould
{
	[Fact]
	public void Initialize_with_correlation_id_and_tenant_id()
	{
		// Arrange
		var correlationId = Guid.NewGuid();

		// Act
		var job = new TestAuthorizeJob(correlationId, "tenant-1");

		// Assert
		job.CorrelationId.ShouldBe(correlationId);
		job.TenantId.ShouldBe("tenant-1");
	}

	[Fact]
	public void Initialize_with_null_tenant_id()
	{
		// Arrange & Act
		var job = new TestAuthorizeJob(Guid.NewGuid(), null);

		// Assert
		job.TenantId.ShouldBeNull();
	}

	[Fact]
	public void Have_null_access_token_by_default()
	{
		// Arrange & Act
		var job = new TestAuthorizeJob();

		// Assert
		job.AccessToken.ShouldBeNull();
	}

	[Fact]
	public void Allow_setting_access_token()
	{
		// Arrange
		var job = new TestAuthorizeJob();
		var token = A.Fake<IAccessToken>();

		// Act
		job.AccessToken = token;

		// Assert
		job.AccessToken.ShouldBeSameAs(token);
	}

	[Fact]
	public void Implement_IAuthorizeJob()
	{
		// Arrange & Act
		var job = new TestAuthorizeJob();

		// Assert
		job.ShouldBeAssignableTo<IAuthorizeJob>();
	}

	[Fact]
	public void Implement_IRequireAuthorization()
	{
		// Arrange & Act
		var job = new TestAuthorizeJob();

		// Assert
		job.ShouldBeAssignableTo<Excalibur.A3.Authorization.IRequireAuthorization>();
	}

	private sealed class TestAuthorizeJob : AuthorizeJobBase
	{
		public TestAuthorizeJob() { }
		public TestAuthorizeJob(Guid correlationId, string? tenantId)
			: base(correlationId, tenantId) { }
	}
}

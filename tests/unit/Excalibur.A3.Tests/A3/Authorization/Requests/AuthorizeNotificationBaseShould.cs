using Excalibur.A3;
using Excalibur.A3.Authorization.Requests;

namespace Excalibur.Tests.A3.Authorization.Requests;

[Trait("Category", "Unit")]
[Trait("Component", "A3")]
public sealed class AuthorizeNotificationBaseShould
{
	[Fact]
	public void Initialize_with_correlation_id_and_tenant_id()
	{
		// Arrange
		var correlationId = Guid.NewGuid();

		// Act
		var notification = new TestNotification(correlationId, "tenant-1");

		// Assert
		notification.CorrelationId.ShouldBe(correlationId);
		notification.TenantId.ShouldBe("tenant-1");
	}

	[Fact]
	public void Use_default_tenant_id_when_null_is_passed()
	{
		// Arrange & Act
		var notification = new TestNotification(Guid.NewGuid());

		// Assert -- NotificationBase returns "Default" when tenantId is null
		notification.TenantId.ShouldNotBeNull();
	}

	[Fact]
	public void Have_null_access_token_by_default()
	{
		// Arrange & Act
		var notification = new TestNotification();

		// Assert
		notification.AccessToken.ShouldBeNull();
	}

	[Fact]
	public void Allow_setting_access_token()
	{
		// Arrange
		var notification = new TestNotification();
		var token = A.Fake<IAccessToken>();

		// Act
		notification.AccessToken = token;

		// Assert
		notification.AccessToken.ShouldBeSameAs(token);
	}

	[Fact]
	public void Implement_IAuthorizeNotification()
	{
		// Arrange & Act
		var notification = new TestNotification();

		// Assert
		notification.ShouldBeAssignableTo<IAuthorizeNotification>();
	}

	[Fact]
	public void Implement_IRequireAuthorization()
	{
		// Arrange & Act
		var notification = new TestNotification();

		// Assert
		notification.ShouldBeAssignableTo<Excalibur.A3.Authorization.IRequireAuthorization>();
	}

	private sealed class TestNotification : AuthorizeNotificationBase
	{
		public TestNotification() { }
		public TestNotification(Guid correlationId, string? tenantId = null)
			: base(correlationId, tenantId) { }
	}
}

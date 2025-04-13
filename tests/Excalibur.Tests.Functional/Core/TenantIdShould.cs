using Excalibur.Core;

using FakeItEasy;

using Shouldly;

namespace Excalibur.Tests.Functional.Core;

public interface ITenantService
{
	public void SetTenantId(TenantId tenantId);
}

public class TenantIdShould
{
	[Fact]
	public void WorkWithServiceRequiringTenantId()
	{
		// Arrange
		var tenantId = new TenantId("service-tenant");
		var service = new MockService(tenantId);

		// Act
		var result = service.GetTenantId();

		// Assert
		result.ShouldBe("service-tenant");
	}

	[Fact]
	public void HandleNullTenantIdInService()
	{
		// Arrange
		var tenantId = new TenantId(null);
		var service = new MockService(tenantId);

		// Act
		var result = service.GetTenantId();

		// Assert
		result.ShouldBe(string.Empty);
	}

	[Fact]
	public void PassTenantIdToServiceCorrectly()
	{
		// Arrange
		var tenantId = new TenantId("integration-tenant");
		var service = A.Fake<ITenantService>();

		// Act
		service.SetTenantId(tenantId);

		// Assert
		_ = A.CallTo(() => service.SetTenantId(A<TenantId>.That.Matches((TenantId t) => t.Value == "integration-tenant")))
			.MustHaveHappened();
	}

	[Fact]
	public void HandleEmptyTenantIdInServiceIntegration()
	{
		// Arrange
		var tenantId = new TenantId();
		var service = A.Fake<ITenantService>();

		// Act
		service.SetTenantId(tenantId);

		// Assert
		_ = A.CallTo(() => service.SetTenantId(A<TenantId>.That.Matches((TenantId t) => t.Value == string.Empty))).MustHaveHappened();
	}

	[Fact]
	public void UseTenantIdInHttpHeader()
	{
		// Arrange
		var tenantId = new TenantId("tenant-header-test");
		var handler = A.Fake<HttpMessageHandler>();
		using var client = new HttpClient(handler);
		using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.example.com/resource");
		request.Headers.Add("excalibur-tenant-id", tenantId.Value);

		// Act
		var tenantIdHeader = request.Headers.GetValues("excalibur-tenant-id").FirstOrDefault();

		// Assert
		tenantIdHeader.ShouldBe("tenant-header-test");
	}
}

// Mock service to demonstrate functional integration
public class MockService(ITenantId tenantId)
{
	public string GetTenantId()
	{
		return tenantId.Value;
	}
}

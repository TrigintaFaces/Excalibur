using Asp.Versioning;
using Asp.Versioning.ApiExplorer;

using Excalibur.Core;
using Excalibur.Core.Concurrency;
using Excalibur.Hosting.Web;
using Excalibur.Tests.Fixtures;
using Excalibur.Tests.Infrastructure.TestBaseClasses.Host;

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Shouldly;

using Xunit.Abstractions;

namespace Excalibur.Tests.Functional.Hosting.Web;

public class ServiceCollectionExtensionsShould(SqlServerContainerFixture fixture, ITestOutputHelper output)
	: SqlServerHostTestBase(fixture, output)
{
	[Fact]
	public void RegisterAllRequiredServices()
	{
		using var scope = TestHost.Services.CreateScope();
		var provider = scope.ServiceProvider;

		// Assert

		// Exception handling services
		_ = provider.GetService<Microsoft.AspNetCore.Diagnostics.IExceptionHandler>().ShouldNotBeNull();
		_ = provider.GetService<Microsoft.AspNetCore.Mvc.Infrastructure.ProblemDetailsFactory>().ShouldNotBeNull();

		// Core services
		_ = provider.GetService<ITenantId>().ShouldNotBeNull();
		_ = provider.GetService<ICorrelationId>().ShouldNotBeNull();
		_ = provider.GetService<IETag>().ShouldNotBeNull();
		_ = provider.GetService<IClientAddress>().ShouldNotBeNull();

		// API versioning services
		_ = provider.GetService<IApiVersionDescriptionProvider>().ShouldNotBeNull();
	}

	[Fact]
	public void ConfigureApiVersioningCorrectly()
	{
		// Get the API versioning options from the service provider
		var provider = TestHost.Services;
		var options = provider.GetRequiredService<ApiVersioningOptions>();

		// Assert
		options.AssumeDefaultVersionWhenUnspecified.ShouldBeTrue();
		options.DefaultApiVersion.MajorVersion.ShouldBe(1);
		options.DefaultApiVersion.MinorVersion.ShouldBe(0);
		options.ReportApiVersions.ShouldBeTrue();
	}

	[Fact]
	public void CreateScopedInstancesOfContextualServices()
	{
		// Get services from two different scopes
		using var scope1 = TestHost.Services.CreateScope();
		using var scope2 = TestHost.Services.CreateScope();

		// Get the services from each scope
		var tenantId1 = scope1.ServiceProvider.GetRequiredService<ITenantId>();
		var tenantId2 = scope2.ServiceProvider.GetRequiredService<ITenantId>();

		var correlationId1 = scope1.ServiceProvider.GetRequiredService<ICorrelationId>();
		var correlationId2 = scope2.ServiceProvider.GetRequiredService<ICorrelationId>();

		var eTag1 = scope1.ServiceProvider.GetRequiredService<IETag>();
		var eTag2 = scope2.ServiceProvider.GetRequiredService<IETag>();

		var clientAddress1 = scope1.ServiceProvider.GetRequiredService<IClientAddress>();
		var clientAddress2 = scope2.ServiceProvider.GetRequiredService<IClientAddress>();

		// Assert - Different instances should be created
		tenantId1.ShouldNotBeSameAs(tenantId2);
		correlationId1.ShouldNotBeSameAs(correlationId2);
		eTag1.ShouldNotBeSameAs(eTag2);
		clientAddress1.ShouldNotBeSameAs(clientAddress2);
	}

	[Fact]
	public void ConfigureTenantIdService()
	{
		// Arrange
		using var scope = TestHost.Services.CreateScope();
		var tenantId = scope.ServiceProvider.GetRequiredService<ITenantId>();

		// Act
		tenantId.Value = "test-tenant";

		// Assert
		tenantId.Value.ShouldBe("test-tenant");
	}

	[Fact]
	public void ConfigureCorrelationIdService()
	{
		// Arrange
		using var scope = TestHost.Services.CreateScope();
		var correlationId = scope.ServiceProvider.GetRequiredService<ICorrelationId>();
		var guid = Guid.NewGuid();

		// Act
		correlationId.Value = guid;

		// Assert
		correlationId.Value.ShouldBe(guid);
	}

	[Fact]
	public void ConfigureETagService()
	{
		// Arrange
		using var scope = TestHost.Services.CreateScope();
		var eTag = scope.ServiceProvider.GetRequiredService<IETag>();

		// Act
		eTag.IncomingValue = "\"test-etag-in\"";
		eTag.OutgoingValue = "\"test-etag-out\"";

		// Assert
		eTag.IncomingValue.ShouldBe("\"test-etag-in\"");
		eTag.OutgoingValue.ShouldBe("\"test-etag-out\"");
	}

	[Fact]
	public void ConfigureClientAddressService()
	{
		// Arrange
		using var scope = TestHost.Services.CreateScope();
		var clientAddress = scope.ServiceProvider.GetRequiredService<IClientAddress>();

		// Act
		clientAddress.Value = "192.168.1.1";

		// Assert
		clientAddress.Value.ShouldBe("192.168.1.1");
	}

	[Fact]
	public void ThrowArgumentNullExceptionWhenServicesIsNull()
	{
		// Arrange
		IServiceCollection services = null;
		var configuration = TestHost.Services.GetRequiredService<IConfiguration>();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
				services.AddExcaliburWebServices(configuration, typeof(ServiceCollectionExtensionsShould).Assembly))
			.ParamName.ShouldBe("services");
	}

	[Fact]
	public void ThrowArgumentNullExceptionWhenConfigurationIsNull()
	{
		// Arrange
		var services = new ServiceCollection();
		IConfiguration configuration = null;

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
				services.AddExcaliburWebServices(configuration, typeof(ServiceCollectionExtensionsShould).Assembly))
			.ParamName.ShouldBe("configuration");
	}

	protected override void ConfigureHostServices(WebApplicationBuilder builder, IDatabaseContainerFixture fixture)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(fixture);

		base.ConfigureHostServices(builder, fixture);

		// Add Excalibur web services
		_ = builder.Services.AddExcaliburWebServices(builder.Configuration, typeof(ServiceCollectionExtensionsShould).Assembly);

		// Add exception handler
		_ = builder.Services.AddGlobalExceptionHandler();
	}
}

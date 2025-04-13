using Alba;

using Excalibur.Core;
using Excalibur.Core.Exceptions;
using Excalibur.Tests.Fixtures;
using Excalibur.Tests.Infrastructure.TestBaseClasses.Host;
using Excalibur.Tests.Mothers.Core;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

using Shouldly;

using Xunit.Abstractions;

namespace Excalibur.Tests.Functional.Core;

public class ApplicationContextShould : SqlServerHostTestBase, IAsyncDisposable
{
	public ApplicationContextShould(SqlServerContainerFixture fixture, ITestOutputHelper output)
		: base(fixture, output)
	{
		ApplicationContextMother.Initialize(new Dictionary<string, string?>
		{
			{ "ApplicationName", "FunctionalTest" },
			{ "ApplicationSystemName", "functional-test" },
			{ "ApplicationDisplayName", "Functional Test App" },
			{ "ServiceAccountName", "functional-service-account" },
			{ "AuthenticationServiceAudience", "functional-audience" },
			{ "AuthenticationServiceEndpoint", "https://%ServiceDomain%/auth" },
			{ "AuthenticationServicePublicKeyPath", "/keys/public.pem" },
			{ "AuthorizationServiceEndpoint", "https://%ServiceDomain%/authz" },
			{ "ServiceDomain", "api.example.com" }
		});
	}

	public async ValueTask DisposeAsync()
	{
		// Reset after each test
		ApplicationContextMother.Reset();
		await base.DisposeAsync().ConfigureAwait(true);
		GC.SuppressFinalize(this);
	}

	[Fact]
	public async Task ExposeApplicationContextValuesToEndpoints()
	{
		// Act
		var response = await TestHost!.GetAsJson<AppContextResponse>("/api/context-info").ConfigureAwait(true);

		// Assert
		response.AppName.ShouldBe("FunctionalTest");
		response.AppDisplayName.ShouldBe("Functional Test App");
		response.AppSystemName.ShouldBe("functional-test");
		response.ServiceAccount.ShouldBe("functional-service-account");
		response.AuthEndpoint.ShouldBe("https://api.example.com/auth");
	}

	[Fact]
	public void ProvideApplicationContextToInjectedServices()
	{
		// Arrange
		var service = GetRequiredService<AppContextAccessService>();

		// Act
		var endpoints = service.GetServiceEndpoints();

		// Assert
		endpoints.AuthService.ShouldBe("https://api.example.com/auth");
		endpoints.AuthzService.ShouldBe("https://api.example.com/authz");
	}

	[Fact]
	public void ExpandNestedPlaceholdersInServiceConfiguration()
	{
		// Act
		var authUrl = ApplicationContext.AuthenticationServiceEndpoint;
		var authzUrl = ApplicationContext.AuthorizationServiceEndpoint;

		// Assert
		authUrl.ShouldBe("https://api.example.com/auth");
		authzUrl.ShouldBe("https://api.example.com/authz");
	}

	[Fact]
	public void ThrowWhenEnvironmentVariableIsMissing()
	{
		// Arrange
		var config = new Dictionary<string, string?> { { "TestConfig", "Value with %UNDEFINED_ENV_VAR% placeholder" } };

		ApplicationContextMother.Initialize(config);

		// Act & Assert
		var exception = Should.Throw<InvalidConfigurationException>(() =>
		{
			_ = ApplicationContext.Get("TestConfig");
		});

		exception.Message.ShouldContain("UNDEFINED_ENV_VAR");
	}

	[Fact]
	public void ExpandRecursivePlaceholders()
	{
		// Arrange
		var config = new Dictionary<string, string?>
		{
			{ "Level1", "Base" }, { "Level2", "%Level1%_Extended" }, { "Level3", "%Level2%_MoreExtended" }
		};

		ApplicationContextMother.Initialize(config);

		// Act
		var result = ApplicationContext.Get("Level3");

		// Assert
		result.ShouldBe("Base_Extended_MoreExtended");
	}

	protected override void ConfigureHostServices(WebApplicationBuilder builder, IDatabaseContainerFixture fixture)
	{
		ArgumentNullException.ThrowIfNull(builder);

		base.ConfigureHostServices(builder, fixture);

		// Register functional test services
		_ = builder.Services.AddTransient<AppContextAccessService>();
	}

	protected override void ConfigureHostApplication(WebApplication app)
	{
		base.ConfigureHostApplication(app);

		// Add a test endpoint that uses ApplicationContext
		_ = app.MapGet("/api/context-info", (HttpContext httpContext) =>
		{
			var contextInfo = new
			{
				AppName = ApplicationContext.ApplicationName,
				AppDisplayName = ApplicationContext.ApplicationDisplayName,
				AppSystemName = ApplicationContext.ApplicationSystemName,
				ServiceAccount = ApplicationContext.ServiceAccountName,
				AuthEndpoint = ApplicationContext.AuthenticationServiceEndpoint
			};

			return Results.Ok(contextInfo);
		});
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing)
		{
			ApplicationContextMother.Reset();
		}

		base.Dispose(disposing);
	}

	// Helper classes for testing
	private sealed class AppContextResponse
	{
		public string AppName { get; set; } = string.Empty;
		public string AppDisplayName { get; set; } = string.Empty;
		public string AppSystemName { get; set; } = string.Empty;
		public string ServiceAccount { get; set; } = string.Empty;
		public string AuthEndpoint { get; set; } = string.Empty;
	}

	private sealed class AppContextAccessService
	{
		public (string AuthService, string AuthzService) GetServiceEndpoints() => (
			ApplicationContext.AuthenticationServiceEndpoint,
			ApplicationContext.AuthorizationServiceEndpoint
		);
	}
}

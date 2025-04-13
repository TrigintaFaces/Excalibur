using System.Net;
using System.Text.Json;

using Excalibur.Hosting;
using Excalibur.Tests.Fixtures;
using Excalibur.Tests.Infrastructure.TestBaseClasses.Host;

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

using Shouldly;

using Xunit.Abstractions;

namespace Excalibur.Tests.Functional.Hosting;

public sealed class ApplicationBuilderExtensionsShould(SqlServerContainerFixture fixture, ITestOutputHelper output)
	: SqlServerHostTestBase(fixture, output)
{
	[Fact]
	public async Task RespondToReadinessEndpoint()
	{
		// Arrange & Act
		var response = await TestHost.Server.CreateClient().GetAsync("/.well-known/ready").ConfigureAwait(true);

		// Assert
		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		response.Content.Headers.ContentType.MediaType.ShouldBe("application/json");

		var content = await response.Content.ReadAsStringAsync().ConfigureAwait(true);
		var healthReport = JsonSerializer.Deserialize<JsonElement>(content);

		healthReport.GetProperty("status").GetString().ShouldBe("Healthy");
		healthReport.GetProperty("entries").GetProperty("TestHealthCheck").GetProperty("status").GetString().ShouldBe("Healthy");
	}

	[Fact]
	public async Task RespondToLivenessEndpoint()
	{
		// Arrange & Act
		var response = await TestHost.Server.CreateClient().GetAsync("/.well-known/live").ConfigureAwait(true);

		// Assert
		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		response.Content.Headers.ContentType.MediaType.ShouldBe("text/plain");

		var content = await response.Content.ReadAsStringAsync().ConfigureAwait(true);
		content.ShouldBe("pong");
	}

	[Fact]
	public async Task ConfigureHealthCheckUIEndpoint()
	{
		// Arrange & Act
		var response = await TestHost.Server.CreateClient().GetAsync("/healthcheck-ui").ConfigureAwait(true);

		// Assert
		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		response.Content.Headers.ContentType.MediaType.ShouldBe("text/html");

		var content = await response.Content.ReadAsStringAsync().ConfigureAwait(true);
		content.ShouldContain("Health Checks UI");
	}

	[Fact]
	public void ThrowArgumentNullExceptionWhenAppIsNull()
	{
		// Arrange
		IApplicationBuilder app = null;

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
		{
			// Use the null app reference to trigger error
			app.UseExcaliburHealthChecks();
		}).ParamName.ShouldBe("app");
	}

	[Fact]
	public async Task HealthCheckUIShouldStillRenderWhenCustomStylesheetMissing()
	{
		var response = await TestHost.Server.CreateClient().GetAsync("/healthcheck-ui").ConfigureAwait(true);
		response.StatusCode.ShouldBe(HttpStatusCode.OK);
	}

	/// <inheritdoc />
	protected override void ConfigureHostServices(WebApplicationBuilder builder, IDatabaseContainerFixture fixture)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(fixture);

		base.ConfigureHostServices(builder, fixture);

		_ = builder.Services.AddHealthChecks()
			.AddCheck("TestHealthCheck", () => HealthCheckResult.Healthy("Test health check is healthy"));

		_ = builder.Services.AddHealthChecksUI(setupSettings =>
			{
				setupSettings.SetEvaluationTimeInSeconds(10);
				setupSettings.MaximumHistoryEntriesPerEndpoint(50);
			})
			.AddInMemoryStorage();
	}

	/// <inheritdoc />
	protected override void ConfigureHostApplication(WebApplication app)
	{
		// Use ArgumentNullException.ThrowIfNull for parameter
		ArgumentNullException.ThrowIfNull(app);

		// Call base implementation
		base.ConfigureHostApplication(app);

		// Configure Excalibur health checks
		_ = app.UseExcaliburHealthChecks();
	}
}

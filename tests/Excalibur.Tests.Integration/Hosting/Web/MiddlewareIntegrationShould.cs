using System.Net;
using System.Text.Json;

using Excalibur.Core;
using Excalibur.Core.Concurrency;
using Excalibur.Hosting.Web;
using Excalibur.Tests.Fixtures;
using Excalibur.Tests.Infrastructure.TestBaseClasses.Host;
using Excalibur.Tests.Integration.Core;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Net.Http.Headers;

using Shouldly;

using Xunit.Abstractions;

namespace Excalibur.Tests.Integration.Hosting.Web;

public class MiddlewareIntegrationShould(SqlServerContainerFixture fixture, ITestOutputHelper output)
	: SqlServerHostTestBase(fixture, output)
{
	[Fact]
	public async Task PropagateCorrelationIdFromRequestHeader()
	{
		var client = TestHost.Server.CreateClient();
		var correlationId = Guid.NewGuid();
		client.DefaultRequestHeaders.Add(ExcaliburHeaderNames.CorrelationId, correlationId.ToString());

		var response = await client.GetAsync("/api/test").ConfigureAwait(true);

		response.StatusCode.ShouldBe(HttpStatusCode.OK);

		var content = await response.Content.ReadAsStringAsync().ConfigureAwait(true);
		var result = JsonSerializer.Deserialize<TestResponse>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

		result.ShouldNotBeNull();
		result!.CorrelationId.ShouldBe(correlationId.ToString());
	}

	[Fact]
	public async Task GenerateNewCorrelationIdWhenNotProvidedInRequest()
	{
		var client = TestHost.Server.CreateClient();

		var response = await client.GetAsync("/api/test").ConfigureAwait(true);

		response.StatusCode.ShouldBe(HttpStatusCode.OK);

		var content = await response.Content.ReadAsStringAsync().ConfigureAwait(true);
		var result = JsonSerializer.Deserialize<TestResponse>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

		result.ShouldNotBeNull();
		Guid.TryParse(result!.CorrelationId, out var parsedGuid).ShouldBeTrue();
		parsedGuid.ShouldNotBe(Guid.Empty);
	}

	[Fact]
	public async Task ExtractTenantIdFromRouteParameters()
	{
		var client = TestHost.Server.CreateClient();
		var tenantId = "tenant-xyz";

		var response = await client.GetAsync($"/api/tenants/{tenantId}/resources").ConfigureAwait(true);

		response.StatusCode.ShouldBe(HttpStatusCode.OK);

		var content = await response.Content.ReadAsStringAsync().ConfigureAwait(true);
		var result = JsonSerializer.Deserialize<TestTenantResponse>(content,
			new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

		result.ShouldNotBeNull();
		result!.TenantId.ShouldBe(tenantId);
	}

	[Fact]
	public async Task PropagateETagFromRequestHeader()
	{
		var client = TestHost.Server.CreateClient();
		var incomingETag = "\"test-etag-in\"";
		client.DefaultRequestHeaders.Add(HeaderNames.IfMatch, incomingETag);

		var response = await client.GetAsync("/api/test").ConfigureAwait(true);

		response.StatusCode.ShouldBe(HttpStatusCode.OK);

		var content = await response.Content.ReadAsStringAsync().ConfigureAwait(true);
		var result = JsonSerializer.Deserialize<TestResponse>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

		result.ShouldNotBeNull();
		result!.IncomingETag.ShouldBe(incomingETag);
	}

	[Fact]
	public async Task AddETagToResponseHeaders()
	{
		var client = TestHost.Server.CreateClient();

		var response = await client.GetAsync("/api/test").ConfigureAwait(true);

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		response.Headers.Contains(HeaderNames.ETag).ShouldBeTrue();
		response.Headers.GetValues(HeaderNames.ETag).First().ShouldBe("\"test-etag-out\"");
	}

	[Fact]
	public async Task CaptureClientIpAddress()
	{
		var client = TestHost.Server.CreateClient();

		var response = await client.GetAsync("/api/test").ConfigureAwait(true);

		response.StatusCode.ShouldBe(HttpStatusCode.OK);

		var content = await response.Content.ReadAsStringAsync().ConfigureAwait(true);
		var result = JsonSerializer.Deserialize<TestResponse>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

		result.ShouldNotBeNull();

		result!.ClientAddress.ShouldNotBeNull();
	}

	[Fact]
	public async Task IntegrateAllMiddlewareComponentsTogether()
	{
		var client = TestHost.Server.CreateClient();
		var correlationId = Guid.NewGuid();
		var incomingETag = "\"combined-test-etag\"";
		var tenantId = "combined-tenant";

		client.DefaultRequestHeaders.Add(ExcaliburHeaderNames.CorrelationId, correlationId.ToString());
		client.DefaultRequestHeaders.Add(HeaderNames.IfMatch, incomingETag);

		var response = await client.GetAsync($"/api/tenants/{tenantId}/resources").ConfigureAwait(true);

		response.StatusCode.ShouldBe(HttpStatusCode.OK);

		var content = await response.Content.ReadAsStringAsync().ConfigureAwait(true);
		var result = JsonSerializer.Deserialize<TestTenantResponse>(content,
			new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

		result.ShouldNotBeNull();
		result!.TenantId.ShouldBe(tenantId);
	}

	protected override void ConfigureHostServices(WebApplicationBuilder builder, IDatabaseContainerFixture fixture)
	{
		ArgumentNullException.ThrowIfNull(builder);

		base.ConfigureHostServices(builder, fixture);

		// Add Excalibur web services
		_ = builder.Services.AddExcaliburWebServices(builder.Configuration, typeof(ServiceCollectionExtensionsShould).Assembly);

		// Add exception handler
		_ = builder.Services.AddGlobalExceptionHandler();
	}

	protected override void ConfigureHostApplication(WebApplication app)
	{
		base.ConfigureHostApplication(app);

		_ = app.Use(async (context, next) =>
		{
			context.Connection.RemoteIpAddress = IPAddress.Parse("127.0.0.1");
			await next().ConfigureAwait(true);
		});

		// Use Excalibur middleware
		_ = app.UseExcaliburWebHost();

		// Add test endpoints to verify middleware functionality
		_ = app.MapGet("/api/test", async (HttpContext context) =>
		{
			var tenantId = context.RequestServices.GetRequiredService<ITenantId>();
			var correlationId = context.RequestServices.GetRequiredService<ICorrelationId>();
			var eTag = context.RequestServices.GetRequiredService<IETag>();
			var clientAddress = context.RequestServices.GetRequiredService<IClientAddress>();

			// Set an outgoing ETag
			eTag.OutgoingValue = "\"test-etag-out\"";

			// Return the middleware-populated values
			await context.Response.WriteAsJsonAsync(new
			{
				TenantId = tenantId.Value,
				CorrelationId = correlationId.Value,
				IncomingETag = eTag.IncomingValue,
				ClientAddress = clientAddress.Value
			}).ConfigureAwait(true);
		});

		// Add an endpoint with tenant ID in route
		_ = app.MapGet("/api/tenants/{tenantId}/resources", (HttpContext context) =>
		{
			var tenantId = context.RequestServices.GetRequiredService<ITenantId>();
			return Results.Ok(new { TenantId = tenantId.Value });
		});
	}

	// Helper DTOs for deserialization
	private sealed class TestResponse
	{
		public string TenantId { get; set; } = string.Empty;
		public string CorrelationId { get; set; } = string.Empty;
		public string IncomingETag { get; set; } = string.Empty;
		public string ClientAddress { get; set; } = string.Empty;
	}

	private sealed class TestTenantResponse
	{
		public string TenantId { get; set; } = string.Empty;
	}
}

using System.Globalization;
using System.Net;

using Excalibur.Core.Exceptions;
using Excalibur.Hosting.Web;
using Excalibur.Tests.Fixtures;
using Excalibur.Tests.Infrastructure.TestBaseClasses.Host;
using Excalibur.Tests.Shared;

using FluentValidation;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

using Shouldly;

using Xunit.Abstractions;

namespace Excalibur.Tests.Functional.Hosting.Web;

public class GlobalExceptionHandlerShould(SqlServerContainerFixture fixture, ITestOutputHelper output)
	: SqlServerHostTestBase(fixture, output)
{
	[Fact]
	public async Task ReturnProblemDetailsForApiException()
	{
		// Arrange
		var client = TestHost.Server.CreateClient();

		// Act
		var response = await client.GetAsync("/api/test/notfound").ConfigureAwait(true);
		var problemDetails = await response.GetProblemDetailsAsync().ConfigureAwait(true);

		// Assert
		response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
		response.Content.Headers.ContentType.MediaType.ShouldBe("application/problem+json");

		problemDetails.ShouldContainProperty("type")
			.GetString()
			.ShouldBe($"https://developer.mozilla.org/en-US/docs/Web/HTTP/Status/404");
		problemDetails.ShouldContainProperty("status").GetInt32().ShouldBe(404);
		problemDetails.ShouldContainProperty("title").GetString().ShouldBe("Not Found");
		problemDetails.ShouldContainProperty("detail").GetString().ShouldBe("Resource not found");
		problemDetails.InstanceShouldStartWith("urn:TestApp:error:");
		problemDetails.ShouldHaveTraceId();
	}

	[Fact]
	public async Task UseFallbackCultureForUnsupportedLocales()
	{
		var originalCulture = CultureInfo.CurrentUICulture;
		CultureInfo.CurrentUICulture = new CultureInfo("tl-PH"); // Tagalog (unsupported)

		try
		{
			var client = TestHost.Server.CreateClient();
			var response = await client.GetAsync("/api/test/notfound").ConfigureAwait(true);
			var problemDetails = await response.GetProblemDetailsAsync().ConfigureAwait(true);

			problemDetails.ShouldContainProperty("type")
				.GetString()
				.ShouldStartWith("https://developer.mozilla.org/en-US/docs/Web/HTTP/Status/");
		}
		finally
		{
			CultureInfo.CurrentUICulture = originalCulture; // restore
		}
	}

	[Fact]
	public async Task IncludeValidationErrorsForValidationException()
	{
		// Arrange
		var client = TestHost.Server.CreateClient();

		// Act
		var response = await client.GetAsync("/api/test/validation").ConfigureAwait(true);
		var problemDetails = await response.GetProblemDetailsAsync().ConfigureAwait(true);

		// Assert
		response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
		problemDetails.ShouldContainProperty("detail").GetString().ShouldBe("Validation failed");
		problemDetails.ShouldHaveValidationErrors();
	}

	[Fact]
	public async Task HandleGenericServerErrors()
	{
		// Arrange
		var client = TestHost.Server.CreateClient();

		// Act
		var response = await client.GetAsync("/api/test/servererror").ConfigureAwait(true);
		var problemDetails = await response.GetProblemDetailsAsync().ConfigureAwait(true);

		// Assert
		response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
		problemDetails.ShouldContainProperty("status").GetInt32().ShouldBe(500);
		problemDetails.ShouldContainProperty("detail").GetString().ShouldBe("Something went wrong");
		problemDetails.ShouldHaveStackTrace();
	}

	[Fact]
	public async Task IncludeErrorCodeWhenAvailable()
	{
		// Arrange
		var client = TestHost.Server.CreateClient();

		// Act
		var response = await client.GetAsync("/api/test/errorcode").ConfigureAwait(true);
		var problemDetails = await response.GetProblemDetailsAsync().ConfigureAwait(true);

		// Assert
		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		problemDetails.ShouldHaveErrorCode("12345");
	}

	[Fact]
	public async Task NotInterfereWithSuccessfulRequests()
	{
		// Arrange
		var client = TestHost.Server.CreateClient();

		// Act
		var response = await client.GetAsync("/api/test/success").ConfigureAwait(true);
		var content = await response.Content.ReadAsStringAsync().ConfigureAwait(true);

		// Assert
		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		content.ShouldBe("\"Success\"");
	}

	protected override void ConfigureHostServices(WebApplicationBuilder builder, IDatabaseContainerFixture fixture)
	{
		ArgumentNullException.ThrowIfNull(builder);

		base.ConfigureHostServices(builder, fixture);

		_ = builder.Services.AddTestHostEnvironment();

		// Add exception handler
		_ = builder.Services.AddGlobalExceptionHandler();

		// Add test controller services
		_ = builder.Services.AddControllers();
		_ = builder.Services.AddEndpointsApiExplorer();
	}

	protected override void ConfigureHostApplication(WebApplication app)
	{
		base.ConfigureHostApplication(app);

		// Add exception handling middleware
		_ = app.UseExceptionHandler();

		// Configure test endpoints
		_ = app.MapGet("/api/test/notfound", () =>
		{
			throw new ApiException(404, "Resource not found", null);
		});

		_ = app.MapGet("/api/test/validation", () =>
		{
			var failures = new List<FluentValidation.Results.ValidationFailure>
			{
				new("Property1", "Property1 is required"), new("Property2", "Property2 must be a valid value")
			};

			throw new ValidationException("Validation failed", failures);
		});

		_ = app.MapGet("/api/test/servererror", () =>
		{
			throw new InvalidOperationException("Something went wrong");
		});

		_ = app.MapGet("/api/test/errorcode", () =>
		{
			var exception = new ApiException(400, "Bad Request", null);
			exception.Data["ErrorCode"] = 12345;
			throw exception;
		});

		_ = app.MapGet("/api/test/success", () => Results.Ok("Success"));

		_ = app.UseRouting();
		_ = app.UseEndpoints(endpoints =>
		{
			_ = endpoints.MapControllers();
		});
	}
}

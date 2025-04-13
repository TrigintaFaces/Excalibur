using Excalibur.Core.Exceptions;
using Excalibur.Hosting.Web.Diagnostics;

using FakeItEasy;

using FluentValidation;
using FluentValidation.Results;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Shouldly;

using ProblemDetailsOptions = Excalibur.Hosting.Web.Diagnostics.ProblemDetailsOptions;

namespace Excalibur.Tests.Unit.Hosting.Web;

public class GlobalExceptionHandlerShould
{
	private readonly IHostEnvironment _mockEnvironment;
	private readonly ILogger<GlobalExceptionHandler> _mockLogger;
	private readonly IOptions<ProblemDetailsOptions> _mockOptions;
	private readonly GlobalExceptionHandler _handler;

	public GlobalExceptionHandlerShould()
	{
		_mockEnvironment = A.Fake<IHostEnvironment>();
		_mockLogger = A.Fake<ILogger<GlobalExceptionHandler>>();
		_mockOptions = A.Fake<IOptions<ProblemDetailsOptions>>();
		_ = A.CallTo(() => _mockOptions.Value).Returns(new ProblemDetailsOptions { StatusTypeBaseUrl = "https://developer.mozilla.org" });

		_ = A.CallTo(() => _mockEnvironment.ApplicationName).Returns("TestApp");

		_handler = new GlobalExceptionHandler(_mockEnvironment, _mockOptions, _mockLogger);
	}

	[Fact]
	public void ThrowArgumentNullExceptionWhenEnvIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
				new GlobalExceptionHandler(null, _mockOptions, _mockLogger))
			.ParamName.ShouldBe("env");
	}

	[Fact]
	public void ThrowArgumentNullExceptionWhenLoggerIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
				new GlobalExceptionHandler(_mockEnvironment, _mockOptions, null))
			.ParamName.ShouldBe("logger");
	}

	[Fact]
	public async Task ThrowArgumentNullExceptionWhenHttpContextIsNull()
	{
		// Arrange
		HttpContext httpContext = null;
#pragma warning disable CA1303 // Do not pass literals as localized parameters
#pragma warning disable CA2201 // Do not raise reserved exception types
		var exception = new Exception("Test exception");
#pragma warning restore CA2201 // Do not raise reserved exception types
#pragma warning restore CA1303 // Do not pass literals as localized parameters

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(async () =>
			await _handler.TryHandleAsync(httpContext, exception, CancellationToken.None).ConfigureAwait(true)).ConfigureAwait(true);
	}

	[Fact]
	public async Task ThrowArgumentNullExceptionWhenExceptionIsNull()
	{
		// Arrange
		var httpContext = new DefaultHttpContext();
		Exception exception = null;

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(async () =>
			await _handler.TryHandleAsync(httpContext, exception, CancellationToken.None).ConfigureAwait(true)).ConfigureAwait(true);
	}

	[Fact]
	public async Task SetCorrectStatusCodeAndContentType()
	{
		// Arrange
		var httpContext = new DefaultHttpContext();
		var services = new ServiceCollection();
		httpContext.RequestServices = services.BuildServiceProvider();

#pragma warning disable CA1303 // Do not pass literals as localized parameters
		var exception = new ApiException(404, "Resource not found", null);
#pragma warning restore CA1303 // Do not pass literals as localized parameters

		// Act
		var result = await _handler.TryHandleAsync(httpContext, exception, CancellationToken.None).ConfigureAwait(true);

		// Assert
		result.ShouldBeTrue();
		httpContext.Response.StatusCode.ShouldBe(404);
		httpContext.Response.ContentType.ShouldBe("application/problem+json");
	}

	[Fact]
	public async Task UseStatusCodeFromException()
	{
		// Arrange
		var httpContext = new DefaultHttpContext();
		httpContext.RequestServices = new ServiceCollection().BuildServiceProvider();

#pragma warning disable CA1303 // Do not pass literals as localized parameters
		var exception = new ApiException(422, "Validation failed", null);
#pragma warning restore CA1303 // Do not pass literals as localized parameters

		// Act
		_ = await _handler.TryHandleAsync(httpContext, exception, CancellationToken.None).ConfigureAwait(true);

		// Assert
		httpContext.Response.StatusCode.ShouldBe(422);
	}

	[Fact]
	public async Task UseDefault500StatusCodeForGenericExceptions()
	{
		// Arrange
		var httpContext = new DefaultHttpContext();
		httpContext.RequestServices = new ServiceCollection().BuildServiceProvider();

#pragma warning disable CA1303 // Do not pass literals as localized parameters
		var exception = new InvalidOperationException("Something went wrong");
#pragma warning restore CA1303 // Do not pass literals as localized parameters

		// Act
		_ = await _handler.TryHandleAsync(httpContext, exception, CancellationToken.None).ConfigureAwait(true);

		// Assert
		httpContext.Response.StatusCode.ShouldBe(500);
	}

	[Fact]
	public async Task AttemptToUseProblemDetailsServiceWhenAvailable()
	{
		// Arrange
		var httpContext = new DefaultHttpContext();
#pragma warning disable CA2201 // Do not raise reserved exception types
		var exception = new Exception("Test exception");
#pragma warning restore CA2201 // Do not raise reserved exception types
		var problemDetailsService = A.Fake<IProblemDetailsService>();

		var serviceProvider = A.Fake<IServiceProvider>();
		_ = A.CallTo(() => serviceProvider.GetService(typeof(IProblemDetailsService)))
			.Returns(problemDetailsService);

		httpContext.RequestServices = serviceProvider;

		_ = A.CallTo(() => problemDetailsService.TryWriteAsync(A<ProblemDetailsContext>._))
			.Returns(new ValueTask<bool>(true));

		// Act
		_ = await _handler.TryHandleAsync(httpContext, exception, CancellationToken.None).ConfigureAwait(true);

		// Assert
		_ = A.CallTo(() => problemDetailsService.TryWriteAsync(A<ProblemDetailsContext>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task FallbackToDirectJsonWritingWhenProblemDetailsServiceIsNotAvailable()
	{
		// Arrange
		var httpContext = new DefaultHttpContext();
		var problemDetailsService = A.Fake<IProblemDetailsService>();
		var services = new ServiceCollection();
		_ = services.AddSingleton(problemDetailsService);
		httpContext.RequestServices = services.BuildServiceProvider();
#pragma warning disable CA1303 // Do not pass literals as localized parameters
#pragma warning disable CA2201 // Do not raise reserved exception types
		var exception = new Exception("Test exception");
#pragma warning restore CA2201 // Do not raise reserved exception types
#pragma warning restore CA1303 // Do not pass literals as localized parameters

		// Mock response body to capture the JSON
		using var memoryStream = new MemoryStream();
		httpContext.Response.Body = memoryStream;

		// Act
		_ = await _handler.TryHandleAsync(httpContext, exception, CancellationToken.None).ConfigureAwait(true);

		// Assert
		memoryStream.Position = 0;
		using var reader = new StreamReader(memoryStream);
		var json = await reader.ReadToEndAsync().ConfigureAwait(true);

		json.ShouldNotBeNullOrWhiteSpace();
	}

	[Fact]
	public async Task IncludeValidationErrorsInProblemDetails()
	{
		// Arrange
		var httpContext = new DefaultHttpContext();
		httpContext.RequestServices = new ServiceCollection().BuildServiceProvider();

		var validationFailures = new List<ValidationFailure>
		{
			new("Property1", "Property1 is required"), new("Property2", "Property2 must be a valid value")
		};

#pragma warning disable CA1303 // Do not pass literals as localized parameters
		var exception = new ValidationException("Validation failed", validationFailures);
#pragma warning restore CA1303 // Do not pass literals as localized parameters

		// Set development environment to include error details
		_ = A.CallTo(() => _mockEnvironment.EnvironmentName).Returns(Environments.Development);

		// Mock response body to capture the JSON
		using var memoryStream = new MemoryStream();
		httpContext.Response.Body = memoryStream;

		// Act
		_ = await _handler.TryHandleAsync(httpContext, exception, CancellationToken.None).ConfigureAwait(true);

		// Assert
		memoryStream.Position = 0;
		using var reader = new StreamReader(memoryStream);
		var json = await reader.ReadToEndAsync().ConfigureAwait(true);

		json.ShouldContain("ValidationErrors");
		json.ShouldContain("Property1");
		json.ShouldContain("Property2");
	}

	[Fact]
	public async Task SanitizeErrorDetailsInProductionForServerErrors()
	{
		// Arrange
		var httpContext = new DefaultHttpContext();
		httpContext.RequestServices = new ServiceCollection().BuildServiceProvider();

#pragma warning disable CA1303 // Do not pass literals as localized parameters
#pragma warning disable CA2201 // Do not raise reserved exception types
		var exception = new Exception("Internal server error with sensitive details");
#pragma warning restore CA2201 // Do not raise reserved exception types
#pragma warning restore CA1303 // Do not pass literals as localized parameters

		// Set production environment
		_ = A.CallTo(() => _mockEnvironment.EnvironmentName).Returns(Environments.Production);

		// Mock response body to capture the JSON
		using var memoryStream = new MemoryStream();
		httpContext.Response.Body = memoryStream;

		// Act
		_ = await _handler.TryHandleAsync(httpContext, exception, CancellationToken.None).ConfigureAwait(true);

		// Assert
		memoryStream.Position = 0;
		using var reader = new StreamReader(memoryStream);
		var json = await reader.ReadToEndAsync().ConfigureAwait(true);

		json.ShouldContain("An unhandled exception has occurred");
		json.ShouldContain("Oops, something went wrong");
		json.ShouldNotContain("Internal server error with sensitive details");
	}

	[Fact]
	public async Task IncludeExceptionDetailsInDevelopmentEnvironment()
	{
		// Arrange
		var httpContext = new DefaultHttpContext();
		httpContext.RequestServices = new ServiceCollection().BuildServiceProvider();

#pragma warning disable CA1303 // Do not pass literals as localized parameters
#pragma warning disable CA2201 // Do not raise reserved exception types
		var exception = new Exception("Test exception details");
#pragma warning restore CA2201 // Do not raise reserved exception types
#pragma warning restore CA1303 // Do not pass literals as localized parameters

		// Set development environment
		_ = A.CallTo(() => _mockEnvironment.EnvironmentName).Returns(Environments.Development);

		// Mock response body to capture the JSON
		using var memoryStream = new MemoryStream();
		httpContext.Response.Body = memoryStream;

		// Act
		_ = await _handler.TryHandleAsync(httpContext, exception, CancellationToken.None).ConfigureAwait(true);

		// Assert
		memoryStream.Position = 0;
		using var reader = new StreamReader(memoryStream);
		var json = await reader.ReadToEndAsync().ConfigureAwait(true);

		json.ShouldContain("Test exception details");
		json.ShouldContain("Stack");
	}

	[Fact]
	public async Task IncludeErrorCodeWhenAvailable()
	{
		// Arrange
		var httpContext = new DefaultHttpContext();
		var services = new ServiceCollection();
		httpContext.RequestServices = services.BuildServiceProvider();

#pragma warning disable CA1303 // Do not pass literals as localized parameters
#pragma warning disable CA2201 // Do not raise reserved exception types
		var exception = new Exception("Test exception");
#pragma warning restore CA2201 // Do not raise reserved exception types
#pragma warning restore CA1303 // Do not pass literals as localized parameters
		exception.Data["ErrorCode"] = 12345;

		// Set development environment to include error details
		_ = A.CallTo(() => _mockEnvironment.EnvironmentName).Returns(Environments.Development);

		// Mock response body to capture the JSON
		using var memoryStream = new MemoryStream();
		httpContext.Response.Body = memoryStream;

		// Act
		_ = await _handler.TryHandleAsync(httpContext, exception, CancellationToken.None).ConfigureAwait(true);

		// Assert
		memoryStream.Position = 0;
		using var reader = new StreamReader(memoryStream);
		var json = await reader.ReadToEndAsync().ConfigureAwait(true);

		json.ShouldContain("\"ErrorCode\":12345");
	}
}

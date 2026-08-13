// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.Json;

using Excalibur.Hosting.Web.Diagnostics;

using Microsoft.AspNetCore.Http;

using ExcaliburProblemDetailsOptions = Excalibur.Hosting.Web.Diagnostics.ProblemDetailsOptions;
using DispatchValidationException = Excalibur.Dispatch.Exceptions.ValidationException;
using FluentValidationException = FluentValidation.ValidationException;
using FluentValidationFailure = FluentValidation.Results.ValidationFailure;

namespace Excalibur.Hosting.Tests.Web;

[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class GlobalExceptionHandlerShould : UnitTestBase
{
	[Fact]
	public void ThrowWhenEnvIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			new GlobalExceptionHandler(null!, Microsoft.Extensions.Options.Options.Create(new ExcaliburProblemDetailsOptions()), NullLogger<GlobalExceptionHandler>.Instance));
	}

	[Fact]
	public void ThrowWhenOptionsIsNull()
	{
		// Arrange
		var env = A.Fake<IHostEnvironment>();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			new GlobalExceptionHandler(env, null!, NullLogger<GlobalExceptionHandler>.Instance));
	}

	[Fact]
	public void ThrowWhenLoggerIsNull()
	{
		// Arrange
		var env = A.Fake<IHostEnvironment>();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			new GlobalExceptionHandler(env, Microsoft.Extensions.Options.Options.Create(new ExcaliburProblemDetailsOptions()), null!));
	}

	[Fact]
	public void ConstructSuccessfully()
	{
		// Arrange
		var env = A.Fake<IHostEnvironment>();
		var options = Microsoft.Extensions.Options.Options.Create(new ExcaliburProblemDetailsOptions());

		// Act
		var handler = new GlobalExceptionHandler(env, options, NullLogger<GlobalExceptionHandler>.Instance);

		// Assert
		handler.ShouldNotBeNull();
	}

	[Fact]
	public async Task HandleExceptionAndReturnTrue()
	{
		// Arrange
		var env = A.Fake<IHostEnvironment>();
		A.CallTo(() => env.EnvironmentName).Returns("Production");
		A.CallTo(() => env.ApplicationName).Returns("TestApp");
		var options = Microsoft.Extensions.Options.Options.Create(new ExcaliburProblemDetailsOptions());
		var handler = new GlobalExceptionHandler(env, options, NullLogger<GlobalExceptionHandler>.Instance);

		// Build a minimal service provider so DefaultHttpContext.RequestServices works
		var services = new ServiceCollection();
		var serviceProvider = services.BuildServiceProvider();

		var httpContext = new DefaultHttpContext
		{
			RequestServices = serviceProvider,
		};
		httpContext.Response.Body = new MemoryStream();
		var exception = new InvalidOperationException("Test error");

		// Act
		var result = await handler.TryHandleAsync(httpContext, exception, CancellationToken.None);

		// Assert
		result.ShouldBeTrue();
		httpContext.Response.StatusCode.ShouldBe(StatusCodes.Status500InternalServerError);
		httpContext.Response.ContentType.ShouldBe("application/problem+json");
	}

	[Fact]
	public async Task ThrowWhenHttpContextIsNull()
	{
		// Arrange
		var env = A.Fake<IHostEnvironment>();
		var options = Microsoft.Extensions.Options.Options.Create(new ExcaliburProblemDetailsOptions());
		var handler = new GlobalExceptionHandler(env, options, NullLogger<GlobalExceptionHandler>.Instance);

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(() =>
			handler.TryHandleAsync(null!, new InvalidOperationException("test"), CancellationToken.None).AsTask());
	}

	[Fact]
	public async Task ThrowWhenExceptionIsNull()
	{
		// Arrange
		var env = A.Fake<IHostEnvironment>();
		var options = Microsoft.Extensions.Options.Options.Create(new ExcaliburProblemDetailsOptions());
		var handler = new GlobalExceptionHandler(env, options, NullLogger<GlobalExceptionHandler>.Instance);
		var httpContext = new DefaultHttpContext();

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(() =>
			handler.TryHandleAsync(httpContext, null!, CancellationToken.None).AsTask());
	}

	#region DispatchValidationException -> "errors" extension (the branch the ASP.NET-end fix added)

	[Fact]
	public async Task IncludeErrorsExtension_WhenDispatchValidationExceptionHasPopulatedErrors()
	{
		// Arrange
		var handler = CreateHandler(environmentName: Environments.Development);
		var exception = new DispatchValidationException("Validation failed");
		_ = exception.AddError("Name", "Name is required");
		_ = exception.AddError("Email", "Email is invalid");

		// Act
		var (_, body) = await InvokeAsync(handler, exception);
		using var document = JsonDocument.Parse(body);

		// Assert -- the per-property dictionary survives intact, keys and arrays.
		document.RootElement.TryGetProperty("errors", out var errors).ShouldBeTrue();
		errors.GetProperty("Name")[0].GetString().ShouldBe("Name is required");
		errors.GetProperty("Email")[0].GetString().ShouldBe("Email is invalid");
	}

	[Fact]
	public async Task OmitErrorsExtension_WhenDispatchValidationExceptionHasNoErrors()
	{
		// Arrange -- liveness twin of the above: the `.Count > 0` guard must suppress the member
		// entirely for the SAME exception type when it carries no errors, not merely emit an empty one.
		var handler = CreateHandler(environmentName: Environments.Development);
		var exception = new DispatchValidationException("Validation failed"); // ValidationErrors starts empty

		// Act
		var (_, body) = await InvokeAsync(handler, exception);
		using var document = JsonDocument.Parse(body);

		// Assert
		document.RootElement.TryGetProperty("errors", out _).ShouldBeFalse();
	}

	#endregion

	#region Status Code — the consumer-visible payoff of routing through DispatchValidationException

	[Fact]
	public async Task ReturnStatus400_ForDispatchValidationException()
	{
		// Arrange -- before the fix, GetStatusCode() had nothing to report for the DataAnnotations
		// exception type and validation failures surfaced as 500. This is the regression lock.
		var handler = CreateHandler(environmentName: Environments.Development);
		var exception = new DispatchValidationException("Validation failed");
		_ = exception.AddError("Name", "Name is required");

		// Act
		var (statusCode, _) = await InvokeAsync(handler, exception);

		// Assert
		statusCode.ShouldBe(StatusCodes.Status400BadRequest);
	}

	#endregion

	#region FluentValidation Path — must remain reachable (the liveness arm for a collapsed branch)

	[Fact]
	public async Task IncludeValidationErrorsExtension_ForFluentValidationExceptionThrownDirectly()
	{
		// Arrange -- a FluentValidation.ValidationException thrown directly by consumer code, not through
		// the dispatch pipeline. It is projected onto the SAME "errors" member and the same per-property
		// shape as a pipeline failure, so a client reads validation errors identically either way.
		// This is also the liveness arm for the second branch: this exception is not a
		// DispatchValidationException, so if the two branches were ever collapsed into one, nothing at all
		// would be emitted here and this test goes RED.
		var handler = CreateHandler(environmentName: Environments.Development);
		var exception = new FluentValidationException(
			[new FluentValidationFailure("Name", "Name is required")]);

		// Act
		var (_, body) = await InvokeAsync(handler, exception);
		using var document = JsonDocument.Parse(body);

		// Assert
		document.RootElement.TryGetProperty("errors", out var errors).ShouldBeTrue();
		errors.TryGetProperty("Name", out var nameErrors).ShouldBeTrue();
		nameErrors.EnumerateArray().Select(element => element.GetString()).ShouldContain("Name is required");

		// The superseded member name must be gone, so no client is left reading a stale one.
		document.RootElement.TryGetProperty("validationErrors", out _).ShouldBeFalse();
	}

	#endregion

	#region Production Masking — does a 500-only rewrite leak onto a non-500 validation response?

	[Fact]
	public async Task PreserveValidationResponse_InProductionEnvironment()
	{
		// Arrange -- the `!IsDevelopment() && statusCode >= 500` block rewrites Title/Status/Detail. A
		// validation failure (400) must survive it unchanged in Production.
		var handler = CreateHandler(environmentName: "Production");
		var exception = new DispatchValidationException("Validation failed");
		_ = exception.AddError("Name", "Name is required");

		// Act
		var (statusCode, body) = await InvokeAsync(handler, exception);
		using var document = JsonDocument.Parse(body);

		// Assert
		statusCode.ShouldBe(StatusCodes.Status400BadRequest);
		document.RootElement.GetProperty("status").GetInt32().ShouldBe(400);
		document.RootElement.TryGetProperty("errors", out _).ShouldBeTrue();
	}

	#endregion

	#region Test Helpers

	private static GlobalExceptionHandler CreateHandler(string environmentName)
	{
		var env = A.Fake<IHostEnvironment>();
		_ = A.CallTo(() => env.EnvironmentName).Returns(environmentName);
		_ = A.CallTo(() => env.ApplicationName).Returns("TestApp");
		var options = Microsoft.Extensions.Options.Options.Create(new ExcaliburProblemDetailsOptions());
		return new GlobalExceptionHandler(env, options, NullLogger<GlobalExceptionHandler>.Instance);
	}

	/// <summary>
	/// Invokes the handler and captures the response status code and the raw JSON body actually written
	/// -- the observable behavior, not the private <c>BuildProblemDetails</c> return value.
	/// </summary>
	private static async Task<(int StatusCode, string Body)> InvokeAsync(GlobalExceptionHandler handler, Exception exception)
	{
		var services = new ServiceCollection();
		var serviceProvider = services.BuildServiceProvider();
		var httpContext = new DefaultHttpContext { RequestServices = serviceProvider };
		httpContext.Response.Body = new MemoryStream();

		_ = await handler.TryHandleAsync(httpContext, exception, CancellationToken.None);

		httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
		using var reader = new StreamReader(httpContext.Response.Body, leaveOpen: true);
		var body = await reader.ReadToEndAsync();
		return (httpContext.Response.StatusCode, body);
	}

	#endregion
}

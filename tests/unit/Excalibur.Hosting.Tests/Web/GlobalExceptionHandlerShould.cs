// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Text.Json;
using System.Diagnostics;
using System.Globalization;

using Excalibur.Hosting.Web.Diagnostics;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection.Extensions;

using ExcaliburProblemDetailsOptions = Excalibur.Hosting.Web.Diagnostics.ProblemDetailsOptions;
using DispatchValidationException = Excalibur.Dispatch.Exceptions.ValidationException;
using FluentValidationException = FluentValidation.ValidationException;
using FluentValidationFailure = FluentValidation.Results.ValidationFailure;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace Excalibur.Hosting.Tests.Web;

[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class GlobalExceptionHandlerShould : UnitTestBase
{
	[Fact]
	[Trait("Pattern", "Regression")]
	public async Task Preserve_Demystified_Async_Stacks_In_Development()
	{
		Exception exception;
		try
		{
			await ThrowAsyncDiagnosticException();
			throw new InvalidOperationException("Expected diagnostic exception");
		}
		catch (InvalidOperationException caught)
		{
			exception = caught;
		}
		var (_, body) = await InvokeAsync(CreateHandler(Environments.Development), exception);
		using var document = JsonDocument.Parse(body);
		var stack = document.RootElement.GetProperty("stack").GetString();
		stack.ShouldContain("async Task");
		stack.ShouldContain(nameof(ThrowAsyncDiagnosticException));
		document.RootElement.GetProperty("detail").GetString().ShouldBe("ASYNC-DIAGNOSTIC");
	}

	private static async Task ThrowAsyncDiagnosticException()
	{
		await Task.Yield();
		throw new InvalidOperationException("ASYNC-DIAGNOSTIC");
	}

	[Fact]
	[Trait("Pattern", "Regression")]
	public async Task Leave_Diagnostic_Stacks_Unformatted_And_Private_In_Production()
	{
		var exception = await CaptureAsyncDiagnosticException();
		var original = exception.ToString();
		var (_, body) = await InvokeAsync(CreateHandler(Environments.Production), exception);
		exception.ToString().ShouldBe(original);
		body.ShouldNotContain("ASYNC-DIAGNOSTIC");
		using var json = JsonDocument.Parse(body);
		json.RootElement.TryGetProperty("stack", out _).ShouldBeFalse();
		json.RootElement.TryGetProperty("errorCode", out _).ShouldBeFalse();
	}

	private static async Task<Exception> CaptureAsyncDiagnosticException()
	{
		try { await ThrowAsyncDiagnosticException(); }
		catch (InvalidOperationException exception) { return exception; }
		throw new InvalidOperationException("Expected exception");
	}

	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	[Trait("Pattern", "Regression")]
	public async Task Preserve_Locale_Exception_Identity_And_Trace_Precedence(bool activityPresent)
	{
		var previousCulture = CultureInfo.CurrentUICulture;
		var previousActivity = Activity.Current;
		using var activity = activityPresent ? new Activity("exception-compatibility").Start() : null;
		if (!activityPresent) Activity.Current = null;
		try
		{
			CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("fr-CA");
			var environment = A.Fake<IHostEnvironment>();
			A.CallTo(() => environment.EnvironmentName).Returns(Environments.Production);
			A.CallTo(() => environment.ApplicationName).Returns("Compatibility");
			var options = new ExcaliburProblemDetailsOptions { StatusTypeBaseUrl = "https://status.example" };
			options.SupportedLocales.Add("fr-CA");
			var handler = new GlobalExceptionHandler(environment, Microsoft.Extensions.Options.Options.Create(options), NullLogger<GlobalExceptionHandler>.Instance);
			var exception = new Excalibur.Dispatch.ApiException(503, "PRIVATE", null);
			var (_, body) = await InvokeAsync(handler, exception);
			using var json = JsonDocument.Parse(body);
			json.RootElement.GetProperty("type").GetString().ShouldBe("https://status.example/fr-CA/docs/Web/HTTP/Status/503");
			json.RootElement.GetProperty("instance").GetString().ShouldBe($"urn:Compatibility:error:{exception.Id:D}");
			json.RootElement.GetProperty("traceId").GetString().ShouldBe(activity?.Id ?? "request-trace");
		}
		finally
		{
			CultureInfo.CurrentUICulture = previousCulture;
			Activity.Current = previousActivity;
		}
	}

	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	[Trait("Pattern", "Regression")]
	public async Task Preserve_Problem_Details_Through_Custom_Writer_And_Rejected_Writer_Fallback(bool accepts)
	{
		var writer = new CompatibilityProblemDetailsWriter(accepts);
		var exception = new DispatchValidationException("PRIVATE");
		exception.AddError("Name", "Name is required");
		var (status, body) = await InvokeAsync(CreateHandler(Environments.Production), exception, services =>
		{
			services.AddProblemDetails();
			services.RemoveAll<IProblemDetailsWriter>();
			services.AddSingleton<IProblemDetailsWriter>(writer);
		});
		using var json = JsonDocument.Parse(body);
		status.ShouldBe(400);
		json.RootElement.GetProperty("status").GetInt32().ShouldBe(400);
		json.RootElement.GetProperty("traceId").GetString().ShouldBe(Activity.Current?.Id ?? "request-trace");
		json.RootElement.GetProperty("errors").GetProperty("Name")[0].GetString().ShouldBe("Name is required");
		writer.WriteCount.ShouldBe(accepts ? 1 : 0);
		json.RootElement.TryGetProperty("customWriter", out _).ShouldBe(accepts);
		body.ShouldNotContain("PRIVATE");
	}

	private sealed class CompatibilityProblemDetailsWriter(bool accepts) : IProblemDetailsWriter
	{
		internal int WriteCount { get; private set; }
		public bool CanWrite(ProblemDetailsContext context) => accepts;
		public async ValueTask WriteAsync(ProblemDetailsContext context)
		{
			WriteCount++;
			context.ProblemDetails.Extensions["customWriter"] = true;
			await context.HttpContext.Response.WriteAsJsonAsync(context.ProblemDetails, options: null,
				contentType: "application/problem+json", cancellationToken: context.HttpContext.RequestAborted);
		}
	}

	[Fact]
	[Trait("Pattern", "Regression")]
	public async Task Describe_Actual_Validation_Response_In_Swagger_Schema()
	{
		var options = new Swashbuckle.AspNetCore.SwaggerGen.SwaggerGenOptions();
		options.AddProblemDetailsSchema();
		var schema = options.SchemaGeneratorOptions.CustomTypeMappings[typeof(Microsoft.AspNetCore.Mvc.ProblemDetails)]();
		var exception = new DispatchValidationException("Validation failed");
		exception.AddError("Name", "Name is required");
		var (_, body) = await InvokeAsync(CreateHandler(Environments.Development), exception);
		using var json = JsonDocument.Parse(body);
		foreach (var property in json.RootElement.EnumerateObject())
		{
			schema.Properties.ShouldContainKey(property.Name);
		}
		schema.Properties["errorCode"].Type.ShouldBe(Microsoft.OpenApi.JsonSchemaType.String);
		json.RootElement.GetProperty("errorCode").ValueKind.ShouldBe(JsonValueKind.String);
		var errors = schema.Properties["errors"];
		errors.Type.ShouldBe(Microsoft.OpenApi.JsonSchemaType.Object);
		errors.AdditionalProperties!.Type.ShouldBe(Microsoft.OpenApi.JsonSchemaType.Array);
		errors.AdditionalProperties.Items!.Type.ShouldBe(Microsoft.OpenApi.JsonSchemaType.String);
		schema.Properties.ShouldNotContainKey("exceptionId");
		schema.Properties.ShouldNotContainKey("validationErrors");
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	[Trait("Pattern", "Regression")]
	public async Task Preserve_Registration_Order_Custom_Options_And_Legacy_Logger(bool dispatchFirst)
	{
		var environment = A.Fake<IHostEnvironment>();
		A.CallTo(() => environment.EnvironmentName).Returns(Environments.Production);
		A.CallTo(() => environment.ApplicationName).Returns("Compatibility");
		var logger = new RecordingLoggerProvider();
		var services = new ServiceCollection();
		services.AddSingleton(environment);
		services.AddLogging(logging => logging.AddProvider(logger));
		if (dispatchFirst)
		{
			services.AddDispatchExceptionHandler(options => options.StatusTypeBaseUrl = "https://dispatch.example");
		}
		services.AddGlobalExceptionHandler(options => options.StatusTypeBaseUrl = "https://legacy.example");
		if (!dispatchFirst)
		{
			services.AddDispatchExceptionHandler(options => options.StatusTypeBaseUrl = "https://dispatch.example");
		}
		using var provider = services.BuildServiceProvider();
		var handlers = provider.GetServices<IExceptionHandler>().ToArray();
		handlers.Length.ShouldBe(2);
		var context = new DefaultHttpContext { RequestServices = provider };
		await using var stream = new MemoryStream();
		context.Response.Body = stream;
		(await handlers[0].TryHandleAsync(context, new Excalibur.Dispatch.ApiException(503, "PRIVATE", null), CancellationToken.None)).ShouldBeTrue();
		stream.Position = 0;
		using var reader = new StreamReader(stream, leaveOpen: true);
		using var json = JsonDocument.Parse(await reader.ReadToEndAsync());
		json.RootElement.GetProperty("type").GetString().ShouldStartWith(dispatchFirst ? "https://dispatch.example/" : "https://legacy.example/");
		logger.Records.ShouldContain(record => record.EventId == 162510 && record.Category ==
			(dispatchFirst ? typeof(Excalibur.Dispatch.Hosting.AspNetCore.Diagnostics.GlobalExceptionHandler).FullName : typeof(GlobalExceptionHandler).FullName));
	}

	private sealed class RecordingLoggerProvider : ILoggerProvider
	{
		internal List<(string Category, int EventId)> Records { get; } = [];
		public ILogger CreateLogger(string categoryName) => new RecordingLogger(categoryName, Records);
		public void Dispose() { }

		private sealed class RecordingLogger(string category, List<(string Category, int EventId)> records) : ILogger
		{
			public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
			public bool IsEnabled(LogLevel logLevel) => true;
			public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
				records.Add((category, eventId.Id));
		}
	}
	[Theory]
	[InlineData(400)]
	[InlineData(503)]
	[Trait("Pattern", "Regression")]
	public async Task Preserve_Mapped_Status_And_Hide_Internal_Detail_In_Production(int status)
	{
		var handler = CreateHandler(Environments.Production);
		var exception = new Excalibur.Dispatch.ApiException(status, "PRIVATE-SENTINEL", null);
		var (actualStatus, body) = await InvokeAsync(handler, exception);
		using var document = JsonDocument.Parse(body);
		actualStatus.ShouldBe(status);
		document.RootElement.GetProperty("status").GetInt32().ShouldBe(status);
		body.ShouldNotContain("PRIVATE-SENTINEL");
		document.RootElement.TryGetProperty("stack", out _).ShouldBeFalse();
	}
	[Fact]
	public void ThrowWhenEnvIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			new GlobalExceptionHandler(null!, Microsoft.Extensions.Options.Options.Create(new ExcaliburProblemDetailsOptions()), NullLogger<GlobalExceptionHandler>.Instance))
			.ParamName.ShouldBe("env");
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
		// Production sanitization must preserve public validation errors and the mapped status.
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
	private static async Task<(int StatusCode, string Body)> InvokeAsync(GlobalExceptionHandler handler, Exception exception,
		Action<IServiceCollection>? configure = null)
	{
		var services = new ServiceCollection();
		services.AddLogging();
		configure?.Invoke(services);
		using var serviceProvider = services.BuildServiceProvider();
		var httpContext = new DefaultHttpContext { RequestServices = serviceProvider, TraceIdentifier = "request-trace" };
		httpContext.Response.Body = new MemoryStream();

		_ = await handler.TryHandleAsync(httpContext, exception, CancellationToken.None);

		httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
		using var reader = new StreamReader(httpContext.Response.Body, leaveOpen: true);
		var body = await reader.ReadToEndAsync();
		return (httpContext.Response.StatusCode, body);
	}

	#endregion
}

// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Hosting.Web.Diagnostics;

using Microsoft.AspNetCore.Http;

using ExcaliburProblemDetailsOptions = Excalibur.Hosting.Web.Diagnostics.ProblemDetailsOptions;

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
}

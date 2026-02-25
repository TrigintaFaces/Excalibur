// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Configuration;
using Excalibur.Dispatch.Hosting.AspNetCore;

using Microsoft.AspNetCore.Http;

namespace Excalibur.Dispatch.Hosting.AspNetCore.Tests.Authorization;

/// <summary>
/// Tests for <see cref="DispatchAspNetCoreAuthorizationExtensions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class DispatchAspNetCoreAuthorizationExtensionsShould : UnitTestBase
{
	[Fact]
	public void UseAspNetCoreAuthorization_ThrowWhenBuilderIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			((IDispatchBuilder)null!).UseAspNetCoreAuthorization());
	}

	[Fact]
	public void UseAspNetCoreAuthorization_RegisterMiddleware()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = A.Fake<IDispatchBuilder>();
		A.CallTo(() => builder.Services).Returns(services);
		A.CallTo(() => builder.UseMiddleware<AspNetCoreAuthorizationMiddleware>()).Returns(builder);

		// Act
		var result = builder.UseAspNetCoreAuthorization();

		// Assert
		result.ShouldNotBeNull();
		A.CallTo(() => builder.UseMiddleware<AspNetCoreAuthorizationMiddleware>())
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public void UseAspNetCoreAuthorization_RegisterHttpContextAccessor()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = A.Fake<IDispatchBuilder>();
		A.CallTo(() => builder.Services).Returns(services);
		A.CallTo(() => builder.UseMiddleware<AspNetCoreAuthorizationMiddleware>()).Returns(builder);

		// Act
		builder.UseAspNetCoreAuthorization();

		// Assert
		services.ShouldContain(sd =>
			sd.ServiceType == typeof(IHttpContextAccessor) &&
			sd.ImplementationType == typeof(HttpContextAccessor));
	}

	[Fact]
	public void UseAspNetCoreAuthorization_InvokeConfigureAction()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = A.Fake<IDispatchBuilder>();
		A.CallTo(() => builder.Services).Returns(services);
		A.CallTo(() => builder.UseMiddleware<AspNetCoreAuthorizationMiddleware>()).Returns(builder);
		var configureCalled = false;

		// Act
		builder.UseAspNetCoreAuthorization(options =>
		{
			configureCalled = true;
			options.Enabled = false;
		});

		// Assert
		configureCalled.ShouldBeFalse(); // Configure is deferred, not called immediately
		// But options should be registered
		services.ShouldContain(sd =>
			sd.ServiceType == typeof(Microsoft.Extensions.Options.IConfigureOptions<AspNetCoreAuthorizationOptions>));
	}

	[Fact]
	public void UseAspNetCoreAuthorization_RegisterOptionsValidation()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = A.Fake<IDispatchBuilder>();
		A.CallTo(() => builder.Services).Returns(services);
		A.CallTo(() => builder.UseMiddleware<AspNetCoreAuthorizationMiddleware>()).Returns(builder);

		// Act
		builder.UseAspNetCoreAuthorization();

		// Assert — ValidateOnStart registers IValidateOptions
		services.ShouldContain(sd =>
			sd.ServiceType == typeof(Microsoft.Extensions.Options.IValidateOptions<AspNetCoreAuthorizationOptions>));
	}

	[Fact]
	public void UseAspNetCoreAuthorization_AcceptNullConfigure()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = A.Fake<IDispatchBuilder>();
		A.CallTo(() => builder.Services).Returns(services);
		A.CallTo(() => builder.UseMiddleware<AspNetCoreAuthorizationMiddleware>()).Returns(builder);

		// Act & Assert — should not throw
		var result = builder.UseAspNetCoreAuthorization(null);
		result.ShouldNotBeNull();
	}
}

// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Configuration;
using Excalibur.Dispatch.Hosting.AspNetCore;

using FakeItEasy;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Hosting.Tests.AspNetCore.Authorization;

/// <summary>
/// Unit tests for <see cref="DispatchAspNetCoreAuthorizationExtensions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Authorization")]
public sealed class DispatchAspNetCoreAuthorizationExtensionsShould
{
	[Fact]
	public void ThrowArgumentNullException_WhenBuilderIsNull()
	{
		IDispatchBuilder builder = null!;
		_ = Should.Throw<ArgumentNullException>(() => builder.UseAspNetCoreAuthorization());
	}

	[Fact]
	public void RegisterHttpContextAccessor()
	{
		var services = new ServiceCollection();
		var builder = A.Fake<IDispatchBuilder>();
		_ = A.CallTo(() => builder.Services).Returns(services);
		_ = A.CallTo(() => builder.UseMiddleware<AspNetCoreAuthorizationMiddleware>()).Returns(builder);

		_ = builder.UseAspNetCoreAuthorization();

		services.ShouldContain(sd => sd.ServiceType == typeof(IHttpContextAccessor));
	}

	[Fact]
	public void RegisterOptions_WithDefaultValues()
	{
		var services = new ServiceCollection();
		var builder = A.Fake<IDispatchBuilder>();
		_ = A.CallTo(() => builder.Services).Returns(services);
		_ = A.CallTo(() => builder.UseMiddleware<AspNetCoreAuthorizationMiddleware>()).Returns(builder);

		_ = builder.UseAspNetCoreAuthorization();

		var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<AspNetCoreAuthorizationOptions>>().Value;
		options.Enabled.ShouldBeTrue();
		options.RequireAuthenticatedUser.ShouldBeTrue();
		options.DefaultPolicy.ShouldBeNull();
	}

	[Fact]
	public void RegisterOptions_WithCustomValues()
	{
		var services = new ServiceCollection();
		var builder = A.Fake<IDispatchBuilder>();
		_ = A.CallTo(() => builder.Services).Returns(services);
		_ = A.CallTo(() => builder.UseMiddleware<AspNetCoreAuthorizationMiddleware>()).Returns(builder);

		_ = builder.UseAspNetCoreAuthorization(o =>
		{
			o.Enabled = false;
			o.RequireAuthenticatedUser = false;
			o.DefaultPolicy = "MyPolicy";
		});

		var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<AspNetCoreAuthorizationOptions>>().Value;
		options.Enabled.ShouldBeFalse();
		options.RequireAuthenticatedUser.ShouldBeFalse();
		options.DefaultPolicy.ShouldBe("MyPolicy");
	}

	[Fact]
	public void ReturnBuilder_ForFluentChaining()
	{
		var services = new ServiceCollection();
		var builder = A.Fake<IDispatchBuilder>();
		_ = A.CallTo(() => builder.Services).Returns(services);
		_ = A.CallTo(() => builder.UseMiddleware<AspNetCoreAuthorizationMiddleware>()).Returns(builder);

		var result = builder.UseAspNetCoreAuthorization();

		result.ShouldBe(builder);
	}

	[Fact]
	public void NotRegisterDuplicateHttpContextAccessor()
	{
		var services = new ServiceCollection();
		_ = services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
		var builder = A.Fake<IDispatchBuilder>();
		_ = A.CallTo(() => builder.Services).Returns(services);
		_ = A.CallTo(() => builder.UseMiddleware<AspNetCoreAuthorizationMiddleware>()).Returns(builder);

		_ = builder.UseAspNetCoreAuthorization();

		var count = services.Count(sd => sd.ServiceType == typeof(IHttpContextAccessor));
		count.ShouldBe(1);
	}
}

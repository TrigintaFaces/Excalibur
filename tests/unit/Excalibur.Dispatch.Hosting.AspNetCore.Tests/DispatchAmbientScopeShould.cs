// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;

using Microsoft.AspNetCore.Http;

namespace Excalibur.Dispatch.Hosting.AspNetCore.Tests;

/// <summary>
/// Tests for the ASP.NET Core ambient-scope integration (ADR-335): <c>AddDispatchAmbientScope</c> and the
/// <see cref="IDispatchAmbientScopeAccessor"/> backed by <c>IHttpContextAccessor</c>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class DispatchAmbientScopeShould
{
	[Fact]
	public void RegisterAmbientScopeAccessorAndHttpContextAccessor()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddDispatchAmbientScope();
		using var provider = services.BuildServiceProvider();

		// Assert
		provider.GetService<IHttpContextAccessor>().ShouldNotBeNull();
		provider.GetService<IDispatchAmbientScopeAccessor>().ShouldNotBeNull();
	}

	[Fact]
	public void BeIdempotent_WhenCalledMoreThanOnce()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddDispatchAmbientScope();
		_ = services.AddDispatchAmbientScope();
		using var provider = services.BuildServiceProvider();

		// Assert — a single registration of each contract.
		provider.GetServices<IDispatchAmbientScopeAccessor>().Count().ShouldBe(1);
		provider.GetServices<IHttpContextAccessor>().Count().ShouldBe(1);
	}

	[Fact]
	public void ReturnNull_WhenNoHttpContextIsActive()
	{
		// Arrange
		using var provider = new ServiceCollection().AddDispatchAmbientScope().BuildServiceProvider();
		var accessor = provider.GetRequiredService<IDispatchAmbientScopeAccessor>();

		// Act / Assert — off-request: no ambient scope, so the dispatcher creates a fresh one.
		accessor.CurrentServiceProvider.ShouldBeNull();
	}

	[Fact]
	public void ExposeRequestServices_AsTheAmbientScope_WhenHttpContextIsActive()
	{
		// Arrange
		using var provider = new ServiceCollection().AddDispatchAmbientScope().BuildServiceProvider();
		var accessor = provider.GetRequiredService<IDispatchAmbientScopeAccessor>();
		var httpContextAccessor = provider.GetRequiredService<IHttpContextAccessor>();

		using var requestScopeProvider = new ServiceCollection().BuildServiceProvider();
		httpContextAccessor.HttpContext = new DefaultHttpContext { RequestServices = requestScopeProvider };

		// Act / Assert — the ambient scope IS the active request scope.
		accessor.CurrentServiceProvider.ShouldBeSameAs(requestScopeProvider);

		// And reverts to null once the request ends.
		httpContextAccessor.HttpContext = null;
		accessor.CurrentServiceProvider.ShouldBeNull();
	}
}

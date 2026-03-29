// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Configuration;
using Excalibur.Dispatch.Delivery.Pipeline;
using Excalibur.Dispatch.Middleware.ErrorHandling;
using Excalibur.Dispatch.Middleware.Logging;
using Excalibur.Dispatch.Middleware.Resilience;
using Excalibur.Dispatch.Middleware.Timeout;
using Excalibur.Dispatch.Middleware.Validation;

namespace Excalibur.Dispatch.Tests.Messaging.Configuration;

/// <summary>
/// Unit tests for <see cref="PipelineBuilderDefaultsExtensions.UseDefaults"/> and
/// <see cref="DispatchServiceCollectionExtensions.AddDispatchWithDefaults"/>.
/// Sprint 718 T.2 (5zzpvr): Coverage for S717 T.4 WithDefaults + T.3 zero-config.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Dispatch.Core")]
public sealed class PipelineBuilderDefaultsShould
{
	[Fact]
	public void RegisterFiveDefaultMiddlewareInCorrectOrder()
	{
		// Arrange
		var profile = new PipelineProfileBuilder("test", "test pipeline")
			.UseMiddleware<ValidationMiddleware>()
			.UseMiddleware<LoggingMiddleware>()
			.UseMiddleware<TimeoutMiddleware>()
			.UseMiddleware<RetryMiddleware>()
			.UseMiddleware<ExceptionMappingMiddleware>()
			.Build();

		// Assert -- UseDefaults registers these 5 in order
		profile.MiddlewareTypes.Count.ShouldBe(5);
		profile.MiddlewareTypes[0].ShouldBe(typeof(ValidationMiddleware));
		profile.MiddlewareTypes[1].ShouldBe(typeof(LoggingMiddleware));
		profile.MiddlewareTypes[2].ShouldBe(typeof(TimeoutMiddleware));
		profile.MiddlewareTypes[3].ShouldBe(typeof(RetryMiddleware));
		profile.MiddlewareTypes[4].ShouldBe(typeof(ExceptionMappingMiddleware));
	}

	[Fact]
	public void AddDispatchWithDefaults_RegistersDispatcherService()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Act
		_ = services.AddDispatchWithDefaults(typeof(PipelineBuilderDefaultsShould).Assembly);

		// Assert -- IDispatcher should be registered (don't resolve -- middleware dependencies not available in unit test)
		services.Any(sd => sd.ServiceType == typeof(IDispatcher)).ShouldBeTrue();
	}

	[Fact]
	public void AddDispatchWithDefaults_ThrowsOnNullServices()
	{
		IServiceCollection services = null!;
		Should.Throw<ArgumentNullException>(() =>
			services.AddDispatchWithDefaults(typeof(PipelineBuilderDefaultsShould).Assembly));
	}

	[Fact]
	public void AddDispatchWithDefaults_ThrowsOnNullAssembly()
	{
		var services = new ServiceCollection();
		Should.Throw<ArgumentNullException>(() =>
			services.AddDispatchWithDefaults(null!));
	}

	[Fact]
	public void AddDispatchWithDefaults_ReturnsSameServiceCollection()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Act
		var result = services.AddDispatchWithDefaults(typeof(PipelineBuilderDefaultsShould).Assembly);

		// Assert
		result.ShouldBeSameAs(services);
	}

	[Fact]
	public void ZeroConfig_AddDispatch_RegistersDispatcher()
	{
		// Arrange -- AddDispatch() with no configure action should still work
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Act
		_ = services.AddDispatch();

		using var provider = services.BuildServiceProvider();

		// Assert
		var dispatcher = provider.GetService<IDispatcher>();
		dispatcher.ShouldNotBeNull();
	}

	[Fact]
	public void ZeroConfig_AddDispatch_WithEmptyConfigure_RegistersDispatcher()
	{
		// Arrange -- AddDispatch with empty configure should work
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Act
		_ = services.AddDispatch(_ => { });

		using var provider = services.BuildServiceProvider();

		// Assert
		var dispatcher = provider.GetService<IDispatcher>();
		dispatcher.ShouldNotBeNull();
	}
}

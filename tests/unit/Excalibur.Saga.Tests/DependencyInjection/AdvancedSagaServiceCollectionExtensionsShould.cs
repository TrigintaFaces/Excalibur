// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;

using Excalibur.Saga;
using Excalibur.Saga.Abstractions;
using Excalibur.Saga.Implementation;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Excalibur.Saga.Tests.DependencyInjection;

/// <summary>
/// Unit tests for <see cref="AdvancedSagaServiceCollectionExtensions"/>.
/// Verifies advanced saga service registration behavior.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Saga")]
public sealed class AdvancedSagaServiceCollectionExtensionsShould
{
	#region AddDispatchAdvancedSagas with Configure Action Tests

	[Fact]
	public void ThrowArgumentNullException_WhenServicesIsNull_ForConfigureOverload()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			AdvancedSagaServiceCollectionExtensions.AddDispatchAdvancedSagas(null!, (Action<AdvancedSagaOptions>)(opts => { })));
	}

	[Fact]
	public void RegisterDefaultOptions_WhenNoConfigureProvided()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();

		// Act
		services.AddDispatchAdvancedSagas();
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetService<IOptions<AdvancedSagaOptions>>();
		options.ShouldNotBeNull();
	}

	[Fact]
	public void ApplyConfigureAction_WhenProvided()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();
		var customTimeout = TimeSpan.FromMinutes(15);

		// Act
		services.AddDispatchAdvancedSagas(opts =>
		{
			opts.DefaultTimeout = customTimeout;
		});
		var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<AdvancedSagaOptions>>().Value;

		// Assert
		options.DefaultTimeout.ShouldBe(customTimeout);
	}

	[Fact]
	public void RegisterDefaultRetryPolicy()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();

		// Act
		services.AddDispatchAdvancedSagas();
		var provider = services.BuildServiceProvider();

		// Assert
		var retryPolicy = provider.GetService<ISagaRetryPolicy>();
		retryPolicy.ShouldNotBeNull();
		retryPolicy.ShouldBeOfType<DefaultSagaRetryPolicy>();
	}

	[Fact]
	public void RegisterAdvancedSagaMiddleware()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();
		services.AddSingleton(A.Fake<ISagaOrchestrator>());
		services.AddSingleton(A.Fake<ISagaStateStore>());

		// Act
		services.AddDispatchAdvancedSagas();
		var provider = services.BuildServiceProvider();

		// Assert
		var middleware = provider.GetService<IDispatchMiddleware>();
		middleware.ShouldNotBeNull();
		middleware.ShouldBeOfType<AdvancedSagaMiddleware>();
	}

	[Fact]
	public void ReturnServicesForChaining()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var result = services.AddDispatchAdvancedSagas();

		// Assert
		result.ShouldBe(services);
	}

	[Fact]
	public void NotReplaceExistingRetryPolicy()
	{
		// Arrange
		var services = new ServiceCollection();
		var customPolicy = A.Fake<ISagaRetryPolicy>();
		services.AddSingleton(customPolicy);
		services.AddLogging();

		// Act
		services.AddDispatchAdvancedSagas();
		var provider = services.BuildServiceProvider();

		// Assert - TryAddSingleton should not replace existing
		var policy = provider.GetService<ISagaRetryPolicy>();
		policy.ShouldBe(customPolicy);
	}

	#endregion

	#region AddDispatchAdvancedSagas with Builder Tests

	[Fact]
	public void ThrowArgumentNullException_WhenServicesIsNull_ForBuilderOverload()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			AdvancedSagaServiceCollectionExtensions.AddDispatchAdvancedSagas(null!, (Action<AdvancedSagaBuilder>)(builder => { })));
	}

	[Fact]
	public void ThrowArgumentNullException_WhenConfigureBuilderIsNull()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			services.AddDispatchAdvancedSagas((Action<AdvancedSagaBuilder>)null!));
	}

	[Fact]
	public void InvokeBuilderConfiguration()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();
		var builderInvoked = false;

		// Act
		services.AddDispatchAdvancedSagas((Action<AdvancedSagaBuilder>)(builder =>
		{
			builderInvoked = true;
		}));

		// Assert
		builderInvoked.ShouldBeTrue();
	}

	[Fact]
	public void ApplyBuilderOptionsToConfiguration()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();
		services.AddSingleton(A.Fake<ISagaOrchestrator>());
		services.AddSingleton(A.Fake<ISagaStateStore>());
		var customTimeout = TimeSpan.FromMinutes(20);

		// Act
		services.AddDispatchAdvancedSagas((Action<AdvancedSagaBuilder>)(builder =>
		{
			builder.WithDefaultTimeout(customTimeout);
		}));
		var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<AdvancedSagaOptions>>().Value;

		// Assert
		options.DefaultTimeout.ShouldBe(customTimeout);
	}

	[Fact]
	public void ApplyAllBuilderOptions()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();
		services.AddSingleton(A.Fake<ISagaOrchestrator>());
		services.AddSingleton(A.Fake<ISagaStateStore>());

		// Act
		services.AddDispatchAdvancedSagas((Action<AdvancedSagaBuilder>)(builder =>
		{
			builder
				.WithDefaultTimeout(TimeSpan.FromMinutes(30))
				.WithStepTimeout(TimeSpan.FromMinutes(5))
				.WithMaxRetries(5)
				.WithMaxParallelism(8)
				.WithAutoCompensation(true)
				.WithStatePersistence(true)
				.WithMetrics(true)
				.WithCompletedSagaRetention(TimeSpan.FromDays(7));
		}));
		var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<AdvancedSagaOptions>>().Value;

		// Assert
		options.DefaultTimeout.ShouldBe(TimeSpan.FromMinutes(30));
		options.DefaultStepTimeout.ShouldBe(TimeSpan.FromMinutes(5));
		options.MaxRetryAttempts.ShouldBe(5);
		options.MaxDegreeOfParallelism.ShouldBe(8);
		options.EnableAutoCompensation.ShouldBeTrue();
		options.EnableStatePersistence.ShouldBeTrue();
		options.EnableMetrics.ShouldBeTrue();
		options.CompletedSagaRetention.ShouldBe(TimeSpan.FromDays(7));
	}

	[Fact]
	public void RegisterDefaultServices_WithBuilder()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();
		services.AddSingleton(A.Fake<ISagaOrchestrator>());
		services.AddSingleton(A.Fake<ISagaStateStore>());

		// Act
		services.AddDispatchAdvancedSagas((Action<AdvancedSagaBuilder>)(builder => { }));
		var provider = services.BuildServiceProvider();

		// Assert
		var retryPolicy = provider.GetService<ISagaRetryPolicy>();
		retryPolicy.ShouldNotBeNull();
		retryPolicy.ShouldBeOfType<DefaultSagaRetryPolicy>();

		var middleware = provider.GetService<IDispatchMiddleware>();
		middleware.ShouldNotBeNull();
		middleware.ShouldBeOfType<AdvancedSagaMiddleware>();
	}

	[Fact]
	public void ReturnServicesForChaining_WithBuilder()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var result = services.AddDispatchAdvancedSagas((Action<AdvancedSagaBuilder>)(builder => { }));

		// Assert
		result.ShouldBe(services);
	}

	[Fact]
	public void ProvideServicesCollection_InBuilder()
	{
		// Arrange
		var services = new ServiceCollection();
		IServiceCollection? capturedServices = null;

		// Act
		services.AddDispatchAdvancedSagas((Action<AdvancedSagaBuilder>)(builder =>
		{
			capturedServices = builder.Services;
		}));

		// Assert
		capturedServices.ShouldBe(services);
	}

	#endregion
}

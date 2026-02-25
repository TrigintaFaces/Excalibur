// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Configuration;

using Excalibur.Saga;
using Excalibur.Saga.Abstractions;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Excalibur.Saga.Tests.DependencyInjection;

/// <summary>
/// Unit tests for <see cref="SagasDispatchBuilderExtensions"/>.
/// Verifies saga dispatch builder extension methods.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Saga")]
public sealed class SagasDispatchBuilderExtensionsShould
{
	#region WithAdvancedSagas Builder Overload Tests

	[Fact]
	public void ThrowArgumentNullException_WhenBuilderIsNull_ForBuilderOverload()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			SagasDispatchBuilderExtensions.WithAdvancedSagas(null!, (Action<AdvancedSagaBuilder>?)null));
	}

	[Fact]
	public void RegisterSagaServices_WhenCalledWithNullConfigure()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();
		var dispatchBuilder = A.Fake<IDispatchBuilder>();
		A.CallTo(() => dispatchBuilder.Services).Returns(services);

		// Act
		dispatchBuilder.WithAdvancedSagas((Action<AdvancedSagaBuilder>?)null);
		var provider = services.BuildServiceProvider();

		// Assert
		var retryPolicy = provider.GetService<ISagaRetryPolicy>();
		retryPolicy.ShouldNotBeNull();
	}

	[Fact]
	public void InvokeConfigureAction_WhenProvided()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();
		services.AddSingleton(A.Fake<ISagaOrchestrator>());
		services.AddSingleton(A.Fake<ISagaStateStore>());
		var dispatchBuilder = A.Fake<IDispatchBuilder>();
		A.CallTo(() => dispatchBuilder.Services).Returns(services);
		var configureInvoked = false;

		// Act
		dispatchBuilder.WithAdvancedSagas((Action<AdvancedSagaBuilder>)(builder =>
		{
			configureInvoked = true;
		}));

		// Assert
		configureInvoked.ShouldBeTrue();
	}

	[Fact]
	public void AddMiddlewareToBuilder()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();
		var dispatchBuilder = A.Fake<IDispatchBuilder>();
		A.CallTo(() => dispatchBuilder.Services).Returns(services);

		// Act
		dispatchBuilder.WithAdvancedSagas((Action<AdvancedSagaBuilder>?)null);

		// Assert
		A.CallTo(() => dispatchBuilder.UseMiddleware<AdvancedSagaMiddleware>())
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public void ReturnDispatchBuilder_ForBuilderOverload()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();
		var dispatchBuilder = A.Fake<IDispatchBuilder>();
		A.CallTo(() => dispatchBuilder.Services).Returns(services);

		// Act
		var result = dispatchBuilder.WithAdvancedSagas((Action<AdvancedSagaBuilder>?)null);

		// Assert
		result.ShouldBe(dispatchBuilder);
	}

	[Fact]
	public void ApplyBuilderConfiguration()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();
		services.AddSingleton(A.Fake<ISagaOrchestrator>());
		services.AddSingleton(A.Fake<ISagaStateStore>());
		var dispatchBuilder = A.Fake<IDispatchBuilder>();
		A.CallTo(() => dispatchBuilder.Services).Returns(services);
		var customTimeout = TimeSpan.FromMinutes(45);

		// Act
		dispatchBuilder.WithAdvancedSagas((Action<AdvancedSagaBuilder>)(builder =>
		{
			builder.WithDefaultTimeout(customTimeout);
		}));
		var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<AdvancedSagaOptions>>().Value;

		// Assert
		options.DefaultTimeout.ShouldBe(customTimeout);
	}

	#endregion

	#region WithAdvancedSagas Options Overload Tests

	[Fact]
	public void ThrowArgumentNullException_WhenBuilderIsNull_ForOptionsOverload()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			SagasDispatchBuilderExtensions.WithAdvancedSagas(null!, (Action<AdvancedSagaOptions>)(_ => { })));
	}

	[Fact]
	public void ThrowArgumentNullException_WhenConfigureOptionsIsNull()
	{
		// Arrange
		var dispatchBuilder = A.Fake<IDispatchBuilder>();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			dispatchBuilder.WithAdvancedSagas((Action<AdvancedSagaOptions>)null!));
	}

	[Fact]
	public void ApplyOptionsConfiguration()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();
		services.AddSingleton(A.Fake<ISagaOrchestrator>());
		services.AddSingleton(A.Fake<ISagaStateStore>());
		var dispatchBuilder = A.Fake<IDispatchBuilder>();
		A.CallTo(() => dispatchBuilder.Services).Returns(services);

		// Act
		dispatchBuilder.WithAdvancedSagas((Action<AdvancedSagaOptions>)(opts =>
		{
			opts.MaxRetryAttempts = 10;
			opts.EnableAutoCompensation = true;
		}));
		var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<AdvancedSagaOptions>>().Value;

		// Assert
		options.MaxRetryAttempts.ShouldBe(10);
		options.EnableAutoCompensation.ShouldBeTrue();
	}

	[Fact]
	public void AddMiddlewareToBuilder_ForOptionsOverload()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();
		var dispatchBuilder = A.Fake<IDispatchBuilder>();
		A.CallTo(() => dispatchBuilder.Services).Returns(services);

		// Act
		dispatchBuilder.WithAdvancedSagas((Action<AdvancedSagaOptions>)(_ => { }));

		// Assert
		A.CallTo(() => dispatchBuilder.UseMiddleware<AdvancedSagaMiddleware>())
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public void ReturnDispatchBuilder_ForOptionsOverload()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();
		var dispatchBuilder = A.Fake<IDispatchBuilder>();
		A.CallTo(() => dispatchBuilder.Services).Returns(services);

		// Act
		var result = dispatchBuilder.WithAdvancedSagas((Action<AdvancedSagaOptions>)(_ => { }));

		// Assert
		result.ShouldBe(dispatchBuilder);
	}

	#endregion
}

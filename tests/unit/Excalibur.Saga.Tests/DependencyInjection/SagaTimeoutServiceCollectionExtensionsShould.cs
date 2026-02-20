// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;

using Excalibur.Saga;
using Excalibur.Saga.Abstractions;
using Excalibur.Saga.Services;
using Excalibur.Saga.Storage;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Excalibur.Saga.Tests.DependencyInjection;

/// <summary>
/// Unit tests for <see cref="SagaTimeoutServiceCollectionExtensions"/>.
/// Verifies saga timeout service registration behavior.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Saga")]
public sealed class SagaTimeoutServiceCollectionExtensionsShould
{
	#region AddSagaTimeoutDelivery Parameterless Tests

	[Fact]
	public void RegisterInMemoryTimeoutStore_WhenCalledWithoutParameters()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();
		services.AddSingleton(A.Fake<IDispatcher>());

		// Act
		services.AddSagaTimeoutDelivery();
		var provider = services.BuildServiceProvider();

		// Assert
		var store = provider.GetService<ISagaTimeoutStore>();
		store.ShouldNotBeNull();
		store.ShouldBeOfType<InMemorySagaTimeoutStore>();
	}

	[Fact]
	public void RegisterHostedService_WhenCalledWithoutParameters()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();
		services.AddSingleton(A.Fake<IDispatcher>());

		// Act
		services.AddSagaTimeoutDelivery();
		var provider = services.BuildServiceProvider();

		// Assert
		var hostedServices = provider.GetServices<IHostedService>();
		hostedServices.ShouldContain(s => s is SagaTimeoutDeliveryService);
	}

	[Fact]
	public void ReturnServicesForChaining_WhenCalledWithoutParameters()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var result = services.AddSagaTimeoutDelivery();

		// Assert
		result.ShouldBe(services);
	}

	#endregion

	#region AddSagaTimeoutDelivery with Configure Tests

	[Fact]
	public void ThrowArgumentNullException_WhenServicesIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			SagaTimeoutServiceCollectionExtensions.AddSagaTimeoutDelivery(null!, _ => { }));
	}

	[Fact]
	public void ThrowArgumentNullException_WhenConfigureIsNull()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			services.AddSagaTimeoutDelivery(null!));
	}

	[Fact]
	public void ApplyConfigureAction()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();
		services.AddSingleton(A.Fake<IDispatcher>());
		var configureInvoked = false;

		// Act
		services.AddSagaTimeoutDelivery(opts =>
		{
			configureInvoked = true;
			opts.PollInterval = TimeSpan.FromSeconds(30);
		});

		// Build provider and resolve options to trigger configure action
		var provider = services.BuildServiceProvider();
		_ = provider.GetRequiredService<IOptions<SagaTimeoutOptions>>().Value;

		// Assert
		configureInvoked.ShouldBeTrue();
	}

	[Fact]
	public void ConfigureOptions_WithCustomValues()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();
		services.AddSingleton(A.Fake<IDispatcher>());

		// Act
		services.AddSagaTimeoutDelivery(opts =>
		{
			opts.PollInterval = TimeSpan.FromMinutes(2);
			opts.BatchSize = 50;
		});
		var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<SagaTimeoutOptions>>().Value;

		// Assert
		options.PollInterval.ShouldBe(TimeSpan.FromMinutes(2));
		options.BatchSize.ShouldBe(50);
	}

	[Fact]
	public void NotReplaceExistingTimeoutStore()
	{
		// Arrange
		var services = new ServiceCollection();
		var customStore = A.Fake<ISagaTimeoutStore>();
		services.AddSingleton(customStore);
		services.AddLogging();
		services.AddSingleton(A.Fake<IDispatcher>());

		// Act
		services.AddSagaTimeoutDelivery(opts => { });
		var provider = services.BuildServiceProvider();

		// Assert - TryAddSingleton should not replace existing registration
		var store = provider.GetService<ISagaTimeoutStore>();
		store.ShouldBe(customStore);
	}

	[Fact]
	public void ReturnServicesForChaining_WithConfigure()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var result = services.AddSagaTimeoutDelivery(_ => { });

		// Assert
		result.ShouldBe(services);
	}

	#endregion

	#region AddSagaTimeoutDeliveryService Tests

	[Fact]
	public void ThrowArgumentNullException_WhenServicesIsNull_ForServiceOnly()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			SagaTimeoutServiceCollectionExtensions.AddSagaTimeoutDeliveryService(null!));
	}

	[Fact]
	public void RegisterHostedService_WithoutStore()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();
		services.AddSingleton(A.Fake<IDispatcher>());
		services.AddSingleton(A.Fake<ISagaTimeoutStore>());

		// Act
		services.AddSagaTimeoutDeliveryService();
		var provider = services.BuildServiceProvider();

		// Assert
		var hostedServices = provider.GetServices<IHostedService>();
		hostedServices.ShouldContain(s => s is SagaTimeoutDeliveryService);
	}

	[Fact]
	public void NotRegisterTimeoutStore_WhenCallingServiceOnly()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();

		// Act
		services.AddSagaTimeoutDeliveryService();
		var provider = services.BuildServiceProvider();

		// Assert - No store should be registered by this method
		var store = provider.GetService<ISagaTimeoutStore>();
		store.ShouldBeNull();
	}

	[Fact]
	public void ApplyConfigureAction_WhenProvided()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();
		services.AddSingleton(A.Fake<IDispatcher>());
		services.AddSingleton(A.Fake<ISagaTimeoutStore>());

		// Act
		services.AddSagaTimeoutDeliveryService(opts =>
		{
			opts.PollInterval = TimeSpan.FromSeconds(45);
		});
		var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<SagaTimeoutOptions>>().Value;

		// Assert
		options.PollInterval.ShouldBe(TimeSpan.FromSeconds(45));
	}

	[Fact]
	public void RegisterDefaultOptions_WhenConfigureIsNull()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();
		services.AddSingleton(A.Fake<IDispatcher>());
		services.AddSingleton(A.Fake<ISagaTimeoutStore>());

		// Act
		services.AddSagaTimeoutDeliveryService(configure: null);
		var provider = services.BuildServiceProvider();

		// Assert - Options should be registered with defaults
		var options = provider.GetService<IOptions<SagaTimeoutOptions>>();
		options.ShouldNotBeNull();
	}

	[Fact]
	public void ReturnServicesForChaining_ForServiceOnly()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var result = services.AddSagaTimeoutDeliveryService();

		// Assert
		result.ShouldBe(services);
	}

	#endregion
}

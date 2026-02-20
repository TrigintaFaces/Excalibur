// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Compliance;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Excalibur.Dispatch.Compliance.Tests.Erasure;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class ErasureServiceCollectionExtensionsShould
{
	[Fact]
	public void RegisterGdprErasureServices()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddGdprErasure();

		// Assert
		var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IErasureService));
		descriptor.ShouldNotBeNull();
		descriptor.Lifetime.ShouldBe(ServiceLifetime.Scoped);
	}

	[Fact]
	public void RegisterGdprErasureServicesWithOptions()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddGdprErasure(opts => opts.MaxRetryAttempts = 5);

		// Assert
		services.Any(d => d.ServiceType == typeof(IErasureService)).ShouldBeTrue();
	}

	[Fact]
	public void RegisterInMemoryErasureStore()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddInMemoryErasureStore();

		// Assert
		var provider = services.BuildServiceProvider();
		provider.GetService<IErasureStore>().ShouldNotBeNull();
		provider.GetService<IErasureCertificateStore>().ShouldNotBeNull();
		provider.GetService<IErasureQueryStore>().ShouldNotBeNull();
	}

	[Fact]
	public void RegisterLegalHoldService()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddLegalHoldService();

		// Assert
		var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(ILegalHoldService));
		descriptor.ShouldNotBeNull();
		descriptor.Lifetime.ShouldBe(ServiceLifetime.Scoped);
	}

	[Fact]
	public void RegisterInMemoryLegalHoldStore()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddInMemoryLegalHoldStore();

		// Assert
		var provider = services.BuildServiceProvider();
		provider.GetService<ILegalHoldStore>().ShouldNotBeNull();
		provider.GetService<ILegalHoldQueryStore>().ShouldNotBeNull();
	}

	[Fact]
	public void RegisterDataInventoryService()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddDataInventoryService();

		// Assert
		var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IDataInventoryService));
		descriptor.ShouldNotBeNull();
		descriptor.Lifetime.ShouldBe(ServiceLifetime.Scoped);
	}

	[Fact]
	public void RegisterInMemoryDataInventoryStore()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddInMemoryDataInventoryStore();

		// Assert
		var provider = services.BuildServiceProvider();
		provider.GetService<IDataInventoryStore>().ShouldNotBeNull();
		provider.GetService<IDataInventoryQueryStore>().ShouldNotBeNull();
	}

	[Fact]
	public void RegisterErasureVerificationService()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddErasureVerificationService();

		// Assert
		var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IErasureVerificationService));
		descriptor.ShouldNotBeNull();
		descriptor.Lifetime.ShouldBe(ServiceLifetime.Scoped);
	}

	[Fact]
	public void RegisterErasureScheduler()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddErasureScheduler();

		// Assert
		services.Any(d => d.ServiceType == typeof(ErasureSchedulerBackgroundService)).ShouldBeTrue();
		services.Any(d => d.ServiceType == typeof(IHostedService)).ShouldBeTrue();
	}

	[Fact]
	public void RegisterErasureSchedulerWithOptions()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddErasureScheduler(opts => opts.PollingInterval = TimeSpan.FromMinutes(10));

		// Assert
		services.Any(d => d.ServiceType == typeof(ErasureSchedulerBackgroundService)).ShouldBeTrue();
	}

	[Fact]
	public void RegisterLegalHoldExpiration()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddLegalHoldExpiration();

		// Assert
		services.Any(d => d.ServiceType == typeof(LegalHoldExpirationService)).ShouldBeTrue();
		services.Any(d => d.ServiceType == typeof(IHostedService)).ShouldBeTrue();
	}

	[Fact]
	public void RegisterLegalHoldExpirationWithOptions()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddLegalHoldExpiration(opts => opts.PollingInterval = TimeSpan.FromMinutes(30));

		// Assert
		services.Any(d => d.ServiceType == typeof(LegalHoldExpirationService)).ShouldBeTrue();
	}

	[Fact]
	public void RegisterGdprErasureFromConfiguration()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddGdprErasureFromConfiguration(opts => opts.MaxRetryAttempts = 3);

		// Assert
		services.Any(d => d.ServiceType == typeof(IErasureService)).ShouldBeTrue();
	}

	[Fact]
	public void ThrowWhenConfigureIsNull_AddGdprErasureFromConfiguration()
	{
		var services = new ServiceCollection();
		Should.Throw<ArgumentNullException>(() =>
			services.AddGdprErasureFromConfiguration(null!));
	}
}

// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Tests.Extensions;

/// <summary>
/// Depth tests for <see cref="ServiceRegistrationExtensions"/>.
/// Covers TryAddService variants (Singleton, Scoped, Transient),
/// HasService, RemoveService, ReplaceService, null guards,
/// and idempotency.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class ServiceRegistrationExtensionsShould
{
	[Fact]
	public void TryAddService_ThrowsWhenServicesIsNull()
	{
		IServiceCollection services = null!;

		Should.Throw<ArgumentNullException>(() =>
			services.TryAddService<ITestService, TestServiceImpl>());
	}

	[Fact]
	public void TryAddService_RegistersWithDefaultScopedLifetime()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.TryAddService<ITestService, TestServiceImpl>();

		// Assert
		var descriptor = services.Single(sd => sd.ServiceType == typeof(ITestService));
		descriptor.Lifetime.ShouldBe(ServiceLifetime.Scoped);
		descriptor.ImplementationType.ShouldBe(typeof(TestServiceImpl));
	}

	[Fact]
	public void TryAddService_DoesNotDuplicateRegistration()
	{
		// Arrange
		var services = new ServiceCollection();
		services.TryAddService<ITestService, TestServiceImpl>();

		// Act
		services.TryAddService<ITestService, TestServiceImpl2>();

		// Assert — first registration wins
		services.Count(sd => sd.ServiceType == typeof(ITestService)).ShouldBe(1);
		var descriptor = services.Single(sd => sd.ServiceType == typeof(ITestService));
		descriptor.ImplementationType.ShouldBe(typeof(TestServiceImpl));
	}

	[Fact]
	public void TryAddSingletonService_RegistersAsSingleton()
	{
		var services = new ServiceCollection();
		services.TryAddSingletonService<ITestService, TestServiceImpl>();

		var descriptor = services.Single(sd => sd.ServiceType == typeof(ITestService));
		descriptor.Lifetime.ShouldBe(ServiceLifetime.Singleton);
	}

	[Fact]
	public void TryAddScopedService_RegistersAsScoped()
	{
		var services = new ServiceCollection();
		services.TryAddScopedService<ITestService, TestServiceImpl>();

		var descriptor = services.Single(sd => sd.ServiceType == typeof(ITestService));
		descriptor.Lifetime.ShouldBe(ServiceLifetime.Scoped);
	}

	[Fact]
	public void TryAddTransientService_RegistersAsTransient()
	{
		var services = new ServiceCollection();
		services.TryAddTransientService<ITestService, TestServiceImpl>();

		var descriptor = services.Single(sd => sd.ServiceType == typeof(ITestService));
		descriptor.Lifetime.ShouldBe(ServiceLifetime.Transient);
	}

	[Fact]
	public void HasService_ReturnsTrueWhenRegistered()
	{
		var services = new ServiceCollection();
		services.AddSingleton<ITestService, TestServiceImpl>();

		services.HasService<ITestService>().ShouldBeTrue();
	}

	[Fact]
	public void HasService_ReturnsFalseWhenNotRegistered()
	{
		var services = new ServiceCollection();

		services.HasService<ITestService>().ShouldBeFalse();
	}

	[Fact]
	public void HasService_ThrowsWhenServicesIsNull()
	{
		IServiceCollection services = null!;

		Should.Throw<ArgumentNullException>(() =>
			services.HasService<ITestService>());
	}

	[Fact]
	public void RemoveService_RemovesAllRegistrations()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddSingleton<ITestService, TestServiceImpl>();
		services.AddSingleton<ITestService, TestServiceImpl2>();

		// Act
		services.RemoveService<ITestService>();

		// Assert
		services.HasService<ITestService>().ShouldBeFalse();
	}

	[Fact]
	public void RemoveService_ThrowsWhenServicesIsNull()
	{
		IServiceCollection services = null!;

		Should.Throw<ArgumentNullException>(() =>
			services.RemoveService<ITestService>());
	}

	[Fact]
	public void RemoveService_ReturnsSameServiceCollection()
	{
		var services = new ServiceCollection();
		var result = services.RemoveService<ITestService>();
		result.ShouldBeSameAs(services);
	}

	[Fact]
	public void ReplaceService_ReplacesExistingRegistration()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddSingleton<ITestService, TestServiceImpl>();

		// Act
		services.ReplaceService<ITestService, TestServiceImpl2>();

		// Assert
		var descriptor = services.Single(sd => sd.ServiceType == typeof(ITestService));
		descriptor.ImplementationType.ShouldBe(typeof(TestServiceImpl2));
	}

	[Fact]
	public void ReplaceService_ThrowsWhenServicesIsNull()
	{
		IServiceCollection services = null!;

		Should.Throw<ArgumentNullException>(() =>
			services.ReplaceService<ITestService, TestServiceImpl>());
	}

	[Fact]
	public void ReplaceService_DefaultsToScopedLifetime()
	{
		var services = new ServiceCollection();
		services.AddSingleton<ITestService, TestServiceImpl>();

		services.ReplaceService<ITestService, TestServiceImpl2>();

		var descriptor = services.Single(sd => sd.ServiceType == typeof(ITestService));
		descriptor.Lifetime.ShouldBe(ServiceLifetime.Scoped);
	}

	[Fact]
	public void ReplaceService_AcceptsCustomLifetime()
	{
		var services = new ServiceCollection();
		services.AddScoped<ITestService, TestServiceImpl>();

		services.ReplaceService<ITestService, TestServiceImpl2>(ServiceLifetime.Transient);

		var descriptor = services.Single(sd => sd.ServiceType == typeof(ITestService));
		descriptor.Lifetime.ShouldBe(ServiceLifetime.Transient);
	}

	// Test helpers
	private interface ITestService;
	private sealed class TestServiceImpl : ITestService;
	private sealed class TestServiceImpl2 : ITestService;
}

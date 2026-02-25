// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Excalibur.Saga.Tests;

/// <summary>
/// Unit tests for <see cref="SagaServiceCollectionExtensions"/>.
/// </summary>
[Trait("Category", "Unit")]
public sealed class SagaServiceCollectionExtensionsShould : UnitTestBase
{
	[Fact]
	public void AddExcaliburSaga_WithNullServices_ThrowsArgumentNullException()
	{
		// Arrange
		IServiceCollection services = null!;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => services.AddExcaliburSaga());
	}

	[Fact]
	public void AddExcaliburSaga_RegistersDefaultOptions()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddExcaliburSaga();
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetService<IOptions<SagaOptions>>();
		_ = options.ShouldNotBeNull();
		_ = options.Value.ShouldNotBeNull();
		options.Value.MaxConcurrency.ShouldBe(10);
	}

	[Fact]
	public void AddExcaliburSaga_WithConfigure_ThrowsOnNullServices()
	{
		// Arrange
		IServiceCollection services = null!;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => services.AddExcaliburSaga((Action<SagaOptions>)(_ => { })));
	}

	[Fact]
	public void AddExcaliburSaga_WithConfigure_ThrowsOnNullConfigure()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => services.AddExcaliburSaga((Action<SagaOptions>)null!));
	}

	[Fact]
	public void AddExcaliburSaga_WithConfigure_AppliesConfiguration()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddExcaliburSaga(options =>
		{
			options.MaxConcurrency = 25;
			options.DefaultTimeout = TimeSpan.FromHours(1);
		});
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<IOptions<SagaOptions>>();
		options.Value.MaxConcurrency.ShouldBe(25);
		options.Value.DefaultTimeout.ShouldBe(TimeSpan.FromHours(1));
	}

	[Fact]
	public void HasExcaliburSaga_ReturnsFalse_WhenNotRegistered()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var result = services.HasExcaliburSaga();

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public void HasExcaliburSaga_ReturnsTrue_WhenRegistered()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddExcaliburSaga();

		// Act
		var result = services.HasExcaliburSaga();

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void HasExcaliburSaga_ThrowsOnNullServices()
	{
		// Arrange
		IServiceCollection services = null!;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => services.HasExcaliburSaga());
	}

	[Fact]
	public void AddExcaliburSaga_ReturnsServiceCollection_ForChaining()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var result = services.AddExcaliburSaga();

		// Assert
		result.ShouldBeSameAs(services);
	}

	[Fact]
	public void AddExcaliburSaga_WithConfigure_ReturnsServiceCollection_ForChaining()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var result = services.AddExcaliburSaga((SagaOptions _) => { });

		// Assert
		result.ShouldBeSameAs(services);
	}
}

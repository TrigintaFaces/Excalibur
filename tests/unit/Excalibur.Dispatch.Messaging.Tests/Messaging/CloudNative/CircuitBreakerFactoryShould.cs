// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.CloudNative;
using Excalibur.Dispatch.Options.Resilience;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Tests.Messaging.CloudNative;

/// <summary>
/// Unit tests for <see cref="CircuitBreakerFactory"/>.
/// </summary>
/// <remarks>
/// Tests the circuit breaker factory for creating and managing circuit breakers.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "CloudNative")]
[Trait("Priority", "0")]
public sealed class CircuitBreakerFactoryShould : IAsyncDisposable
{
	private readonly CircuitBreakerFactory _factory;

	public CircuitBreakerFactoryShould()
	{
		_factory = new CircuitBreakerFactory();
	}

	public async ValueTask DisposeAsync()
	{
		await _factory.DisposeAsync();
	}

	#region Constructor Tests

	[Fact]
	public async Task Constructor_Default_CreatesFactory()
	{
		// Arrange & Act
		await using var factory = new CircuitBreakerFactory();

		// Assert
		_ = factory.ShouldNotBeNull();
	}

	[Fact]
	public async Task Constructor_WithDefaultOptions_UsesOptions()
	{
		// Arrange
		var options = new CircuitBreakerOptions
		{
			FailureThreshold = 10,
			OpenDuration = TimeSpan.FromSeconds(60),
		};

		// Act
		await using var factory = new CircuitBreakerFactory(options);

		// Assert
		_ = factory.ShouldNotBeNull();
	}

	[Fact]
	public async Task Constructor_WithLogger_UsesLogger()
	{
		// Arrange
		var logger = NullLogger<CircuitBreakerFactory>.Instance;

		// Act
		await using var factory = new CircuitBreakerFactory(logger: logger);

		// Assert
		_ = factory.ShouldNotBeNull();
	}

	[Fact]
	public async Task Constructor_WithOptionsAndLogger_UsesBoth()
	{
		// Arrange
		var options = new CircuitBreakerOptions();
		var logger = NullLogger<CircuitBreakerFactory>.Instance;

		// Act
		await using var factory = new CircuitBreakerFactory(options, logger);

		// Assert
		_ = factory.ShouldNotBeNull();
	}

	#endregion

	#region GetOrCreate Tests

	[Fact]
	public void GetOrCreate_WithName_ReturnsCircuitBreaker()
	{
		// Arrange & Act
		var breaker = _factory.GetOrCreate("test-breaker");

		// Assert
		_ = breaker.ShouldNotBeNull();
	}

	[Fact]
	public void GetOrCreate_WithNullName_ThrowsArgumentNullException()
	{
		// Arrange & Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => _factory.GetOrCreate(null!));
	}

	[Fact]
	public void GetOrCreate_WithSameName_ReturnsSameInstance()
	{
		// Arrange
		var name = "shared-breaker";

		// Act
		var breaker1 = _factory.GetOrCreate(name);
		var breaker2 = _factory.GetOrCreate(name);

		// Assert
		breaker1.ShouldBeSameAs(breaker2);
	}

	[Fact]
	public void GetOrCreate_WithDifferentNames_ReturnsDifferentInstances()
	{
		// Arrange & Act
		var breaker1 = _factory.GetOrCreate("breaker-1");
		var breaker2 = _factory.GetOrCreate("breaker-2");

		// Assert
		breaker1.ShouldNotBeSameAs(breaker2);
	}

	[Fact]
	public void GetOrCreate_WithOptions_UsesProvidedOptions()
	{
		// Arrange
		var options = new CircuitBreakerOptions
		{
			FailureThreshold = 5,
			OpenDuration = TimeSpan.FromSeconds(30),
		};

		// Act
		var breaker = _factory.GetOrCreate("custom-options-breaker", options);

		// Assert
		_ = breaker.ShouldNotBeNull();
	}

	[Fact]
	public void GetOrCreate_WithNullOptions_UsesDefaultOptions()
	{
		// Arrange & Act
		var breaker = _factory.GetOrCreate("default-options-breaker", null);

		// Assert
		_ = breaker.ShouldNotBeNull();
	}

	#endregion

	#region GetAllMetrics Tests

	[Fact]
	public async Task GetAllMetrics_WithNoBreakers_ReturnsEmptyDictionary()
	{
		// Arrange
		await using var factory = new CircuitBreakerFactory();

		// Act
		var metrics = factory.GetAllMetrics();

		// Assert
		metrics.ShouldBeEmpty();
	}

	[Fact]
	public void GetAllMetrics_WithBreakers_ReturnsAllMetrics()
	{
		// Arrange
		_ = _factory.GetOrCreate("breaker-a");
		_ = _factory.GetOrCreate("breaker-b");
		_ = _factory.GetOrCreate("breaker-c");

		// Act
		var metrics = _factory.GetAllMetrics();

		// Assert
		metrics.Count.ShouldBe(3);
		metrics.ShouldContainKey("breaker-a");
		metrics.ShouldContainKey("breaker-b");
		metrics.ShouldContainKey("breaker-c");
	}

	[Fact]
	public void GetAllMetrics_ReturnsMetricsForEachBreaker()
	{
		// Arrange
		_ = _factory.GetOrCreate("test-breaker");

		// Act
		var metrics = _factory.GetAllMetrics();

		// Assert
		metrics.Count.ShouldBe(1);
		_ = metrics["test-breaker"].ShouldNotBeNull();
	}

	#endregion

	#region Remove Tests

	[Fact]
	public void Remove_WithExistingBreaker_ReturnsTrue()
	{
		// Arrange
		_ = _factory.GetOrCreate("removable-breaker");

		// Act
		var result = _factory.Remove("removable-breaker");

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void Remove_WithNonExistentBreaker_ReturnsFalse()
	{
		// Arrange & Act
		var result = _factory.Remove("non-existent-breaker");

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public void Remove_WithNullName_ThrowsArgumentNullException()
	{
		// Arrange & Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => _factory.Remove(null!));
	}

	[Fact]
	public void Remove_MakesBreakersDisappearFromMetrics()
	{
		// Arrange
		_ = _factory.GetOrCreate("temp-breaker");
		_factory.GetAllMetrics().Count.ShouldBe(1);

		// Act
		_ = _factory.Remove("temp-breaker");

		// Assert
		_factory.GetAllMetrics().Count.ShouldBe(0);
	}

	[Fact]
	public void Remove_AllowsCreatingSameName()
	{
		// Arrange
		var breaker1 = _factory.GetOrCreate("reusable-name");
		_ = _factory.Remove("reusable-name");

		// Act
		var breaker2 = _factory.GetOrCreate("reusable-name");

		// Assert - Should be different instances
		breaker1.ShouldNotBeSameAs(breaker2);
	}

	#endregion

	#region DisposeAsync Tests

	[Fact]
	public async Task DisposeAsync_ClearsAllBreakers()
	{
		// Arrange
		var factory = new CircuitBreakerFactory();
		_ = factory.GetOrCreate("breaker-1");
		_ = factory.GetOrCreate("breaker-2");

		// Act
		await factory.DisposeAsync();

		// Assert
		factory.GetAllMetrics().Count.ShouldBe(0);
	}

	[Fact]
	public async Task DisposeAsync_CanBeCalledMultipleTimes()
	{
		// Arrange
		var factory = new CircuitBreakerFactory();
		_ = factory.GetOrCreate("test");

		// Act & Assert - Should not throw
		await factory.DisposeAsync();
		await factory.DisposeAsync();
	}

	#endregion

	#region Thread Safety Tests

	[Fact]
	public void GetOrCreate_IsConcurrentSafe()
	{
		// Arrange
		var exceptions = new List<Exception>();

		// Act
		_ = Parallel.For(0, 100, i =>
		{
			try
			{
				var breaker = _factory.GetOrCreate($"concurrent-breaker-{i}");
				_ = breaker.ShouldNotBeNull();
			}
			catch (Exception ex)
			{
				lock (exceptions)
				{
					exceptions.Add(ex);
				}
			}
		});

		// Assert
		exceptions.ShouldBeEmpty();
		_factory.GetAllMetrics().Count.ShouldBe(100);
	}

	[Fact]
	public void GetOrCreate_WithSameNameConcurrently_ReturnsSameInstance()
	{
		// Arrange
		var breakers = new CircuitBreakerPattern[100];

		// Act
		_ = Parallel.For(0, 100, i =>
		{
			breakers[i] = _factory.GetOrCreate("shared-concurrent");
		});

		// Assert - All should be the same instance
		var first = breakers[0];
		foreach (var breaker in breakers)
		{
			breaker.ShouldBeSameAs(first);
		}
	}

	#endregion
}

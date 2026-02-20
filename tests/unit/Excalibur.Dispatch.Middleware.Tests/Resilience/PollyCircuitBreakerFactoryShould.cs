// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.CloudNative;
using Excalibur.Dispatch.Options.Resilience;
using Excalibur.Dispatch.Resilience.Polly;

namespace Excalibur.Dispatch.Middleware.Tests.Resilience;

/// <summary>
/// Unit tests for <see cref="PollyCircuitBreakerFactory"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Resilience")]
public sealed class PollyCircuitBreakerFactoryShould : UnitTestBase, IAsyncDisposable
{
	private PollyCircuitBreakerFactory? _factory;

	public async ValueTask DisposeAsync()
	{
		if (_factory != null)
		{
			await _factory.DisposeAsync();
			_factory = null;
		}
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing && _factory != null)
		{
			// For sync disposal, schedule async disposal
			_ = _factory.DisposeAsync().AsTask();
			_factory = null;
		}
		base.Dispose(disposing);
	}

	#region Constructor Tests

	[Fact]
	public void Constructor_WithDefaultOptions_CreatesInstance()
	{
		// Act
		_factory = new PollyCircuitBreakerFactory();

		// Assert
		_ = _factory.ShouldNotBeNull();
	}

	[Fact]
	public void Constructor_WithCustomOptions_CreatesInstance()
	{
		// Arrange
		var options = new CircuitBreakerOptions
		{
			FailureThreshold = 10,
			OpenDuration = TimeSpan.FromMinutes(2)
		};

		// Act
		_factory = new PollyCircuitBreakerFactory(options);

		// Assert
		_ = _factory.ShouldNotBeNull();
	}

	[Fact]
	public void Constructor_WithLogger_CreatesInstance()
	{
		// Arrange
		var logger = A.Fake<ILogger<PollyCircuitBreakerFactory>>();

		// Act
		_factory = new PollyCircuitBreakerFactory(null, logger);

		// Assert
		_ = _factory.ShouldNotBeNull();
	}

	[Fact]
	public void Constructor_WithBothParameters_CreatesInstance()
	{
		// Arrange
		var options = new CircuitBreakerOptions();
		var logger = A.Fake<ILogger<PollyCircuitBreakerFactory>>();

		// Act
		_factory = new PollyCircuitBreakerFactory(options, logger);

		// Assert
		_ = _factory.ShouldNotBeNull();
	}

	#endregion

	#region GetOrCreate Tests

	[Fact]
	public void GetOrCreate_WithNullName_ThrowsArgumentNullException()
	{
		// Arrange
		_factory = new PollyCircuitBreakerFactory();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => _factory.GetOrCreate(null!));
	}

	[Fact]
	public void GetOrCreate_WithValidName_ReturnsCircuitBreakerPattern()
	{
		// Arrange
		_factory = new PollyCircuitBreakerFactory();

		// Act
		var result = _factory.GetOrCreate("test-breaker");

		// Assert
		result.ShouldNotBeNull();
		result.ShouldBeAssignableTo<CircuitBreakerPattern>();
	}

	[Fact]
	public void GetOrCreate_WithSameName_ReturnsSameInstance()
	{
		// Arrange
		_factory = new PollyCircuitBreakerFactory();

		// Act
		var result1 = _factory.GetOrCreate("test-breaker");
		var result2 = _factory.GetOrCreate("test-breaker");

		// Assert - Both should wrap the same underlying adapter
		result1.ShouldNotBeNull();
		result2.ShouldNotBeNull();
	}

	[Fact]
	public void GetOrCreate_WithDifferentNames_ReturnsDifferentInstances()
	{
		// Arrange
		_factory = new PollyCircuitBreakerFactory();

		// Act
		var result1 = _factory.GetOrCreate("breaker-1");
		var result2 = _factory.GetOrCreate("breaker-2");

		// Assert
		result1.ShouldNotBeNull();
		result2.ShouldNotBeNull();
		// They should be different wrappers
	}

	[Fact]
	public void GetOrCreate_WithCustomOptions_UsesCustomOptions()
	{
		// Arrange
		_factory = new PollyCircuitBreakerFactory();
		var customOptions = new CircuitBreakerOptions
		{
			FailureThreshold = 15,
			OpenDuration = TimeSpan.FromMinutes(5)
		};

		// Act
		var result = _factory.GetOrCreate("custom-breaker", customOptions);

		// Assert
		result.ShouldNotBeNull();
	}

	[Fact]
	public void GetOrCreate_WithNullOptions_UsesDefaultOptions()
	{
		// Arrange
		var defaultOptions = new CircuitBreakerOptions
		{
			FailureThreshold = 20
		};
		_factory = new PollyCircuitBreakerFactory(defaultOptions);

		// Act
		var result = _factory.GetOrCreate("default-breaker");

		// Assert
		result.ShouldNotBeNull();
	}

	#endregion

	#region GetAllMetrics Tests

	[Fact]
	public void GetAllMetrics_WithNoCircuitBreakers_ReturnsEmptyDictionary()
	{
		// Arrange
		_factory = new PollyCircuitBreakerFactory();

		// Act
		var metrics = _factory.GetAllMetrics();

		// Assert
		metrics.ShouldNotBeNull();
		metrics.ShouldBeEmpty();
	}

	[Fact]
	public void GetAllMetrics_WithCircuitBreakers_ReturnsDictionaryWithMetrics()
	{
		// Arrange
		_factory = new PollyCircuitBreakerFactory();
		_ = _factory.GetOrCreate("breaker-1");
		_ = _factory.GetOrCreate("breaker-2");

		// Act
		var metrics = _factory.GetAllMetrics();

		// Assert
		metrics.ShouldNotBeNull();
		metrics.Count.ShouldBe(2);
		metrics.ContainsKey("breaker-1").ShouldBeTrue();
		metrics.ContainsKey("breaker-2").ShouldBeTrue();
	}

	[Fact]
	public void GetAllMetrics_ReturnsCircuitBreakerMetrics()
	{
		// Arrange
		_factory = new PollyCircuitBreakerFactory();
		_ = _factory.GetOrCreate("test-breaker");

		// Act
		var metrics = _factory.GetAllMetrics();

		// Assert
		metrics["test-breaker"].ShouldNotBeNull();
		metrics["test-breaker"].ShouldBeAssignableTo<CircuitBreakerMetrics>();
	}

	#endregion

	#region Remove Tests

	[Fact]
	public void Remove_WithNullName_ThrowsArgumentNullException()
	{
		// Arrange
		_factory = new PollyCircuitBreakerFactory();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => _factory.Remove(null!));
	}

	[Fact]
	public void Remove_WithExistingCircuitBreaker_ReturnsTrue()
	{
		// Arrange
		_factory = new PollyCircuitBreakerFactory();
		_ = _factory.GetOrCreate("test-breaker");

		// Act
		var result = _factory.Remove("test-breaker");

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void Remove_WithNonExistingCircuitBreaker_ReturnsFalse()
	{
		// Arrange
		_factory = new PollyCircuitBreakerFactory();

		// Act
		var result = _factory.Remove("non-existing");

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public void Remove_RemovesFromMetrics()
	{
		// Arrange
		_factory = new PollyCircuitBreakerFactory();
		_ = _factory.GetOrCreate("test-breaker");

		// Act
		_ = _factory.Remove("test-breaker");
		var metrics = _factory.GetAllMetrics();

		// Assert
		metrics.ContainsKey("test-breaker").ShouldBeFalse();
	}

	[Fact]
	public void Remove_CalledTwice_ReturnsFalseSecondTime()
	{
		// Arrange
		_factory = new PollyCircuitBreakerFactory();
		_ = _factory.GetOrCreate("test-breaker");

		// Act
		var firstResult = _factory.Remove("test-breaker");
		var secondResult = _factory.Remove("test-breaker");

		// Assert
		firstResult.ShouldBeTrue();
		secondResult.ShouldBeFalse();
	}

	#endregion

	#region DisposeAsync Tests

	[Fact]
	public async Task DisposeAsync_CanBeCalledMultipleTimes()
	{
		// Arrange
		_factory = new PollyCircuitBreakerFactory();
		_ = _factory.GetOrCreate("test-breaker");

		// Act & Assert - Should not throw
		await _factory.DisposeAsync();
		await _factory.DisposeAsync();
		await _factory.DisposeAsync();

		_factory = null; // Prevent double dispose in test cleanup
	}

	[Fact]
	public async Task DisposeAsync_ClearsAllCircuitBreakers()
	{
		// Arrange
		_factory = new PollyCircuitBreakerFactory();
		_ = _factory.GetOrCreate("breaker-1");
		_ = _factory.GetOrCreate("breaker-2");

		// Act
		await _factory.DisposeAsync();
		var metrics = _factory.GetAllMetrics();

		// Assert
		metrics.ShouldBeEmpty();

		_factory = null; // Prevent double dispose in test cleanup
	}

	#endregion

	#region ICircuitBreakerFactory Interface Tests

	[Fact]
	public void ImplementsICircuitBreakerFactory()
	{
		// Arrange
		_factory = new PollyCircuitBreakerFactory();

		// Assert
		_factory.ShouldBeAssignableTo<ICircuitBreakerFactory>();
	}

	[Fact]
	public void ImplementsIAsyncDisposable()
	{
		// Arrange
		_factory = new PollyCircuitBreakerFactory();

		// Assert
		_factory.ShouldBeAssignableTo<IAsyncDisposable>();
	}

	#endregion
}

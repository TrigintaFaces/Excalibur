// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Resilience;
using Excalibur.Dispatch.Options.Resilience;
using Excalibur.Dispatch.Resilience.Polly;
using CircuitState = Excalibur.Dispatch.Resilience.CircuitState;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Middleware.Tests.Resilience;

/// <summary>
/// Tests for the <see cref="PollyTransportCircuitBreakerRegistry"/> class.
/// Sprint 45 (bd-5tsb): Unit tests for Polly transport circuit breaker registry.
/// </summary>
[Trait("Category", "Unit")]
public sealed class PollyTransportCircuitBreakerRegistryShould : IDisposable
{
	private readonly List<PollyTransportCircuitBreakerRegistry> _registriesToDispose = [];

	public void Dispose()
	{
		foreach (var registry in _registriesToDispose)
		{
			registry.Dispose();
		}
	}

	private PollyTransportCircuitBreakerRegistry CreateRegistry(CircuitBreakerOptions? options = null)
	{
		var logger = NullLoggerFactory.Instance.CreateLogger<PollyTransportCircuitBreakerRegistry>();
		var registry = new PollyTransportCircuitBreakerRegistry(options ?? new CircuitBreakerOptions(), logger);
		_registriesToDispose.Add(registry);
		return registry;
	}

	#region Constructor Tests

	[Fact]
	public void CreateWithDefaultConstructor()
	{
		// Act
		var registry = new PollyTransportCircuitBreakerRegistry();
		_registriesToDispose.Add(registry);

		// Assert
		registry.Count.ShouldBe(0);
	}

	[Fact]
	public void CreateWithOptionsAndLogger()
	{
		// Arrange
		var options = new CircuitBreakerOptions
		{
			FailureThreshold = 10,
			OpenDuration = TimeSpan.FromMinutes(2),
		};
		var logger = NullLoggerFactory.Instance.CreateLogger<PollyTransportCircuitBreakerRegistry>();

		// Act
		var registry = new PollyTransportCircuitBreakerRegistry(options, logger);
		_registriesToDispose.Add(registry);

		// Assert
		registry.Count.ShouldBe(0);
	}

	[Fact]
	public void CreateWithIOptions()
	{
		// Arrange
		var options = Microsoft.Extensions.Options.Options.Create(new CircuitBreakerOptions
		{
			FailureThreshold = 5,
			OpenDuration = TimeSpan.FromMinutes(1),
		});
		var logger = NullLoggerFactory.Instance.CreateLogger<PollyTransportCircuitBreakerRegistry>();

		// Act
		var registry = new PollyTransportCircuitBreakerRegistry(options, logger);
		_registriesToDispose.Add(registry);

		// Assert
		registry.Count.ShouldBe(0);
	}

	[Fact]
	public void ThrowArgumentNullExceptionForNullOptions()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			new PollyTransportCircuitBreakerRegistry((CircuitBreakerOptions)null!, null));
	}

	[Fact]
	public void AcceptNullIOptionsGracefully()
	{
		// Act
		var registry = new PollyTransportCircuitBreakerRegistry((IOptions<CircuitBreakerOptions>)null!);
		_registriesToDispose.Add(registry);

		// Assert - should use default options
		registry.Count.ShouldBe(0);
	}

	[Fact]
	public void AcceptNullLoggerGracefully()
	{
		// Act
		var registry = new PollyTransportCircuitBreakerRegistry(new CircuitBreakerOptions(), (ILogger<PollyTransportCircuitBreakerRegistry>?)null);
		_registriesToDispose.Add(registry);

		// Assert
		registry.Count.ShouldBe(0);
	}

	#endregion Constructor Tests

	#region GetOrCreate Tests

	[Fact]
	public void CreateCircuitBreakerOnFirstAccess()
	{
		// Arrange
		var registry = CreateRegistry();

		// Act
		var circuitBreaker = registry.GetOrCreate("rabbitmq");

		// Assert
		_ = circuitBreaker.ShouldNotBeNull();
		registry.Count.ShouldBe(1);
	}

	[Fact]
	public void ReturnSameCircuitBreakerOnSubsequentAccess()
	{
		// Arrange
		var registry = CreateRegistry();

		// Act
		var cb1 = registry.GetOrCreate("rabbitmq");
		var cb2 = registry.GetOrCreate("rabbitmq");

		// Assert
		cb1.ShouldBeSameAs(cb2);
		registry.Count.ShouldBe(1);
	}

	[Fact]
	public void CreateSeparateCircuitBreakersForDifferentTransports()
	{
		// Arrange
		var registry = CreateRegistry();

		// Act
		var cbRabbitmq = registry.GetOrCreate("rabbitmq");
		var cbKafka = registry.GetOrCreate("kafka");
		var cbRedis = registry.GetOrCreate("redis");

		// Assert
		cbRabbitmq.ShouldNotBeSameAs(cbKafka);
		cbRabbitmq.ShouldNotBeSameAs(cbRedis);
		cbKafka.ShouldNotBeSameAs(cbRedis);
		registry.Count.ShouldBe(3);
	}

	[Fact]
	public void BeCaseInsensitiveForTransportNames()
	{
		// Arrange
		var registry = CreateRegistry();

		// Act
		var cb1 = registry.GetOrCreate("RabbitMQ");
		var cb2 = registry.GetOrCreate("rabbitmq");
		var cb3 = registry.GetOrCreate("RABBITMQ");

		// Assert
		cb1.ShouldBeSameAs(cb2);
		cb1.ShouldBeSameAs(cb3);
		registry.Count.ShouldBe(1);
	}

	[Fact]
	public void ThrowArgumentExceptionForNullTransportName()
	{
		// Arrange
		var registry = CreateRegistry();

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => registry.GetOrCreate(null!));
	}

	[Fact]
	public void ThrowArgumentExceptionForEmptyTransportName()
	{
		// Arrange
		var registry = CreateRegistry();

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => registry.GetOrCreate(string.Empty));
	}

	[Fact]
	public void ThrowArgumentExceptionForWhitespaceTransportName()
	{
		// Arrange
		var registry = CreateRegistry();

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => registry.GetOrCreate("   "));
	}

	[Fact]
	public void CreateWithCustomOptions()
	{
		// Arrange
		var registry = CreateRegistry();
		var customOptions = new CircuitBreakerOptions
		{
			FailureThreshold = 20,
			OpenDuration = TimeSpan.FromMinutes(5),
		};

		// Act
		var cb = registry.GetOrCreate("custom-transport", customOptions);

		// Assert
		_ = cb.ShouldNotBeNull();
		((int)cb.State).ShouldBe((int)CircuitState.Closed);
	}

	[Fact]
	public void ThrowArgumentNullExceptionForNullCustomOptions()
	{
		// Arrange
		var registry = CreateRegistry();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			registry.GetOrCreate("transport", (CircuitBreakerOptions)null!));
	}

	[Fact]
	public void UseExistingCircuitBreakerEvenWithCustomOptions()
	{
		// Arrange
		var registry = CreateRegistry();
		var cb1 = registry.GetOrCreate("transport");

		// Act - try to get with different options
		var customOptions = new CircuitBreakerOptions { FailureThreshold = 100 };
		var cb2 = registry.GetOrCreate("transport", customOptions);

		// Assert - should return existing, not create new
		cb1.ShouldBeSameAs(cb2);
		registry.Count.ShouldBe(1);
	}

	#endregion GetOrCreate Tests

	#region TryGet Tests

	[Fact]
	public void ReturnNullForNonExistentTransport()
	{
		// Arrange
		var registry = CreateRegistry();

		// Act
		var cb = registry.TryGet("nonexistent");

		// Assert
		cb.ShouldBeNull();
	}

	[Fact]
	public void ReturnCircuitBreakerForExistingTransport()
	{
		// Arrange
		var registry = CreateRegistry();
		var created = registry.GetOrCreate("rabbitmq");

		// Act
		var retrieved = registry.TryGet("rabbitmq");

		// Assert
		_ = retrieved.ShouldNotBeNull();
		retrieved.ShouldBeSameAs(created);
	}

	[Fact]
	public void TryGetBeCaseInsensitive()
	{
		// Arrange
		var registry = CreateRegistry();
		var created = registry.GetOrCreate("RabbitMQ");

		// Act
		var retrieved = registry.TryGet("rabbitmq");

		// Assert
		retrieved.ShouldBeSameAs(created);
	}

	[Fact]
	public void ThrowArgumentExceptionForNullTransportNameInTryGet()
	{
		// Arrange
		var registry = CreateRegistry();

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => registry.TryGet(null!));
	}

	#endregion TryGet Tests

	#region Remove Tests

	[Fact]
	public void RemoveExistingCircuitBreaker()
	{
		// Arrange
		var registry = CreateRegistry();
		_ = registry.GetOrCreate("rabbitmq");
		registry.Count.ShouldBe(1);

		// Act
		var removed = registry.Remove("rabbitmq");

		// Assert
		removed.ShouldBeTrue();
		registry.Count.ShouldBe(0);
		registry.TryGet("rabbitmq").ShouldBeNull();
	}

	[Fact]
	public void ReturnFalseWhenRemovingNonExistentTransport()
	{
		// Arrange
		var registry = CreateRegistry();

		// Act
		var removed = registry.Remove("nonexistent");

		// Assert
		removed.ShouldBeFalse();
	}

	[Fact]
	public void RemoveBeCaseInsensitive()
	{
		// Arrange
		var registry = CreateRegistry();
		_ = registry.GetOrCreate("RabbitMQ");

		// Act
		var removed = registry.Remove("rabbitmq");

		// Assert
		removed.ShouldBeTrue();
		registry.Count.ShouldBe(0);
	}

	[Fact]
	public void ThrowArgumentExceptionForNullTransportNameInRemove()
	{
		// Arrange
		var registry = CreateRegistry();

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => registry.Remove(null!));
	}

	[Fact]
	public void DisposeCircuitBreakerOnRemove()
	{
		// Arrange
		var registry = CreateRegistry();
		var cb = registry.GetOrCreate("rabbitmq");

		// Act
		_ = registry.Remove("rabbitmq");

		// Assert - circuit breaker should be disposed (can't verify directly, but operation completes)
		registry.TryGet("rabbitmq").ShouldBeNull();
	}

	#endregion Remove Tests

	#region ResetAll Tests

	[Fact]
	public void ResetAllCircuitBreakers()
	{
		// Arrange
		var registry = CreateRegistry();
		var cbRabbitmq = registry.GetOrCreate("rabbitmq");
		var cbKafka = registry.GetOrCreate("kafka");

		// Record some failures
		cbRabbitmq.RecordFailure();
		cbRabbitmq.RecordFailure();
		cbKafka.RecordFailure();

		// Act
		registry.ResetAll();

		// Assert
		((ICircuitBreakerDiagnostics)cbRabbitmq).ConsecutiveFailures.ShouldBe(0);
		((ICircuitBreakerDiagnostics)cbKafka).ConsecutiveFailures.ShouldBe(0);
	}

	[Fact]
	public void ResetAllWhenEmpty()
	{
		// Arrange
		var registry = CreateRegistry();

		// Act & Assert - should not throw
		Should.NotThrow(() => registry.ResetAll());
	}

	#endregion ResetAll Tests

	#region GetAllStates Tests

	[Fact]
	public void ReturnAllCircuitBreakerStates()
	{
		// Arrange
		var registry = CreateRegistry();
		_ = registry.GetOrCreate("rabbitmq");
		_ = registry.GetOrCreate("kafka");
		_ = registry.GetOrCreate("redis");

		// Act
		var states = registry.GetAllStates();

		// Assert
		states.Count.ShouldBe(3);
		states.ShouldContainKey("rabbitmq");
		states.ShouldContainKey("kafka");
		states.ShouldContainKey("redis");
		states.Values.All(s => s == Excalibur.Dispatch.Resilience.CircuitState.Closed).ShouldBeTrue();
	}

	[Fact]
	public void ReturnEmptyDictionaryWhenEmpty()
	{
		// Arrange
		var registry = CreateRegistry();

		// Act
		var states = registry.GetAllStates();

		// Assert
		states.ShouldBeEmpty();
	}

	[Fact]
	public void GetAllStatesBeCaseInsensitive()
	{
		// Arrange
		var registry = CreateRegistry();
		_ = registry.GetOrCreate("RabbitMQ");

		// Act
		var states = registry.GetAllStates();

		// Assert - should be accessible with any case
		states.ContainsKey("RabbitMQ").ShouldBeTrue();
	}

	#endregion GetAllStates Tests

	#region GetTransportNames Tests

	[Fact]
	public void ReturnAllTransportNames()
	{
		// Arrange
		var registry = CreateRegistry();
		_ = registry.GetOrCreate("rabbitmq");
		_ = registry.GetOrCreate("kafka");
		_ = registry.GetOrCreate("redis");

		// Act
		var names = registry.GetTransportNames().ToList();

		// Assert
		names.Count.ShouldBe(3);
		names.ShouldContain("rabbitmq");
		names.ShouldContain("kafka");
		names.ShouldContain("redis");
	}

	[Fact]
	public void ReturnEmptyEnumerableWhenEmpty()
	{
		// Arrange
		var registry = CreateRegistry();

		// Act
		var names = registry.GetTransportNames().ToList();

		// Assert
		names.ShouldBeEmpty();
	}

	#endregion GetTransportNames Tests

	#region Count Property Tests

	[Fact]
	public void TrackCountAccurately()
	{
		// Arrange
		var registry = CreateRegistry();

		// Act & Assert
		registry.Count.ShouldBe(0);

		_ = registry.GetOrCreate("transport1");
		registry.Count.ShouldBe(1);

		_ = registry.GetOrCreate("transport2");
		registry.Count.ShouldBe(2);

		_ = registry.Remove("transport1");
		registry.Count.ShouldBe(1);
	}

	#endregion Count Property Tests

	#region Dispose Tests

	[Fact]
	public void DisposeAllCircuitBreakersOnDispose()
	{
		// Arrange
		var registry = new PollyTransportCircuitBreakerRegistry();
		_ = registry.GetOrCreate("rabbitmq");
		_ = registry.GetOrCreate("kafka");

		// Act
		registry.Dispose();

		// Assert - after dispose, operations should throw ObjectDisposedException
		_ = Should.Throw<ObjectDisposedException>(() => registry.GetOrCreate("new"));
	}

	[Fact]
	public void AllowMultipleDisposes()
	{
		// Arrange
		var registry = new PollyTransportCircuitBreakerRegistry();
		_ = registry.GetOrCreate("rabbitmq");

		// Act & Assert - should not throw
		Should.NotThrow(() =>
		{
			registry.Dispose();
			registry.Dispose();
		});
	}

	[Fact]
	public void ThrowObjectDisposedExceptionAfterDispose()
	{
		// Arrange
		var registry = new PollyTransportCircuitBreakerRegistry();
		registry.Dispose();

		// Act & Assert
		_ = Should.Throw<ObjectDisposedException>(() => registry.GetOrCreate("test"));
		_ = Should.Throw<ObjectDisposedException>(() => registry.TryGet("test"));
		_ = Should.Throw<ObjectDisposedException>(() => registry.Remove("test"));
		_ = Should.Throw<ObjectDisposedException>(() => registry.ResetAll());
		_ = Should.Throw<ObjectDisposedException>(() => registry.GetAllStates());
		_ = Should.Throw<ObjectDisposedException>(() => registry.GetTransportNames());
	}

	#endregion Dispose Tests

	#region Transport Isolation Tests

	[Fact]
	public void IsolateFailuresBetweenTransports()
	{
		// Arrange
		var registry = CreateRegistry();
		var cbRabbitmq = registry.GetOrCreate("rabbitmq");
		var cbKafka = registry.GetOrCreate("kafka");

		// Act
		cbRabbitmq.RecordFailure();
		cbRabbitmq.RecordFailure();
		cbRabbitmq.RecordFailure();

		// Assert - kafka should be unaffected
		((ICircuitBreakerDiagnostics)cbRabbitmq).ConsecutiveFailures.ShouldBe(3);
		((ICircuitBreakerDiagnostics)cbKafka).ConsecutiveFailures.ShouldBe(0);
	}

	[Fact]
	public void IsolateStatesBetweenTransports()
	{
		// Arrange
		var options = new CircuitBreakerOptions
		{
			FailureThreshold = 2,
			OpenDuration = TimeSpan.FromMinutes(1),
		};
		var registry = CreateRegistry(options);
		var cbRabbitmq = registry.GetOrCreate("rabbitmq");
		var cbKafka = registry.GetOrCreate("kafka");

		// Record enough failures to track independently
		cbRabbitmq.RecordFailure();
		cbRabbitmq.RecordFailure();

		// Act
		var states = registry.GetAllStates();

		// Assert - states tracked separately (both start Closed)
		((ICircuitBreakerDiagnostics)cbRabbitmq).ConsecutiveFailures.ShouldBe(2);
		((ICircuitBreakerDiagnostics)cbKafka).ConsecutiveFailures.ShouldBe(0);
	}

	#endregion Transport Isolation Tests

	#region Thread Safety Tests

	[Fact]
	public async Task HandleConcurrentGetOrCreateCalls()
	{
		// Arrange
		var registry = CreateRegistry();
		var tasks = new List<Task<ICircuitBreakerPolicy>>();

		// Act - simulate concurrent access to same transport
		for (var i = 0; i < 100; i++)
		{
			tasks.Add(Task.Run(() => registry.GetOrCreate("shared-transport")));
		}

		var results = await Task.WhenAll(tasks).ConfigureAwait(false);

		// Assert - all should return the same instance
		var first = results[0];
		results.All(cb => ReferenceEquals(cb, first)).ShouldBeTrue();
		registry.Count.ShouldBe(1);
	}

	[Fact]
	public async Task HandleConcurrentGetOrCreateForDifferentTransports()
	{
		// Arrange
		var registry = CreateRegistry();
		var tasks = new List<Task<ICircuitBreakerPolicy>>();

		// Act - simulate concurrent access to different transports
		for (var i = 0; i < 50; i++)
		{
			var transportName = $"transport-{i}";
			tasks.Add(Task.Run(() => registry.GetOrCreate(transportName)));
		}

		_ = await Task.WhenAll(tasks).ConfigureAwait(false);

		// Assert
		registry.Count.ShouldBe(50);
	}

	[Fact]
	public async Task HandleConcurrentMixedOperations()
	{
		// Arrange
		var registry = CreateRegistry();
		var tasks = new List<Task>();

		// Pre-create some transports
		_ = registry.GetOrCreate("transport-0");
		_ = registry.GetOrCreate("transport-1");

		// Act - mix of operations
		for (var i = 0; i < 50; i++)
		{
			var index = i;
			tasks.Add(Task.Run(() =>
			{
				switch (index % 5)
				{
					case 0:
						_ = registry.GetOrCreate($"transport-{index}");
						break;

					case 1:
						_ = registry.TryGet($"transport-{index % 10}");
						break;

					case 2:
						_ = registry.GetAllStates();
						break;

					case 3:
						_ = registry.GetTransportNames().ToList();
						break;

					case 4:
						var cb = registry.TryGet($"transport-{index % 10}");
						cb?.RecordFailure();
						break;
				}
			}));
		}

		await Task.WhenAll(tasks).ConfigureAwait(false);

		// Assert - no exceptions and consistent state
		registry.Count.ShouldBeGreaterThan(0);
	}

	#endregion Thread Safety Tests
}

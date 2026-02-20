// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Options.Resilience;
using Excalibur.Dispatch.Resilience.Polly;

namespace Excalibur.Dispatch.Middleware.Tests.Resilience;

/// <summary>
/// Functional tests for <see cref="PollyTransportCircuitBreakerRegistry"/> verifying
/// per-transport circuit breaker creation, lookup, removal, and lifecycle.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Resilience")]
public sealed class PollyTransportCircuitBreakerRegistryFunctionalShould : IDisposable
{
	private readonly PollyTransportCircuitBreakerRegistry _sut;

	public PollyTransportCircuitBreakerRegistryFunctionalShould()
	{
		var options = new CircuitBreakerOptions
		{
			FailureThreshold = 3,
			SuccessThreshold = 2,
			OpenDuration = TimeSpan.FromSeconds(5),
		};
		_sut = new PollyTransportCircuitBreakerRegistry(options, null);
	}

	public void Dispose()
	{
		_sut.Dispose();
	}

	[Fact]
	public void Start_with_zero_circuit_breakers()
	{
		_sut.Count.ShouldBe(0);
	}

	[Fact]
	public void Create_circuit_breaker_for_transport()
	{
		var cb = _sut.GetOrCreate("kafka");

		cb.ShouldNotBeNull();
		_sut.Count.ShouldBe(1);
	}

	[Fact]
	public void Return_same_circuit_breaker_for_same_transport()
	{
		var cb1 = _sut.GetOrCreate("rabbitmq");
		var cb2 = _sut.GetOrCreate("rabbitmq");

		cb1.ShouldBeSameAs(cb2);
		_sut.Count.ShouldBe(1);
	}

	[Fact]
	public void Create_separate_circuit_breakers_for_different_transports()
	{
		var cbKafka = _sut.GetOrCreate("kafka");
		var cbRabbit = _sut.GetOrCreate("rabbitmq");
		var cbSqs = _sut.GetOrCreate("aws-sqs");

		cbKafka.ShouldNotBeSameAs(cbRabbit);
		cbRabbit.ShouldNotBeSameAs(cbSqs);
		_sut.Count.ShouldBe(3);
	}

	[Fact]
	public void Create_with_custom_options_per_transport()
	{
		var customOptions = new CircuitBreakerOptions
		{
			FailureThreshold = 10,
			OpenDuration = TimeSpan.FromMinutes(1),
		};

		var cb = _sut.GetOrCreate("custom-transport", customOptions);

		cb.ShouldNotBeNull();
	}

	[Fact]
	public void Try_get_returns_null_for_nonexistent()
	{
		var result = _sut.TryGet("nonexistent");

		result.ShouldBeNull();
	}

	[Fact]
	public void Try_get_returns_existing_circuit_breaker()
	{
		var created = _sut.GetOrCreate("kafka");

		var found = _sut.TryGet("kafka");

		found.ShouldBeSameAs(created);
	}

	[Fact]
	public void Remove_existing_circuit_breaker()
	{
		_sut.GetOrCreate("kafka");

		var removed = _sut.Remove("kafka");

		removed.ShouldBeTrue();
		_sut.Count.ShouldBe(0);
		_sut.TryGet("kafka").ShouldBeNull();
	}

	[Fact]
	public void Remove_nonexistent_returns_false()
	{
		var removed = _sut.Remove("nonexistent");

		removed.ShouldBeFalse();
	}

	[Fact]
	public void Get_all_states()
	{
		_sut.GetOrCreate("kafka");
		_sut.GetOrCreate("rabbitmq");

		var states = _sut.GetAllStates();

		states.Count.ShouldBe(2);
		states.ShouldContainKey("kafka");
		states.ShouldContainKey("rabbitmq");
	}

	[Fact]
	public void Get_transport_names()
	{
		_sut.GetOrCreate("kafka");
		_sut.GetOrCreate("rabbitmq");
		_sut.GetOrCreate("aws-sqs");

		var names = _sut.GetTransportNames().ToList();

		names.Count.ShouldBe(3);
		names.ShouldContain("kafka");
		names.ShouldContain("rabbitmq");
		names.ShouldContain("aws-sqs");
	}

	[Fact]
	public void Reset_all_circuit_breakers()
	{
		_sut.GetOrCreate("kafka");
		_sut.GetOrCreate("rabbitmq");

		// Should not throw
		_sut.ResetAll();
	}

	[Fact]
	public void Be_case_insensitive_for_transport_names()
	{
		var cb1 = _sut.GetOrCreate("Kafka");
		var cb2 = _sut.GetOrCreate("kafka");

		cb1.ShouldBeSameAs(cb2);
		_sut.Count.ShouldBe(1);
	}

	[Fact]
	public void Throw_for_null_transport_name()
	{
		Should.Throw<ArgumentException>(() => _sut.GetOrCreate(null!));
	}

	[Fact]
	public void Throw_for_empty_transport_name()
	{
		Should.Throw<ArgumentException>(() => _sut.GetOrCreate(""));
	}

	[Fact]
	public void Throw_for_whitespace_transport_name()
	{
		Should.Throw<ArgumentException>(() => _sut.GetOrCreate("   "));
	}

	[Fact]
	public void Throw_after_disposal()
	{
		_sut.Dispose();

		Should.Throw<ObjectDisposedException>(() => _sut.GetOrCreate("kafka"));
	}

	[Fact]
	public void Dispose_idempotently()
	{
		_sut.GetOrCreate("kafka");

		_sut.Dispose();
		_sut.Dispose(); // Should not throw
	}

	[Fact]
	public void Create_with_default_constructor()
	{
		using var registry = new PollyTransportCircuitBreakerRegistry();

		var cb = registry.GetOrCreate("test");
		cb.ShouldNotBeNull();
	}

	[Fact]
	public void Throw_for_null_options_in_constructor()
	{
		Should.Throw<ArgumentNullException>(
			() => new PollyTransportCircuitBreakerRegistry((CircuitBreakerOptions)null!, null));
	}
}

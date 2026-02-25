// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Resilience;
using Excalibur.Dispatch.Options.Resilience;

namespace Excalibur.Dispatch.Tests.Messaging.Resilience;

/// <summary>
///     Tests for the <see cref="NullTransportCircuitBreakerRegistry" /> class.
/// </summary>
[Trait("Category", "Unit")]
public sealed class NullTransportCircuitBreakerRegistryShould
{
	[Fact]
	public void ProvideSingletonInstance()
	{
		NullTransportCircuitBreakerRegistry.Instance.ShouldNotBeNull();
	}

	[Fact]
	public void ReturnSameInstanceOnMultipleAccesses()
	{
		NullTransportCircuitBreakerRegistry.Instance
			.ShouldBeSameAs(NullTransportCircuitBreakerRegistry.Instance);
	}

	[Fact]
	public void HaveZeroCount()
	{
		NullTransportCircuitBreakerRegistry.Instance.Count.ShouldBe(0);
	}

	[Fact]
	public void ReturnNullCircuitBreakerPolicyFromGetOrCreate()
	{
		var result = NullTransportCircuitBreakerRegistry.Instance.GetOrCreate("RabbitMQ");

		result.ShouldBeSameAs(NullCircuitBreakerPolicy.Instance);
	}

	[Fact]
	public void ReturnNullCircuitBreakerPolicyFromGetOrCreateWithOptions()
	{
		var result = NullTransportCircuitBreakerRegistry.Instance
			.GetOrCreate("Kafka", new CircuitBreakerOptions());

		result.ShouldBeSameAs(NullCircuitBreakerPolicy.Instance);
	}

	[Fact]
	public void ReturnNullFromTryGet()
	{
		NullTransportCircuitBreakerRegistry.Instance.TryGet("RabbitMQ").ShouldBeNull();
	}

	[Fact]
	public void ReturnFalseFromRemove()
	{
		NullTransportCircuitBreakerRegistry.Instance.Remove("RabbitMQ").ShouldBeFalse();
	}

	[Fact]
	public void NotThrowOnResetAll()
	{
		Should.NotThrow(() => NullTransportCircuitBreakerRegistry.Instance.ResetAll());
	}

	[Fact]
	public void ReturnEmptyStatesFromGetAllStates()
	{
		var states = NullTransportCircuitBreakerRegistry.Instance.GetAllStates();

		states.ShouldNotBeNull();
		states.Count.ShouldBe(0);
	}

	[Fact]
	public void ReturnEmptyTransportNames()
	{
		var names = NullTransportCircuitBreakerRegistry.Instance.GetTransportNames();

		names.ShouldNotBeNull();
		names.ShouldBeEmpty();
	}

	[Fact]
	public void ImplementITransportCircuitBreakerRegistry()
	{
		NullTransportCircuitBreakerRegistry.Instance
			.ShouldBeAssignableTo<ITransportCircuitBreakerRegistry>();
	}

	[Fact]
	public void ImplementITransportCircuitBreakerDiagnostics()
	{
		NullTransportCircuitBreakerRegistry.Instance
			.ShouldBeAssignableTo<ITransportCircuitBreakerDiagnostics>();
	}
}

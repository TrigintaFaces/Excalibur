// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.ElasticSearch.Resilience;

namespace Excalibur.Data.Tests.ElasticSearch.Resilience;

/// <summary>
/// Unit tests for the <see cref="IElasticsearchCircuitBreaker"/> interface definition.
/// </summary>
/// <remarks>
/// Sprint 514 (S514.2): Resilience unit tests.
/// Tests verify interface members and contract.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Elasticsearch")]
[Trait("Feature", "Resilience")]
public sealed class IElasticsearchCircuitBreakerShould
{
	#region Interface Definition Tests

	[Fact]
	public void DefineIsOpenProperty()
	{
		// Assert
		var property = typeof(IElasticsearchCircuitBreaker).GetProperty("IsOpen");
		property.ShouldNotBeNull();
		property.PropertyType.ShouldBe(typeof(bool));
		property.CanRead.ShouldBeTrue();
	}

	[Fact]
	public void DefineIsHalfOpenProperty()
	{
		// Assert
		var property = typeof(IElasticsearchCircuitBreaker).GetProperty("IsHalfOpen");
		property.ShouldNotBeNull();
		property.PropertyType.ShouldBe(typeof(bool));
		property.CanRead.ShouldBeTrue();
	}

	[Fact]
	public void DefineStateProperty()
	{
		// Assert
		var property = typeof(IElasticsearchCircuitBreaker).GetProperty("State");
		property.ShouldNotBeNull();
		property.PropertyType.ShouldBe(typeof(CircuitBreakerState));
		property.CanRead.ShouldBeTrue();
	}

	[Fact]
	public void DefineFailureRateProperty()
	{
		// Assert
		var property = typeof(IElasticsearchCircuitBreaker).GetProperty("FailureRate");
		property.ShouldNotBeNull();
		property.PropertyType.ShouldBe(typeof(double));
		property.CanRead.ShouldBeTrue();
	}

	[Fact]
	public void DefineConsecutiveFailuresProperty()
	{
		// Assert
		var property = typeof(IElasticsearchCircuitBreaker).GetProperty("ConsecutiveFailures");
		property.ShouldNotBeNull();
		property.PropertyType.ShouldBe(typeof(int));
		property.CanRead.ShouldBeTrue();
	}

	[Fact]
	public void DefineRecordSuccessAsyncMethod()
	{
		// Assert
		var method = typeof(IElasticsearchCircuitBreaker).GetMethod("RecordSuccessAsync");
		method.ShouldNotBeNull();
		method.ReturnType.ShouldBe(typeof(Task));
		method.GetParameters().Length.ShouldBe(0);
	}

	[Fact]
	public void DefineRecordFailureAsyncMethod()
	{
		// Assert
		var method = typeof(IElasticsearchCircuitBreaker).GetMethod("RecordFailureAsync");
		method.ShouldNotBeNull();
		method.ReturnType.ShouldBe(typeof(Task));
		method.GetParameters().Length.ShouldBe(0);
	}

	[Fact]
	public void DefineExecuteAsyncMethod()
	{
		// Assert
		var method = typeof(IElasticsearchCircuitBreaker).GetMethod("ExecuteAsync");
		method.ShouldNotBeNull();
		method.IsGenericMethod.ShouldBeTrue();

		var parameters = method.GetParameters();
		parameters.Length.ShouldBe(2);
		parameters[1].ParameterType.ShouldBe(typeof(CancellationToken));
	}

	#endregion

	#region Interface Inheritance Tests

	[Fact]
	public void ImplementIDisposable()
	{
		// Assert
		typeof(IDisposable).IsAssignableFrom(typeof(IElasticsearchCircuitBreaker)).ShouldBeTrue();
	}

	#endregion

	#region Interface Contract Tests

	[Fact]
	public void BeImplementableInterface()
	{
		// Assert
		typeof(IElasticsearchCircuitBreaker).IsInterface.ShouldBeTrue();
	}

	#endregion
}

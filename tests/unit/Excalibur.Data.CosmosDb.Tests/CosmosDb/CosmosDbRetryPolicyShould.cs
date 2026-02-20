// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Net;
using System.Reflection;

using Excalibur.Data.CosmosDb;

using Microsoft.Azure.Cosmos;

namespace Excalibur.Data.Tests.CosmosDb;

/// <summary>
/// Unit tests for the CosmosDbRetryPolicy class.
/// </summary>
/// <remarks>
/// Sprint 514 (S514.3): CosmosDB unit tests.
/// Tests verify retry policy behavior for transient failures.
/// Note: CosmosDbRetryPolicy is internal, so we use reflection to test it.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "CosmosDb")]
[Trait("Feature", "Resilience")]
public sealed class CosmosDbRetryPolicyShould
{
	private readonly Type _policyType;
	private readonly object _policyInstance;

	public CosmosDbRetryPolicyShould()
	{
		// Get the internal type via reflection
		var assembly = typeof(CosmosDbOptions).Assembly;
		_policyType = assembly.GetType("Excalibur.Data.CosmosDb.CosmosDbRetryPolicy")!;
		var instanceProperty = _policyType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
		_policyInstance = instanceProperty!.GetValue(null)!;
	}

	#region Singleton Tests

	[Fact]
	public void Instance_ReturnsSameInstance()
	{
		// Arrange
		var instanceProperty = _policyType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);

		// Act
		var instance1 = instanceProperty.GetValue(null);
		var instance2 = instanceProperty.GetValue(null);

		// Assert
		instance1.ShouldBeSameAs(instance2);
	}

	#endregion

	#region MaxRetryAttempts Tests

	[Fact]
	public void MaxRetryAttempts_ReturnsNine()
	{
		// Arrange
		var property = _policyType.GetProperty("MaxRetryAttempts");

		// Act
		var result = (int)property!.GetValue(_policyInstance)!;

		// Assert
		result.ShouldBe(9);
	}

	#endregion

	#region BaseRetryDelay Tests

	[Fact]
	public void BaseRetryDelay_ReturnsOneHundredMilliseconds()
	{
		// Arrange
		var property = _policyType.GetProperty("BaseRetryDelay");

		// Act
		var result = (TimeSpan)property!.GetValue(_policyInstance)!;

		// Assert
		result.ShouldBe(TimeSpan.FromMilliseconds(100));
	}

	#endregion

	#region ShouldRetry Tests

	[Fact]
	public void ShouldRetry_ReturnsTrue_ForHttpRequestException()
	{
		// Arrange
		var method = _policyType.GetMethod("ShouldRetry");
		var exception = new HttpRequestException("Test");

		// Act
		var result = (bool)method!.Invoke(_policyInstance, new object[] { exception })!;

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void ShouldRetry_ReturnsFalse_ForTaskCanceledException()
	{
		// Arrange
		var method = _policyType.GetMethod("ShouldRetry");
		var exception = new TaskCanceledException("Test");

		// Act
		var result = (bool)method!.Invoke(_policyInstance, new object[] { exception })!;

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public void ShouldRetry_ReturnsFalse_ForOperationCanceledException()
	{
		// Arrange
		var method = _policyType.GetMethod("ShouldRetry");
		var exception = new OperationCanceledException("Test");

		// Act
		var result = (bool)method!.Invoke(_policyInstance, new object[] { exception })!;

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public void ShouldRetry_ReturnsFalse_ForGenericException()
	{
		// Arrange
		var method = _policyType.GetMethod("ShouldRetry");
		var exception = new InvalidOperationException("Test");

		// Act
		var result = (bool)method!.Invoke(_policyInstance, new object[] { exception })!;

		// Assert
		result.ShouldBeFalse();
	}

	#endregion

	#region Type Tests

	[Fact]
	public void IsSealed()
	{
		// Assert
		_policyType.IsSealed.ShouldBeTrue();
	}

	[Fact]
	public void IsInternal()
	{
		// Assert
		_policyType.IsNotPublic.ShouldBeTrue();
	}

	[Fact]
	public void ImplementsIDataRequestRetryPolicy()
	{
		// Arrange
		var interfaceType = typeof(Data.Abstractions.Resilience.IDataRequestRetryPolicy);

		// Assert
		interfaceType.IsAssignableFrom(_policyType).ShouldBeTrue();
	}

	#endregion
}

// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;

using Excalibur.Data.Abstractions.Resilience;

using Microsoft.Extensions.Options;

using Excalibur.Data.InMemory;

namespace Excalibur.Data.Tests.InMemory;

/// <summary>
/// Unit tests for NullRetryPolicy.
/// </summary>
[Trait("Category", "Unit")]
public sealed class NullRetryPolicyShould : UnitTestBase
{
	private static IDataRequestRetryPolicy CreateNullRetryPolicy()
	{
		// NullRetryPolicy is internal, so we access it via reflection through InMemoryPersistenceProvider
		var logger = new Microsoft.Extensions.Logging.Abstractions.NullLogger<InMemoryPersistenceProvider>();
		var options = Options.Create(new InMemoryProviderOptions());
		var provider = new InMemoryPersistenceProvider(options, logger);
		return provider.RetryPolicy;
	}

	#region Properties

	[Fact]
	public void HaveZeroMaxRetryAttempts()
	{
		// Arrange
		var policy = CreateNullRetryPolicy();

		// Assert
		policy.MaxRetryAttempts.ShouldBe(0);
	}

	[Fact]
	public void HaveZeroBaseRetryDelay()
	{
		// Arrange
		var policy = CreateNullRetryPolicy();

		// Assert
		policy.BaseRetryDelay.ShouldBe(TimeSpan.Zero);
	}

	[Fact]
	public void HaveZeroInitialDelay()
	{
		// Arrange - Access static property via reflection
		var policyType = CreateNullRetryPolicy().GetType();
		var property = policyType.GetProperty("InitialDelay", BindingFlags.Public | BindingFlags.Static);
		var value = (TimeSpan)property!.GetValue(null)!;

		// Assert
		value.ShouldBe(TimeSpan.Zero);
	}

	[Fact]
	public void HaveZeroMaxDelay()
	{
		// Arrange - Access static property via reflection
		var policyType = CreateNullRetryPolicy().GetType();
		var property = policyType.GetProperty("MaxDelay", BindingFlags.Public | BindingFlags.Static);
		var value = (TimeSpan)property!.GetValue(null)!;

		// Assert
		value.ShouldBe(TimeSpan.Zero);
	}

	#endregion Properties

	#region ShouldRetry

	[Fact]
	public void ShouldRetry_ReturnsFalse_ForAnyException()
	{
		// Arrange
		var policy = CreateNullRetryPolicy();
		var exception = new InvalidOperationException("Test exception");

		// Act
		var result = policy.ShouldRetry(exception);

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public void ShouldRetry_ReturnsFalse_ForTimeoutException()
	{
		// Arrange
		var policy = CreateNullRetryPolicy();
		var exception = new TimeoutException("Timeout");

		// Act
		var result = policy.ShouldRetry(exception);

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public void ShouldRetry_ReturnsFalse_ForIOException()
	{
		// Arrange
		var policy = CreateNullRetryPolicy();
		var exception = new IOException("IO error");

		// Act
		var result = policy.ShouldRetry(exception);

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public void ShouldRetryWithAttempt_ReturnsFalse_ForAnyAttempt()
	{
		// Arrange - Access static method via reflection
		var policyType = CreateNullRetryPolicy().GetType();
		var method = policyType.GetMethod("ShouldRetry", BindingFlags.Public | BindingFlags.Static,
			new[] { typeof(Exception), typeof(int) });
		var exception = new InvalidOperationException("Test");

		// Act & Assert
		((bool)method!.Invoke(null, [exception, 0])!).ShouldBeFalse();
		((bool)method!.Invoke(null, [exception, 1])!).ShouldBeFalse();
		((bool)method!.Invoke(null, [exception, 5])!).ShouldBeFalse();
		((bool)method!.Invoke(null, [exception, 100])!).ShouldBeFalse();
	}

	#endregion ShouldRetry

	#region GetDelay

	[Theory]
	[InlineData(0)]
	[InlineData(1)]
	[InlineData(5)]
	[InlineData(10)]
	[InlineData(100)]
	public void GetDelay_AlwaysReturnsZero(int attemptNumber)
	{
		// Arrange - Access static method via reflection
		var policyType = CreateNullRetryPolicy().GetType();
		var method = policyType.GetMethod("GetDelay", BindingFlags.Public | BindingFlags.Static);

		// Act
		var delay = (TimeSpan)method!.Invoke(null, [attemptNumber])!;

		// Assert
		delay.ShouldBe(TimeSpan.Zero);
	}

	#endregion GetDelay

	#region Interface Implementation

	[Fact]
	public void ImplementsIDataRequestRetryPolicy()
	{
		// Arrange
		var policy = CreateNullRetryPolicy();

		// Assert
		_ = policy.ShouldBeAssignableTo<IDataRequestRetryPolicy>();
	}

	#endregion Interface Implementation
}

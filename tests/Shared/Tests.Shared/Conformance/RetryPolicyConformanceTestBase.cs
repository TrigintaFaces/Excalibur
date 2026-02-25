// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Abstractions.Resilience;

namespace Tests.Shared.Conformance;

/// <summary>
/// Base class for IDataRequestRetryPolicy conformance tests.
/// Implementations must provide a concrete policy instance for testing.
/// </summary>
/// <remarks>
/// This conformance test kit verifies that retry policy implementations
/// correctly implement the IDataRequestRetryPolicy interface contract.
/// </remarks>
public abstract class RetryPolicyConformanceTestBase
{
	/// <summary>
	/// Indicates whether this is a null/no-retry policy.
	/// </summary>
	protected virtual bool IsNullPolicy => false;

	/// <summary>
	/// Creates a new instance of the retry policy under test.
	/// </summary>
	/// <param name="maxRetryAttempts">The maximum retry attempts to configure.</param>
	/// <returns>A configured retry policy instance.</returns>
	protected abstract IDataRequestRetryPolicy CreatePolicy(int maxRetryAttempts);

	/// <summary>
	/// Gets an exception that should trigger a retry for this policy type.
	/// </summary>
	/// <returns>An exception that should be retried.</returns>
	protected abstract Exception CreateRetryableException();

	/// <summary>
	/// Gets an exception that should NOT trigger a retry for this policy type.
	/// </summary>
	/// <returns>An exception that should not be retried.</returns>
	protected abstract Exception CreateNonRetryableException();

	#region Interface Implementation Tests

	[Fact]
	public void Policy_ShouldImplementIDataRequestRetryPolicy()
	{
		// Arrange
		var policy = CreatePolicy(3);

		// Assert
		_ = policy.ShouldBeAssignableTo<IDataRequestRetryPolicy>();
	}

	#endregion Interface Implementation Tests

	#region MaxRetryAttempts Tests

	[Theory]
	[InlineData(0)]
	[InlineData(1)]
	[InlineData(3)]
	[InlineData(5)]
	[InlineData(10)]
	public void MaxRetryAttempts_ShouldMatchConfiguredValue(int maxRetryAttempts)
	{
		// Null policies always have 0 retries regardless of configuration
		if (IsNullPolicy && maxRetryAttempts > 0)
			return;

		// Arrange
		var policy = CreatePolicy(maxRetryAttempts);

		// Assert
		policy.MaxRetryAttempts.ShouldBe(maxRetryAttempts);
	}

	[Fact]
	public void MaxRetryAttempts_ShouldBeNonNegative()
	{
		// Arrange
		var policy = CreatePolicy(3);

		// Assert
		policy.MaxRetryAttempts.ShouldBeGreaterThanOrEqualTo(0);
	}

	#endregion MaxRetryAttempts Tests

	#region BaseRetryDelay Tests

	[Fact]
	public void BaseRetryDelay_ShouldBeNonNegative()
	{
		// Arrange
		var policy = CreatePolicy(3);

		// Assert
		policy.BaseRetryDelay.ShouldBeGreaterThanOrEqualTo(TimeSpan.Zero);
	}

	[Fact]
	public void BaseRetryDelay_ForNullPolicy_ShouldBeZero()
	{
		if (!IsNullPolicy)
			return;

		// Arrange
		var policy = CreatePolicy(0);

		// Assert
		policy.BaseRetryDelay.ShouldBe(TimeSpan.Zero);
	}

	#endregion BaseRetryDelay Tests

	#region ShouldRetry Exception Tests

	[Fact]
	public void ShouldRetry_WithRetryableException_ReturnsExpectedResult()
	{
		// Arrange
		var policy = CreatePolicy(3);
		var exception = CreateRetryableException();

		// Act
		var result = policy.ShouldRetry(exception);

		// Assert
		if (IsNullPolicy)
		{
			result.ShouldBeFalse("Null policy should never retry");
		}
		else
		{
			result.ShouldBeTrue("Policy should retry retryable exceptions");
		}
	}

	[Fact]
	public void ShouldRetry_WithNonRetryableException_ReturnsFalse()
	{
		// Arrange
		var policy = CreatePolicy(3);
		var exception = CreateNonRetryableException();

		// Act
		var result = policy.ShouldRetry(exception);

		// Assert
		result.ShouldBeFalse("Policy should not retry non-retryable exceptions");
	}

	#endregion ShouldRetry Exception Tests

	#region BaseRetryDelay Behavior Tests

	[Fact]
	public void BaseRetryDelay_ForNonNullPolicy_ShouldBePositive()
	{
		if (IsNullPolicy)
			return;

		// Arrange
		var policy = CreatePolicy(3);

		// Assert - non-null policies should have positive base delay for exponential backoff
		policy.BaseRetryDelay.ShouldBeGreaterThan(TimeSpan.Zero);
	}

	#endregion BaseRetryDelay Behavior Tests
}

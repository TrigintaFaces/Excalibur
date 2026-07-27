// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

#pragma warning disable IDE0007 // Use implicit type (var)
#pragma warning disable IDE0270 // Null check can be simplified

using Excalibur.Data.Resilience;

namespace Excalibur.Testing.Conformance;

/// <summary>
/// Abstract conformance test kit for <see cref="IDataRequestRetryPolicy"/> implementations.
/// </summary>
/// <remarks>
/// <para>
/// Inherit from this kit and implement <see cref="CreatePolicy"/>, <see cref="CreateRetryableException"/>,
/// and <see cref="CreateNonRetryableException"/> to verify that your retry policy conforms to the
/// <see cref="IDataRequestRetryPolicy"/> contract. Override <see cref="IsNullPolicy"/> to return
/// <see langword="true"/> for a no-retry policy so the null-policy branches are asserted instead.
/// </para>
/// <para>
/// The kit exposes plain <c>public virtual</c> methods with no test-framework attributes. Add the
/// attributes required by your own test framework (for example <c>[Fact]</c>) on thin overrides in your
/// derived class, and the kit runs against your implementation reporting pass/fail.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// public sealed class MyRetryPolicyConformanceTests : RetryPolicyConformanceTestKit
/// {
///     protected override IDataRequestRetryPolicy CreatePolicy(int maxRetryAttempts) =>
///         new MyRetryPolicy(maxRetryAttempts);
///
///     protected override Exception CreateRetryableException() => new TimeoutException();
///     protected override Exception CreateNonRetryableException() => new ArgumentException();
///
///     [Fact] public void MaxRetryAttempts_Match() => MaxRetryAttempts_ShouldMatchConfiguredValue();
/// }
/// </code>
/// </example>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores",
	Justification = "Test method naming convention")]
public abstract class RetryPolicyConformanceTestKit
{
	/// <summary>
	/// Gets a value indicating whether the policy under test is a null/no-retry policy.
	/// </summary>
	/// <value><see langword="true"/> if the policy never retries; otherwise, <see langword="false"/>.</value>
	protected virtual bool IsNullPolicy => false;

	/// <summary>
	/// Creates a new instance of the retry policy under test.
	/// </summary>
	/// <param name="maxRetryAttempts">The maximum retry attempts to configure.</param>
	/// <returns>A configured retry policy instance.</returns>
	protected abstract IDataRequestRetryPolicy CreatePolicy(int maxRetryAttempts);

	/// <summary>
	/// Creates an exception that should trigger a retry for this policy type.
	/// </summary>
	/// <returns>An exception that should be retried.</returns>
	protected abstract Exception CreateRetryableException();

	/// <summary>
	/// Creates an exception that should NOT trigger a retry for this policy type.
	/// </summary>
	/// <returns>An exception that should not be retried.</returns>
	protected abstract Exception CreateNonRetryableException();

	/// <summary>
	/// Verifies the created policy is a non-null <see cref="IDataRequestRetryPolicy"/> instance.
	/// </summary>
	public virtual void Policy_ShouldImplementIDataRequestRetryPolicy()
	{
		var policy = CreatePolicy(3);

		if (policy is null)
		{
			throw new TestFixtureAssertionException(
				"Expected CreatePolicy to return a non-null IDataRequestRetryPolicy instance.");
		}
	}

	/// <summary>
	/// Verifies <see cref="IDataRequestRetryPolicy.MaxRetryAttempts"/> matches the configured value
	/// across a representative range.
	/// </summary>
	public virtual void MaxRetryAttempts_ShouldMatchConfiguredValue()
	{
		foreach (var maxRetryAttempts in new[] { 0, 1, 3, 5, 10 })
		{
			// Null policies always have 0 retries regardless of configuration.
			if (IsNullPolicy && maxRetryAttempts > 0)
			{
				continue;
			}

			var policy = CreatePolicy(maxRetryAttempts);

			if (policy.MaxRetryAttempts != maxRetryAttempts)
			{
				throw new TestFixtureAssertionException(
					$"Expected MaxRetryAttempts to be {maxRetryAttempts} but was {policy.MaxRetryAttempts}.");
			}
		}
	}

	/// <summary>
	/// Verifies <see cref="IDataRequestRetryPolicy.MaxRetryAttempts"/> is never negative.
	/// </summary>
	public virtual void MaxRetryAttempts_ShouldBeNonNegative()
	{
		var policy = CreatePolicy(3);

		if (policy.MaxRetryAttempts < 0)
		{
			throw new TestFixtureAssertionException(
				$"Expected MaxRetryAttempts to be non-negative but was {policy.MaxRetryAttempts}.");
		}
	}

	/// <summary>
	/// Verifies <see cref="IDataRequestRetryPolicy.BaseRetryDelay"/> is never negative.
	/// </summary>
	public virtual void BaseRetryDelay_ShouldBeNonNegative()
	{
		var policy = CreatePolicy(3);

		if (policy.BaseRetryDelay < TimeSpan.Zero)
		{
			throw new TestFixtureAssertionException(
				$"Expected BaseRetryDelay to be non-negative but was {policy.BaseRetryDelay}.");
		}
	}

	/// <summary>
	/// Verifies a null policy reports a zero <see cref="IDataRequestRetryPolicy.BaseRetryDelay"/>.
	/// No-op for non-null policies.
	/// </summary>
	public virtual void BaseRetryDelay_ForNullPolicy_ShouldBeZero()
	{
		if (!IsNullPolicy)
		{
			return;
		}

		var policy = CreatePolicy(0);

		if (policy.BaseRetryDelay != TimeSpan.Zero)
		{
			throw new TestFixtureAssertionException(
				$"Expected a null policy BaseRetryDelay to be zero but was {policy.BaseRetryDelay}.");
		}
	}

	/// <summary>
	/// Verifies <see cref="IDataRequestRetryPolicy.ShouldRetry"/> returns the expected result for a
	/// retryable exception (true for a real policy, false for a null policy).
	/// </summary>
	public virtual void ShouldRetry_WithRetryableException_ReturnsExpectedResult()
	{
		var policy = CreatePolicy(3);
		var exception = CreateRetryableException();

		var result = policy.ShouldRetry(exception);

		if (IsNullPolicy && result)
		{
			throw new TestFixtureAssertionException("A null policy should never retry.");
		}

		if (!IsNullPolicy && !result)
		{
			throw new TestFixtureAssertionException("Expected the policy to retry a retryable exception.");
		}
	}

	/// <summary>
	/// Verifies <see cref="IDataRequestRetryPolicy.ShouldRetry"/> returns <see langword="false"/> for a
	/// non-retryable exception.
	/// </summary>
	public virtual void ShouldRetry_WithNonRetryableException_ReturnsFalse()
	{
		var policy = CreatePolicy(3);
		var exception = CreateNonRetryableException();

		if (policy.ShouldRetry(exception))
		{
			throw new TestFixtureAssertionException(
				"Expected the policy not to retry a non-retryable exception.");
		}
	}

	/// <summary>
	/// Verifies a non-null policy exposes a positive <see cref="IDataRequestRetryPolicy.BaseRetryDelay"/>
	/// for exponential backoff. No-op for null policies.
	/// </summary>
	public virtual void BaseRetryDelay_ForNonNullPolicy_ShouldBePositive()
	{
		if (IsNullPolicy)
		{
			return;
		}

		var policy = CreatePolicy(3);

		if (policy.BaseRetryDelay <= TimeSpan.Zero)
		{
			throw new TestFixtureAssertionException(
				$"Expected a non-null policy BaseRetryDelay to be positive but was {policy.BaseRetryDelay}.");
		}
	}
}

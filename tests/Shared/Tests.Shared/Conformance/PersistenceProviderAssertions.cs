// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Data;

using Excalibur.Data.Abstractions.Persistence;
using Excalibur.Data.Abstractions.Resilience;

namespace Tests.Shared.Conformance;

/// <summary>
/// Assertion helpers for IPersistenceProvider testing.
/// Provides fluent assertions for verifying provider behavior.
/// </summary>
public static class PersistenceProviderAssertions
{
	/// <summary>
	/// Asserts that the provider has valid base properties set.
	/// </summary>
	/// <param name="provider">The provider to verify.</param>
	/// <returns>The provider for fluent chaining.</returns>
	public static IPersistenceProvider ShouldHaveValidBaseProperties(this IPersistenceProvider provider)
	{
		_ = provider.ShouldNotBeNull();
		provider.Name.ShouldNotBeNullOrEmpty("Provider Name should not be null or empty");
		provider.ProviderType.ShouldNotBeNullOrEmpty("Provider ProviderType should not be null or empty");

		var transaction = provider.GetService(typeof(IPersistenceProviderTransaction)) as IPersistenceProviderTransaction;
		_ = transaction.ShouldNotBeNull("Provider should support IPersistenceProviderTransaction");
		_ = transaction.ConnectionString.ShouldNotBeNull("Provider ConnectionString should not be null");
		_ = transaction.RetryPolicy.ShouldNotBeNull("Provider RetryPolicy should not be null");
		return provider;
	}

	/// <summary>
	/// Asserts that the provider is available.
	/// </summary>
	/// <param name="provider">The provider to verify.</param>
	/// <returns>The provider for fluent chaining.</returns>
	public static IPersistenceProvider ShouldBeAvailable(this IPersistenceProvider provider)
	{
		var health = provider.GetService(typeof(IPersistenceProviderHealth)) as IPersistenceProviderHealth;
		_ = health.ShouldNotBeNull("Provider should support IPersistenceProviderHealth");
		health.IsAvailable.ShouldBeTrue("Provider should be available");
		return provider;
	}

	/// <summary>
	/// Asserts that the provider is not available.
	/// </summary>
	/// <param name="provider">The provider to verify.</param>
	/// <returns>The provider for fluent chaining.</returns>
	public static IPersistenceProvider ShouldNotBeAvailable(this IPersistenceProvider provider)
	{
		var health = provider.GetService(typeof(IPersistenceProviderHealth)) as IPersistenceProviderHealth;
		_ = health.ShouldNotBeNull("Provider should support IPersistenceProviderHealth");
		health.IsAvailable.ShouldBeFalse("Provider should not be available");
		return provider;
	}

	/// <summary>
	/// Asserts that the provider has a specific provider type.
	/// </summary>
	/// <param name="provider">The provider to verify.</param>
	/// <param name="expectedType">The expected provider type.</param>
	/// <returns>The provider for fluent chaining.</returns>
	public static IPersistenceProvider ShouldBeProviderType(this IPersistenceProvider provider, string expectedType)
	{
		provider.ProviderType.ShouldBe(expectedType, $"Provider type should be '{expectedType}'");
		return provider;
	}

	/// <summary>
	/// Asserts that the retry policy has the expected max retry attempts.
	/// </summary>
	/// <param name="policy">The retry policy to verify.</param>
	/// <param name="expected">The expected max retry attempts.</param>
	/// <returns>The policy for fluent chaining.</returns>
	public static IDataRequestRetryPolicy ShouldHaveMaxRetryAttempts(this IDataRequestRetryPolicy policy, int expected)
	{
		policy.MaxRetryAttempts.ShouldBe(expected);
		return policy;
	}

	/// <summary>
	/// Asserts that the retry policy has zero max retry attempts (no retries).
	/// </summary>
	/// <param name="policy">The retry policy to verify.</param>
	/// <returns>The policy for fluent chaining.</returns>
	public static IDataRequestRetryPolicy ShouldHaveNoRetries(this IDataRequestRetryPolicy policy)
	{
		policy.MaxRetryAttempts.ShouldBe(0);
		policy.BaseRetryDelay.ShouldBe(TimeSpan.Zero);
		return policy;
	}

	/// <summary>
	/// Asserts that the retry policy has exponential backoff configured.
	/// </summary>
	/// <param name="policy">The retry policy to verify.</param>
	/// <returns>The policy for fluent chaining.</returns>
	public static IDataRequestRetryPolicy ShouldHaveExponentialBackoff(this IDataRequestRetryPolicy policy)
	{
		policy.MaxRetryAttempts.ShouldBeGreaterThan(0);
		policy.BaseRetryDelay.ShouldBeGreaterThan(TimeSpan.Zero);
		return policy;
	}

	/// <summary>
	/// Asserts that the transaction scope has the expected isolation level.
	/// </summary>
	/// <param name="scope">The transaction scope to verify.</param>
	/// <param name="expected">The expected isolation level.</param>
	/// <returns>The scope for fluent chaining.</returns>
	public static ITransactionScope ShouldHaveIsolationLevel(this ITransactionScope scope, IsolationLevel expected)
	{
		scope.IsolationLevel.ShouldBe(expected);
		return scope;
	}

	/// <summary>
	/// Asserts that the metrics dictionary contains the standard provider metrics.
	/// </summary>
	/// <param name="metrics">The metrics dictionary to verify.</param>
	/// <returns>The metrics for fluent chaining.</returns>
	public static IDictionary<string, object> ShouldContainStandardMetrics(this IDictionary<string, object> metrics)
	{
		metrics.ShouldContainKey("Provider");
		metrics.ShouldContainKey("Name");
		metrics.ShouldContainKey("IsAvailable");
		return metrics;
	}

	/// <summary>
	/// Asserts that the provider can create a valid transaction scope.
	/// </summary>
	/// <param name="provider">The provider to verify.</param>
	/// <returns>The provider for fluent chaining.</returns>
	public static IPersistenceProvider ShouldSupportTransactions(this IPersistenceProvider provider)
	{
		var transaction = provider.GetService(typeof(IPersistenceProviderTransaction)) as IPersistenceProviderTransaction;
		_ = transaction.ShouldNotBeNull("Provider should support IPersistenceProviderTransaction");
		using var scope = transaction.CreateTransactionScope();
		_ = scope.ShouldNotBeNull("Provider should create non-null transaction scope");
		_ = scope.ShouldBeAssignableTo<ITransactionScope>();
		return provider;
	}

	/// <summary>
	/// Asserts that the provider can be disposed safely.
	/// </summary>
	/// <param name="provider">The provider to verify.</param>
	public static void ShouldDisposeCleanly(this IPersistenceProvider provider)
	{
		var health = provider.GetService(typeof(IPersistenceProviderHealth)) as IPersistenceProviderHealth;
		Should.NotThrow(provider.Dispose);
		health?.IsAvailable.ShouldBeFalse("Provider should not be available after dispose");
	}

	/// <summary>
	/// Asserts that the provider can be disposed asynchronously safely.
	/// </summary>
	/// <param name="provider">The provider to verify.</param>
	public static async Task ShouldDisposeAsyncCleanly(this IPersistenceProvider provider)
	{
		var health = provider.GetService(typeof(IPersistenceProviderHealth)) as IPersistenceProviderHealth;
		await Should.NotThrowAsync(() => provider.DisposeAsync().AsTask());
		health?.IsAvailable.ShouldBeFalse("Provider should not be available after async dispose");
	}
}

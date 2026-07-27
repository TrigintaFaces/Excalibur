// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

#pragma warning disable IDE0007 // Use implicit type (var)
#pragma warning disable IDE0270 // Null check can be simplified

using System.Data;

using Excalibur.Data.Persistence;
using Excalibur.Data.Resilience;

namespace Excalibur.Testing.Conformance;

/// <summary>
/// Abstract conformance test kit for <see cref="IPersistenceProvider"/> implementations.
/// </summary>
/// <remarks>
/// <para>
/// Inherit from this kit and implement <see cref="CreateProvider"/>, <see cref="ExpectedProviderName"/>,
/// and <see cref="ExpectedProviderType"/> to verify that your <see cref="IPersistenceProvider"/>
/// implementation conforms to the contract, including its <see cref="IPersistenceProviderHealth"/> and
/// <see cref="IPersistenceProviderTransaction"/> sub-services resolved through
/// <see cref="IPersistenceProvider.GetService"/>.
/// </para>
/// <para>
/// The kit exposes plain <c>public virtual</c> methods with no test-framework attributes; add the
/// attributes your test framework requires (for example <c>[Fact]</c>) on thin overrides in your derived
/// class.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// public sealed class MyProviderConformanceTests : PersistenceProviderConformanceTestKit
/// {
///     protected override string ExpectedProviderName => "MyProvider";
///     protected override string ExpectedProviderType => "Document";
///     protected override IPersistenceProvider CreateProvider() => new MyPersistenceProvider(...);
///
///     [Fact] public void Name() => Provider_ShouldHaveExpectedName();
/// }
/// </code>
/// </example>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores",
	Justification = "Test method naming convention")]
public abstract class PersistenceProviderConformanceTestKit
{
	/// <summary>
	/// Gets the expected provider type (for example "SQL", "Document", "KeyValue", "InMemory").
	/// </summary>
	protected abstract string ExpectedProviderType { get; }

	/// <summary>
	/// Gets the expected provider name.
	/// </summary>
	protected abstract string ExpectedProviderName { get; }

	/// <summary>
	/// Creates a new instance of the provider under test.
	/// </summary>
	/// <returns>A configured persistence provider instance.</returns>
	protected abstract IPersistenceProvider CreateProvider();

	private static IPersistenceProviderHealth GetHealth(IPersistenceProvider provider)
	{
		if (provider.GetService(typeof(IPersistenceProviderHealth)) is not IPersistenceProviderHealth health)
		{
			throw new TestFixtureAssertionException("Provider should support IPersistenceProviderHealth.");
		}

		return health;
	}

	private static IPersistenceProviderTransaction GetTransaction(IPersistenceProvider provider)
	{
		if (provider.GetService(typeof(IPersistenceProviderTransaction)) is not IPersistenceProviderTransaction transaction)
		{
			throw new TestFixtureAssertionException("Provider should support IPersistenceProviderTransaction.");
		}

		return transaction;
	}

	/// <summary>Verifies the provider exposes a non-empty <see cref="IPersistenceProvider.Name"/>.</summary>
	public virtual void Provider_ShouldHaveNonNullName()
	{
		using var provider = CreateProvider();

		if (string.IsNullOrEmpty(provider.Name))
		{
			throw new TestFixtureAssertionException("Expected Name to be non-null and non-empty.");
		}
	}

	/// <summary>Verifies <see cref="IPersistenceProvider.Name"/> matches <see cref="ExpectedProviderName"/>.</summary>
	public virtual void Provider_ShouldHaveExpectedName()
	{
		using var provider = CreateProvider();

		if (provider.Name != ExpectedProviderName)
		{
			throw new TestFixtureAssertionException(
				$"Expected Name '{ExpectedProviderName}' but was '{provider.Name}'.");
		}
	}

	/// <summary>Verifies the provider exposes a non-empty <see cref="IPersistenceProvider.ProviderType"/>.</summary>
	public virtual void Provider_ShouldHaveNonNullProviderType()
	{
		using var provider = CreateProvider();

		if (string.IsNullOrEmpty(provider.ProviderType))
		{
			throw new TestFixtureAssertionException("Expected ProviderType to be non-null and non-empty.");
		}
	}

	/// <summary>Verifies <see cref="IPersistenceProvider.ProviderType"/> matches <see cref="ExpectedProviderType"/>.</summary>
	public virtual void Provider_ShouldHaveExpectedProviderType()
	{
		using var provider = CreateProvider();

		if (provider.ProviderType != ExpectedProviderType)
		{
			throw new TestFixtureAssertionException(
				$"Expected ProviderType '{ExpectedProviderType}' but was '{provider.ProviderType}'.");
		}
	}

	/// <summary>Verifies the transaction service exposes a non-null connection string.</summary>
	public virtual void Provider_ShouldHaveNonNullConnectionString()
	{
		using var provider = CreateProvider();
		var transaction = GetTransaction(provider);

		if (transaction.ConnectionString is null)
		{
			throw new TestFixtureAssertionException("Expected a non-null ConnectionString.");
		}
	}

	/// <summary>Verifies the transaction service exposes a non-null <see cref="IDataRequestRetryPolicy"/>.</summary>
	public virtual void Provider_ShouldHaveNonNullRetryPolicy()
	{
		using var provider = CreateProvider();
		var transaction = GetTransaction(provider);

		if (transaction.RetryPolicy is null)
		{
			throw new TestFixtureAssertionException("Expected a non-null RetryPolicy.");
		}
	}

	/// <summary>Verifies <see cref="IPersistenceProviderTransaction.CreateTransactionScope"/> returns a non-null scope.</summary>
	public virtual void CreateTransactionScope_ShouldReturnNonNullScope()
	{
		using var provider = CreateProvider();
		var transaction = GetTransaction(provider);

		using var scope = transaction.CreateTransactionScope();

		if (scope is null)
		{
			throw new TestFixtureAssertionException("Expected a non-null transaction scope.");
		}
	}

	/// <summary>Verifies creating a transaction scope with an isolation level returns a non-null scope.</summary>
	public virtual void CreateTransactionScope_WithIsolationLevel_ShouldReturnScope()
	{
		using var provider = CreateProvider();
		var transaction = GetTransaction(provider);

		using var scope = transaction.CreateTransactionScope(IsolationLevel.Serializable);

		if (scope is null)
		{
			throw new TestFixtureAssertionException("Expected a non-null transaction scope.");
		}
	}

	/// <summary>Verifies creating a transaction scope with a timeout returns a non-null scope.</summary>
	public virtual void CreateTransactionScope_WithTimeout_ShouldReturnScope()
	{
		using var provider = CreateProvider();
		var transaction = GetTransaction(provider);

		using var scope = transaction.CreateTransactionScope(timeout: TimeSpan.FromMinutes(5));

		if (scope is null)
		{
			throw new TestFixtureAssertionException("Expected a non-null transaction scope.");
		}
	}

	/// <summary>Verifies <see cref="IPersistenceProviderHealth.GetMetricsAsync"/> returns a non-null dictionary.</summary>
	public virtual async Task GetMetricsAsync_ShouldReturnNonNullDictionary()
	{
		using var provider = CreateProvider();
		var health = GetHealth(provider);

		var metrics = await health.GetMetricsAsync(CancellationToken.None).ConfigureAwait(false);

		if (metrics is null)
		{
			throw new TestFixtureAssertionException("Expected a non-null metrics dictionary.");
		}
	}

	/// <summary>Verifies the metrics dictionary contains the "Provider" key.</summary>
	public virtual async Task GetMetricsAsync_ShouldContainProviderKey()
	{
		using var provider = CreateProvider();
		var health = GetHealth(provider);

		var metrics = await health.GetMetricsAsync(CancellationToken.None).ConfigureAwait(false);

		if (metrics is null || !metrics.ContainsKey("Provider"))
		{
			throw new TestFixtureAssertionException("Expected the metrics dictionary to contain a 'Provider' key.");
		}
	}

	/// <summary>Verifies <see cref="IDisposable.Dispose"/> does not throw.</summary>
	public virtual void Dispose_ShouldNotThrow()
	{
		var provider = CreateProvider();

		// A failure surfaces as an unhandled exception failing the test.
		provider.Dispose();
	}

	/// <summary>Verifies disposing the provider multiple times does not throw (idempotent disposal).</summary>
	public virtual void Dispose_CalledMultipleTimes_ShouldNotThrow()
	{
		var provider = CreateProvider();

		provider.Dispose();
		provider.Dispose();
	}

	/// <summary>Verifies <see cref="IAsyncDisposable.DisposeAsync"/> does not throw.</summary>
	public virtual async Task DisposeAsync_ShouldNotThrow()
	{
		var provider = CreateProvider();

		await provider.DisposeAsync().ConfigureAwait(false);
	}

	/// <summary>Verifies <see cref="IPersistenceProviderHealth.IsAvailable"/> is false after disposal.</summary>
	public virtual void IsAvailable_AfterDispose_ShouldBeFalse()
	{
		var provider = CreateProvider();
		var health = GetHealth(provider);

		provider.Dispose();

		if (health.IsAvailable)
		{
			throw new TestFixtureAssertionException("Expected IsAvailable to be false after disposal.");
		}
	}

	/// <summary>Verifies the provider is assignable to <see cref="IDisposable"/>.</summary>
	public virtual void Provider_ShouldImplementIDisposable()
	{
		using var provider = CreateProvider();

		if (provider is not IDisposable)
		{
			throw new TestFixtureAssertionException("Expected the provider to implement IDisposable.");
		}
	}

	/// <summary>Verifies the provider is assignable to <see cref="IAsyncDisposable"/>.</summary>
	public virtual void Provider_ShouldImplementIAsyncDisposable()
	{
		using var provider = CreateProvider();

		if (provider is not IAsyncDisposable)
		{
			throw new TestFixtureAssertionException("Expected the provider to implement IAsyncDisposable.");
		}
	}
}

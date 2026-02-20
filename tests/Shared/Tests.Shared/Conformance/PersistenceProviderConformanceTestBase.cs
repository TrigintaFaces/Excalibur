// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Data;

using Excalibur.Data.Abstractions.Persistence;
using Excalibur.Data.Abstractions.Resilience;

namespace Tests.Shared.Conformance;

/// <summary>
/// Base class for IPersistenceProvider conformance tests.
/// Implementations must provide a concrete provider instance for testing.
/// </summary>
/// <remarks>
/// This conformance test kit verifies that persistence provider implementations
/// correctly implement the IPersistenceProvider interface contract.
/// </remarks>
public abstract class PersistenceProviderConformanceTestBase : IDisposable
{
	private bool _disposed;

	/// <summary>
	/// Gets the expected provider type (e.g., "SQL", "Document", "KeyValue", "InMemory").
	/// </summary>
	protected abstract string ExpectedProviderType { get; }

	/// <summary>
	/// Gets the expected provider name.
	/// </summary>
	protected abstract string ExpectedProviderName { get; }

	/// <inheritdoc/>
	public void Dispose()
	{
		Dispose(disposing: true);
		GC.SuppressFinalize(this);
	}

	/// <summary>
	/// Creates a new instance of the provider under test.
	/// </summary>
	/// <returns>A configured persistence provider instance.</returns>
	protected abstract IPersistenceProvider CreateProvider();

	/// <summary>
	/// Gets the health sub-interface from a provider via GetService.
	/// </summary>
	private static IPersistenceProviderHealth GetHealth(IPersistenceProvider provider)
	{
		var health = provider.GetService(typeof(IPersistenceProviderHealth)) as IPersistenceProviderHealth;
		health.ShouldNotBeNull("Provider should support IPersistenceProviderHealth");
		return health;
	}

	/// <summary>
	/// Gets the transaction sub-interface from a provider via GetService.
	/// </summary>
	private static IPersistenceProviderTransaction GetTransaction(IPersistenceProvider provider)
	{
		var transaction = provider.GetService(typeof(IPersistenceProviderTransaction)) as IPersistenceProviderTransaction;
		transaction.ShouldNotBeNull("Provider should support IPersistenceProviderTransaction");
		return transaction;
	}

	#region Required Interface Property Tests

	[Fact]
	public void Provider_ShouldHaveNonNullName()
	{
		// Arrange
		using var provider = CreateProvider();

		// Assert
		provider.Name.ShouldNotBeNullOrEmpty();
	}

	[Fact]
	public void Provider_ShouldHaveExpectedName()
	{
		// Arrange
		using var provider = CreateProvider();

		// Assert
		provider.Name.ShouldBe(ExpectedProviderName);
	}

	[Fact]
	public void Provider_ShouldHaveNonNullProviderType()
	{
		// Arrange
		using var provider = CreateProvider();

		// Assert
		provider.ProviderType.ShouldNotBeNullOrEmpty();
	}

	[Fact]
	public void Provider_ShouldHaveExpectedProviderType()
	{
		// Arrange
		using var provider = CreateProvider();

		// Assert
		provider.ProviderType.ShouldBe(ExpectedProviderType);
	}

	[Fact]
	public void Provider_ShouldHaveNonNullConnectionString()
	{
		// Arrange
		using var provider = CreateProvider();

		// Assert
		var transaction = GetTransaction(provider);
		_ = transaction.ConnectionString.ShouldNotBeNull();
	}

	[Fact]
	public void Provider_ShouldHaveNonNullRetryPolicy()
	{
		// Arrange
		using var provider = CreateProvider();

		// Assert
		var transaction = GetTransaction(provider);
		_ = transaction.RetryPolicy.ShouldNotBeNull();
		_ = transaction.RetryPolicy.ShouldBeAssignableTo<IDataRequestRetryPolicy>();
	}

	#endregion Required Interface Property Tests

	#region Transaction Scope Tests

	[Fact]
	public void CreateTransactionScope_ShouldReturnNonNullScope()
	{
		// Arrange
		using var provider = CreateProvider();
		var transaction = GetTransaction(provider);

		// Act
		using var scope = transaction.CreateTransactionScope();

		// Assert
		_ = scope.ShouldNotBeNull();
		_ = scope.ShouldBeAssignableTo<ITransactionScope>();
	}

	[Fact]
	public void CreateTransactionScope_WithIsolationLevel_ShouldReturnScope()
	{
		// Arrange
		using var provider = CreateProvider();
		var transaction = GetTransaction(provider);

		// Act
		using var scope = transaction.CreateTransactionScope(IsolationLevel.Serializable);

		// Assert
		_ = scope.ShouldNotBeNull();
	}

	[Fact]
	public void CreateTransactionScope_WithTimeout_ShouldReturnScope()
	{
		// Arrange
		using var provider = CreateProvider();
		var transaction = GetTransaction(provider);

		// Act
		using var scope = transaction.CreateTransactionScope(timeout: TimeSpan.FromMinutes(5));

		// Assert
		_ = scope.ShouldNotBeNull();
	}

	#endregion Transaction Scope Tests

	#region Metrics Tests

	[Fact]
	public async Task GetMetricsAsync_ShouldReturnNonNullDictionary()
	{
		// Arrange
		using var provider = CreateProvider();
		var health = GetHealth(provider);

		// Act
		var metrics = await health.GetMetricsAsync(CancellationToken.None);

		// Assert
		_ = metrics.ShouldNotBeNull();
	}

	[Fact]
	public async Task GetMetricsAsync_ShouldContainProviderKey()
	{
		// Arrange
		using var provider = CreateProvider();
		var health = GetHealth(provider);

		// Act
		var metrics = await health.GetMetricsAsync(CancellationToken.None);

		// Assert
		metrics.ShouldContainKey("Provider");
	}

	#endregion Metrics Tests

	#region Disposal Tests

	[Fact]
	public void Dispose_ShouldNotThrow()
	{
		// Arrange
		var provider = CreateProvider();

		// Act & Assert
		Should.NotThrow(provider.Dispose);
	}

	[Fact]
	public void Dispose_CalledMultipleTimes_ShouldNotThrow()
	{
		// Arrange
		var provider = CreateProvider();

		// Act & Assert
		Should.NotThrow(provider.Dispose);
		Should.NotThrow(provider.Dispose);
	}

	[Fact]
	public async Task DisposeAsync_ShouldNotThrow()
	{
		// Arrange
		var provider = CreateProvider();

		// Act & Assert
		await Should.NotThrowAsync(() => provider.DisposeAsync().AsTask());
	}

	[Fact]
	public void IsAvailable_AfterDispose_ShouldBeFalse()
	{
		// Arrange
		var provider = CreateProvider();
		var health = GetHealth(provider);

		// Act
		provider.Dispose();

		// Assert
		health.IsAvailable.ShouldBeFalse();
	}

	#endregion Disposal Tests

	#region Interface Implementation Tests

	[Fact]
	public void Provider_ShouldImplementIPersistenceProvider()
	{
		// Arrange
		using var provider = CreateProvider();

		// Assert
		_ = provider.ShouldBeAssignableTo<IPersistenceProvider>();
	}

	[Fact]
	public void Provider_ShouldImplementIDisposable()
	{
		// Arrange
		using var provider = CreateProvider();

		// Assert
		_ = provider.ShouldBeAssignableTo<IDisposable>();
	}

	[Fact]
	public void Provider_ShouldImplementIAsyncDisposable()
	{
		// Arrange
		using var provider = CreateProvider();

		// Assert
		_ = provider.ShouldBeAssignableTo<IAsyncDisposable>();
	}

	#endregion Interface Implementation Tests

	/// <summary>
	/// Releases unmanaged resources.
	/// </summary>
	/// <param name="disposing">True if disposing managed resources.</param>
	protected virtual void Dispose(bool disposing)
	{
		if (_disposed)
			return;
		_disposed = true;
	}
}

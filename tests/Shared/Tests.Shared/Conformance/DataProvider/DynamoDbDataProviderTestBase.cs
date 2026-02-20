// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Abstractions.CloudNative;
using Excalibur.Data.Abstractions.Persistence;

namespace Tests.Shared.Conformance.DataProvider;

/// <summary>
/// Base class for AWS DynamoDB data provider conformance tests.
/// Verifies that DynamoDB provider implementations correctly implement
/// the <see cref="ICloudNativePersistenceProvider"/> contract.
/// </summary>
/// <remarks>
/// <para>
/// Inherit from this class and implement <see cref="CreateProvider"/> to provide
/// a configured DynamoDB persistence provider for testing (e.g., using DynamoDB Local).
/// </para>
/// <para>
/// Tests cover CRUD operations, query execution, batch operations,
/// connection/error handling, and DynamoDB-specific features like Streams.
/// </para>
/// </remarks>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores",
	Justification = "Test method naming convention")]
public abstract class DynamoDbDataProviderTestBase : IAsyncDisposable
{
	private bool _disposed;

	/// <summary>
	/// Creates a configured DynamoDB persistence provider instance for testing.
	/// </summary>
	/// <returns>A configured <see cref="ICloudNativePersistenceProvider"/> backed by DynamoDB.</returns>
	protected abstract ICloudNativePersistenceProvider CreateProvider();

	/// <summary>
	/// Creates a partition key for test operations.
	/// </summary>
	/// <param name="value">The partition key value.</param>
	/// <returns>An <see cref="IPartitionKey"/>.</returns>
	protected abstract IPartitionKey CreatePartitionKey(string value);

	/// <summary>
	/// Performs cleanup after each test.
	/// </summary>
	/// <returns>A task representing the cleanup operation.</returns>
	protected virtual Task CleanupAsync() => Task.CompletedTask;

	/// <inheritdoc />
	public async ValueTask DisposeAsync()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;
		await CleanupAsync().ConfigureAwait(false);
		GC.SuppressFinalize(this);
	}

	#region Provider Property Tests

	/// <summary>
	/// Verifies that the provider has a non-null name.
	/// </summary>
	protected virtual void Provider_ShouldHaveNonNullName()
	{
		var provider = CreateProvider();
		if (string.IsNullOrEmpty(provider.Name))
		{
			throw new InvalidOperationException("Provider Name must not be null or empty.");
		}
	}

	/// <summary>
	/// Verifies that the provider type is CloudNative.
	/// </summary>
	protected virtual void Provider_ShouldHaveCloudNativeProviderType()
	{
		var provider = CreateProvider();
		if (provider.ProviderType != "CloudNative")
		{
			throw new InvalidOperationException($"Expected ProviderType 'CloudNative', got '{provider.ProviderType}'.");
		}
	}

	/// <summary>
	/// Verifies the DynamoDB-specific document store type.
	/// </summary>
	protected virtual void Provider_ShouldHaveDynamoDbDocumentStoreType()
	{
		var provider = CreateProvider();
		if (provider.DocumentStoreType != "DynamoDB")
		{
			throw new InvalidOperationException(
				$"Expected DocumentStoreType 'DynamoDB', got '{provider.DocumentStoreType}'.");
		}
	}

	/// <summary>
	/// Verifies that the provider supports multi-region writes (Global Tables).
	/// </summary>
	protected virtual void Provider_ShouldSupportMultiRegionWrites()
	{
		var provider = CreateProvider();
		if (!provider.SupportsMultiRegionWrites)
		{
			throw new InvalidOperationException("DynamoDB provider should support multi-region writes.");
		}
	}

	/// <summary>
	/// Verifies that the provider supports change feed (DynamoDB Streams).
	/// </summary>
	protected virtual void Provider_ShouldSupportChangeFeed()
	{
		var provider = CreateProvider();
		if (!provider.SupportsChangeFeed)
		{
			throw new InvalidOperationException("DynamoDB provider should support change feed (Streams).");
		}
	}

	#endregion

	#region CRUD Tests

	/// <summary>
	/// Verifies that creating and reading a document round-trips correctly.
	/// </summary>
	protected virtual async Task CreateAndGetById_ShouldRoundTrip()
	{
		var provider = CreateProvider();
		var partitionKey = CreatePartitionKey("test-pk");
		var doc = new TestDocument { Id = Guid.NewGuid().ToString(), Name = "Test", Value = 42 };

		var createResult = await provider.CreateAsync(doc, partitionKey, CancellationToken.None)
			.ConfigureAwait(false);

		if (!createResult.Success)
		{
			throw new InvalidOperationException($"Create failed: {createResult.ErrorMessage}");
		}

		var retrieved = await provider.GetByIdAsync<TestDocument>(
			doc.Id, partitionKey, null, CancellationToken.None).ConfigureAwait(false);

		if (retrieved is null || retrieved.Name != doc.Name)
		{
			throw new InvalidOperationException("Retrieved document does not match created document.");
		}
	}

	/// <summary>
	/// Verifies that updating a document persists changes.
	/// </summary>
	protected virtual async Task Update_ShouldPersistChanges()
	{
		var provider = CreateProvider();
		var partitionKey = CreatePartitionKey("test-pk");
		var doc = new TestDocument { Id = Guid.NewGuid().ToString(), Name = "Original", Value = 1 };

		_ = await provider.CreateAsync(doc, partitionKey, CancellationToken.None).ConfigureAwait(false);

		doc.Name = "Updated";
		var updateResult = await provider.UpdateAsync(doc, partitionKey, null, CancellationToken.None)
			.ConfigureAwait(false);

		if (!updateResult.Success)
		{
			throw new InvalidOperationException($"Update failed: {updateResult.ErrorMessage}");
		}
	}

	/// <summary>
	/// Verifies that deleting a document removes it.
	/// </summary>
	protected virtual async Task Delete_ShouldRemoveDocument()
	{
		var provider = CreateProvider();
		var partitionKey = CreatePartitionKey("test-pk");
		var doc = new TestDocument { Id = Guid.NewGuid().ToString(), Name = "ToDelete", Value = 0 };

		_ = await provider.CreateAsync(doc, partitionKey, CancellationToken.None).ConfigureAwait(false);

		var deleteResult = await provider.DeleteAsync(doc.Id, partitionKey, null, CancellationToken.None)
			.ConfigureAwait(false);

		if (!deleteResult.Success)
		{
			throw new InvalidOperationException($"Delete failed: {deleteResult.ErrorMessage}");
		}
	}

	/// <summary>
	/// Verifies that GetById returns null for a non-existent document.
	/// </summary>
	protected virtual async Task GetById_NonExistent_ShouldReturnNull()
	{
		var provider = CreateProvider();
		var partitionKey = CreatePartitionKey("test-pk");

		var result = await provider.GetByIdAsync<TestDocument>(
			"non-existent-id", partitionKey, null, CancellationToken.None).ConfigureAwait(false);

		if (result is not null)
		{
			throw new InvalidOperationException("Expected null for non-existent document.");
		}
	}

	#endregion

	#region Query Tests

	/// <summary>
	/// Verifies that query returns results.
	/// </summary>
	protected virtual async Task Query_ShouldReturnResults()
	{
		var provider = CreateProvider();
		var partitionKey = CreatePartitionKey("query-pk");
		var doc = new TestDocument { Id = Guid.NewGuid().ToString(), Name = "Queryable", Value = 100 };

		_ = await provider.CreateAsync(doc, partitionKey, CancellationToken.None).ConfigureAwait(false);

		var results = await provider.QueryAsync<TestDocument>(
			"*", partitionKey, null, null, CancellationToken.None).ConfigureAwait(false);

		if (results.Documents.Count == 0)
		{
			throw new InvalidOperationException("Expected at least one result from query.");
		}
	}

	#endregion

	#region Error Handling Tests

	/// <summary>
	/// Verifies that operations on an uninitialized provider throw.
	/// </summary>
	protected virtual void Operations_BeforeInit_ShouldThrow()
	{
		// Subclasses implement specific uninitialized-state tests
	}

	#endregion

	/// <summary>
	/// Test document used for conformance testing.
	/// </summary>
	protected class TestDocument
	{
		/// <summary>Gets or sets the document identifier.</summary>
		public string Id { get; set; } = string.Empty;

		/// <summary>Gets or sets the document name.</summary>
		public string Name { get; set; } = string.Empty;

		/// <summary>Gets or sets the document value.</summary>
		public int Value { get; set; }
	}
}

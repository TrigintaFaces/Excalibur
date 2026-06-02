// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;
using System.Text.Json.Serialization;

using Elastic.Clients.Elasticsearch;

using Excalibur.Data.ElasticSearch.Projections;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Data.Tests.ElasticSearch.Projections;

/// <summary>
/// Unit tests for <see cref="ElasticSearchProjectionStore{TProjection}"/>.
/// Covers constructor validation, argument guards, disposal behavior, field path
/// generation (flat storage — no <c>data.</c> prefix), and options name resolution.
/// </summary>
/// <remarks>
/// <para>
/// After the Sprint 827 envelope removal refactor, projections are stored flat as
/// <c>TProjection</c> — no wrapper document. These tests verify the behavioral
/// contracts that changed: field paths, query building, and index naming.
/// </para>
/// <para>
/// Tests that exercise actual ElasticSearch HTTP calls are in integration tests
/// (<c>Excalibur.Integration.Tests</c>). These unit tests verify the defensive
/// coding layer and static helper behavior without network dependencies.
/// </para>
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, "Data")]
[Trait(TraitNames.Feature, TestFeatures.Projections)]
public sealed class ElasticSearchProjectionStoreShould
{
	#region Constructor Validation

	[Fact]
	public void ThrowWhenOptionsMonitorIsNull_DefaultConstructor()
	{
		// Arrange
		var logger = NullLogger<ElasticSearchProjectionStore<TestProjection>>.Instance;

		// Act & Assert
		Should.Throw<ArgumentNullException>(
			() => new ElasticSearchProjectionStore<TestProjection>(
				(IOptionsMonitor<ElasticSearchProjectionStoreOptions>)null!, logger));
	}

	[Fact]
	public void ThrowWhenLoggerIsNull_DefaultConstructor()
	{
		// Arrange
		var optionsMonitor = CreateOptionsMonitor();

		// Act & Assert
		Should.Throw<ArgumentNullException>(
			() => new ElasticSearchProjectionStore<TestProjection>(
				optionsMonitor, null!));
	}

	[Fact]
	public void ThrowWhenClientIsNull_ClientConstructor()
	{
		// Arrange
		var optionsMonitor = CreateOptionsMonitor();
		var logger = NullLogger<ElasticSearchProjectionStore<TestProjection>>.Instance;

		// Act & Assert
		Should.Throw<ArgumentNullException>(
			() => new ElasticSearchProjectionStore<TestProjection>(
				(ElasticsearchClient)null!, optionsMonitor, logger));
	}

	[Fact]
	public void ThrowWhenOptionsMonitorIsNull_ClientConstructor()
	{
		// Arrange
		var client = new ElasticsearchClient();
		var logger = NullLogger<ElasticSearchProjectionStore<TestProjection>>.Instance;

		// Act & Assert
		Should.Throw<ArgumentNullException>(
			() => new ElasticSearchProjectionStore<TestProjection>(
				client, null!, logger));
	}

	[Fact]
	public void ThrowWhenLoggerIsNull_ClientConstructor()
	{
		// Arrange
		var client = new ElasticsearchClient();
		var optionsMonitor = CreateOptionsMonitor();

		// Act & Assert
		Should.Throw<ArgumentNullException>(
			() => new ElasticSearchProjectionStore<TestProjection>(
				client, optionsMonitor, null!));
	}

	[Fact]
	public void ConstructSuccessfully_WithValidParameters()
	{
		// Arrange
		var client = new ElasticsearchClient();
		var optionsMonitor = CreateOptionsMonitor();
		var logger = NullLogger<ElasticSearchProjectionStore<TestProjection>>.Instance;

		// Act
		var store = new ElasticSearchProjectionStore<TestProjection>(
			client, optionsMonitor, logger);

		// Assert
		store.ShouldNotBeNull();
	}

	[Fact]
	public void ThrowWhenOptionsValidationFails()
	{
		// Arrange — NodeUri is required when NodeUris is not set
		var client = new ElasticsearchClient();
		var options = new ElasticSearchProjectionStoreOptions { NodeUri = "" };
		var optionsMonitor = CreateOptionsMonitor(options);
		var logger = NullLogger<ElasticSearchProjectionStore<TestProjection>>.Instance;

		// Act & Assert
		Should.Throw<InvalidOperationException>(
			() => new ElasticSearchProjectionStore<TestProjection>(
				client, optionsMonitor, logger));
	}

	#endregion

	#region Options Name Resolution

	[Fact]
	public void UseProjectionTypeNameAsOptionsKey()
	{
		// The store resolves named options by typeof(TProjection).Name
		ElasticSearchProjectionStore<TestProjection>.OptionsName
			.ShouldBe(nameof(TestProjection));
	}

	[Fact]
	public void UseCorrectOptionsKeyForDifferentProjectionTypes()
	{
		ElasticSearchProjectionStore<OrderSummaryProjection>.OptionsName
			.ShouldBe(nameof(OrderSummaryProjection));
	}

	#endregion

	#region GetByIdAsync — Argument Validation

	[Fact]
	public async Task GetByIdAsync_ThrowWhenDisposed()
	{
		// Arrange
		var store = CreateStore();
		await store.DisposeAsync().ConfigureAwait(false);

		// Act & Assert
		await Should.ThrowAsync<ObjectDisposedException>(
			() => store.GetByIdAsync("test-id", CancellationToken.None));
	}

	[Fact]
	public async Task GetByIdAsync_ThrowWhenIdIsNull()
	{
		// Arrange
		var store = CreateStore();

		// Act & Assert
		await Should.ThrowAsync<ArgumentException>(
			() => store.GetByIdAsync(null!, CancellationToken.None));
	}

	[Fact]
	public async Task GetByIdAsync_ThrowWhenIdIsEmpty()
	{
		// Arrange
		var store = CreateStore();

		// Act & Assert
		await Should.ThrowAsync<ArgumentException>(
			() => store.GetByIdAsync("", CancellationToken.None));
	}

	[Fact]
	public async Task GetByIdAsync_ThrowWhenIdIsWhitespace()
	{
		// Arrange
		var store = CreateStore();

		// Act & Assert
		await Should.ThrowAsync<ArgumentException>(
			() => store.GetByIdAsync("   ", CancellationToken.None));
	}

	#endregion

	#region UpsertAsync — Argument Validation

	[Fact]
	public async Task UpsertAsync_ThrowWhenDisposed()
	{
		// Arrange
		var store = CreateStore();
		await store.DisposeAsync().ConfigureAwait(false);

		// Act & Assert
		await Should.ThrowAsync<ObjectDisposedException>(
			() => store.UpsertAsync("test-id", new TestProjection(), CancellationToken.None));
	}

	[Fact]
	public async Task UpsertAsync_ThrowWhenIdIsNull()
	{
		// Arrange
		var store = CreateStore();

		// Act & Assert
		await Should.ThrowAsync<ArgumentException>(
			() => store.UpsertAsync(null!, new TestProjection(), CancellationToken.None));
	}

	[Fact]
	public async Task UpsertAsync_ThrowWhenIdIsEmpty()
	{
		// Arrange
		var store = CreateStore();

		// Act & Assert
		await Should.ThrowAsync<ArgumentException>(
			() => store.UpsertAsync("", new TestProjection(), CancellationToken.None));
	}

	[Fact]
	public async Task UpsertAsync_ThrowWhenProjectionIsNull()
	{
		// Arrange
		var store = CreateStore();

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(
			() => store.UpsertAsync("test-id", null!, CancellationToken.None));
	}

	#endregion

	#region DeleteAsync — Argument Validation

	[Fact]
	public async Task DeleteAsync_ThrowWhenDisposed()
	{
		// Arrange
		var store = CreateStore();
		await store.DisposeAsync().ConfigureAwait(false);

		// Act & Assert
		await Should.ThrowAsync<ObjectDisposedException>(
			() => store.DeleteAsync("test-id", CancellationToken.None));
	}

	[Fact]
	public async Task DeleteAsync_ThrowWhenIdIsNull()
	{
		// Arrange
		var store = CreateStore();

		// Act & Assert
		await Should.ThrowAsync<ArgumentException>(
			() => store.DeleteAsync(null!, CancellationToken.None));
	}

	[Fact]
	public async Task DeleteAsync_ThrowWhenIdIsEmpty()
	{
		// Arrange
		var store = CreateStore();

		// Act & Assert
		await Should.ThrowAsync<ArgumentException>(
			() => store.DeleteAsync("", CancellationToken.None));
	}

	#endregion

	#region QueryAsync — Argument Validation

	[Fact]
	public async Task QueryAsync_ThrowWhenDisposed()
	{
		// Arrange
		var store = CreateStore();
		await store.DisposeAsync().ConfigureAwait(false);

		// Act & Assert
		await Should.ThrowAsync<ObjectDisposedException>(
			() => store.QueryAsync(null, null, CancellationToken.None));
	}

	#endregion

	#region CountAsync — Argument Validation

	[Fact]
	public async Task CountAsync_ThrowWhenDisposed()
	{
		// Arrange
		var store = CreateStore();
		await store.DisposeAsync().ConfigureAwait(false);

		// Act & Assert
		await Should.ThrowAsync<ObjectDisposedException>(
			() => store.CountAsync(null, CancellationToken.None));
	}

	#endregion

	#region DisposeAsync

	[Fact]
	public async Task DisposeAsync_CompleteSuccessfully()
	{
		// Arrange
		var store = CreateStore();

		// Act & Assert — should not throw
		await store.DisposeAsync().ConfigureAwait(false);
	}

	[Fact]
	public async Task DisposeAsync_BeIdempotent()
	{
		// Arrange
		var store = CreateStore();

		// Act — dispose twice
		await store.DisposeAsync().ConfigureAwait(false);
		await store.DisposeAsync().ConfigureAwait(false);

		// Assert — second dispose should be a no-op (no exception)
	}

	[Fact]
	public async Task DisposeAsync_PreventSubsequentOperations()
	{
		// Arrange
		var store = CreateStore();
		await store.DisposeAsync().ConfigureAwait(false);

		// Act & Assert — all operations should throw ObjectDisposedException
		await Should.ThrowAsync<ObjectDisposedException>(
			() => store.GetByIdAsync("id", CancellationToken.None));
		await Should.ThrowAsync<ObjectDisposedException>(
			() => store.UpsertAsync("id", new TestProjection(), CancellationToken.None));
		await Should.ThrowAsync<ObjectDisposedException>(
			() => store.DeleteAsync("id", CancellationToken.None));
		await Should.ThrowAsync<ObjectDisposedException>(
			() => store.QueryAsync(null, null, CancellationToken.None));
		await Should.ThrowAsync<ObjectDisposedException>(
			() => store.CountAsync(null, CancellationToken.None));
	}

	#endregion

	#region Field Path Generation (Flat Storage — No data. Prefix)

	[Fact]
	public void FieldDefinitions_MapPropertiesToCamelCaseJsonNames()
	{
		// The store builds field definitions from TProjection's public properties.
		// After envelope removal, GetFieldPath returns the JSON name directly (no "data." prefix).
		// We verify via reflection that the static FieldDefinitions dictionary is populated correctly.
		var fieldDefinitions = GetFieldDefinitions();

		// TestProjection has: Id, Name, Amount, CreatedAt, IsActive
		fieldDefinitions.ShouldContainKey("Id");
		fieldDefinitions.ShouldContainKey("Name");
		fieldDefinitions.ShouldContainKey("Amount");
		fieldDefinitions.ShouldContainKey("CreatedAt");
		fieldDefinitions.ShouldContainKey("IsActive");
	}

	[Fact]
	public void FieldDefinitions_UseCaseInsensitiveComparer()
	{
		// BuildFieldDefinitions uses StringComparer.OrdinalIgnoreCase, so
		// "Id" and "id" are the same key — TryAdd for camelCase is a no-op
		// when the PascalCase key already exists. Lookups by either case succeed.
		var storeType = typeof(ElasticSearchProjectionStore<TestProjection>);
		var field = storeType.GetField("FieldDefinitions", BindingFlags.NonPublic | BindingFlags.Static);
		field.ShouldNotBeNull();
		var value = field.GetValue(null);
		value.ShouldNotBeNull();

		// Use the actual IReadOnlyDictionary interface to verify case-insensitive lookup
		var dictType = value.GetType();
		var containsKeyMethod = dictType.GetMethod("ContainsKey");
		containsKeyMethod.ShouldNotBeNull();

		// PascalCase lookup
		((bool)containsKeyMethod.Invoke(value, ["Id"])!).ShouldBeTrue();
		// camelCase lookup — works because of OrdinalIgnoreCase
		((bool)containsKeyMethod.Invoke(value, ["id"])!).ShouldBeTrue();
		((bool)containsKeyMethod.Invoke(value, ["createdAt"])!).ShouldBeTrue();
		((bool)containsKeyMethod.Invoke(value, ["CreatedAt"])!).ShouldBeTrue();
	}

	[Fact]
	public void FieldDefinitions_ResolveJsonPropertyNameAttribute()
	{
		// TestProjection.CustomName has [JsonPropertyName("custom_field")]
		var fieldDefinitions = GetFieldDefinitionsFor<JsonAnnotatedProjection>();

		fieldDefinitions.ShouldContainKey("CustomName");
		fieldDefinitions.ShouldContainKey("custom_field");
	}

	[Fact]
	public void FieldDefinitions_StoreJsonNameDirectly_NoDataPrefix()
	{
		// After envelope removal, field paths should NOT have "data." prefix.
		// GetFieldPath returns fieldDefinition.JsonName directly.
		// We verify by inspecting the JsonName property of stored FieldDefinitions.
		var storeType = typeof(ElasticSearchProjectionStore<TestProjection>);
		var field = storeType.GetField("FieldDefinitions", BindingFlags.NonPublic | BindingFlags.Static);
		field.ShouldNotBeNull();
		var definitions = field.GetValue(null);
		definitions.ShouldNotBeNull();

		// Access the indexer to get a specific field definition
		var dictType = definitions.GetType();
		var itemProperty = dictType.GetProperty("Item");
		itemProperty.ShouldNotBeNull();

		var nameDef = itemProperty.GetValue(definitions, ["Name"]);
		nameDef.ShouldNotBeNull();

		// ProjectionFieldDefinition is a record with JsonName property
		var jsonNameProp = nameDef.GetType().GetProperty("JsonName");
		jsonNameProp.ShouldNotBeNull();
		var jsonName = jsonNameProp.GetValue(nameDef) as string;

		// CRITICAL: After envelope removal, the stored JSON name is the camelCase
		// property name — NOT "data.name". GetFieldPath returns this directly.
		jsonName.ShouldBe("name");
		jsonName.ShouldNotStartWith("data.");
	}

	[Fact]
	public void FieldDefinitions_ClassifyFieldTypesCorrectly()
	{
		// Verify the type classification: String, Numeric, Date, Bool
		var storeType = typeof(ElasticSearchProjectionStore<TestProjection>);
		var field = storeType.GetField("FieldDefinitions", BindingFlags.NonPublic | BindingFlags.Static);
		field.ShouldNotBeNull();
		var definitions = field.GetValue(null)!;
		var dictType = definitions.GetType();
		var itemProperty = dictType.GetProperty("Item")!;

		// Helper to get FieldType enum value as string
		string GetFieldTypeName(string key)
		{
			var def = itemProperty.GetValue(definitions, [key])!;
			var fieldTypeProp = def.GetType().GetProperty("FieldType")!;
			return fieldTypeProp.GetValue(def)!.ToString()!;
		}

		GetFieldTypeName("Name").ShouldBe("String");      // string -> String
		GetFieldTypeName("Amount").ShouldBe("Numeric");    // decimal -> Numeric
		GetFieldTypeName("CreatedAt").ShouldBe("Date");    // DateTime -> Date
		GetFieldTypeName("IsActive").ShouldBe("Bool");     // bool -> Bool
	}

	#endregion

	#region Index Naming Convention

	[Fact]
	public void IndexName_FollowsProjectionTypeConvention()
	{
		// The index name uses ElasticSearchProjectionIndexConvention
		var options = new ElasticSearchProjectionStoreOptions
		{
			IndexPrefix = "projections"
		};

		var indexName = ElasticSearchProjectionIndexConvention.GetIndexName<TestProjection>(options);

		indexName.ShouldBe("projections-testprojection");
	}

	[Fact]
	public void IndexName_UsesCustomIndexNameWhenSet()
	{
		var options = new ElasticSearchProjectionStoreOptions
		{
			IndexPrefix = "my-app",
			IndexName = "orders"
		};

		var indexName = ElasticSearchProjectionIndexConvention.GetIndexName<TestProjection>(options);

		indexName.ShouldBe("my-app-orders");
	}

	[Fact]
	public void IndexName_OmitsPrefixWhenEmpty()
	{
		var options = new ElasticSearchProjectionStoreOptions
		{
			IndexPrefix = ""
		};

		var indexName = ElasticSearchProjectionIndexConvention.GetIndexName<TestProjection>(options);

		indexName.ShouldBe("testprojection");
	}

	#endregion

	#region Helpers

	private static ElasticSearchProjectionStore<TestProjection> CreateStore()
	{
		var client = new ElasticsearchClient(
			new ElasticsearchClientSettings(new Uri("http://localhost:9200")));
		var optionsMonitor = CreateOptionsMonitor();
		var logger = NullLogger<ElasticSearchProjectionStore<TestProjection>>.Instance;
		return new ElasticSearchProjectionStore<TestProjection>(client, optionsMonitor, logger);
	}

	private static IOptionsMonitor<ElasticSearchProjectionStoreOptions> CreateOptionsMonitor(
		ElasticSearchProjectionStoreOptions? options = null)
	{
		options ??= new ElasticSearchProjectionStoreOptions
		{
			NodeUri = "http://localhost:9200",
			IndexPrefix = "test-projections",
			CreateIndexOnInitialize = false
		};

		var monitor = A.Fake<IOptionsMonitor<ElasticSearchProjectionStoreOptions>>();
		A.CallTo(() => monitor.Get(A<string>._)).Returns(options);
		return monitor;
	}

	/// <summary>
	/// Gets the private static FieldDefinitions dictionary via reflection.
	/// </summary>
	private static IDictionary<string, object> GetFieldDefinitions()
	{
		return GetFieldDefinitionsFor<TestProjection>();
	}

	/// <summary>
	/// Gets the private static FieldDefinitions dictionary for a given projection type via reflection.
	/// </summary>
	private static IDictionary<string, object> GetFieldDefinitionsFor<TProjection>()
		where TProjection : class
	{
		var storeType = typeof(ElasticSearchProjectionStore<TProjection>);
		var field = storeType.GetField("FieldDefinitions", BindingFlags.NonPublic | BindingFlags.Static);
		field.ShouldNotBeNull("FieldDefinitions static field should exist");

		var value = field.GetValue(null);
		value.ShouldNotBeNull();

		// FieldDefinitions is IReadOnlyDictionary<string, ProjectionFieldDefinition>
		// where ProjectionFieldDefinition is a private nested record.
		// We cast to non-generic IDictionary for key inspection.
		var dict = new Dictionary<string, object>();
		var enumerableType = value.GetType();
		var keysProperty = enumerableType.GetProperty("Keys");
		keysProperty.ShouldNotBeNull();
		var keys = (IEnumerable<string>)keysProperty.GetValue(value)!;
		foreach (var key in keys)
		{
			dict[key] = key; // We only need to verify key presence
		}

		return dict;
	}

	#endregion

	#region Test Projection Types

	/// <summary>
	/// A simple test projection with common property types for verifying
	/// field definition building and type classification.
	/// </summary>
	private sealed class TestProjection
	{
		public string Id { get; init; } = string.Empty;
		public string Name { get; init; } = string.Empty;
		public decimal Amount { get; init; }
		public DateTime CreatedAt { get; init; }
		public bool IsActive { get; init; }
	}

	/// <summary>
	/// A projection with <see cref="JsonPropertyNameAttribute"/> to verify
	/// custom JSON name resolution in field definitions.
	/// </summary>
	private sealed class JsonAnnotatedProjection
	{
		public string Id { get; init; } = string.Empty;

		[JsonPropertyName("custom_field")]
		public string CustomName { get; init; } = string.Empty;
	}

	/// <summary>
	/// Separate projection type for verifying OptionsName resolution varies per TProjection.
	/// </summary>
	private sealed class OrderSummaryProjection
	{
		public string OrderId { get; init; } = string.Empty;
		public decimal Total { get; init; }
	}

	#endregion
}

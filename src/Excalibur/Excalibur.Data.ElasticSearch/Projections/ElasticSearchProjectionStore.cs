// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.Mapping;
using Elastic.Clients.Elasticsearch.QueryDsl;
using Elastic.Transport;

using Excalibur.Data.ElasticSearch.Diagnostics;
using Excalibur.Data.ElasticSearch.Exceptions;
using Excalibur.EventSourcing;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Data.ElasticSearch.Projections;

#pragma warning disable CS8604 // Possible null reference argument -- Elastic client API nullability annotations are overly strict for query builder lambdas

/// <summary>
/// ElasticSearch implementation of <see cref="IProjectionStore{TProjection}"/>.
/// </summary>
/// <remarks>
/// <para>
/// Provides projection storage using ElasticSearch indices with JSON documents.
/// Each projection type gets a dedicated index (via <see cref="ElasticSearchProjectionIndexConvention"/>)
/// for optimal query performance. The projection is stored flat as the document root —
/// no envelope wrapper — so custom repositories using <see cref="ElasticRepositoryBase{TDocument}"/>
/// can query the same index with natural field names.
/// </para>
/// <para>
/// Supports dictionary-based filters translated to ElasticSearch Query DSL.
/// </para>
/// <para>
/// Each projection type resolves its own named options instance keyed by
/// <c>typeof(TProjection).Name</c>, allowing multiple projection stores to
/// coexist with independent configurations.
/// </para>
/// </remarks>
/// <typeparam name="TProjection">The projection type to store.</typeparam>
public sealed partial class ElasticSearchProjectionStore<
	[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.Interfaces)] TProjection> : IProjectionStore<TProjection>,
	IPositionedProjectionStore<TProjection>,
	IAsyncDisposable
	where TProjection : class
{
	private static readonly IReadOnlyDictionary<string, ProjectionFieldDefinition> FieldDefinitions =
		BuildFieldDefinitions();

	/// <summary>
	/// The named options key used for this projection type.
	/// </summary>
	internal static readonly string OptionsName = typeof(TProjection).Name;

	private readonly ElasticSearchProjectionStoreOptions _options;
	private readonly ILogger<ElasticSearchProjectionStore<TProjection>> _logger;
	private readonly string _projectionType;
	private readonly string _indexName;
	private ElasticsearchClient? _client;
	// Serialises first-time initialisation. Without it concurrent first callers each run the
	// provisioning below, and where more than one field is assigned a second caller can observe
	// a partly-built state and dereference null. Same defect class as the MongoDB stores.
	private readonly SemaphoreSlim _initLock = new(1, 1);

	// volatile: read on the fast path outside the lock.
	private volatile bool _initialized;
	private volatile bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="ElasticSearchProjectionStore{TProjection}"/> class
	/// using named options resolved via <see cref="IOptionsMonitor{TOptions}"/>.
	/// </summary>
	/// <param name="optionsMonitor">The options monitor for named options resolution.</param>
	/// <param name="logger">The logger instance.</param>
	public ElasticSearchProjectionStore(
		IOptionsMonitor<ElasticSearchProjectionStoreOptions> optionsMonitor,
		ILogger<ElasticSearchProjectionStore<TProjection>> logger)
	{
		ArgumentNullException.ThrowIfNull(optionsMonitor);
		ArgumentNullException.ThrowIfNull(logger);

		_options = optionsMonitor.Get(OptionsName);
		_options.Validate();
		_logger = logger;
		_projectionType = typeof(TProjection).Name;
		_indexName = ElasticSearchProjectionIndexConvention.GetIndexName(_options, _projectionType);
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="ElasticSearchProjectionStore{TProjection}"/> class with an existing client
	/// using named options resolved via <see cref="IOptionsMonitor{TOptions}"/>.
	/// </summary>
	/// <param name="client">An existing ElasticSearch client.</param>
	/// <param name="optionsMonitor">The options monitor for named options resolution.</param>
	/// <param name="logger">The logger instance.</param>
	public ElasticSearchProjectionStore(
		ElasticsearchClient client,
		IOptionsMonitor<ElasticSearchProjectionStoreOptions> optionsMonitor,
		ILogger<ElasticSearchProjectionStore<TProjection>> logger)
	{
		ArgumentNullException.ThrowIfNull(client);
		ArgumentNullException.ThrowIfNull(optionsMonitor);
		ArgumentNullException.ThrowIfNull(logger);

		_client = client;
		_options = optionsMonitor.Get(OptionsName);
		_options.Validate();
		_logger = logger;
		_projectionType = typeof(TProjection).Name;
		_indexName = ElasticSearchProjectionIndexConvention.GetIndexName(_options, _projectionType);
	}

	private enum ProjectionFieldType
	{
		String,
		Numeric,
		Date,
		Bool,
		Unknown
	}

	private enum RangeOperator
	{
		GreaterThan,
		GreaterThanOrEqual,
		LessThan,
		LessThanOrEqual
	}

	/// <inheritdoc/>
	[RequiresUnreferencedCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	[RequiresDynamicCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	public async Task<TProjection?> GetByIdAsync(
		string id,
		CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		ArgumentException.ThrowIfNullOrWhiteSpace(id);

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var response = await _client!
			.GetAsync<TProjection>(_indexName, id, cancellationToken)
			.ConfigureAwait(false);

		if (response is { IsValidResponse: true, Found: true } && response.Source is not null)
		{
			return response.Source;
		}

		if (!response.Found || response.ApiCallDetails?.HttpStatusCode == (int)HttpStatusCode.NotFound)
		{
			return null;
		}

		var errorMessage = response.ApiCallDetails?.ToString() ?? "Unknown error";
		_logger.LogError(
			"Failed to get projection {ProjectionType}/{Id} from index {IndexName}: {Error}",
			_projectionType,
			id,
			_indexName,
			errorMessage);
		throw new ElasticsearchGetByIdException(
			id,
			typeof(TProjection),
			errorMessage,
			response.ApiCallDetails?.OriginalException);
	}

	/// <inheritdoc/>
	[RequiresUnreferencedCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	[RequiresDynamicCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	public async Task UpsertAsync(
		string id,
		TProjection projection,
		CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		ArgumentException.ThrowIfNullOrWhiteSpace(id);
		ArgumentNullException.ThrowIfNull(projection);

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		// IndexAsync with the same id performs an upsert (insert or replace).
		//
		// THE FIELD IS WRITTEN, NOT OMITTED, AND THAT IS THE CHANGE. Indexing replaces the whole
		// document, so a position the row used to carry vanishes with it -- and indexing the projection
		// DIRECTLY, as this call used to, could not carry one at all. The row then read back exactly
		// like a row that never had a position, and those two must be treated OPPOSITELY: a
		// never-positioned row IS a complete fold and is adoptable, whereas a row whose state was just
		// replaced by a value this store cannot relate to the stream is not. Adopting the second stamps
		// a position onto a state that does not contain that prefix, and every event below it is then
		// silently missing from the read model forever.
		//
		// The sentinel rides the same single index operation as the state, so there is no window in
		// which a destroyed position is recorded as a never-established one.
		var response = await _client!
			.IndexAsync(
				BuildDocument(projection, ProjectionPosition.Unplaceable),
				r => r.Index(_indexName).Id(id),
				cancellationToken)
			.ConfigureAwait(false);

		if (!response.IsValidResponse)
		{
			var errorMessage = response.ApiCallDetails?.ToString() ?? "Unknown error";
			_logger.LogError(
				"Failed to upsert projection {ProjectionType}/{Id} in index {IndexName}: {Error}",
				_projectionType,
				id,
				_indexName,
				errorMessage);
			throw new ElasticsearchIndexingException(
				_indexName,
				typeof(TProjection),
				errorMessage,
				response.ApiCallDetails?.OriginalException);
		}

		LogUpserted(_projectionType, id);
	}

	/// <inheritdoc />
	/// <remarks>
	/// The same single index operation as <see cref="UpsertAsync"/>, differing only in what it asserts:
	/// this state IS a complete fold, so a later positioned writer may adopt the row. Unconditional on
	/// purpose -- the caller is claiming completeness, not a place in the stream, so there is no
	/// position for a condition to be written against.
	/// </remarks>
	[RequiresUnreferencedCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	[RequiresDynamicCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	public async Task UpsertUnnumberedAsync(
		string id,
		TProjection projection,
		CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		ArgumentException.ThrowIfNullOrWhiteSpace(id);
		ArgumentNullException.ThrowIfNull(projection);

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var response = await _client!
			.IndexAsync(
				BuildDocument(projection, ProjectionPosition.Unnumbered),
				r => r.Index(_indexName).Id(id),
				cancellationToken)
			.ConfigureAwait(false);

		if (!response.IsValidResponse)
		{
			throw IndexingFailure(response);
		}

		LogUpserted(_projectionType, id);
	}

	/// <inheritdoc/>
	public async Task DeleteAsync(
		string id,
		CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		ArgumentException.ThrowIfNullOrWhiteSpace(id);

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var deleteRequest = new DeleteRequest(_indexName, new Id(id));
		var response = await _client!
			.DeleteAsync(deleteRequest, cancellationToken)
			.ConfigureAwait(false);

		// Treat NotFound as success (idempotent delete)
		if (!response.IsValidResponse && response.ApiCallDetails?.HttpStatusCode != (int)HttpStatusCode.NotFound)
		{
			var errorMessage = response.ApiCallDetails?.ToString() ?? "Unknown error";
			_logger.LogError(
				"Failed to delete projection {ProjectionType}/{Id} from index {IndexName}: {Error}",
				_projectionType,
				id,
				_indexName,
				errorMessage);
			throw new ElasticsearchDeleteException(
				id,
				typeof(TProjection),
				errorMessage,
				response.ApiCallDetails?.OriginalException);
		}

		LogDeleted(_projectionType, id);
	}

	/// <inheritdoc/>
	[RequiresUnreferencedCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	[RequiresDynamicCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	public async Task<IReadOnlyList<TProjection>> QueryAsync(
		IDictionary<string, object>? filters,
		QueryOptions? options,
		CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var searchRequest = BuildSearchRequest(filters, options);

		var response = await _client!
			.SearchAsync<TProjection>(searchRequest, cancellationToken)
			.ConfigureAwait(false);

		if (!response.IsValidResponse)
		{
			var errorMessage = response.ApiCallDetails?.ToString() ?? "Unknown error";
			_logger.LogError(
				"Failed to query projections {ProjectionType} in index {IndexName}: {Error}",
				_projectionType,
				_indexName,
				errorMessage);
			throw new ElasticsearchSearchException(
				_indexName,
				typeof(TProjection),
				errorMessage,
				response.ApiCallDetails?.OriginalException);
		}

		return response.Hits
			.Where(h => h.Source is not null)
			.Select(h => h.Source!)
			.ToList();
	}

	/// <inheritdoc/>
	public async Task<long> CountAsync(
		IDictionary<string, object>? filters,
		CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var query = BuildQuery(filters);

		var response = await _client!
			.CountAsync<TProjection>(c => c
				.Indices(_indexName)
				.Query(query), cancellationToken)
			.ConfigureAwait(false);

		if (!response.IsValidResponse)
		{
			var errorMessage = response.ApiCallDetails?.ToString() ?? "Unknown error";
			_logger.LogError(
				"Failed to count projections {ProjectionType} in index {IndexName}: {Error}",
				_projectionType,
				_indexName,
				errorMessage);
			throw new ElasticsearchSearchException(
				_indexName,
				typeof(TProjection),
				errorMessage,
				response.ApiCallDetails?.OriginalException);
		}

		return response.Count;
	}

	/// <inheritdoc/>
	public ValueTask DisposeAsync()
	{
		if (_disposed)
		{
			return ValueTask.CompletedTask;
		}

		_disposed = true;

		// Disposed AFTER _disposed is set, and the ordering is the whole point. _disposed is what
		// stops a caller reaching WaitAsync/Release, so destroying the semaphore first creates an
		// interval where the guard is gone but callers are still admitted. In that interval an
		// in-flight initialiser's Release() throws ObjectDisposedException from its finally --
		// replacing whatever the try produced, including the real diagnostic -- and any caller
		// already blocked in WaitAsync is never signalled at all.
		//
		// The earlier comment here claimed disposing first meant "a throw later still frees the
		// handle". That was backwards: it does not protect against a later throw, it maximises the
		// window in which the initialiser's Release is guaranteed to throw. try/finally is what
		// frees a handle on a throw.
		_initLock?.Dispose();
		// ElasticsearchClient doesn't implement IDisposable - it manages connections internally
		return ValueTask.CompletedTask;
	}

	private static Action<QueryDescriptor<TProjection>> BuildFilterCondition(
		string fieldName,
		ProjectionFieldType fieldType,
		bool requiresKeywordSuffix,
		FilterOperator op,
		object value)
	{
		return op switch
		{
			FilterOperator.Equals => q => q.Term(t => t
				.Field(GetExactMatchFieldName(fieldName, requiresKeywordSuffix))
				.Value(ConvertToFieldValue(value))),
			FilterOperator.NotEquals => q => q.Bool(b => b.MustNot(mn => mn.Term(t => t
				.Field(GetExactMatchFieldName(fieldName, requiresKeywordSuffix))
				.Value(ConvertToFieldValue(value))))),
			FilterOperator.GreaterThan => BuildRangeCondition(
				fieldName,
				fieldType,
				value,
				RangeOperator.GreaterThan),
			FilterOperator.GreaterThanOrEqual => BuildRangeCondition(
				fieldName,
				fieldType,
				value,
				RangeOperator.GreaterThanOrEqual),
			FilterOperator.LessThan => BuildRangeCondition(
				fieldName,
				fieldType,
				value,
				RangeOperator.LessThan),
			FilterOperator.LessThanOrEqual => BuildRangeCondition(
				fieldName,
				fieldType,
				value,
				RangeOperator.LessThanOrEqual),
			FilterOperator.In => BuildInCondition(fieldName, fieldType, requiresKeywordSuffix, value),
			FilterOperator.Contains => q => q.Wildcard(w => w.Field(fieldName).Value($"*{value}*").CaseInsensitive(true)),
			_ => q => q.Term(t => t
				.Field(GetExactMatchFieldName(fieldName, requiresKeywordSuffix))
				.Value(ConvertToFieldValue(value)))
		};
	}

	private static Action<QueryDescriptor<TProjection>> BuildInCondition(
		string fieldName,
		ProjectionFieldType fieldType,
		bool requiresKeywordSuffix,
		object value)
	{
		if (value is not IEnumerable enumerable || value is string)
		{
			// Single value, treat as equals
			return q => q.Term(t => t
				.Field(GetExactMatchFieldName(fieldName, requiresKeywordSuffix))
				.Value(ConvertToFieldValue(value)));
		}

		var values = new List<FieldValue>();
		foreach (var item in enumerable)
		{
			values.Add(ConvertToFieldValue(item));
		}

		if (values.Count == 0)
		{
			// Empty IN clause - return filter that matches nothing
			return MatchNoneCondition();
		}

		return q => q.Terms(t => t
			.Field(GetExactMatchFieldName(fieldName, requiresKeywordSuffix))
			.Terms(new TermsQueryField(values)));
	}

	private static FieldValue ConvertToFieldValue(object? value)
	{
		return value switch
		{
			null => FieldValue.Null,
			string s => s,
			int i => i,
			long l => l,
			double d => d,
			decimal dec => (double)dec,
			float f => f,
			bool b => b,
			DateTime dt => dt.ToString("O"),
			DateTimeOffset dto => dto.ToString("O"),
			_ => value.ToString() ?? string.Empty
		};
	}

	private static double? ConvertToDouble(object? value)
	{
		return value switch
		{
			null => null,
			int i => i,
			long l => l,
			double d => d,
			decimal dec => (double)dec,
			float f => f,
			string s when double.TryParse(s, out var result) => result,
			_ => null
		};
	}

	private static DateMath? ConvertToDateMath(object? value)
	{
		return value switch
		{
			DateTime dateTime => DateMath.Anchored(dateTime),
			DateTimeOffset dateTimeOffset => DateMath.Anchored(dateTimeOffset.UtcDateTime),
			string text => DateMath.Anchored(text),
			_ => null
		};
	}

	private static Action<QueryDescriptor<TProjection>> BuildRangeCondition(
		string fieldName,
		ProjectionFieldType fieldType,
		object value,
		RangeOperator rangeOperator)
	{
		if (fieldType == ProjectionFieldType.Date)
		{
			var dateMath = ConvertToDateMath(value);
			if (dateMath is null)
			{
				return MatchNoneCondition();
			}

			return rangeOperator switch
			{
				RangeOperator.GreaterThan => q => q.Range(r => r.DateRange(dr => dr
					.Field(fieldName)
					.Gt(dateMath))),
				RangeOperator.GreaterThanOrEqual => q => q.Range(r => r.DateRange(dr => dr
					.Field(fieldName)
					.Gte(dateMath))),
				RangeOperator.LessThan => q => q.Range(r => r.DateRange(dr => dr
					.Field(fieldName)
					.Lt(dateMath))),
				RangeOperator.LessThanOrEqual => q => q.Range(r => r.DateRange(dr => dr
					.Field(fieldName)
					.Lte(dateMath))),
				_ => MatchNoneCondition()
			};
		}

		var numberValue = ConvertToDouble(value);
		if (numberValue is null)
		{
			return MatchNoneCondition();
		}

		return rangeOperator switch
		{
			RangeOperator.GreaterThan => q => q.Range(r => r.NumberRange(nr => nr
				.Field(fieldName)
				.Gt(numberValue))),
			RangeOperator.GreaterThanOrEqual => q => q.Range(r => r.NumberRange(nr => nr
				.Field(fieldName)
				.Gte(numberValue))),
			RangeOperator.LessThan => q => q.Range(r => r.NumberRange(nr => nr
				.Field(fieldName)
				.Lt(numberValue))),
			RangeOperator.LessThanOrEqual => q => q.Range(r => r.NumberRange(nr => nr
				.Field(fieldName)
				.Lte(numberValue))),
			_ => MatchNoneCondition()
		};
	}

	private static Action<QueryDescriptor<TProjection>> MatchNoneCondition()
	{
		return q => q.Term(t => t.Field("_nonexistent_field_").Value("impossible_value"));
	}

	private static ProjectionFieldDefinition ResolveFieldDefinition(string propertyName)
	{
		if (!string.IsNullOrWhiteSpace(propertyName) &&
			FieldDefinitions.TryGetValue(propertyName, out var definition))
		{
			return definition;
		}

		// Unknown/undeclared field → treated as ES-dynamic (`text` + `.keyword`), so exact-match needs the suffix.
		return new ProjectionFieldDefinition(ToCamelCase(propertyName), ProjectionFieldType.Unknown, RequiresKeywordSuffix: true);
	}

	private static string GetFieldPath(ProjectionFieldDefinition fieldDefinition)
	{
		return fieldDefinition.JsonName;
	}

	// the exact-match (`.keyword`) decision comes from the DECLARED ES mapping (see
	// BuildFieldDefinitions), NOT a re-inference from the runtime property type. A `keyword`-mapped field is
	// already exact-match → queried as-is; a `text`-mapped field is exact-matched via its `.keyword`
	// sub-field. This keeps query field naming consistent-by-construction with ElasticIndexMappingBuilder
	// (which maps string/Guid/enum to `keyword` by default) — previously the query always appended
	// `.keyword`, so exact-match/sort on the default keyword mappings queried a non-existent sub-field and
	// silently matched nothing.
	private static string GetExactMatchFieldName(string fieldName, bool requiresKeywordSuffix)
	{
		return requiresKeywordSuffix ? $"{fieldName}.keyword" : fieldName;
	}

	private static string GetSortFieldName(string fieldName, bool requiresKeywordSuffix)
	{
		return GetExactMatchFieldName(fieldName, requiresKeywordSuffix);
	}

	private static IReadOnlyDictionary<string, ProjectionFieldDefinition> BuildFieldDefinitions()
	{
		var definitions = new Dictionary<string, ProjectionFieldDefinition>(
			StringComparer.OrdinalIgnoreCase);

		// derive each field's type AND exact-match treatment from the SAME declared ES mapping the
		// index is built from (ElasticIndexMappingBuilder) — explicit (IElasticIndexConfiguration) or inferred.
		// This makes query-side field naming consistent-by-construction with the index mapping instead of a
		// second source of truth (the runtime PropertyType) that re-diverged on every field-type addition.
		var declaredMapping = ElasticIndexMappingBuilder.BuildMappingProperties<TProjection>();
		var declaredByName = new Dictionary<string, IProperty>(StringComparer.OrdinalIgnoreCase);
		foreach (var mapping in declaredMapping)
		{
			declaredByName[mapping.Key.ToString()] = mapping.Value;
		}

		foreach (var property in typeof(TProjection).GetProperties(BindingFlags.Public | BindingFlags.Instance))
		{
			if (!property.CanRead)
			{
				continue;
			}

			var jsonName =
				property.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name ??
				ToCamelCase(property.Name);
			var (fieldType, requiresKeywordSuffix) = ClassifyDeclaredField(declaredByName, jsonName);
			var definition = new ProjectionFieldDefinition(jsonName, fieldType, requiresKeywordSuffix);

			_ = definitions.TryAdd(property.Name, definition);
			_ = definitions.TryAdd(jsonName, definition);
		}

		return definitions;
	}

	// Classifies a field by its DECLARED Elasticsearch property type, returning both the value-
	// semantics type (for range/term conversion) and whether exact-match/sort needs the `.keyword` sub-field:
	//   - `keyword`  → exact-match as-is (NO suffix)            — the default for string/Guid/enum mappings
	//   - `text`     → exact-match via the `.keyword` sub-field — the standard explicit analyzed-text pattern
	//   - date/bool/number → exact-match on the field directly (no suffix)
	//   - absent (complex/dynamic) → ES dynamically maps strings as `text` + `.keyword`, so suffix is needed
	private static (ProjectionFieldType FieldType, bool RequiresKeywordSuffix) ClassifyDeclaredField(
		IReadOnlyDictionary<string, IProperty> declaredByName,
		string jsonName)
	{
		if (declaredByName.TryGetValue(jsonName, out var declared))
		{
			return declared switch
			{
				KeywordProperty => (ProjectionFieldType.String, false),
				TextProperty => (ProjectionFieldType.String, true),
				DateProperty => (ProjectionFieldType.Date, false),
				BooleanProperty => (ProjectionFieldType.Bool, false),
				IntegerNumberProperty or LongNumberProperty or DoubleNumberProperty => (ProjectionFieldType.Numeric, false),
				_ => (ProjectionFieldType.Unknown, true),
			};
		}

		// Not in the declared mapping (complex/nested type) → ES dynamic mapping maps a string as `text` with
		// a `.keyword` sub-field, so exact-match/sort still needs the suffix (unchanged for dynamic fields).
		return (ProjectionFieldType.Unknown, true);
	}

	private static string ToCamelCase(string value)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			return value;
		}

		if (value.Length == 1)
		{
			return value.ToLowerInvariant();
		}

		return string.Concat(char.ToLowerInvariant(value[0]).ToString(), value.AsSpan(1).ToString());
	}

	private SearchRequestDescriptor<TProjection> BuildSearchRequest(
		IDictionary<string, object>? filters,
		QueryOptions? options)
	{
		var descriptor = new SearchRequestDescriptor<TProjection>()
			.Index(_indexName)
			.Query(BuildQuery(filters));

		// Pagination with From/Size
		if (options?.Skip is not null)
		{
			descriptor = descriptor.From(options.Skip.Value);
		}

		if (options?.Take is not null)
		{
			descriptor = descriptor.Size(options.Take.Value);
		}
		else
		{
			// Default size to prevent unbounded queries
			descriptor = descriptor.Size(100);
		}

		// Sorting
		if (options?.OrderBy is not null)
		{
			var fieldDefinition = ResolveFieldDefinition(options.OrderBy);
			var fieldName = GetFieldPath(fieldDefinition);
			var sortField = GetSortFieldName(fieldName, fieldDefinition.RequiresKeywordSuffix);
			descriptor = options.Descending
				? descriptor.Sort(s => s.Field(sortField, f => f.Order(SortOrder.Desc)))
				: descriptor.Sort(s => s.Field(sortField, f => f.Order(SortOrder.Asc)));
		}
		// No default sort - projections do not require deterministic ordering
		// without an explicit OrderBy. Sorting on _id is disallowed in ES 8.x.

		return descriptor;
	}

	private static Action<QueryDescriptor<TProjection>> BuildQuery(IDictionary<string, object>? filters)
	{
		return q =>
		{
			// Each projection type gets its own index (via ElasticSearchProjectionIndexConvention),
			// so no projectionType discriminator is needed — unlike shared-container stores
			// (CosmosDB, MongoDB) which use a type field for multi-type indices.
			if (filters is null || filters.Count == 0)
			{
				_ = q.MatchAll(new MatchAllQuery());
				return;
			}

			var conditions = new List<Action<QueryDescriptor<TProjection>>>();

			foreach (var (key, value) in filters)
			{
				var parsed = FilterParser.Parse(key);
				var fieldDefinition = ResolveFieldDefinition(parsed.PropertyName);
				var fieldName = GetFieldPath(fieldDefinition);

				conditions.Add(BuildFilterCondition(
					fieldName,
					fieldDefinition.FieldType,
					fieldDefinition.RequiresKeywordSuffix,
					parsed.Operator,
					value));
			}

			_ = q.Bool(b => b.Must(conditions.ToArray()));
		};
	}

	private async Task EnsureInitializedAsync(CancellationToken cancellationToken)
	{
		if (_initialized)
		{
			return;
		}


		await _initLock.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			// Re-check inside the lock: the winner finished while this caller waited.
			if (_initialized)
			{
				return;
			}
			if (_client is null)
			{
				ElasticsearchClientSettings settings;

				if (_options.NodeUris is { Count: > 0 } uris)
				{
					NodePool pool = _options.ConnectionPoolType switch
					{
						ConnectionPoolType.Sniffing => new SniffingNodePool(uris),
						_ => new StaticNodePool(uris),
					};
					settings = new ElasticsearchClientSettings(pool)
						.RequestTimeout(TimeSpan.FromSeconds(_options.RequestTimeoutSeconds));
				}
				else
				{
					settings = new ElasticsearchClientSettings(new Uri(_options.NodeUri))
						.RequestTimeout(TimeSpan.FromSeconds(_options.RequestTimeoutSeconds));
				}

				if (_options.EnableDebugMode)
				{
					settings = settings.DisableDirectStreaming();
				}

				if (!string.IsNullOrWhiteSpace(_options.Auth.ApiKey))
				{
					settings = settings.Authentication(new ApiKey(_options.Auth.ApiKey));
				}
				else if (!string.IsNullOrWhiteSpace(_options.Auth.Username) && !string.IsNullOrWhiteSpace(_options.Auth.Password))
				{
					settings = settings.Authentication(new BasicAuthentication(_options.Auth.Username, _options.Auth.Password));
				}

				_client = new ElasticsearchClient(settings);
			}

			if (_options.Index.CreateIndexOnInitialize)
			{
				await CreateIndexIfNotExistsAsync(cancellationToken).ConfigureAwait(false);
			}

			_initialized = true;
		}
		finally
		{
			_ = _initLock.Release();
		}
		LogInitialized(_indexName, _projectionType);
	}

	private async Task CreateIndexIfNotExistsAsync(CancellationToken cancellationToken)
	{
		var existsResponse = await _client!.Indices
			.ExistsAsync(_indexName, cancellationToken)
			.ConfigureAwait(false);

		if (existsResponse.Exists)
		{
			return;
		}

		// Build explicit field mappings for the projection type using the three-tier strategy:
		// 1. Explicit: IElasticIndexConfiguration<TProjection> (consumer-declared, full control)
		// 2. Inferred: Reflection over public properties (keyword for strings, long/double for numerics, etc.)
		// 3. Dynamic: Elasticsearch guesses (only for unknown/complex types not covered by tiers 1-2)
		//
		// Projections are stored flat — the TProjection is the document root. Each projection
		// type gets its own index (via ElasticSearchProjectionIndexConvention), so no envelope
		// metadata (projectionType discriminator, etc.) is needed.
		var properties = ElasticIndexMappingBuilder.BuildMappingProperties<TProjection>();

		// Mapped EXPLICITLY, not left to dynamic mapping. The position is a field the projection type
		// does not declare, so the mapping built from its public properties cannot contain it. Under the
		// default dynamic mapping the engine would infer `long` and the write would work -- but a
		// consumer who sets `dynamic: strict` through the index convention would have every positioned
		// write REJECTED for an undeclared field, and nothing in this store would explain why. Declaring
		// it removes the dependence on a setting we do not control.
		properties[PositionField] = new LongNumberProperty();

		// Apply convention-based customization if configured.
		// This allows consumers to globally override mapping defaults (e.g., text + keyword
		// multi-fields for strings) without implementing IElasticIndexConfiguration<T> per type.
		var convention = _options.Index.IndexMappingConvention;
		if (convention is not null)
		{
			properties = convention.ConfigureMappings(typeof(TProjection), properties);
		}

		var createResponse = await _client!.Indices
			.CreateAsync(_indexName, c => c
				.Settings(s => s
					.NumberOfShards(_options.Index.NumberOfShards)
					.NumberOfReplicas(_options.Index.NumberOfReplicas)
					.RefreshInterval(_options.Index.RefreshInterval))
				.Mappings(m => m
					.Properties(properties)), cancellationToken)
			.ConfigureAwait(false);

		if (!createResponse.IsValidResponse && !createResponse.Acknowledged)
		{
			var errorMessage = createResponse.ApiCallDetails?.ToString() ?? "Unknown error";
			LogIndexCreationFailed(_indexName, errorMessage);
		}
	}

	[LoggerMessage(DataElasticsearchEventId.ProjectionStoreInitialized, LogLevel.Information,
		"Initialized ElasticSearch projection store with index '{IndexName}' for type '{ProjectionType}'")]
	private partial void LogInitialized(string indexName, string projectionType);

	[LoggerMessage(DataElasticsearchEventId.ProjectionUpserted, LogLevel.Debug, "Upserted projection {ProjectionType}/{Id}")]
	private partial void LogUpserted(string projectionType, string id);

	[LoggerMessage(DataElasticsearchEventId.ProjectionDeleted, LogLevel.Debug, "Deleted projection {ProjectionType}/{Id}")]
	private partial void LogDeleted(string projectionType, string id);

	[LoggerMessage(DataElasticsearchEventId.ProjectionIndexCreationFailed, LogLevel.Warning,
		"Failed to create index '{IndexName}': {ErrorMessage}")]
	private partial void LogIndexCreationFailed(string indexName, string errorMessage);

	private sealed record ProjectionFieldDefinition(
		string JsonName,
		ProjectionFieldType FieldType,
		bool RequiresKeywordSuffix);
	/// <summary>
	/// Reserved document field holding the last global-stream position folded into this projection.
	/// </summary>
	/// <remarks>
	/// <para>
	/// A single reserved root field, not an envelope. The store's whole point is that the projection is
	/// the document root so a consumer can query it with natural field names, and wrapping it to carry
	/// one number would take that away.
	/// </para>
	/// <para>
	/// The name carries a prefix rather than a leading underscore: underscore-prefixed names are the
	/// engine's own metadata namespace, and a document field there is a collision waiting for a version
	/// that adds a metafield of that name.
	/// </para>
	/// </remarks>
	private const string PositionField = "excaliburProjectionPosition";

	/// <inheritdoc />
	[RequiresUnreferencedCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	[RequiresDynamicCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	public async Task<(TProjection? Projection, ProjectionPosition Position)> GetWithPositionAsync(
		string id,
		CancellationToken cancellationToken)
	{
		var read = await ReadForUpdateAsync(id, cancellationToken).ConfigureAwait(false);
		return (read.Projection, read.Position);
	}

	/// <inheritdoc />
	/// <remarks>
	/// <para>
	/// <b>Why not external versioning, which looks like it was made for this.</b> Indexing with
	/// <c>version_type=external</c> refuses any version not strictly greater than the stored one, so it
	/// gives monotonicity — and only monotonicity. It cannot express the other half of the contract:
	/// that the state being written was folded from the position it claims to advance FROM. A writer
	/// holding a stale read and a high position satisfies external versioning and silently drops every
	/// event in between.
	/// </para>
	/// <para>
	/// So the condition is the engine's optimistic-concurrency pair (<c>if_seq_no</c> /
	/// <c>if_primary_term</c>) taken from the read, plus the position comparison the read makes
	/// possible. The sequence pair makes the write atomic against a concurrent writer; the position
	/// comparison is what refuses a re-delivery.
	/// </para>
	/// <para>
	/// <b>A spurious refusal is possible and it is safe.</b> Any unrelated write moves the sequence
	/// number, so a caller can be told <c>Superseded</c> while the position is unchanged. It re-reads
	/// and retries, which is correct — ignoring the pair would accept a write that clobbered the other
	/// writer.
	/// </para>
	/// <para>
	/// The read-back is sound despite the engine being near-real-time for SEARCH: a get by document id
	/// is realtime, so it observes a write that no refresh has exposed to a query yet.
	/// </para>
	/// </remarks>
	[RequiresUnreferencedCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	[RequiresDynamicCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	public async Task<ProjectionAdvanceResult> UpsertAtPositionAsync(
		string id,
		TProjection projection,
		long? expectedPosition,
		long newPosition,
		CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		ArgumentException.ThrowIfNullOrWhiteSpace(id);
		ArgumentOutOfRangeException.ThrowIfNegative(newPosition);
		ArgumentNullException.ThrowIfNull(projection);

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var document = BuildDocument(projection, ProjectionPosition.At(newPosition));

		if (expectedPosition is null)
		{
			// Create-only. The engine's 409 IS the refusal -- never an index-as-upsert, which would let
			// a late starter reset a projection another writer is already advancing.
			var created = await _client!
				.IndexAsync(
					document,
					r => r.Index(_indexName).Id(id).OpType(OpType.Create),
					cancellationToken).ConfigureAwait(false);

			if (created.IsValidResponse)
			{
				return new ProjectionAdvanceResult(ProjectionAdvanceOutcome.Applied, newPosition);
			}

			if (IsVersionConflict(created))
			{
				// Something is already there. ADOPTION MATCHES EXACTLY ONE OF THE THREE STATES: an
				// unnumbered row is a complete fold whose coordinate is merely unknown, so folding this
				// batch onto it and stamping this batch's position states something true. Without
				// adoption a caller reading such a row would claim no position and be refused every
				// subsequent attempt identically -- a silent permanent stall rather than a conflict.
				//
				// The other two are refusals, and they are refused DIFFERENTLY. A positioned row means
				// a real writer is already advancing it -- the late starter this branch exists to
				// refuse -- and the caller re-reads and retries. AN UNPLACEABLE ROW CAN NEVER BE
				// ADOPTED: its state is not a fold over any prefix, so telling the caller to retry
				// would spin it forever against a row that will not change on its own.
				var conflicting = await ReadForUpdateAsync(id, cancellationToken).ConfigureAwait(false);

				if (conflicting.Position.Kind == ProjectionPositionKind.Unplaceable)
				{
					return new ProjectionAdvanceResult(ProjectionAdvanceOutcome.Unplaceable, null);
				}

				if (conflicting.Position.Kind == ProjectionPositionKind.Positioned)
				{
					return new ProjectionAdvanceResult(
						ProjectionAdvanceOutcome.Superseded, conflicting.Position.Value);
				}

				if (conflicting.SequenceNumber is not { } adoptSeqNo
					|| conflicting.PrimaryTerm is not { } adoptTerm)
				{
					// Deleted between the create and this read. The next attempt creates it.
					return new ProjectionAdvanceResult(ProjectionAdvanceOutcome.Superseded, null);
				}

				return await WriteAtSequenceAsync(
					id, document, adoptSeqNo, adoptTerm, newPosition, cancellationToken)
					.ConfigureAwait(false);
			}

			throw IndexingFailure(created);
		}

		var read = await ReadForUpdateAsync(id, cancellationToken).ConfigureAwait(false);

		if (read.SequenceNumber is not { } sequenceNumber || read.PrimaryTerm is not { } primaryTerm)
		{
			// Gone: deleted, which is how erasure removes personal data. Re-folding the event stream
			// would reinstate it, so this is settled rather than retried.
			return new ProjectionAdvanceResult(ProjectionAdvanceOutcome.Vanished, null);
		}

		// An unplaceable row is terminal and is reported as such rather than as a supersede. A
		// superseded caller re-reads and retries; this row yields the same refusal on every re-read, so
		// reporting Superseded here is an unbounded redelivery loop against a projection that can only
		// be fixed by rebuilding it.
		if (read.Position.Kind == ProjectionPositionKind.Unplaceable)
		{
			return new ProjectionAdvanceResult(ProjectionAdvanceOutcome.Unplaceable, null);
		}

		// Both conjuncts. The second is not redundant: the caller obtained its expected value BY READING
		// IT, so a re-delivery satisfies the first by construction and only monotonicity refuses it.
		//
		// ExpectedPositionOrNull renders an unnumbered row as null, which is right here: the caller
		// arrived with a non-null expectation, so an unnumbered row does not match it and the write is
		// refused. Adoption is the expectedPosition-is-null branch above, and only that branch.
		var held = read.Position.ExpectedPositionOrNull;
		if (held != expectedPosition || newPosition <= held)
		{
			return new ProjectionAdvanceResult(ProjectionAdvanceOutcome.Superseded, held);
		}

		return await WriteAtSequenceAsync(
			id, document, sequenceNumber, primaryTerm, newPosition, cancellationToken)
			.ConfigureAwait(false);
	}

	/// <inheritdoc />
	/// <remarks>
	/// <para>
	/// Reads the document's sequence number and primary term, checks the position matches, and writes
	/// conditional on that pair. There is no create path: an absent document means the projection was
	/// DELETED, deletion is how erasure removes personal data, and recreating it would reinstate what
	/// the erasure removed.
	/// </para>
	/// <para>
	/// The sequence number makes the write conditional on the document not having changed between the
	/// read and the write, so a concurrent writer is not overwritten even though the position does not
	/// move.
	/// </para>
	/// </remarks>
	[RequiresUnreferencedCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	[RequiresDynamicCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	public async Task<ProjectionRefoldResult> RefoldAtPositionAsync(
		string id,
		TProjection projection,
		long atPosition,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(id);
		ArgumentOutOfRangeException.ThrowIfNegative(atPosition);
		ArgumentNullException.ThrowIfNull(projection);

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var read = await ReadForUpdateAsync(id, cancellationToken).ConfigureAwait(false);

		if (read.SequenceNumber is not { } sequenceNumber || read.PrimaryTerm is not { } primaryTerm)
		{
			return new ProjectionRefoldResult(ProjectionRefoldOutcome.Vanished, null);
		}

		if (read.Position.Kind != ProjectionPositionKind.Positioned)
		{
			// No established position -- unnumbered or unplaceable, and neither can be matched -- so
			// there is nothing for the re-fold to be placed against, and re-reading cannot change that.
			return new ProjectionRefoldResult(ProjectionRefoldOutcome.RequiresRebuild, null);
		}

		if (read.Position.Value != atPosition)
		{
			return new ProjectionRefoldResult(ProjectionRefoldOutcome.Superseded, read.Position.Value);
		}

		// Carries atPosition, so the position is written back unchanged.
		var document = BuildDocument(projection, ProjectionPosition.At(atPosition));

		var advanced = await WriteAtSequenceAsync(
			id, document, sequenceNumber, primaryTerm, atPosition, cancellationToken)
			.ConfigureAwait(false);

		// The sequence-write helper speaks the ADVANCE vocabulary. Translating here rather than
		// reusing its result type is the point of the separate type: an advance's Superseded is
		// settled when the store is ahead, and a re-fold's never is.
		return advanced.Outcome switch
		{
			ProjectionAdvanceOutcome.Applied => new(ProjectionRefoldOutcome.Applied, atPosition),
			ProjectionAdvanceOutcome.Vanished => new(ProjectionRefoldOutcome.Vanished, null),
			_ => new(ProjectionRefoldOutcome.Superseded, advanced.CurrentPosition),
		};
	}

	/// <summary>
	/// Writes the document only if it still carries the optimistic-concurrency pair the caller read.
	/// </summary>
	/// <remarks>
	/// The sequence pair is what makes the write atomic against a concurrent writer. The position
	/// comparison the caller already performed is what refuses a re-delivery; neither substitutes for
	/// the other.
	/// </remarks>
	[RequiresUnreferencedCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	[RequiresDynamicCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	private async Task<ProjectionAdvanceResult> WriteAtSequenceAsync(
		string id,
		JsonObject document,
		long sequenceNumber,
		long primaryTerm,
		long newPosition,
		CancellationToken cancellationToken)
	{
		var response = await _client!
			.IndexAsync(
				document,
				r => r.Index(_indexName).Id(id).IfSeqNo(sequenceNumber).IfPrimaryTerm(primaryTerm),
				cancellationToken).ConfigureAwait(false);

		if (response.IsValidResponse)
		{
			LogUpserted(_projectionType, id);
			return new ProjectionAdvanceResult(ProjectionAdvanceOutcome.Applied, newPosition);
		}

		if (IsVersionConflict(response))
		{
			var current = await ReadForUpdateAsync(id, cancellationToken).ConfigureAwait(false);

			// Same discrimination as the pre-write check, and it has to be repeated here because the
			// row can become unplaceable BETWEEN the caller's read and this write -- an unconditional
			// upsert landing in that window is exactly what moves the sequence number and causes this
			// conflict.
			return current.Position.Kind == ProjectionPositionKind.Unplaceable
				? new ProjectionAdvanceResult(ProjectionAdvanceOutcome.Unplaceable, null)
				: new ProjectionAdvanceResult(
					ProjectionAdvanceOutcome.Superseded, current.Position.ExpectedPositionOrNull);
		}

		throw IndexingFailure(response);
	}

	/// <summary>
	/// Reads the document as raw JSON, returning the projection, its stored position and the
	/// optimistic-concurrency pair a conditional write needs.
	/// </summary>
	/// <remarks>
	/// Read as JSON rather than as <typeparamref name="TProjection"/> because the position is a document
	/// field the projection type does not declare, and deserializing straight to the projection would
	/// discard it. The projection is then materialised through the client's OWN source serializer, so it
	/// is the same shape the unconditional read produces -- deserializing with a separately-configured
	/// serializer would diverge the moment a consumer customises naming or converters.
	/// </remarks>
	[RequiresUnreferencedCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	[RequiresDynamicCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	private async Task<PositionedRead> ReadForUpdateAsync(string id, CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		ArgumentException.ThrowIfNullOrWhiteSpace(id);

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		// The explicit request, not the (index, id) shorthand: the shorthand is ambiguous when the
		// document type is itself a JSON node, since a node is also a candidate document.
		var response = await _client!
			.GetAsync<JsonNode>(new GetRequest(_indexName, id), cancellationToken)
			.ConfigureAwait(false);

		if (response.Source is not JsonObject source || !response.Found)
		{
			// NOT `default`. A defaulted ProjectionPosition is Positioned(0), not "no position" -- the
			// struct's zero value is a real coordinate -- so returning the struct default here would
			// report an absent document as one folded up to position zero. Named explicitly instead;
			// the absence is carried by the null sequence number, which every caller tests first.
			return new PositionedRead(null, ProjectionPosition.Unnumbered, null, null);
		}

		// The stored value decodes to one of THREE states, and the two that carry no number need
		// opposite treatment from a positioned writer. An ABSENT field reads as Unnumbered, which is
		// correct: a document written before this field existed IS a complete fold, only its coordinate
		// is unknown, so it stays adoptable. The decoding lives in ProjectionPosition so the eight
		// providers cannot drift.
		var position = ProjectionPosition.FromStored(
			source.TryGetPropertyValue(PositionField, out var stored) && stored is not null
				? stored.GetValue<long>()
				: null);

		// The position is the store's bookkeeping, not part of the projection.
		_ = source.Remove(PositionField);

		return new PositionedRead(
			Deserialize(source),
			position,
			response.SeqNo,
			response.PrimaryTerm);
	}

	/// <summary>
	/// Serializes the projection with the client's own source serializer and stamps onto it what this
	/// write asserts about the prefix folded into the state.
	/// </summary>
	/// <remarks>
	/// The field is written for all three states, including the two that carry no number. An absent
	/// field is indistinguishable from a field nobody wrote, so leaving it out is how "this state
	/// cannot be placed" became "this state was never placed" -- the one distinction a positioned
	/// writer has to act on.
	/// </remarks>
	[RequiresUnreferencedCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	[RequiresDynamicCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	private JsonObject BuildDocument(TProjection projection, ProjectionPosition position)
	{
		using var buffer = new MemoryStream();
		_client!.ElasticsearchClientSettings.SourceSerializer.Serialize(projection, buffer);

		buffer.Position = 0;

		var document = JsonNode.Parse(buffer) as JsonObject
			?? throw new InvalidOperationException(
				$"The projection type '{typeof(TProjection).Name}' did not serialize to a JSON object, so "
				+ "it cannot be stored as a document. Projections must be object-shaped.");

		document[PositionField] = position.ToStored();
		return document;
	}

	/// <summary>Materializes a projection from stored JSON using the client's own source serializer.</summary>
	[RequiresUnreferencedCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	[RequiresDynamicCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	private TProjection? Deserialize(JsonObject source)
	{
		using var buffer = new MemoryStream(Encoding.UTF8.GetBytes(source.ToJsonString()));
		return _client!.ElasticsearchClientSettings.SourceSerializer.Deserialize<TProjection>(buffer);
	}

	/// <summary>Whether a write was refused because another writer got there first.</summary>
	private static bool IsVersionConflict(TransportResponse response) =>
		response.ApiCallDetails?.HttpStatusCode == (int)HttpStatusCode.Conflict;

	/// <summary>Builds the failure for a write the engine rejected for a reason that is not a conflict.</summary>
	private Exception IndexingFailure(TransportResponse response)
	{
		var errorMessage = response.ApiCallDetails?.ToString() ?? "Unknown error";
		return new ElasticsearchIndexingException(
			_indexName,
			typeof(TProjection),
			errorMessage,
			response.ApiCallDetails?.OriginalException);
	}

	/// <summary>What one read of a stored projection yields for a conditional write.</summary>
	/// <param name="Projection">The stored state, or <see langword="null"/> when absent.</param>
	/// <param name="Position">What the stored row asserts about the prefix folded into it.</param>
	/// <param name="SequenceNumber">
	/// The document's sequence number, or <see langword="null"/> when the document is absent -- which is
	/// how the caller distinguishes a vanished projection from a merely un-positioned one.
	/// </param>
	/// <param name="PrimaryTerm">The document's primary term, paired with the sequence number.</param>
	private readonly record struct PositionedRead(
		TProjection? Projection,
		ProjectionPosition Position,
		long? SequenceNumber,
		long? PrimaryTerm);

}

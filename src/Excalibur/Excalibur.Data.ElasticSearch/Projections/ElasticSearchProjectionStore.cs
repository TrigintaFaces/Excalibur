// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

using Elastic.Clients.Elasticsearch;
using
	Elastic.Clients.Elasticsearch.QueryDsl;
using Elastic.Transport;

using Excalibur.Data.ElasticSearch.Diagnostics;
using Excalibur.Data.ElasticSearch.Exceptions;
using Excalibur.EventSourcing.Abstractions;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Data.ElasticSearch.Projections;

/// <summary>
/// ElasticSearch implementation of <see cref="IProjectionStore{TProjection}"/>.
/// </summary>
/// <remarks>
/// <para>
/// Provides projection storage using ElasticSearch indices with JSON documents.
/// Each projection type gets a dedicated index for optimal query performance.
/// Uses IndexAsync with the same document ID for atomic upsert operations.
/// </para>
/// <para>
/// Supports dictionary-based filters translated to ElasticSearch Query DSL.
/// </para>
/// </remarks>
/// <typeparam name="TProjection">The projection type to store.</typeparam>
public sealed partial class ElasticSearchProjectionStore<
	[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] TProjection> : IProjectionStore<TProjection>,
	IAsyncDisposable
	where TProjection : class
{
	private static readonly JsonSerializerOptions JsonOptions = new()
	{
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
		WriteIndented = false
	};

	private static readonly IReadOnlyDictionary<string, ProjectionFieldDefinition> FieldDefinitions =
		BuildFieldDefinitions();

	private readonly ElasticSearchProjectionStoreOptions _options;
	private readonly ILogger<ElasticSearchProjectionStore<TProjection>> _logger;
	private readonly string _projectionType;
	private readonly string _indexName;
	private ElasticsearchClient? _client;
	private bool _initialized;
	private volatile bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="ElasticSearchProjectionStore{TProjection}"/> class.
	/// </summary>
	/// <param name="options">The projection store options.</param>
	/// <param name="logger">The logger instance.</param>
	public ElasticSearchProjectionStore(
		IOptions<ElasticSearchProjectionStoreOptions> options,
		ILogger<ElasticSearchProjectionStore<TProjection>> logger)
	{
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(logger);

		_options = options.Value;
		_options.Validate();
		_logger = logger;
		_projectionType = typeof(TProjection).Name;
		_indexName = $"{_options.IndexPrefix}-{_projectionType.ToLowerInvariant()}";
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="ElasticSearchProjectionStore{TProjection}"/> class with an existing client.
	/// </summary>
	/// <param name="client">An existing ElasticSearch client.</param>
	/// <param name="options">The projection store options.</param>
	/// <param name="logger">The logger instance.</param>
	public ElasticSearchProjectionStore(
		ElasticsearchClient client,
		IOptions<ElasticSearchProjectionStoreOptions> options,
		ILogger<ElasticSearchProjectionStore<TProjection>> logger)
	{
		ArgumentNullException.ThrowIfNull(client);
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(logger);

		_client = client;
		_options = options.Value;
		_options.Validate();
		_logger = logger;
		_projectionType = typeof(TProjection).Name;
		_indexName = $"{_options.IndexPrefix}-{_projectionType.ToLowerInvariant()}";
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
	public async Task<TProjection?> GetByIdAsync(
		string id,
		CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		ArgumentException.ThrowIfNullOrWhiteSpace(id);

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var response = await _client
			.GetAsync<ElasticSearchProjectionDocument>(_indexName, id, cancellationToken)
			.ConfigureAwait(false);

		if (response is { IsValidResponse: true, Found: true } && response.Source is not null)
		{
			return response.Source.ToProjection<TProjection>();
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
	public async Task UpsertAsync(
		string id,
		TProjection projection,
		CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		ArgumentException.ThrowIfNullOrWhiteSpace(id);
		ArgumentNullException.ThrowIfNull(projection);

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var document = ElasticSearchProjectionDocument.FromProjection(id, _projectionType, projection);

		// IndexAsync with same ID performs upsert (insert or replace)
		var response = await _client
			.IndexAsync(document, _indexName, id, cancellationToken)
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

	/// <inheritdoc/>
	public async Task DeleteAsync(
		string id,
		CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		ArgumentException.ThrowIfNullOrWhiteSpace(id);

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var deleteRequest = new DeleteRequest(_indexName, new Id(id));
		var response = await _client
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
	public async Task<IReadOnlyList<TProjection>> QueryAsync(
		IDictionary<string, object>? filters,
		QueryOptions? options,
		CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var searchRequest = BuildSearchRequest(filters, options);

		var response = await _client
			.SearchAsync(searchRequest, cancellationToken)
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

		var results = new List<TProjection>();
		foreach (var hit in response.Hits)
		{
			if (hit.Source is not null)
			{
				var projection = hit.Source.ToProjection<TProjection>();
				if (projection is not null)
				{
					results.Add(projection);
				}
			}
		}

		return results;
	}

	/// <inheritdoc/>
	public async Task<long> CountAsync(
		IDictionary<string, object>? filters,
		CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var query = BuildQuery(filters);

		var response = await _client
			.CountAsync<ElasticSearchProjectionDocument>(c => c
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
		// ElasticsearchClient doesn't implement IDisposable - it manages connections internally
		return ValueTask.CompletedTask;
	}

	private static Action<QueryDescriptor<ElasticSearchProjectionDocument>> BuildFilterCondition(
		string fieldName,
		ProjectionFieldType fieldType,
		FilterOperator op,
		object value)
	{
		return op switch
		{
			FilterOperator.Equals => q => q.Term(t => t
				.Field(GetExactMatchFieldName(fieldName, fieldType))
				.Value(ConvertToFieldValue(value))),
			FilterOperator.NotEquals => q => q.Bool(b => b.MustNot(mn => mn.Term(t => t
				.Field(GetExactMatchFieldName(fieldName, fieldType))
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
			FilterOperator.In => BuildInCondition(fieldName, fieldType, value),
			FilterOperator.Contains => q => q.Wildcard(w => w.Field(fieldName).Value($"*{value}*").CaseInsensitive(true)),
			_ => q => q.Term(t => t
				.Field(GetExactMatchFieldName(fieldName, fieldType))
				.Value(ConvertToFieldValue(value)))
		};
	}

	private static Action<QueryDescriptor<ElasticSearchProjectionDocument>> BuildInCondition(
		string fieldName,
		ProjectionFieldType fieldType,
		object value)
	{
		if (value is not IEnumerable enumerable || value is string)
		{
			// Single value, treat as equals
			return q => q.Term(t => t
				.Field(GetExactMatchFieldName(fieldName, fieldType))
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
			.Field(GetExactMatchFieldName(fieldName, fieldType))
			.Term(new TermsQueryField(values)));
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

	private static Action<QueryDescriptor<ElasticSearchProjectionDocument>> BuildRangeCondition(
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

	private static Action<QueryDescriptor<ElasticSearchProjectionDocument>> MatchNoneCondition()
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

		return new ProjectionFieldDefinition(ToCamelCase(propertyName), ProjectionFieldType.Unknown);
	}

	private static string GetFieldPath(ProjectionFieldDefinition fieldDefinition)
	{
		return $"data.{fieldDefinition.JsonName}";
	}

	private static string GetExactMatchFieldName(string fieldName, ProjectionFieldType fieldType)
	{
		return fieldType is ProjectionFieldType.String or ProjectionFieldType.Unknown
			? $"{fieldName}.keyword"
			: fieldName;
	}

	private static string GetSortFieldName(string fieldName, ProjectionFieldType fieldType)
	{
		return GetExactMatchFieldName(fieldName, fieldType);
	}

	private static IReadOnlyDictionary<string, ProjectionFieldDefinition> BuildFieldDefinitions()
	{
		var definitions = new Dictionary<string, ProjectionFieldDefinition>(
			StringComparer.OrdinalIgnoreCase);

		foreach (var property in typeof(TProjection).GetProperties(BindingFlags.Public | BindingFlags.Instance))
		{
			if (!property.CanRead)
			{
				continue;
			}

			var jsonName =
				property.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name ??
				ToCamelCase(property.Name);
			var fieldType = GetFieldType(property.PropertyType);
			var definition = new ProjectionFieldDefinition(jsonName, fieldType);

			_ = definitions.TryAdd(property.Name, definition);
			_ = definitions.TryAdd(jsonName, definition);
		}

		return definitions;
	}

	private static ProjectionFieldType GetFieldType(Type propertyType)
	{
		var type = Nullable.GetUnderlyingType(propertyType) ?? propertyType;

		if (type == typeof(string) || type == typeof(Guid) || type.IsEnum)
		{
			return ProjectionFieldType.String;
		}

		if (type == typeof(DateTime) || type == typeof(DateTimeOffset))
		{
			return ProjectionFieldType.Date;
		}

		if (type == typeof(bool))
		{
			return ProjectionFieldType.Bool;
		}

		if (type == typeof(byte) || type == typeof(sbyte) ||
			type == typeof(short) || type == typeof(ushort) ||
			type == typeof(int) || type == typeof(uint) ||
			type == typeof(long) || type == typeof(ulong) ||
			type == typeof(float) || type == typeof(double) ||
			type == typeof(decimal))
		{
			return ProjectionFieldType.Numeric;
		}

		return ProjectionFieldType.Unknown;
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

	private SearchRequestDescriptor<ElasticSearchProjectionDocument> BuildSearchRequest(
		IDictionary<string, object>? filters,
		QueryOptions? options)
	{
		var descriptor = new SearchRequestDescriptor<ElasticSearchProjectionDocument>()
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
			var sortField = GetSortFieldName(fieldName, fieldDefinition.FieldType);
			descriptor = options.Descending
				? descriptor.Sort(s => s.Field(sortField, f => f.Order(SortOrder.Desc)))
				: descriptor.Sort(s => s.Field(sortField, f => f.Order(SortOrder.Asc)));
		}
		// No default sort - projections do not require deterministic ordering
		// without an explicit OrderBy. Sorting on _id is disallowed in ES 8.x.

		return descriptor;
	}

	private Action<QueryDescriptor<ElasticSearchProjectionDocument>> BuildQuery(IDictionary<string, object>? filters)
	{
		return q =>
		{
			// Always filter by projection type
			// With dynamic mapping, string fields get both text and .keyword subfields
			// Use the .keyword subfield for exact term matching
			var conditions = new List<Action<QueryDescriptor<ElasticSearchProjectionDocument>>>
			{
				mq => mq.Term(t => t.Field("projectionType.keyword").Value(_projectionType))
			};

			if (filters is not null && filters.Count > 0)
			{
				foreach (var (key, value) in filters)
				{
					var parsed = FilterParser.Parse(key);
					var fieldDefinition = ResolveFieldDefinition(parsed.PropertyName);
					var fieldName = GetFieldPath(fieldDefinition);

					conditions.Add(BuildFilterCondition(
						fieldName,
						fieldDefinition.FieldType,
						parsed.Operator,
						value));
				}
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

		if (_client is null)
		{
			var settings = new ElasticsearchClientSettings(new Uri(_options.NodeUri))
				.RequestTimeout(TimeSpan.FromSeconds(_options.RequestTimeoutSeconds));

			if (_options.EnableDebugMode)
			{
				settings = settings.DisableDirectStreaming();
			}

			if (!string.IsNullOrWhiteSpace(_options.ApiKey))
			{
				settings = settings.Authentication(new ApiKey(_options.ApiKey));
			}
			else if (!string.IsNullOrWhiteSpace(_options.Username) && !string.IsNullOrWhiteSpace(_options.Password))
			{
				settings = settings.Authentication(new BasicAuthentication(_options.Username, _options.Password));
			}

			_client = new ElasticsearchClient(settings);
		}

		if (_options.CreateIndexOnInitialize)
		{
			await CreateIndexIfNotExistsAsync(cancellationToken).ConfigureAwait(false);
		}

		_initialized = true;
		LogInitialized(_indexName, _projectionType);
	}

	private async Task CreateIndexIfNotExistsAsync(CancellationToken cancellationToken)
	{
		var existsResponse = await _client.Indices
			.ExistsAsync(_indexName, cancellationToken)
			.ConfigureAwait(false);

		if (existsResponse.Exists)
		{
			return;
		}

		// Create index with dynamic mapping enabled
		// Field names are serialized as camelCase via JsonPropertyName attributes
		// Dynamic mapping will create appropriate field types automatically:
		// - Strings become text with .keyword subfield
		// - Numbers become long/double
		// - Dates become date
		var createResponse = await _client.Indices
			.CreateAsync(_indexName, c => c
				.Settings(s => s
					.NumberOfShards(_options.NumberOfShards)
					.NumberOfReplicas(_options.NumberOfReplicas)
					.RefreshInterval(_options.RefreshInterval)), cancellationToken)
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

	/// <summary>
	/// Internal document structure for ElasticSearch storage.
	/// </summary>
	internal sealed class ElasticSearchProjectionDocument
	{
		/// <summary>
		/// Gets or sets the original projection identifier.
		/// </summary>
		[JsonPropertyName("projectionId")]
		public string ProjectionId { get; set; } = string.Empty;

		/// <summary>
		/// Gets or sets the projection type name for filtering.
		/// </summary>
		[JsonPropertyName("projectionType")]
		public string ProjectionType { get; set; } = string.Empty;

		/// <summary>
		/// Gets or sets the projection data as a JSON object.
		/// </summary>
		[JsonPropertyName("data")]
		public JsonElement Data { get; set; }

		/// <summary>
		/// Gets or sets the last update timestamp.
		/// </summary>
		[JsonPropertyName("updatedAt")]
		public DateTimeOffset UpdatedAt { get; set; }

		/// <summary>
		/// Creates a document from a projection instance.
		/// </summary>
		/// <typeparam name="T">The projection type.</typeparam>
		/// <param name="id">The projection identifier.</param>
		/// <param name="projectionType">The projection type name.</param>
		/// <param name="projection">The projection instance.</param>
		/// <returns>A new document instance.</returns>
		public static ElasticSearchProjectionDocument FromProjection<T>(string id, string projectionType, T projection)
			where T : class
		{
			var json = JsonSerializer.Serialize(projection, JsonOptions);
			var data = JsonDocument.Parse(json).RootElement;

			return new ElasticSearchProjectionDocument
			{
				ProjectionId = id,
				ProjectionType = projectionType,
				Data = data,
				UpdatedAt = DateTimeOffset.UtcNow
			};
		}

		/// <summary>
		/// Converts the document back to a projection instance.
		/// </summary>
		/// <typeparam name="T">The projection type.</typeparam>
		/// <returns>The deserialized projection, or null if data is empty.</returns>
		public T? ToProjection<T>()
			where T : class
		{
			if (Data.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
			{
				return null;
			}

			return JsonSerializer.Deserialize<T>(Data.GetRawText(), JsonOptions);
		}
	}

	private sealed record ProjectionFieldDefinition(string JsonName, ProjectionFieldType FieldType);
}

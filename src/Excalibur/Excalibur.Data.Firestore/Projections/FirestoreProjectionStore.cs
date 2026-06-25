// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections;
using System.Globalization;
using System.Text.Json;

using Excalibur.EventSourcing;

using Google.Cloud.Firestore;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Data.Firestore.Projections;

/// <summary>
/// Google Cloud Firestore implementation of <see cref="IProjectionStore{TProjection}"/>.
/// </summary>
/// <remarks>
/// <para>
/// Stores projections as Firestore documents within a collection.
/// Each projection type uses a subcollection keyed by projection type name.
/// Supports dictionary-based filter queries translated to Firestore queries.
/// </para>
/// </remarks>
/// <typeparam name="TProjection">The projection type to store.</typeparam>
public sealed class FirestoreProjectionStore<TProjection> : IProjectionStore<TProjection>
	where TProjection : class
{
	/// <summary>
	/// Reserved document field holding the write-only flat query index — a map of the projection's
	/// top-level scalar properties as Firestore-native values. It exists solely so the store can issue
	/// server-side <c>Where*</c> clauses; it is never read back into the projection (the canonical
	/// <c>data</c> JSON blob is the source of truth, preserving full decimal/<see cref="DateTimeOffset"/>
	/// fidelity that the Firestore-native field types lose).
	/// </summary>
	private const string QueryFieldsKey = "_q";

	private readonly FirestoreDb _db;
	private readonly FirestoreProjectionStoreOptions _options;
	private readonly ILogger<FirestoreProjectionStore<TProjection>> _logger;
	private readonly string _projectionType;
	private readonly JsonSerializerOptions _jsonOptions;

	/// <summary>
	/// Initializes a new instance of the <see cref="FirestoreProjectionStore{TProjection}"/> class.
	/// </summary>
	/// <param name="db">The Firestore database instance.</param>
	/// <param name="options">The projection store options.</param>
	/// <param name="logger">The logger instance.</param>
	public FirestoreProjectionStore(
		FirestoreDb db,
		IOptions<FirestoreProjectionStoreOptions> options,
		ILogger<FirestoreProjectionStore<TProjection>> logger)
	{
		ArgumentNullException.ThrowIfNull(db);
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(logger);

		_db = db;
		_options = options.Value;
		_logger = logger;
		_projectionType = typeof(TProjection).Name;
		_jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
	}

	/// <inheritdoc/>
	public async Task<TProjection?> GetByIdAsync(string id, CancellationToken cancellationToken)
	{
		var docRef = GetCollection().Document(id);
		var snapshot = await docRef.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);

		if (!snapshot.Exists)
		{
			return null;
		}

		return DeserializeDocument(snapshot);
	}

	/// <inheritdoc/>
	public async Task UpsertAsync(string id, TProjection projection, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(projection);

#pragma warning disable IL2026 // Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code
		var json = JsonSerializer.Serialize(projection, _jsonOptions);
#pragma warning restore IL2026
		var data = new Dictionary<string, object>
		{
			// The canonical JSON blob is the SOURCE OF TRUTH for deserialization — it preserves full
			// fidelity (decimal precision, DateTimeOffset offset/sub-second) that Firestore-native field
			// types would lose.
			["data"] = json,
			["projectionType"] = _projectionType,
			["updatedAt"] = Timestamp.GetCurrentTimestamp(),
			// Denormalized, WRITE-ONLY flat index of the projection's top-level scalar properties so
			// QueryAsync/CountAsync can issue server-side Where* clauses. Nested under a reserved map key
			// so it can never collide with a projection property, and never read back into the projection.
			[QueryFieldsKey] = BuildQueryFields(json),
		};

		var docRef = GetCollection().Document(id);
		await docRef.SetAsync(data, cancellationToken: cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc/>
	public async Task DeleteAsync(string id, CancellationToken cancellationToken)
	{
		var docRef = GetCollection().Document(id);
		await docRef.DeleteAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc/>
	public async Task<IReadOnlyList<TProjection>> QueryAsync(
		IDictionary<string, object>? filters,
		QueryOptions? options,
		CancellationToken cancellationToken)
	{
		var query = ApplyFilters(GetCollection(), filters);

		if (options?.Take > 0)
		{
			query = query.Limit(options.Take.Value);
		}

		var snapshot = await query.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);
		var results = new List<TProjection>();

		foreach (var doc in snapshot.Documents)
		{
			var projection = DeserializeDocument(doc);
			if (projection != null)
			{
				results.Add(projection);
			}
		}

		return results;
	}

	/// <inheritdoc/>
	public async Task<long> CountAsync(
		IDictionary<string, object>? filters,
		CancellationToken cancellationToken)
	{
		var snapshot = await ApplyFilters(GetCollection(), filters)
			.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);
		return snapshot.Count;
	}

	/// <summary>
	/// Translates the filter dictionary into Firestore <c>Where*</c> clauses over the write-only flat
	/// query index (<see cref="QueryFieldsKey"/>), AND-combining every key (EC-P1.1). A null/empty filter
	/// leaves the query unchanged (AC-P1.4). A nested (dotted) or otherwise untranslatable key throws
	/// <see cref="NotSupportedException"/> rather than silently returning unfiltered data (FR-P1.5).
	/// </summary>
	private static Query ApplyFilters(Query query, IDictionary<string, object>? filters)
	{
		if (filters is null || filters.Count == 0)
		{
			return query;
		}

		foreach (var (key, value) in filters)
		{
			var parsed = FilterParser.Parse(key);

			if (parsed.PropertyName.Contains('.', StringComparison.Ordinal))
			{
				throw new NotSupportedException(
					$"Firestore projection filter cannot translate the nested key '{key}'. Only top-level "
					+ "scalar projection properties are queryable.");
			}

			var fieldPath = $"{QueryFieldsKey}.{ToCamelCase(parsed.PropertyName)}";
			query = ApplyCondition(query, fieldPath, parsed.Operator, value);
		}

		return query;
	}

	private static Query ApplyCondition(Query query, string fieldPath, FilterOperator op, object? value)
	{
		return op switch
		{
			FilterOperator.Equals => query.WhereEqualTo(fieldPath, ToFirestoreValue(value)),
			FilterOperator.NotEquals => query.WhereNotEqualTo(fieldPath, ToFirestoreValue(value)),
			FilterOperator.GreaterThan => query.WhereGreaterThan(fieldPath, ToFirestoreValue(value)),
			FilterOperator.GreaterThanOrEqual => query.WhereGreaterThanOrEqualTo(fieldPath, ToFirestoreValue(value)),
			FilterOperator.LessThan => query.WhereLessThan(fieldPath, ToFirestoreValue(value)),
			FilterOperator.LessThanOrEqual => query.WhereLessThanOrEqualTo(fieldPath, ToFirestoreValue(value)),
			FilterOperator.In => query.WhereIn(fieldPath, ToFirestoreValueList(value)),
			_ => throw new NotSupportedException(
				$"Firestore projection filter does not support the '{op}' operator (Firestore has no native "
				+ "substring search). Use equality, range, or In on a top-level scalar property."),
		};
	}

	/// <summary>
	/// Extracts the projection's top-level SCALAR properties from the canonical JSON into a flat map of
	/// Firestore-native values used purely as a server-side query index. Non-scalar properties (nested
	/// objects, arrays, nulls) are omitted — they are not queryable via a simple <c>Where</c> clause. The
	/// JSON property names are already camelCase (the serializer naming policy), matching the query path.
	/// </summary>
	private static Dictionary<string, object> BuildQueryFields(string json)
	{
		var fields = new Dictionary<string, object>(StringComparer.Ordinal);

		using var doc = JsonDocument.Parse(json);
		if (doc.RootElement.ValueKind != JsonValueKind.Object)
		{
			return fields;
		}

		foreach (var property in doc.RootElement.EnumerateObject())
		{
			switch (property.Value.ValueKind)
			{
				case JsonValueKind.String:
					fields[property.Name] = property.Value.GetString()!;
					break;
				case JsonValueKind.Number:
					fields[property.Name] = property.Value.TryGetInt64(out var l) ? l : property.Value.GetDouble();
					break;
				case JsonValueKind.True:
				case JsonValueKind.False:
					fields[property.Name] = property.Value.GetBoolean();
					break;
				default:
					// Objects, arrays, and nulls are not indexed as queryable scalars.
					break;
			}
		}

		return fields;
	}

	/// <summary>
	/// Converts a filter value to the Firestore-native type matching how the flat index stored it
	/// (string → string, integral → long, floating → double, bool → bool). A null or otherwise
	/// untranslatable value throws <see cref="NotSupportedException"/> (FR-P1.5).
	/// </summary>
	private static object ToFirestoreValue(object? value)
	{
		return value switch
		{
			null => throw new NotSupportedException(
				"Firestore projection filter cannot translate a null filter value."),
			string s => s,
			bool b => b,
			byte or sbyte or short or ushort or int or uint or long or ulong
				=> Convert.ToInt64(value, CultureInfo.InvariantCulture),
			float or double or decimal => Convert.ToDouble(value, CultureInfo.InvariantCulture),
			_ => throw new NotSupportedException(
				$"Firestore projection filter cannot translate a filter value of type '{value.GetType()}'."),
		};
	}

	private static IEnumerable<object> ToFirestoreValueList(object? value)
	{
		if (value is not IEnumerable enumerable || value is string)
		{
			return [ToFirestoreValue(value)];
		}

		var values = new List<object>();
		foreach (var item in enumerable)
		{
			values.Add(ToFirestoreValue(item));
		}

		return values;
	}

	private static string ToCamelCase(string propertyName)
	{
		if (string.IsNullOrEmpty(propertyName) || char.IsLower(propertyName[0]))
		{
			return propertyName;
		}

		return $"{char.ToLowerInvariant(propertyName[0])}{propertyName[1..]}";
	}

	private CollectionReference GetCollection()
	{
		return _db.Collection(_options.CollectionName).Document(_projectionType).Collection("items");
	}

	private TProjection? DeserializeDocument(DocumentSnapshot snapshot)
	{
		if (!snapshot.TryGetValue<string>("data", out var json) || json is null)
		{
			return null;
		}

#pragma warning disable IL2026 // Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code
		return JsonSerializer.Deserialize<TProjection>(json, _jsonOptions);
#pragma warning restore IL2026
	}
}

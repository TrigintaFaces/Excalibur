// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Collections;
using System.Globalization;
using System.Text.Json;

using Excalibur.EventSourcing;

using Google.Cloud.Firestore;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using System.Diagnostics.CodeAnalysis;

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
public sealed class FirestoreProjectionStore<TProjection>
	: IProjectionStore<TProjection>, IPositionedProjectionStore<TProjection>
	where TProjection : class
{
	/// <summary>
	/// Reserved document field holding the last global-stream position folded into this projection.
	/// </summary>
	/// <remarks>
	/// A top-level reserved field rather than a member of the projection JSON: the position describes
	/// how far the state has been folded, not the state itself, and a consumer property must never be
	/// able to collide with it.
	/// </remarks>
	private const string PositionKey = "_pos";

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
		_projectionType = typeof(TProjection).Name;
		_jsonOptions = ProjectionSerializationDefaults.CreateReadModelOptions();
	}

	/// <inheritdoc/>
	[RequiresUnreferencedCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	[RequiresDynamicCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
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
	[RequiresUnreferencedCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	[RequiresDynamicCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	public async Task UpsertAsync(string id, TProjection projection, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(projection);

		// THE FIELD IS WRITTEN, NOT OMITTED, AND THAT IS THE CHANGE.
		//
		// SetAsync replaces the whole document, so a position the row used to carry vanishes with it.
		// Omitting the field left the row reading back exactly like a row that never had a position --
		// and those two must stay DISTINGUISHABLE: a never-positioned row IS a complete fold
		// whose coordinate is merely unknown, whereas a row whose state was just replaced holds a fold over
		// no known prefix at all. A positioned write refuses BOTH -- neither carries a number it can advance
		// from -- so what the distinction decides is not the next write but what the row can honestly be
		// said to hold while it waits to be rebuilt, which is what an operator reads it for.
		//
		// The sentinel rides the same single Set as the state, so there is no window in which a
		// destroyed position is recorded as a never-established one.
		var docRef = GetCollection().Document(id);
		await docRef
			.SetAsync(
				BuildDocument(projection, ProjectionPosition.Unplaceable),
				cancellationToken: cancellationToken)
			.ConfigureAwait(false);
	}

	/// <inheritdoc />
	/// <remarks>
	/// The same single <c>Set</c> as <see cref="UpsertAsync"/>, differing only in what it asserts: this
	/// state IS a complete fold, only its coordinate unknown. Unconditional on
	/// purpose -- the caller is claiming completeness, not a place in the stream, so there is no
	/// position for a condition to be written against.
	/// <para>
	/// <b>The row is still REFUSED by a positioned write</b>, which has no number to advance from. What
	/// this buys over the blind surface is that the row reads back as a complete answer awaiting a
	/// coordinate rather than as a state related to no prefix at all; a rebuild is what numbers it.
	/// </para>
	/// </remarks>
	[RequiresUnreferencedCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	[RequiresDynamicCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	public async Task UpsertUnnumberedAsync(
		string id,
		TProjection projection,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(id);
		ArgumentNullException.ThrowIfNull(projection);

		await GetCollection().Document(id)
			.SetAsync(
				BuildDocument(projection, ProjectionPosition.Unnumbered),
				cancellationToken: cancellationToken)
			.ConfigureAwait(false);
	}

	/// <inheritdoc />
	/// <remarks>
	/// <para>
	/// A transaction rather than the bare <c>Set</c> the other unconditional writes use. Firestore gives
	/// the read and the write one transaction, so the existence test is not a separate observation the
	/// document could be deleted after -- it is part of the same atomic action as the write, which is what
	/// every other provider gets from a conditional write.
	/// </para>
	/// <para>
	/// No position is compared, because a state folded from an empty seed has no prior prefix for a
	/// condition to be written against. <c>Set</c> is never called on a missing document: an absent
	/// document means the projection was DELETED, deletion is how erasure removes personal data, and a
	/// whole-stream replay is precisely the write that could reconstruct it.
	/// </para>
	/// <para>
	/// <b>The SDK's automatic retry is safe here.</b> A transaction aborted by contention re-runs this
	/// callback, which re-reads the document and re-derives the outcome from what it then finds, so a
	/// <c>Vanished</c> is reproduced rather than swallowed.
	/// </para>
	/// </remarks>
	[RequiresUnreferencedCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	[RequiresDynamicCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	public async Task<ProjectionRebuildResult> RebuildAtPositionAsync(
		string id,
		TProjection projection,
		long newPosition,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(id);
		ArgumentNullException.ThrowIfNull(projection);
		ArgumentOutOfRangeException.ThrowIfNegative(newPosition);

		var docRef = GetCollection().Document(id);
		var document = BuildDocument(projection, ProjectionPosition.At(newPosition));

		return await _db.RunTransactionAsync(
			async transaction =>
			{
				var snapshot = await transaction.GetSnapshotAsync(docRef, cancellationToken)
					.ConfigureAwait(false);

				if (!snapshot.Exists)
				{
					return new ProjectionRebuildResult(ProjectionRebuildOutcome.Vanished);
				}

				transaction.Set(docRef, document);

				return new ProjectionRebuildResult(ProjectionRebuildOutcome.Applied);
			},
			options: null,
			cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc />
	[RequiresUnreferencedCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	[RequiresDynamicCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	public async Task<(TProjection? Projection, ProjectionPosition Position)> GetWithPositionAsync(
		string id,
		CancellationToken cancellationToken)
	{
		var snapshot = await GetCollection().Document(id).GetSnapshotAsync(cancellationToken)
			.ConfigureAwait(false);

		return snapshot.Exists
			? (DeserializeDocument(snapshot), ReadPosition(snapshot))
			// An absent document is not a fold over any prefix. Unnumbered would assert a complete fold
			// over state that does not exist.
			: (null, ProjectionPosition.Unplaceable);
	}

	/// <inheritdoc />
	/// <remarks>
	/// <para>
	/// Firestore gives the read and the write one transaction, so unlike the stores that must condition
	/// on an ETag or a version token, the position comparison here IS the condition — no second
	/// mechanism, and no refusal that the position did not actually earn.
	/// </para>
	/// <para>
	/// <b>The SDK's automatic retry is safe here, and that is worth stating.</b> A transaction aborted
	/// by contention re-runs this callback, which re-reads the document and re-derives the outcome from
	/// what it finds. A <c>Superseded</c> is therefore reproduced rather than swallowed: the retry
	/// observes the same advanced position and declines again, committing nothing.
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
		ArgumentException.ThrowIfNullOrWhiteSpace(id);
		ArgumentOutOfRangeException.ThrowIfNegative(newPosition);
		ArgumentNullException.ThrowIfNull(projection);

		// A NEGATIVE EXPECTATION IS THE ADOPT LICENCE THROUGH A DIFFERENT DOOR. The negatives are the
		// sentinel space for the two states that carry no number, so a caller naming one as "the position I
		// read" would be matching a row this store refuses by design. ExpectedPositionOrNull is the only
		// legal source for this argument and never yields a negative.
		if (expectedPosition is { } claimed)
		{
			ArgumentOutOfRangeException.ThrowIfNegative(claimed, nameof(expectedPosition));
		}

		var docRef = GetCollection().Document(id);
		var document = BuildDocument(projection, ProjectionPosition.At(newPosition));

		return await _db.RunTransactionAsync(
			async transaction =>
			{
				var snapshot = await transaction.GetSnapshotAsync(docRef, cancellationToken)
					.ConfigureAwait(false);

				if (expectedPosition is null)
				{
					// CREATE-IF-ABSENT and NOTHING ELSE. An existing document is refused whatever its own
					// position reads, because a caller that read no position knows nothing about the
					// prefix the stored state covers -- so stamping this batch's position onto it would
					// assert a prefix the state need not hold, and every event below that position is then
					// absent from the read model while the position says it is present. The numberless
					// case used to be ADOPTED here; it is refused now, and the refusal is repairable by
					// RebuildAtPositionAsync, which folds the whole stream and numbers the row from what
					// it folded.
					//
					// The two refusals are DIFFERENT. A positioned document means a real writer is already
					// advancing it -- the late starter this branch exists to refuse -- and the caller
					// re-reads and retries. A document carrying no number can never be advanced from at
					// all, so telling the caller to retry would spin it forever against a document that
					// will not change.
					if (snapshot.Exists)
					{
						var conflicting = ReadPosition(snapshot);

						return conflicting.Kind == ProjectionPositionKind.Positioned
							? new ProjectionAdvanceResult(ProjectionAdvanceOutcome.Superseded, conflicting.Value)
							: new ProjectionAdvanceResult(ProjectionAdvanceOutcome.Unplaceable, null);
					}

					transaction.Set(docRef, document);
					return new ProjectionAdvanceResult(ProjectionAdvanceOutcome.Applied, newPosition);
				}

				if (!snapshot.Exists)
				{
					// Deleted, which is how erasure removes personal data. Re-folding the stream would
					// reinstate it, so this is settled rather than retried.
					return new ProjectionAdvanceResult(ProjectionAdvanceOutcome.Vanished, null);
				}

				var storedPosition = ReadPosition(snapshot);

				// Terminal, not a supersede: there is no number to advance from, and re-reading yields the
				// same value and the same refusal, so Superseded here is an unbounded redelivery loop. That is the
				// arm a careless edit reintroduces, and a numberless row landing in it retries forever.
				//
				// BOTH no-number states report the SAME outcome, deliberately. This result describes what the WRITE
				// did; it carries no state, and the position was read at a different instant from the one the write
				// was refused at, so an outcome characterising the stored STATE would attribute a property of the
				// row-at-read-time to a write refused earlier. A caller that needs to know what the row holds reads
				// its position, where ProjectionPositionKind reports it as a measured fact.
				if (storedPosition.Kind != ProjectionPositionKind.Positioned)
				{
					return new ProjectionAdvanceResult(ProjectionAdvanceOutcome.Unplaceable, null);
				}

				// Both conjuncts. The second is not redundant: the caller obtained its expected value BY
				// READING IT, so a redelivery satisfies the first by construction and only monotonicity
				// refuses it.
				var held = storedPosition.ExpectedPositionOrNull;
				if (held != expectedPosition || newPosition <= held)
				{
					return new ProjectionAdvanceResult(ProjectionAdvanceOutcome.Superseded, held);
				}

				transaction.Set(docRef, document);
				return new ProjectionAdvanceResult(ProjectionAdvanceOutcome.Applied, newPosition);
			},
			options: null,
			cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc />
	/// <remarks>
	/// The transaction already holds the snapshot, so the four cases are a switch on a local value and
	/// cost no extra read. It never calls Set on a missing document: an absent document means the
	/// projection was DELETED, deletion is how erasure removes personal data, and recreating it would
	/// reinstate what the erasure removed.
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

		var docRef = GetCollection().Document(id);

		// Carries atPosition, so the position is written back unchanged.
		var document = BuildDocument(projection, ProjectionPosition.At(atPosition));

		return await _db.RunTransactionAsync(
			async transaction =>
			{
				var snapshot = await transaction.GetSnapshotAsync(docRef, cancellationToken)
					.ConfigureAwait(false);

				if (!snapshot.Exists)
				{
					return new ProjectionRefoldResult(ProjectionRefoldOutcome.Vanished, null);
				}

				var held = ReadPosition(snapshot);

				if (held.Kind != ProjectionPositionKind.Positioned)
				{
					// No established position -- unnumbered or unplaceable, and neither can be matched
					// -- so there is nothing for the re-fold to be placed against, and re-reading
					// cannot change that.
					return new ProjectionRefoldResult(ProjectionRefoldOutcome.RequiresRebuild, null);
				}

				if (held.Value != atPosition)
				{
					return new ProjectionRefoldResult(ProjectionRefoldOutcome.Superseded, held.Value);
				}

				transaction.Set(docRef, document);

				return new ProjectionRefoldResult(ProjectionRefoldOutcome.Applied, atPosition);
			},
			options: null,
			cancellationToken).ConfigureAwait(false);
	}

	/// <summary>
	/// Builds the stored document: the canonical JSON blob, the query index, and what this write
	/// asserts about the prefix folded into the state.
	/// </summary>
	/// <param name="projection">The projection state.</param>
	/// <param name="position">
	/// What this write asserts about the prefix folded into <paramref name="projection"/>. Always
	/// stored, in all three of its states -- see the remarks.
	/// </param>
	/// <remarks>
	/// One shape for both write paths, deliberately. Two builders would drift, and the drift would be a
	/// document the other path cannot read back.
	/// </remarks>
	[RequiresUnreferencedCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	[RequiresDynamicCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	private Dictionary<string, object> BuildDocument(TProjection projection, ProjectionPosition position)
	{
		var json = JsonSerializer.Serialize(projection, _jsonOptions);
		var data = new Dictionary<string, object>(StringComparer.Ordinal)
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

		// Unconditionally, including for the two states that carry no number. An absent field is
		// indistinguishable from a field nobody wrote, so leaving it out is how "this state cannot be
		// placed" became "this state was never placed" -- the one distinction a positioned writer has
		// to act on. The encoding is the shared one, never this provider's own sentinel.
		data[PositionKey] = position.ToStored();

		return data;
	}

	/// <summary>Decodes the stored field into one of the three states.</summary>
	/// <remarks>
	/// An ABSENT field reads as <see cref="ProjectionPositionKind.Unnumbered"/>, which is what
	/// <see cref="ProjectionPosition.FromStored"/> does with a null. That is correct and deliberate: a
	/// document written before this field existed IS a complete fold, only its coordinate is unknown,
	/// so it reads as UNNUMBERED rather than UNPLACEABLE. Every provider goes through FromStored so the eight of them cannot drift.
	/// </remarks>
	private static ProjectionPosition ReadPosition(DocumentSnapshot snapshot) =>
		ProjectionPosition.FromStored(
			snapshot.TryGetValue<long>(PositionKey, out var position) ? position : null);

	/// <inheritdoc/>
	public async Task DeleteAsync(string id, CancellationToken cancellationToken)
	{
		var docRef = GetCollection().Document(id);
		await docRef.DeleteAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc/>
	[RequiresUnreferencedCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	[RequiresDynamicCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	public async Task<IReadOnlyList<TProjection>> QueryAsync(
		IDictionary<string, object>? filters,
		QueryOptions? options,
		CancellationToken cancellationToken)
	{
		var query = ApplyOptions(ApplyFilters(GetCollection(), filters), options);

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
	/// query index (<see cref="QueryFieldsKey"/>), AND-combining every key. A null/empty filter
	/// leaves the query unchanged. A nested (dotted) or otherwise untranslatable key throws
	/// <see cref="NotSupportedException"/> rather than silently returning unfiltered data.
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

	/// <summary>
	/// Translates <see cref="QueryOptions"/> into Firestore ordering and pagination.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Every option this store accepts is either applied or refused. An option that was accepted and
	/// dropped cannot be distinguished by the caller from one that was applied and had no effect: a
	/// caller passing <c>Skip</c> and receiving page one has no signal that anything went wrong, and a
	/// paging loop built on it returns the same page forever rather than terminating late.
	/// </para>
	/// <para>
	/// Ordering runs over the same write-only flat query index the filters use
	/// (<see cref="QueryFieldsKey"/>), because that is where the scalar values Firestore can sort on are
	/// stored. Ordering by the canonical JSON blob would sort documents by their serialized text.
	/// </para>
	/// </remarks>
	private static Query ApplyOptions(Query query, QueryOptions? options)
	{
		if (options is null)
		{
			return query;
		}

		if (!string.IsNullOrWhiteSpace(options.OrderBy))
		{
			if (options.OrderBy.Contains('.', StringComparison.Ordinal))
			{
				throw new NotSupportedException(
					$"Firestore projection ordering cannot translate the nested key '{options.OrderBy}'. Only top-level scalar projection properties are sortable.");
			}

			var orderPath = $"{QueryFieldsKey}.{ToCamelCase(options.OrderBy)}";
			query = options.Descending
				? query.OrderByDescending(orderPath)
				: query.OrderBy(orderPath);
		}

		// Skip AFTER ordering: an offset into an unordered result set is not a stable page.
		if (options.Skip > 0)
		{
			query = query.Offset(options.Skip.Value);
		}

		if (options.Take > 0)
		{
			query = query.Limit(options.Take.Value);
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
	/// untranslatable value throws <see cref="NotSupportedException"/>.
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

#pragma warning disable IL2026, IL3050 // Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code
		return JsonSerializer.Deserialize<TProjection>(json, _jsonOptions);
#pragma warning restore IL2026, IL3050
	}
}

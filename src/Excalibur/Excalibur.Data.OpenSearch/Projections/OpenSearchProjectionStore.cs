// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Text;
using System.Text.Json.Nodes;

using Excalibur.EventSourcing;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using OpenSearch.Client;
using OpenSearch.Net;

using System.Diagnostics.CodeAnalysis;

namespace Excalibur.Data.OpenSearch.Projections;

/// <summary>
/// OpenSearch implementation of <see cref="IProjectionStore{TProjection}"/>.
/// </summary>
/// <remarks>
/// <para>
/// Stores projections as JSON documents in OpenSearch indices.
/// Each projection type gets a dedicated index (<c>{prefix}-{typename}</c>).
/// Uses IndexAsync with document ID for upsert operations.
/// </para>
/// <para>
/// Each projection type resolves its own named options instance keyed by
/// <c>typeof(TProjection).Name</c>, allowing multiple projection stores to
/// coexist with independent configurations.
/// </para>
/// </remarks>
/// <typeparam name="TProjection">The projection type to store.</typeparam>
[SuppressMessage(
	"Design",
	"CA1506:AvoidExcessiveClassCoupling",
	Justification = "One store implements two contracts over a driver whose request, response, query and "
		+ "low-level transport types are separate namespaces. Splitting the positioned write into its own "
		+ "type would duplicate the document layout, which is the one thing the conditional and "
		+ "unconditional writes must agree on exactly.")]
public sealed class OpenSearchProjectionStore<TProjection>
	: IProjectionStore<TProjection>, IPositionedProjectionStore<TProjection>
	where TProjection : class
{
	/// <summary>
	/// Reserved document field holding the last global-stream position folded into this projection.
	/// </summary>
	/// <remarks>
	/// A single reserved root field, not an envelope: the projection is the document root so a consumer
	/// can query it with natural field names, and wrapping it to carry one number would take that away.
	/// The name carries a prefix rather than a leading underscore, which is the engine's own metadata
	/// namespace.
	/// </remarks>
	private const string PositionField = "excaliburProjectionPosition";

	/// <summary>
	/// The named options key used for this projection type.
	/// </summary>
	internal static readonly string OptionsName = typeof(TProjection).Name;

	private readonly OpenSearchProjectionStoreOptions _options;
	private readonly IOpenSearchClient _client;
	private readonly string _indexName;
	private readonly ILogger<OpenSearchProjectionStore<TProjection>> _logger;
	private volatile bool _indexVerified;

	/// <summary>
	/// Initializes a new instance of the <see cref="OpenSearchProjectionStore{TProjection}"/> class
	/// using named options resolved via <see cref="IOptionsMonitor{TOptions}"/>.
	/// </summary>
	/// <param name="optionsMonitor">The options monitor for named options resolution.</param>
	/// <param name="logger">The logger instance.</param>
#pragma warning disable RS0016 // Analyzer cannot resolve nullable annotations for OpenSearch.Client types
	public OpenSearchProjectionStore(
		IOptionsMonitor<OpenSearchProjectionStoreOptions> optionsMonitor,
		ILogger<OpenSearchProjectionStore<TProjection>> logger)
	{
		ArgumentNullException.ThrowIfNull(optionsMonitor);
		ArgumentNullException.ThrowIfNull(logger);

		_options = optionsMonitor.Get(OptionsName);
		_options.Validate();
		_logger = logger;
		_indexName = GetIndexName(_options);

#pragma warning disable CA2000 // ConnectionSettings lifetime managed by OpenSearchClient
		var settings = new ConnectionSettings(new Uri(_options.NodeUri))
			.DefaultIndex(_indexName)
			.ThrowExceptions();
#pragma warning restore CA2000

		_client = new OpenSearchClient(settings);
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="OpenSearchProjectionStore{TProjection}"/> class
	/// with an existing client.
	/// </summary>
	/// <param name="client">An existing OpenSearch client, typically one the consumer registered in DI.</param>
	/// <param name="optionsMonitor">The options monitor for named options resolution.</param>
	/// <param name="logger">The logger instance.</param>
	public OpenSearchProjectionStore(
		IOpenSearchClient client,
		IOptionsMonitor<OpenSearchProjectionStoreOptions> optionsMonitor,
		ILogger<OpenSearchProjectionStore<TProjection>> logger)
#pragma warning restore RS0016
	{
		ArgumentNullException.ThrowIfNull(client);
		ArgumentNullException.ThrowIfNull(optionsMonitor);
		ArgumentNullException.ThrowIfNull(logger);

		_client = client;
		_options = optionsMonitor.Get(OptionsName);
		_options.Validate();
		_logger = logger;
		_indexName = GetIndexName(_options);
	}

	/// <inheritdoc/>
	[RequiresUnreferencedCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	[RequiresDynamicCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	public async Task<TProjection?> GetByIdAsync(string id, CancellationToken cancellationToken)
	{
		await EnsureIndexAsync(cancellationToken).ConfigureAwait(false);

		var response = await _client.GetAsync<TProjection>(
			id,
			g => g.Index(_indexName),
			cancellationToken).ConfigureAwait(false);

		if (!response.Found)
		{
			return null;
		}

		return response.Source;
	}

	/// <inheritdoc/>
	[RequiresUnreferencedCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	[RequiresDynamicCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	public async Task UpsertAsync(string id, TProjection projection, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(id);
		ArgumentNullException.ThrowIfNull(projection);
		await EnsureIndexAsync(cancellationToken).ConfigureAwait(false);

		// THE FIELD IS WRITTEN, NOT OMITTED, AND THAT IS THE CHANGE.
		//
		// Indexing replaces the whole document, so a position the row used to carry vanishes with it --
		// and indexing the projection DIRECTLY through the typed client, as this call used to, could
		// not carry one at all. The row then read back exactly like a row that never had a position,
		// and those two must be treated OPPOSITELY: a never-positioned row IS a complete fold and is
		// adoptable, whereas a row whose state was just replaced by a value this store cannot relate to
		// the stream is not. Adopting the second stamps a position onto a state that does not contain
		// that prefix, and every event below it is then silently missing from the read model forever.
		//
		// Moving to the low-level client is what carrying the field costs here: the typed client's
		// serializer cannot send this document (see BuildDocument). The sentinel rides the same single
		// index operation as the state, so there is no window in which a destroyed position is recorded
		// as a never-established one.
		await IndexUnconditionallyAsync(
			id,
			BuildDocument(projection, ProjectionPosition.Unplaceable),
			cancellationToken).ConfigureAwait(false);
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
		ArgumentException.ThrowIfNullOrWhiteSpace(id);
		ArgumentNullException.ThrowIfNull(projection);
		await EnsureIndexAsync(cancellationToken).ConfigureAwait(false);

		await IndexUnconditionallyAsync(
			id,
			BuildDocument(projection, ProjectionPosition.Unnumbered),
			cancellationToken).ConfigureAwait(false);
	}

	/// <summary>Indexes a prepared document with no condition, failing loudly if the engine refuses.</summary>
	/// <remarks>
	/// Shared by the two unconditional writes so they cannot drift: they differ only in the position
	/// the document carries, and that difference belongs in the caller rather than in two copies of the
	/// request.
	/// </remarks>
	private async Task IndexUnconditionallyAsync(
		string id,
		string document,
		CancellationToken cancellationToken)
	{
		var response = await _client.LowLevel.IndexAsync<StringResponse>(
			_indexName,
			id,
			PostData.String(document),
			new IndexRequestParameters { Refresh = Refresh.WaitFor },
			cancellationToken).ConfigureAwait(false);

		// A rejected index used to return here indistinguishable from a successful one, so a caller
		// that had lost its write was told it had made it.
		if (!response.Success)
		{
			throw IndexingFailure(id, response);
		}
	}

	/// <inheritdoc/>
	public async Task DeleteAsync(string id, CancellationToken cancellationToken)
	{
		await EnsureIndexAsync(cancellationToken).ConfigureAwait(false);

		await _client.DeleteAsync<TProjection>(
			id,
			d => d.Index(_indexName),
			cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc/>
	[RequiresUnreferencedCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	[RequiresDynamicCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	public async Task<IReadOnlyList<TProjection>> QueryAsync(
		IDictionary<string, object>? filters,
		QueryOptions? options,
		CancellationToken cancellationToken)
	{
		await EnsureIndexAsync(cancellationToken).ConfigureAwait(false);

		var response = await _client.SearchAsync<TProjection>(s =>
		{
			s.Index(_indexName);

			// Apply the caller-supplied filters. Previously this parameter was ignored and the whole
			// index was returned regardless of filters (MS-A5 silent-wrong-results bug). Empty/null
			// filters match all (BuildQuery handles that).
			s.Query(BuildQuery(filters));

			if (options?.Take > 0)
			{
				s.Size(options.Take.Value);
			}

			if (options?.Skip > 0)
			{
				s.From(options.Skip.Value);
			}

			if (options?.OrderBy != null)
			{
				s.Sort(sort => sort.Field(
					options.OrderBy,
					options.Descending ? SortOrder.Descending : SortOrder.Ascending));
			}

			return s;
		}, cancellationToken).ConfigureAwait(false);

		return response.Documents.ToList();
	}

	/// <inheritdoc/>
	public async Task<long> CountAsync(
		IDictionary<string, object>? filters,
		CancellationToken cancellationToken)
	{
		await EnsureIndexAsync(cancellationToken).ConfigureAwait(false);

		var response = await _client.CountAsync<TProjection>(
			c => c.Index(_indexName).Query(BuildQuery(filters)),
			cancellationToken).ConfigureAwait(false);

		return response.Count;
	}

	// Translates the caller's filter dictionary into an OpenSearch query. Empty/null filters match all;
	// otherwise every (field, value) pair becomes an exact-match term, AND-combined (bool must). String
	// values target the `.keyword` sub-field produced by dynamic mapping (a term query against the
	// analyzed text field would not match an exact value) — mirroring the ES projection store's
	// GetExactMatchFieldName semantics.
	private static Func<QueryContainerDescriptor<TProjection>, QueryContainer> BuildQuery(
		IDictionary<string, object>? filters)
	{
		if (filters is null || filters.Count == 0)
		{
			return static q => q.MatchAll();
		}

		return q =>
		{
			QueryContainer? query = null;

			foreach (var filter in filters)
			{
				var fieldName = filter.Value is string ? $"{filter.Key}.keyword" : filter.Key;
				query &= q.Term(t => t.Field(fieldName).Value(filter.Value));
			}

			return query ?? q.MatchAll();
		};
	}

	private static string GetIndexName(OpenSearchProjectionStoreOptions opts)
	{
		var name = opts.IndexName ?? typeof(TProjection).Name;

		var composed = string.IsNullOrWhiteSpace(opts.IndexPrefix)
			? name
			: $"{opts.IndexPrefix}-{name}";

		// OpenSearch index names MUST be lowercase. Lowercase the entire composed name (prefix
		// included) so a consumer-supplied IndexPrefix/IndexName or environment-derived segment
		// (e.g. "Development") cannot produce an invalid index name.
#pragma warning disable CA1308 // OpenSearch index names must be lowercase
		return composed.ToLowerInvariant();
#pragma warning restore CA1308
	}

	private async Task EnsureIndexAsync(CancellationToken cancellationToken)
	{
		if (_indexVerified || !_options.CreateIndexOnInitialize)
		{
			return;
		}

		var exists = await _client.Indices.ExistsAsync(_indexName, ct: cancellationToken)
			.ConfigureAwait(false);

		if (!exists.Exists)
		{
			await _client.Indices.CreateAsync(
				_indexName,
				c => c.Settings(s => s
					.NumberOfShards(_options.NumberOfShards)
					.NumberOfReplicas(_options.NumberOfReplicas)),
				cancellationToken).ConfigureAwait(false);

			_logger.LogInformation("Created OpenSearch index {IndexName}", _indexName);
		}

		_indexVerified = true;
	}
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
	/// <b>Why not external versioning, which looks like it was made for this.</b> Indexing with an
	/// external version refuses any version not strictly greater than the stored one, so it gives
	/// monotonicity — and only monotonicity. It cannot express the other half of the contract: that the
	/// state being written was folded from the position it claims to advance FROM. A writer holding a
	/// stale read and a high position satisfies external versioning and silently drops every event in
	/// between.
	/// </para>
	/// <para>
	/// So the condition is the engine's optimistic-concurrency pair (sequence number and primary term)
	/// taken from the read, plus the position comparison the read makes possible. The pair makes the
	/// write atomic against a concurrent writer; the position comparison is what refuses a re-delivery.
	/// A spurious refusal is possible — any unrelated write moves the sequence number — and it is safe:
	/// the caller re-reads and retries.
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
		ArgumentException.ThrowIfNullOrWhiteSpace(id);
		ArgumentOutOfRangeException.ThrowIfNegative(newPosition);
		ArgumentNullException.ThrowIfNull(projection);

		await EnsureIndexAsync(cancellationToken).ConfigureAwait(false);

		var document = BuildDocument(projection, ProjectionPosition.At(newPosition));

		if (expectedPosition is null)
		{
			// Create-only. The engine's 409 IS the refusal -- never an index-as-upsert, which would let
			// a late starter reset a projection another writer is already advancing.
			var created = await _client.LowLevel.IndexAsync<StringResponse>(
				_indexName,
				id,
				PostData.String(document),
				new IndexRequestParameters
				{
					OpType = OpType.Create,
					Refresh = Refresh.WaitFor,
				},
				cancellationToken).ConfigureAwait(false);

			if (created.Success)
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

			throw IndexingFailure(id, created);
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

	/// <summary>
	/// Writes the document only if it still carries the optimistic-concurrency pair the caller read.
	/// </summary>
	/// <remarks>
	/// The sequence pair is what makes the write atomic against a concurrent writer. The position
	/// comparison the caller already performed is what refuses a re-delivery; neither substitutes for
	/// the other.
	/// </remarks>
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

		await EnsureIndexAsync(cancellationToken).ConfigureAwait(false);

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

	[RequiresUnreferencedCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	[RequiresDynamicCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	private async Task<ProjectionAdvanceResult> WriteAtSequenceAsync(
		string id,
		string document,
		long sequenceNumber,
		long primaryTerm,
		long newPosition,
		CancellationToken cancellationToken)
	{
		var response = await _client.LowLevel.IndexAsync<StringResponse>(
			_indexName,
			id,
			PostData.String(document),
			new IndexRequestParameters
			{
				IfSequenceNumber = sequenceNumber,
				IfPrimaryTerm = primaryTerm,
				Refresh = Refresh.WaitFor,
			},
			cancellationToken).ConfigureAwait(false);

		if (response.Success)
		{
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

		throw IndexingFailure(id, response);
	}

	/// <summary>
	/// Reads the document as raw JSON, returning the projection, its stored position and the
	/// optimistic-concurrency pair a conditional write needs.
	/// </summary>
	/// <remarks>
	/// Read as JSON rather than as <typeparamref name="TProjection"/> because the position is a document
	/// field the projection type does not declare, and deserializing straight to the projection would
	/// discard it. The projection is then materialised through the client's OWN source serializer, so it
	/// is the same shape the unconditional read produces.
	/// </remarks>
	[RequiresUnreferencedCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	[RequiresDynamicCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	private async Task<PositionedRead> ReadForUpdateAsync(string id, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(id);

		await EnsureIndexAsync(cancellationToken).ConfigureAwait(false);

		// Read as RAW JSON through the low-level client, not as a typed document.
		//
		// Two things have to come back from one read and the typed client can supply neither pairing.
		// Asked for the projection type it silently drops the reserved position field, because the
		// projection does not declare it. Asked for a JSON node it throws outright -- this client's
		// serializer cannot construct a System.Text.Json node ("does not have a public constructor
		// accepting IDictionary"). The raw envelope carries the source, the sequence number and the
		// primary term together, which is exactly the single observation the contract requires.
		var response = await _client.LowLevel
			.GetAsync<StringResponse>(_indexName, id, ctx: cancellationToken)
			.ConfigureAwait(false);

		// NOT `default` on either absent path. A defaulted ProjectionPosition is Positioned(0), not "no
		// position" -- the struct's zero value is a real coordinate -- so returning the struct default
		// would report an absent document as one folded up to position zero. Named explicitly instead;
		// the absence is carried by the null sequence number, which every caller tests first.
		if (response.Body is null)
		{
			return Absent();
		}

		if (JsonNode.Parse(response.Body) is not JsonObject envelope
			|| envelope["found"]?.GetValue<bool>() != true
			|| envelope["_source"] is not JsonObject source)
		{
			return Absent();
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
			envelope["_seq_no"]?.GetValue<long>(),
			envelope["_primary_term"]?.GetValue<long>());

		static PositionedRead Absent() =>
			new(null, ProjectionPosition.Unnumbered, null, null);
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
	private string BuildDocument(TProjection projection, ProjectionPosition position)
	{
		using var buffer = new MemoryStream();
		_client.SourceSerializer.Serialize(projection, buffer);

		buffer.Position = 0;

		var document = JsonNode.Parse(buffer) as JsonObject
			?? throw new InvalidOperationException(
				$"The projection type '{typeof(TProjection).Name}' did not serialize to a JSON object, so "
				+ "it cannot be stored as a document. Projections must be object-shaped.");

		document[PositionField] = position.ToStored();

		// Returned as TEXT and sent verbatim through the low-level client. The typed client cannot
		// carry this document: its serializer refuses a System.Text.Json node outright ("does not have
		// a public constructor accepting IDictionary"), and a dictionary would re-encode values the
		// source serializer has already encoded correctly. The bytes here are the client's OWN output
		// plus one field, so sending them unmodified is the only shape that cannot drift -- and now
		// that the unconditional writes go through this same builder, there is only one shape at all.
		return document.ToJsonString();
	}

	/// <summary>Materializes a projection from stored JSON using the client's own source serializer.</summary>
	[RequiresUnreferencedCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	[RequiresDynamicCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	private TProjection? Deserialize(JsonObject source)
	{
		using var buffer = new MemoryStream(Encoding.UTF8.GetBytes(source.ToJsonString()));
		return _client.SourceSerializer.Deserialize<TProjection>(buffer);
	}

	/// <summary>Whether a write was refused because another writer got there first.</summary>
	private static bool IsVersionConflict(StringResponse response) =>
		response.HttpStatusCode == 409;

	/// <summary>Builds the failure for a write the engine rejected for a reason that is not a conflict.</summary>
	private InvalidOperationException IndexingFailure(string id, StringResponse response) =>
		new(
			$"Failed to write projection '{typeof(TProjection).Name}' with id '{id}' to OpenSearch index "
			+ $"'{_indexName}': {response.Body ?? response.DebugInformation}",
			response.OriginalException);

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

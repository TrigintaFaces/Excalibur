// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.ComponentModel.DataAnnotations;

using Excalibur.Dispatch;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;

namespace Excalibur.Compliance.Stores.MongoDb;

/// <summary>
/// Configuration options for MongoDB compliance store.
/// </summary>
public sealed class MongoDbComplianceOptions
{
	/// <summary>
	/// Gets or sets the MongoDB connection string.
	/// </summary>
	[Required]
	public string? ConnectionString { get; set; }

	/// <summary>
	/// Gets or sets the database name.
	/// Default: "compliance".
	/// </summary>
	public string DatabaseName { get; set; } = "compliance";

	/// <summary>
	/// Gets or sets the collection name prefix.
	/// Default: "dispatch_".
	/// </summary>
	public string CollectionPrefix { get; set; } = "dispatch_";

	/// <summary>
	/// Gets or sets the server selection timeout in seconds.
	/// Default: 30 seconds.
	/// </summary>
	public int ServerSelectionTimeoutSeconds { get; set; } = 30;

	/// <summary>
	/// Gets or sets the connect timeout in seconds.
	/// Default: 10 seconds.
	/// </summary>
	public int ConnectTimeoutSeconds { get; set; } = 10;

	/// <summary>
	/// Gets the consent records collection name.
	/// </summary>
	internal string ConsentCollectionName => $"{CollectionPrefix}consent_records";

	/// <summary>
	/// Gets the erasure logs collection name.
	/// </summary>
	internal string ErasureLogsCollectionName => $"{CollectionPrefix}erasure_logs";

	/// <summary>
	/// Gets the subject access requests collection name.
	/// </summary>
	internal string SubjectAccessCollectionName => $"{CollectionPrefix}subject_access_requests";
}

/// <summary>
/// MongoDB implementation of <see cref="IComplianceStore"/>.
/// </summary>
/// <remarks>
/// <para>
/// Provides durable storage for consent records, erasure logs, and subject access
/// request tracking in MongoDB. This implementation uses the MongoDB .NET driver
/// for document operations.
/// </para>
/// <para>
/// Collections created: {prefix}consent_records, {prefix}erasure_logs, {prefix}subject_access_requests.
/// </para>
/// <para>
/// Upsert identity comes from the document key, not from a secondary index. The consent key is the
/// tenant, subject and purpose together; the subject-access key is the tenant and request identifier.
/// The tenant participates in both because those keys are the upsert conflict targets: without it, two
/// tenants recording data for the same subject would collapse onto one document and the later write
/// would overwrite the earlier tenant's record.
/// </para>
/// </remarks>
public sealed partial class MongoDbComplianceStore : IComplianceStore, IDisposable
{
	private readonly MongoDbComplianceOptions _options;
	private readonly ILogger<MongoDbComplianceStore> _logger;
	private readonly bool _ownsClient;
	private readonly ITenantContext _tenantContext;

	/// <summary>
	/// Gets the tenant scope this store runs under, resolved in one place so every statement it builds binds
	/// the same term.
	/// </summary>
	/// <remarks>
	/// The context is <strong>required</strong>, so there is no branch here and no fallback for the store to
	/// pick on the host's behalf. A single-tenant host receives the framework's default context and operates
	/// as the one canonical tenant; a multi-tenant host receives the resolving context. Both are decisions
	/// the composition root made and this store reads. An optional context could not be relied on: a store
	/// handed <see langword="null"/> would silently widen to the untenanted partition, and — because nothing
	/// downstream can tell a deliberately untenanted store from an unwired one — it could not honestly
	/// attest that it scopes by tenant at all.
	/// </remarks>
	private TenantScope CurrentTenantScope => TenantScope.FromContext(_tenantContext);

	private IMongoClient? _client;
	private IMongoDatabase? _database;
	private IMongoCollection<ConsentDocument>? _consentCollection;
	private IMongoCollection<ErasureLogDocument>? _erasureLogCollection;
	private IMongoCollection<SubjectAccessDocument>? _subjectAccessCollection;
	private volatile bool _disposed;
	// Serialises first-time initialisation. Without it two concurrent first callers race:
	// one assigns the client and is still assigning the collection when the other observes a
	// non-null client, skips the whole block, and dereferences a collection that is still null.
	// That is a NullReferenceException a few instructions wide, so it is intermittent and
	// load-dependent -- it was observed in CI on two different stores in a single run.
	private readonly SemaphoreSlim _initLock = new(1, 1);

	// volatile: the fast path reads this outside the lock.
	private volatile bool _initialized;

	/// <summary>
	/// Initializes a new instance of the <see cref="MongoDbComplianceStore"/> class.
	/// </summary>
	/// <param name="options">The MongoDB compliance options.</param>
	/// <param name="tenantContext">
	/// The ambient tenant context. Required: this store partitions its documents by tenant and resolves that
	/// partition from here, so there is no state in which the partition is undecided. A single-tenant host
	/// receives the framework's default context and operates as the one canonical tenant.
	/// </param>
	/// <param name="logger">The logger.</param>
	// Deterministic DI construction: the advanced constructor below also accepts an ITenantContext, so
	// without this marker ActivatorUtilities' selection depends on which services happen to be
	// registered, and reports a missing dependency as a constructor ambiguity.
	[ActivatorUtilitiesConstructor]
	public MongoDbComplianceStore(
		IOptions<MongoDbComplianceOptions> options,
		ITenantContext tenantContext,
		ILogger<MongoDbComplianceStore> logger)
	{
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(tenantContext);
		ArgumentNullException.ThrowIfNull(logger);

		_options = options.Value;
		_logger = logger;
		_tenantContext = tenantContext;
		_ownsClient = true;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="MongoDbComplianceStore"/> class
	/// with an existing MongoDB client.
	/// </summary>
	/// <param name="client">An existing MongoDB client.</param>
	/// <param name="options">The MongoDB compliance options.</param>
	/// <param name="tenantContext">
	/// The ambient tenant context. Required: this store partitions its documents by tenant and resolves that
	/// partition from here, so there is no state in which the partition is undecided. A single-tenant host
	/// receives the framework's default context and operates as the one canonical tenant.
	/// </param>
	/// <param name="logger">The logger.</param>
	public MongoDbComplianceStore(
		IMongoClient client,
		IOptions<MongoDbComplianceOptions> options,
		ITenantContext tenantContext,
		ILogger<MongoDbComplianceStore> logger)
	{
		ArgumentNullException.ThrowIfNull(client);
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(tenantContext);
		ArgumentNullException.ThrowIfNull(logger);

		_options = options.Value;
		_logger = logger;
		_tenantContext = tenantContext;
		_ownsClient = false;
		_client = client;
		_database = client.GetDatabase(_options.DatabaseName);
		_consentCollection = _database.GetCollection<ConsentDocument>(_options.ConsentCollectionName);
		_erasureLogCollection = _database.GetCollection<ErasureLogDocument>(_options.ErasureLogsCollectionName);
		_subjectAccessCollection = _database.GetCollection<SubjectAccessDocument>(_options.SubjectAccessCollectionName);
	}

	/// <summary>
	/// Gets the tenant value written to and filtered on by every operation, resolved for the current call.
	/// </summary>
	/// <remarks>
	/// Never empty. An unscoped store uses the reserved framework sentinel rather than an empty string, so a
	/// tenant term is always present in the document key and a missing tenant can never read as "matches
	/// anything". Resolved per operation because this store outlives any single tenant's request.
	/// </remarks>
	private string TenantValue
	{
		get
		{
			var scope = CurrentTenantScope;
			return scope.TenantId;
		}
	}

	/// <inheritdoc />
	public async Task StoreConsentAsync(
		ConsentRecord record,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(record);

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var document = ConsentDocument.FromRecord(record, TenantValue);

		var filter = Builders<ConsentDocument>.Filter.Eq(d => d.Id, document.Id);

		_ = await _consentCollection!.ReplaceOneAsync(
			filter,
			document,
			new ReplaceOptions { IsUpsert = true },
			cancellationToken).ConfigureAwait(false);

		LogMongoDbOperation("StoreConsent", record.SubjectId);
	}

	/// <inheritdoc />
	public async Task<ConsentRecord?> GetConsentAsync(
		string subjectId,
		string purpose,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(subjectId);
		ArgumentException.ThrowIfNullOrWhiteSpace(purpose);

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var id = ConsentDocument.CreateId(TenantValue, subjectId, purpose);
		var filter = Builders<ConsentDocument>.Filter.Eq(d => d.Id, id);

		var document = await _consentCollection!
			.Find(filter)
			.FirstOrDefaultAsync(cancellationToken)
			.ConfigureAwait(false);

		LogMongoDbOperation("GetConsent", subjectId);

		return document?.ToConsentRecord();
	}

	/// <inheritdoc />
	public async Task StoreErasureLogAsync(
		string subjectId,
		string details,
		DateTimeOffset erasedAt,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(subjectId);

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var document = new ErasureLogDocument
		{
			Id = $"{subjectId}_{erasedAt:O}_{Guid.NewGuid():N}",
			TenantId = TenantValue,
			SubjectId = subjectId,
			Details = details ?? string.Empty,
			ErasedAt = erasedAt
		};

		await _erasureLogCollection!.InsertOneAsync(document, cancellationToken: cancellationToken).ConfigureAwait(false);

		LogMongoDbOperation("StoreErasureLog", subjectId);
	}

	/// <inheritdoc />
	public async Task StoreSubjectAccessRequestAsync(
		SubjectAccessResult result,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(result);

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var document = SubjectAccessDocument.FromResult(result, TenantValue);

		var filter = Builders<SubjectAccessDocument>.Filter.Eq(d => d.Id, document.Id);

		_ = await _subjectAccessCollection!.ReplaceOneAsync(
			filter,
			document,
			new ReplaceOptions { IsUpsert = true },
			cancellationToken).ConfigureAwait(false);

		LogMongoDbOperation("StoreSubjectAccessRequest", result.RequestId);
	}

	/// <inheritdoc />
	public void Dispose()
	{
		if (_disposed)
		{
			return;
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

		if (_ownsClient && _client is IDisposable disposableClient)
		{
			disposableClient.Dispose();
		}
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
			// Re-check under the lock: the winner of the race above completed initialisation
			// while this caller was waiting, and repeating the work would be wrong as well as wasteful.
			if (_initialized)
			{
				return;
			}
			if (_client == null)
			{
				if (string.IsNullOrWhiteSpace(_options.ConnectionString))
				{
					throw new InvalidOperationException(
						$"'{nameof(MongoDbComplianceOptions)}.{nameof(MongoDbComplianceOptions.ConnectionString)}' is required. " +
						$"Configure it via services.Configure<{nameof(MongoDbComplianceOptions)}>(config.GetSection(\"MongoDbCompliance\")) " +
						"or set the ConnectionString property directly.");
				}

				var settings = MongoClientSettings.FromConnectionString(_options.ConnectionString);
				settings.ServerSelectionTimeout = TimeSpan.FromSeconds(_options.ServerSelectionTimeoutSeconds);
				settings.ConnectTimeout = TimeSpan.FromSeconds(_options.ConnectTimeoutSeconds);

				_client = new MongoClient(settings);
				_database = _client.GetDatabase(_options.DatabaseName);
				_consentCollection = _database.GetCollection<ConsentDocument>(_options.ConsentCollectionName);
				_erasureLogCollection = _database.GetCollection<ErasureLogDocument>(_options.ErasureLogsCollectionName);
				_subjectAccessCollection = _database.GetCollection<SubjectAccessDocument>(_options.SubjectAccessCollectionName);
			}

			// Create unique index on erasure logs for subject_id queries
			// Tenant-first compound index: every read of this collection is tenant-scoped, so leading with the
			// tenant lets a scoped scan use the index prefix instead of scanning other tenants' documents.
			var erasureIndexBuilder = Builders<ErasureLogDocument>.IndexKeys;
			var subjectIdIndex = new CreateIndexModel<ErasureLogDocument>(
				erasureIndexBuilder.Ascending(d => d.TenantId).Ascending(d => d.SubjectId));

			_ = await _erasureLogCollection!.Indexes
				.CreateOneAsync(subjectIdIndex, cancellationToken: cancellationToken)
				.ConfigureAwait(false);

			_initialized = true;
		}
		finally
		{
			_ = _initLock.Release();
		}
	}

	[LoggerMessage(
		LogLevel.Debug,
		"MongoDB compliance store: {Operation} for {Identifier}")]
	private partial void LogMongoDbOperation(string operation, string identifier);

	/// <summary>
	/// MongoDB document for consent records.
	/// Uses a composite key of subject_id and purpose as the document ID.
	/// </summary>
	internal sealed class ConsentDocument
	{
		[BsonId] public string Id { get; set; } = string.Empty;

		/// <summary>
		/// Gets or sets the owning tenant, or the reserved framework sentinel when unscoped.
		/// </summary>
		/// <remarks>
		/// Never empty. Persisted as its own element so every read can filter by tenant directly and so the
		/// owning tenant is legible to an operator inspecting a document.
		/// </remarks>
		[BsonElement("tenant_id")] public string TenantId { get; set; } = string.Empty;

		[BsonElement("subject_id")] public string SubjectId { get; set; } = string.Empty;

		[BsonElement("purpose")] public string Purpose { get; set; } = string.Empty;

		[BsonElement("granted_at")] [BsonRepresentation(BsonType.DateTime)] public DateTimeOffset GrantedAt { get; set; }

		[BsonElement("expires_at")] [BsonRepresentation(BsonType.DateTime)] public DateTimeOffset? ExpiresAt { get; set; }

		[BsonElement("legal_basis")] public int LegalBasis { get; set; }

		[BsonElement("is_withdrawn")] public bool IsWithdrawn { get; set; }

		[BsonElement("withdrawn_at")] [BsonRepresentation(BsonType.DateTime)] public DateTimeOffset? WithdrawnAt { get; set; }

		/// <summary>
		/// Builds the document key for a tenant's consent record.
		/// </summary>
		/// <remarks>
		/// <para>
		/// The tenant participates in the key because this <c>_id</c> is the upsert conflict target: without
		/// it, two tenants recording consent for the same subject and purpose produce the same key, and the
		/// second tenant's write silently overwrites the first tenant's — a cross-tenant write, not merely a
		/// read leak.
		/// </para>
		/// <para>
		/// Each part is length-prefixed rather than simply delimited. A plain <c>tenant:subject:purpose</c>
		/// join is ambiguous when a value may itself contain the delimiter — tenant <c>"a:b"</c> with subject
		/// <c>"c"</c> and tenant <c>"a"</c> with subject <c>"b:c"</c> collapse to the same string — which would
		/// reintroduce the very collision this key exists to prevent, for any consumer whose tenant
		/// identifiers contain a colon. Length prefixes make the encoding injective for arbitrary inputs.
		/// </para>
		/// </remarks>
		public static string CreateId(string tenantId, string subjectId, string purpose)
			=> $"{tenantId.Length}:{tenantId}:{subjectId.Length}:{subjectId}:{purpose}";

		public static ConsentDocument FromRecord(ConsentRecord record, string tenantId) => new()
		{
			Id = CreateId(tenantId, record.SubjectId, record.Purpose),
			TenantId = tenantId,
			SubjectId = record.SubjectId,
			Purpose = record.Purpose,
			GrantedAt = record.GrantedAt,
			ExpiresAt = record.ExpiresAt,
			LegalBasis = (int)record.LegalBasis,
			IsWithdrawn = record.IsWithdrawn,
			WithdrawnAt = record.WithdrawnAt
		};

		public ConsentRecord ToConsentRecord() => new()
		{
			SubjectId = SubjectId,
			Purpose = Purpose,
			GrantedAt = GrantedAt,
			ExpiresAt = ExpiresAt,
			LegalBasis = (LegalBasis)LegalBasis,
			IsWithdrawn = IsWithdrawn,
			WithdrawnAt = WithdrawnAt
		};
	}

	/// <summary>
	/// MongoDB document for erasure log entries.
	/// </summary>
	internal sealed class ErasureLogDocument
	{
		[BsonId] public string Id { get; set; } = string.Empty;

		/// <summary>
		/// Gets or sets the owning tenant, or the reserved framework sentinel when unscoped.
		/// </summary>
		/// <remarks>
		/// Never empty. Persisted as its own element so every read can filter by tenant directly and so the
		/// owning tenant is legible to an operator inspecting a document.
		/// </remarks>
		[BsonElement("tenant_id")] public string TenantId { get; set; } = string.Empty;

		[BsonElement("subject_id")] public string SubjectId { get; set; } = string.Empty;

		[BsonElement("details")] public string Details { get; set; } = string.Empty;

		[BsonElement("erased_at")] [BsonRepresentation(BsonType.DateTime)] public DateTimeOffset ErasedAt { get; set; }
	}

	/// <summary>
	/// MongoDB document for subject access requests.
	/// Uses request_id as the document ID.
	/// </summary>
	internal sealed class SubjectAccessDocument
	{
		[BsonId] public string Id { get; set; } = string.Empty;

		/// <summary>
		/// Gets or sets the owning tenant, or the reserved framework sentinel when unscoped.
		/// </summary>
		[BsonElement("tenant_id")] public string TenantId { get; set; } = string.Empty;

		[BsonElement("status")] public int Status { get; set; }

		[BsonElement("deadline")] [BsonRepresentation(BsonType.DateTime)] public DateTimeOffset? Deadline { get; set; }

		[BsonElement("fulfilled_at")] [BsonRepresentation(BsonType.DateTime)] public DateTimeOffset? FulfilledAt { get; set; }

		/// <summary>
		/// Builds the document key for a tenant's subject-access request.
		/// </summary>
		/// <remarks>
		/// The request identifier alone is not a safe key: it is supplied by the caller, so two tenants can
		/// legitimately present the same one, and this <c>_id</c> is the upsert conflict target — the second
		/// tenant's write would overwrite the first tenant's request. Length-prefixed for the same reason as
		/// the consent key: a plain delimiter join is ambiguous when a tenant identifier may contain the
		/// delimiter.
		/// </remarks>
		public static string CreateId(string tenantId, string requestId)
			=> $"{tenantId.Length}:{tenantId}:{requestId}";

		public static SubjectAccessDocument FromResult(SubjectAccessResult result, string tenantId) => new()
		{
			Id = CreateId(tenantId, result.RequestId),
			TenantId = tenantId,
			Status = (int)result.Status,
			Deadline = result.Deadline,
			FulfilledAt = result.FulfilledAt
		};
	}
}

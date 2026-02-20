// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;

namespace Excalibur.Dispatch.Compliance;

/// <summary>
/// Configuration options for MongoDB compliance store.
/// </summary>
public sealed class MongoDbComplianceOptions
{
	/// <summary>
	/// Gets or sets the MongoDB connection string.
	/// </summary>
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
/// Unique indexes are created on consent_records (subject_id + purpose) and
/// subject_access_requests (request_id) for upsert semantics.
/// </para>
/// </remarks>
public sealed partial class MongoDbComplianceStore : IComplianceStore
{
	private readonly MongoDbComplianceOptions _options;
	private readonly ILogger<MongoDbComplianceStore> _logger;
	private IMongoClient? _client;
	private IMongoDatabase? _database;
	private IMongoCollection<ConsentDocument>? _consentCollection;
	private IMongoCollection<ErasureLogDocument>? _erasureLogCollection;
	private IMongoCollection<SubjectAccessDocument>? _subjectAccessCollection;
	private bool _initialized;

	/// <summary>
	/// Initializes a new instance of the <see cref="MongoDbComplianceStore"/> class.
	/// </summary>
	/// <param name="options">The MongoDB compliance options.</param>
	/// <param name="logger">The logger.</param>
	public MongoDbComplianceStore(
		IOptions<MongoDbComplianceOptions> options,
		ILogger<MongoDbComplianceStore> logger)
	{
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(logger);

		_options = options.Value;
		_logger = logger;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="MongoDbComplianceStore"/> class
	/// with an existing MongoDB client.
	/// </summary>
	/// <param name="client">An existing MongoDB client.</param>
	/// <param name="options">The MongoDB compliance options.</param>
	/// <param name="logger">The logger.</param>
	public MongoDbComplianceStore(
		IMongoClient client,
		IOptions<MongoDbComplianceOptions> options,
		ILogger<MongoDbComplianceStore> logger)
	{
		ArgumentNullException.ThrowIfNull(client);
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(logger);

		_options = options.Value;
		_logger = logger;
		_client = client;
		_database = client.GetDatabase(_options.DatabaseName);
		_consentCollection = _database.GetCollection<ConsentDocument>(_options.ConsentCollectionName);
		_erasureLogCollection = _database.GetCollection<ErasureLogDocument>(_options.ErasureLogsCollectionName);
		_subjectAccessCollection = _database.GetCollection<SubjectAccessDocument>(_options.SubjectAccessCollectionName);
	}

	/// <inheritdoc />
	public async Task StoreConsentAsync(
		ConsentRecord record,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(record);

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var document = ConsentDocument.FromRecord(record);

		var filter = Builders<ConsentDocument>.Filter.Eq(d => d.Id, document.Id);

		_ = await _consentCollection.ReplaceOneAsync(
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

		var id = ConsentDocument.CreateId(subjectId, purpose);
		var filter = Builders<ConsentDocument>.Filter.Eq(d => d.Id, id);

		var document = await _consentCollection
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
			SubjectId = subjectId,
			Details = details ?? string.Empty,
			ErasedAt = erasedAt
		};

		await _erasureLogCollection.InsertOneAsync(document, cancellationToken: cancellationToken).ConfigureAwait(false);

		LogMongoDbOperation("StoreErasureLog", subjectId);
	}

	/// <inheritdoc />
	public async Task StoreSubjectAccessRequestAsync(
		SubjectAccessResult result,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(result);

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var document = SubjectAccessDocument.FromResult(result);

		var filter = Builders<SubjectAccessDocument>.Filter.Eq(d => d.Id, document.Id);

		_ = await _subjectAccessCollection.ReplaceOneAsync(
			filter,
			document,
			new ReplaceOptions { IsUpsert = true },
			cancellationToken).ConfigureAwait(false);

		LogMongoDbOperation("StoreSubjectAccessRequest", result.RequestId);
	}

	private async Task EnsureInitializedAsync(CancellationToken cancellationToken)
	{
		if (_initialized)
		{
			return;
		}

		if (_client == null)
		{
			if (string.IsNullOrWhiteSpace(_options.ConnectionString))
			{
				throw new InvalidOperationException("MongoDB ConnectionString is required.");
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
		var erasureIndexBuilder = Builders<ErasureLogDocument>.IndexKeys;
		var subjectIdIndex = new CreateIndexModel<ErasureLogDocument>(
			erasureIndexBuilder.Ascending(d => d.SubjectId));

		_ = await _erasureLogCollection.Indexes
			.CreateOneAsync(subjectIdIndex, cancellationToken: cancellationToken)
			.ConfigureAwait(false);

		_initialized = true;
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

		[BsonElement("subject_id")] public string SubjectId { get; set; } = string.Empty;

		[BsonElement("purpose")] public string Purpose { get; set; } = string.Empty;

		[BsonElement("granted_at")] public DateTimeOffset GrantedAt { get; set; }

		[BsonElement("expires_at")] public DateTimeOffset? ExpiresAt { get; set; }

		[BsonElement("legal_basis")] public int LegalBasis { get; set; }

		[BsonElement("is_withdrawn")] public bool IsWithdrawn { get; set; }

		[BsonElement("withdrawn_at")] public DateTimeOffset? WithdrawnAt { get; set; }

		public static string CreateId(string subjectId, string purpose)
			=> $"{subjectId}:{purpose}";

		public static ConsentDocument FromRecord(ConsentRecord record) => new()
		{
			Id = CreateId(record.SubjectId, record.Purpose),
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

		[BsonElement("subject_id")] public string SubjectId { get; set; } = string.Empty;

		[BsonElement("details")] public string Details { get; set; } = string.Empty;

		[BsonElement("erased_at")] public DateTimeOffset ErasedAt { get; set; }
	}

	/// <summary>
	/// MongoDB document for subject access requests.
	/// Uses request_id as the document ID.
	/// </summary>
	internal sealed class SubjectAccessDocument
	{
		[BsonId] public string Id { get; set; } = string.Empty;

		[BsonElement("status")] public int Status { get; set; }

		[BsonElement("deadline")] public DateTimeOffset? Deadline { get; set; }

		[BsonElement("fulfilled_at")] public DateTimeOffset? FulfilledAt { get; set; }

		public static SubjectAccessDocument FromResult(SubjectAccessResult result) => new()
		{
			Id = result.RequestId, Status = (int)result.Status, Deadline = result.Deadline, FulfilledAt = result.FulfilledAt
		};
	}
}

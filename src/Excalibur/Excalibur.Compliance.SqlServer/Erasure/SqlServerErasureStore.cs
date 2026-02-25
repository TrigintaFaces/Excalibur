// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

using Dapper;

using Excalibur.Dispatch.Compliance;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Compliance.SqlServer.Erasure;

/// <summary>
/// SQL Server implementation of <see cref="IErasureStore"/> using Dapper.
/// </summary>
/// <remarks>
/// This store provides:
/// <list type="bullet">
/// <item>Secure storage of erasure requests with hashed data subject IDs</item>
/// <item>Compliance certificate persistence for audit trails</item>
/// <item>Support for GDPR 30-day deadline tracking</item>
/// <item>7-year certificate retention for regulatory compliance</item>
/// </list>
/// </remarks>
public sealed partial class SqlServerErasureStore : IErasureStore, IErasureCertificateStore, IErasureQueryStore
{
	private readonly SqlServerErasureStoreOptions _options;
	private readonly ILogger<SqlServerErasureStore> _logger;
	private volatile bool _initialized;

	/// <summary>
	/// Initializes a new instance of the <see cref="SqlServerErasureStore"/> class.
	/// </summary>
	public SqlServerErasureStore(
		IOptions<SqlServerErasureStoreOptions> options,
		ILogger<SqlServerErasureStore> logger)
	{
		_options = options?.Value ?? throw new ArgumentNullException(nameof(options));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));

		_options.Validate();
	}

	/// <inheritdoc />
	public async Task SaveRequestAsync(
		ErasureRequest request,
		DateTimeOffset scheduledExecutionTime,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(request);
		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var sql = $@"
			INSERT INTO {_options.FullRequestsTableName}
				(RequestId, DataSubjectIdHash, IdType, TenantId, Scope, LegalBasis,
				 ExternalReference, RequestedBy, RequestedAt, ScheduledExecutionAt,
				 Status, DataCategories, CreatedAt, UpdatedAt)
			VALUES
				(@RequestId, @DataSubjectIdHash, @IdType, @TenantId, @Scope, @LegalBasis,
				 @ExternalReference, @RequestedBy, @RequestedAt, @ScheduledExecutionAt,
				 @Status, @DataCategories, @CreatedAt, @UpdatedAt)";

		await using var connection = new SqlConnection(_options.ConnectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		var now = DateTimeOffset.UtcNow;
		_ = await connection.ExecuteAsync(new CommandDefinition(sql, new
		{
			request.RequestId,
			DataSubjectIdHash = HashDataSubjectId(request.DataSubjectId),
			IdType = (int)request.IdType,
			request.TenantId,
			Scope = (int)request.Scope,
			LegalBasis = (int)request.LegalBasis,
			request.ExternalReference,
			request.RequestedBy,
			request.RequestedAt,
			ScheduledExecutionAt = scheduledExecutionTime,
			Status = (int)ErasureRequestStatus.Scheduled,
			DataCategories = request.DataCategories is not null
				? JsonSerializer.Serialize(
					request.DataCategories,
					SqlServerComplianceJsonContext.Default.IReadOnlyListString)
				: null,
			CreatedAt = now,
			UpdatedAt = now
		}, cancellationToken: cancellationToken, commandTimeout: _options.CommandTimeoutSeconds)).ConfigureAwait(false);

		LogSavedRequest(request.RequestId, scheduledExecutionTime);
	}

	/// <inheritdoc />
	public async Task<ErasureStatus?> GetStatusAsync(
		Guid requestId,
		CancellationToken cancellationToken)
	{
		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var sql = $@"
			SELECT RequestId, DataSubjectIdHash, IdType, TenantId, Scope, LegalBasis,
				   ExternalReference, RequestedBy, RequestedAt, ScheduledExecutionAt,
				   ExecutedAt, CompletedAt, CancelledAt, CancellationReason, CancelledBy,
				   Status, KeysDeleted, RecordsAffected, CertificateId, ErrorMessage, UpdatedAt
			FROM {_options.FullRequestsTableName}
			WHERE RequestId = @RequestId";

		await using var connection = new SqlConnection(_options.ConnectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		var row = await connection.QuerySingleOrDefaultAsync<ErasureRequestRow>(
				new CommandDefinition(sql, new { RequestId = requestId }, cancellationToken: cancellationToken, commandTimeout: _options.CommandTimeoutSeconds))
			.ConfigureAwait(false);

		return row?.ToStatus();
	}

	/// <inheritdoc />
	public async Task<bool> UpdateStatusAsync(
		Guid requestId,
		ErasureRequestStatus status,
		string? errorMessage,
		CancellationToken cancellationToken)
	{
		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var sql = $@"
			UPDATE {_options.FullRequestsTableName}
			SET Status = @Status,
				ErrorMessage = @ErrorMessage,
				ExecutedAt = CASE WHEN @Status = {(int)ErasureRequestStatus.InProgress} THEN @Now ELSE ExecutedAt END,
				UpdatedAt = @Now
			WHERE RequestId = @RequestId";

		await using var connection = new SqlConnection(_options.ConnectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		var affected = await connection.ExecuteAsync(new CommandDefinition(sql,
			new { RequestId = requestId, Status = (int)status, ErrorMessage = errorMessage, Now = DateTimeOffset.UtcNow },
			cancellationToken: cancellationToken, commandTimeout: _options.CommandTimeoutSeconds)).ConfigureAwait(false);

		return affected > 0;
	}

	/// <inheritdoc />
	public async Task RecordCompletionAsync(
		Guid requestId,
		int keysDeleted,
		int recordsAffected,
		Guid certificateId,
		CancellationToken cancellationToken)
	{
		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var sql = $@"
			UPDATE {_options.FullRequestsTableName}
			SET Status = @Status,
				KeysDeleted = @KeysDeleted,
				RecordsAffected = @RecordsAffected,
				CertificateId = @CertificateId,
				CompletedAt = @Now,
				UpdatedAt = @Now
			WHERE RequestId = @RequestId";

		await using var connection = new SqlConnection(_options.ConnectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		_ = await connection.ExecuteAsync(new CommandDefinition(sql,
			new
			{
				RequestId = requestId,
				Status = (int)ErasureRequestStatus.Completed,
				KeysDeleted = keysDeleted,
				RecordsAffected = recordsAffected,
				CertificateId = certificateId,
				Now = DateTimeOffset.UtcNow
			}, cancellationToken: cancellationToken, commandTimeout: _options.CommandTimeoutSeconds)).ConfigureAwait(false);
	}

	/// <inheritdoc />
	public async Task<bool> RecordCancellationAsync(
		Guid requestId,
		string reason,
		string cancelledBy,
		CancellationToken cancellationToken)
	{
		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var sql = $@"
			UPDATE {_options.FullRequestsTableName}
			SET Status = @Status,
				CancellationReason = @Reason,
				CancelledBy = @CancelledBy,
				CancelledAt = @Now,
				UpdatedAt = @Now
			WHERE RequestId = @RequestId
			  AND Status IN ({(int)ErasureRequestStatus.Pending}, {(int)ErasureRequestStatus.Scheduled})";

		await using var connection = new SqlConnection(_options.ConnectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		var affected = await connection.ExecuteAsync(new CommandDefinition(sql,
			new
			{
				RequestId = requestId,
				Status = (int)ErasureRequestStatus.Cancelled,
				Reason = reason,
				CancelledBy = cancelledBy,
				Now = DateTimeOffset.UtcNow
			}, cancellationToken: cancellationToken, commandTimeout: _options.CommandTimeoutSeconds)).ConfigureAwait(false);

		return affected > 0;
	}

	/// <inheritdoc />
	public async Task<IReadOnlyList<ErasureStatus>> GetScheduledRequestsAsync(
		int maxResults,
		CancellationToken cancellationToken)
	{
		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var sql = $@"
			SELECT TOP (@MaxResults)
				   RequestId, DataSubjectIdHash, IdType, TenantId, Scope, LegalBasis,
				   ExternalReference, RequestedBy, RequestedAt, ScheduledExecutionAt,
				   ExecutedAt, CompletedAt, CancelledAt, CancellationReason, CancelledBy,
				   Status, KeysDeleted, RecordsAffected, CertificateId, ErrorMessage, UpdatedAt
			FROM {_options.FullRequestsTableName}
			WHERE Status = {(int)ErasureRequestStatus.Scheduled}
			  AND ScheduledExecutionAt <= @Now
			ORDER BY ScheduledExecutionAt";

		await using var connection = new SqlConnection(_options.ConnectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		var rows = await connection.QueryAsync<ErasureRequestRow>(
			new CommandDefinition(sql, new { MaxResults = maxResults, Now = DateTimeOffset.UtcNow },
				cancellationToken: cancellationToken, commandTimeout: _options.CommandTimeoutSeconds)).ConfigureAwait(false);

		return rows.Select(r => r.ToStatus()).ToList();
	}

	/// <inheritdoc />
	public async Task<IReadOnlyList<ErasureStatus>> ListRequestsAsync(
		ErasureRequestStatus? status,
		string? tenantId,
		DateTimeOffset? fromDate,
		DateTimeOffset? toDate,
		int pageNumber,
		int pageSize,
		CancellationToken cancellationToken)
	{
		ArgumentOutOfRangeException.ThrowIfLessThan(pageNumber, 1);
		ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);
		ArgumentOutOfRangeException.ThrowIfGreaterThan(pageSize, 1000);

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var whereClauses = new List<string>();
		var parameters = new DynamicParameters();

		if (status.HasValue)
		{
			whereClauses.Add("Status = @Status");
			parameters.Add("Status", (int)status.Value);
		}

		if (!string.IsNullOrEmpty(tenantId))
		{
			whereClauses.Add("TenantId = @TenantId");
			parameters.Add("TenantId", tenantId);
		}

		if (fromDate.HasValue)
		{
			whereClauses.Add("RequestedAt >= @FromDate");
			parameters.Add("FromDate", fromDate.Value);
		}

		if (toDate.HasValue)
		{
			whereClauses.Add("RequestedAt <= @ToDate");
			parameters.Add("ToDate", toDate.Value);
		}

		var whereClause = whereClauses.Count > 0
			? "WHERE " + string.Join(" AND ", whereClauses)
			: string.Empty;

		var offset = (pageNumber - 1) * pageSize;
		parameters.Add("Offset", offset);
		parameters.Add("PageSize", pageSize);

		var sql = $@"
			SELECT RequestId, DataSubjectIdHash, IdType, TenantId, Scope, LegalBasis,
				   ExternalReference, RequestedBy, RequestedAt, ScheduledExecutionAt,
				   ExecutedAt, CompletedAt, CancelledAt, CancellationReason, CancelledBy,
				   Status, KeysDeleted, RecordsAffected, CertificateId, ErrorMessage, UpdatedAt
			FROM {_options.FullRequestsTableName}
			{whereClause}
			ORDER BY RequestedAt DESC
			OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";

		await using var connection = new SqlConnection(_options.ConnectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		var rows = await connection.QueryAsync<ErasureRequestRow>(
			new CommandDefinition(sql, parameters, cancellationToken: cancellationToken, commandTimeout: _options.CommandTimeoutSeconds)).ConfigureAwait(false);

		return rows.Select(r => r.ToStatus()).ToList();
	}

	/// <inheritdoc />
	public async Task SaveCertificateAsync(
		ErasureCertificate certificate,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(certificate);
		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var sql = $@"
			INSERT INTO {_options.FullCertificatesTableName}
				(CertificateId, RequestId, DataSubjectReference, RequestReceivedAt, CompletedAt,
				 Method, Summary, Verification, LegalBasis, Signature, RetainUntil, CreatedAt)
			VALUES
				(@CertificateId, @RequestId, @DataSubjectReference, @RequestReceivedAt, @CompletedAt,
				 @Method, @Summary, @Verification, @LegalBasis, @Signature, @RetainUntil, @CreatedAt)";

		await using var connection = new SqlConnection(_options.ConnectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		_ = await connection.ExecuteAsync(new CommandDefinition(sql, new
		{
			certificate.CertificateId,
			certificate.RequestId,
			certificate.DataSubjectReference,
			certificate.RequestReceivedAt,
			certificate.CompletedAt,
			Method = (int)certificate.Method,
			Summary = JsonSerializer.Serialize(
				certificate.Summary,
				SqlServerComplianceJsonContext.Default.ErasureSummary),
			Verification = JsonSerializer.Serialize(
				certificate.Verification,
				SqlServerComplianceJsonContext.Default.VerificationSummary),
			LegalBasis = (int)certificate.LegalBasis,
			certificate.Signature,
			certificate.RetainUntil,
			CreatedAt = DateTimeOffset.UtcNow
		}, cancellationToken: cancellationToken, commandTimeout: _options.CommandTimeoutSeconds)).ConfigureAwait(false);

		LogSavedCertificate(certificate.CertificateId, certificate.RequestId);
	}

	/// <inheritdoc />
	public async Task<ErasureCertificate?> GetCertificateAsync(
		Guid requestId,
		CancellationToken cancellationToken)
	{
		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var sql = $@"
			SELECT CertificateId, RequestId, DataSubjectReference, RequestReceivedAt, CompletedAt,
				   Method, Summary, Verification, LegalBasis, Signature, RetainUntil
			FROM {_options.FullCertificatesTableName}
			WHERE RequestId = @RequestId";

		await using var connection = new SqlConnection(_options.ConnectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		var row = await connection.QuerySingleOrDefaultAsync<CertificateRow>(
				new CommandDefinition(sql, new { RequestId = requestId }, cancellationToken: cancellationToken, commandTimeout: _options.CommandTimeoutSeconds))
			.ConfigureAwait(false);

		return row?.ToCertificate();
	}

	/// <inheritdoc />
	public async Task<ErasureCertificate?> GetCertificateByIdAsync(
		Guid certificateId,
		CancellationToken cancellationToken)
	{
		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var sql = $@"
			SELECT CertificateId, RequestId, DataSubjectReference, RequestReceivedAt, CompletedAt,
				   Method, Summary, Verification, LegalBasis, Signature, RetainUntil
			FROM {_options.FullCertificatesTableName}
			WHERE CertificateId = @CertificateId";

		await using var connection = new SqlConnection(_options.ConnectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		var row = await connection.QuerySingleOrDefaultAsync<CertificateRow>(
				new CommandDefinition(sql, new { CertificateId = certificateId }, cancellationToken: cancellationToken, commandTimeout: _options.CommandTimeoutSeconds))
			.ConfigureAwait(false);

		return row?.ToCertificate();
	}

	/// <inheritdoc />
	public async Task<int> CleanupExpiredCertificatesAsync(
		CancellationToken cancellationToken)
	{
		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var sql = $@"
			DELETE FROM {_options.FullCertificatesTableName}
			WHERE RetainUntil < @Now";

		await using var connection = new SqlConnection(_options.ConnectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		var deleted = await connection.ExecuteAsync(
				new CommandDefinition(sql, new { Now = DateTimeOffset.UtcNow }, cancellationToken: cancellationToken, commandTimeout: _options.CommandTimeoutSeconds))
			.ConfigureAwait(false);

		if (deleted > 0)
		{
			LogCleanedUpCertificates(deleted);
		}

		return deleted;
	}

	/// <inheritdoc />
	public object? GetService(Type serviceType)
	{
		ArgumentNullException.ThrowIfNull(serviceType);

		if (serviceType == typeof(IErasureCertificateStore))
		{
			return this;
		}

		if (serviceType == typeof(IErasureQueryStore))
		{
			return this;
		}

		return null;
	}

	private static string HashDataSubjectId(string dataSubjectId) =>
		DataSubjectHasher.HashDataSubjectId(dataSubjectId);

	private static VerificationSummary CreateDefaultVerificationSummary() => new()
	{
		Verified = false,
		Methods = VerificationMethod.None,
		VerifiedAt = DateTimeOffset.MinValue
	};

	[LoggerMessage(LogLevel.Debug, "Saved erasure request {RequestId} scheduled for {ScheduledTime}")]
	private partial void LogSavedRequest(Guid requestId, DateTimeOffset scheduledTime);

	[LoggerMessage(LogLevel.Debug, "Saved erasure certificate {CertificateId} for request {RequestId}")]
	private partial void LogSavedCertificate(Guid certificateId, Guid requestId);

	[LoggerMessage(LogLevel.Information, "Cleaned up {Count} expired erasure certificates")]
	private partial void LogCleanedUpCertificates(int count);

	[LoggerMessage(LogLevel.Debug, "Ensured SQL Server erasure schema and tables exist")]
	private partial void LogSchemaEnsured();

	private async Task EnsureInitializedAsync(CancellationToken cancellationToken)
	{
		if (_initialized)
		{
			return;
		}

		if (_options.AutoCreateSchema)
		{
			await CreateSchemaIfNotExistsAsync(cancellationToken).ConfigureAwait(false);
		}

		_initialized = true;
	}

	private async Task CreateSchemaIfNotExistsAsync(CancellationToken cancellationToken)
	{
		var createSchemaSql = $@"
			IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = '{_options.SchemaName}')
			BEGIN
				EXEC('CREATE SCHEMA [{_options.SchemaName}]')
			END";

		var createRequestsTableSql = $@"
			IF NOT EXISTS (SELECT 1 FROM sys.tables t
				JOIN sys.schemas s ON t.schema_id = s.schema_id
				WHERE s.name = '{_options.SchemaName}' AND t.name = '{_options.RequestsTableName}')
			BEGIN
				CREATE TABLE {_options.FullRequestsTableName} (
					RequestId UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
					DataSubjectIdHash NVARCHAR(128) NOT NULL,
					IdType INT NOT NULL,
					TenantId NVARCHAR(256) NULL,
					Scope INT NOT NULL,
					LegalBasis INT NOT NULL,
					ExternalReference NVARCHAR(256) NULL,
					RequestedBy NVARCHAR(256) NOT NULL,
					RequestedAt DATETIMEOFFSET NOT NULL,
					ScheduledExecutionAt DATETIMEOFFSET NULL,
					ExecutedAt DATETIMEOFFSET NULL,
					CompletedAt DATETIMEOFFSET NULL,
					CancelledAt DATETIMEOFFSET NULL,
					CancellationReason NVARCHAR(1000) NULL,
					CancelledBy NVARCHAR(256) NULL,
					Status INT NOT NULL,
					KeysDeleted INT NULL,
					RecordsAffected INT NULL,
					CertificateId UNIQUEIDENTIFIER NULL,
					ErrorMessage NVARCHAR(2000) NULL,
					DataCategories NVARCHAR(MAX) NULL,
					CreatedAt DATETIMEOFFSET NOT NULL,
					UpdatedAt DATETIMEOFFSET NOT NULL,
					INDEX IX_{_options.RequestsTableName}_Status (Status, ScheduledExecutionAt),
					INDEX IX_{_options.RequestsTableName}_TenantId (TenantId, RequestedAt),
					INDEX IX_{_options.RequestsTableName}_DataSubject (DataSubjectIdHash)
				)
			END";

		var createCertificatesTableSql = $@"
			IF NOT EXISTS (SELECT 1 FROM sys.tables t
				JOIN sys.schemas s ON t.schema_id = s.schema_id
				WHERE s.name = '{_options.SchemaName}' AND t.name = '{_options.CertificatesTableName}')
			BEGIN
				CREATE TABLE {_options.FullCertificatesTableName} (
					CertificateId UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
					RequestId UNIQUEIDENTIFIER NOT NULL,
					DataSubjectReference NVARCHAR(256) NOT NULL,
					RequestReceivedAt DATETIMEOFFSET NOT NULL,
					CompletedAt DATETIMEOFFSET NOT NULL,
					Method INT NOT NULL,
					Summary NVARCHAR(MAX) NOT NULL,
					Verification NVARCHAR(MAX) NOT NULL,
					LegalBasis INT NOT NULL,
					Signature NVARCHAR(512) NOT NULL,
					RetainUntil DATETIMEOFFSET NOT NULL,
					CreatedAt DATETIMEOFFSET NOT NULL,
					INDEX IX_{_options.CertificatesTableName}_RequestId (RequestId),
					INDEX IX_{_options.CertificatesTableName}_RetainUntil (RetainUntil)
				)
			END";

		await using var connection = new SqlConnection(_options.ConnectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		_ = await connection.ExecuteAsync(new CommandDefinition(createSchemaSql, cancellationToken: cancellationToken, commandTimeout: _options.CommandTimeoutSeconds))
			.ConfigureAwait(false);
		_ = await connection.ExecuteAsync(new CommandDefinition(createRequestsTableSql, cancellationToken: cancellationToken, commandTimeout: _options.CommandTimeoutSeconds))
			.ConfigureAwait(false);
		_ = await connection.ExecuteAsync(new CommandDefinition(createCertificatesTableSql, cancellationToken: cancellationToken, commandTimeout: _options.CommandTimeoutSeconds))
			.ConfigureAwait(false);

		LogSchemaEnsured();
	}

	// Internal row classes for Dapper mapping
	[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Dapper materializes this type.")]
	private sealed class ErasureRequestRow
	{
		public Guid RequestId { get; init; }
		public string DataSubjectIdHash { get; init; } = string.Empty;
		public int IdType { get; init; }
		public string? TenantId { get; init; }
		public int Scope { get; init; }
		public int LegalBasis { get; init; }
		public string? ExternalReference { get; init; }
		public string RequestedBy { get; init; } = string.Empty;
		public DateTimeOffset RequestedAt { get; init; }
		public DateTimeOffset? ScheduledExecutionAt { get; init; }
		public DateTimeOffset? ExecutedAt { get; init; }
		public DateTimeOffset? CompletedAt { get; init; }
		public DateTimeOffset? CancelledAt { get; init; }
		public string? CancellationReason { get; init; }
		public string? CancelledBy { get; init; }
		public int Status { get; init; }
		public int? KeysDeleted { get; init; }
		public int? RecordsAffected { get; init; }
		public Guid? CertificateId { get; init; }
		public string? ErrorMessage { get; init; }
		public DateTimeOffset UpdatedAt { get; init; }

		public ErasureStatus ToStatus() => new()
		{
			RequestId = RequestId,
			DataSubjectIdHash = DataSubjectIdHash,
			IdType = (DataSubjectIdType)IdType,
			TenantId = TenantId,
			Scope = (ErasureScope)Scope,
			LegalBasis = (ErasureLegalBasis)LegalBasis,
			ExternalReference = ExternalReference,
			RequestedBy = RequestedBy,
			RequestedAt = RequestedAt,
			ScheduledExecutionAt = ScheduledExecutionAt,
			ExecutedAt = ExecutedAt,
			CompletedAt = CompletedAt,
			CancelledAt = CancelledAt,
			CancellationReason = CancellationReason,
			CancelledBy = CancelledBy,
			Status = (ErasureRequestStatus)Status,
			KeysDeleted = KeysDeleted,
			RecordsAffected = RecordsAffected,
			CertificateId = CertificateId,
			ErrorMessage = ErrorMessage,
			UpdatedAt = UpdatedAt
		};
	}

	[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Dapper materializes this type.")]
	private sealed class CertificateRow
	{
		public Guid CertificateId { get; init; }
		public Guid RequestId { get; init; }
		public string DataSubjectReference { get; init; } = string.Empty;
		public DateTimeOffset RequestReceivedAt { get; init; }
		public DateTimeOffset CompletedAt { get; init; }
		public int Method { get; init; }
		public string Summary { get; init; } = string.Empty;
		public string Verification { get; init; } = string.Empty;
		public int LegalBasis { get; init; }
		public string Signature { get; init; } = string.Empty;
		public DateTimeOffset RetainUntil { get; init; }

		public ErasureCertificate ToCertificate() => new()
		{
			CertificateId = CertificateId,
			RequestId = RequestId,
			DataSubjectReference = DataSubjectReference,
			RequestReceivedAt = RequestReceivedAt,
			CompletedAt = CompletedAt,
			Method = (ErasureMethod)Method,
			Summary = JsonSerializer.Deserialize(
				Summary,
				SqlServerComplianceJsonContext.Default.ErasureSummary) ?? new ErasureSummary(),
			Verification = JsonSerializer.Deserialize(
				Verification,
				SqlServerComplianceJsonContext.Default.VerificationSummary) ?? CreateDefaultVerificationSummary(),
			LegalBasis = (ErasureLegalBasis)LegalBasis,
			Signature = Signature,
			RetainUntil = RetainUntil
		};
	}
}

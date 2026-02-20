// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

using Dapper;

using Excalibur.Dispatch.Compliance;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Npgsql;

namespace Excalibur.Compliance.Postgres.Erasure;

/// <summary>
/// Postgres implementation of <see cref="IErasureStore"/> using Dapper.
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
public sealed partial class PostgresErasureStore : IErasureStore, IErasureCertificateStore, IErasureQueryStore
{
	private readonly PostgresErasureStoreOptions _options;
	private readonly ILogger<PostgresErasureStore> _logger;
	private volatile bool _initialized;

	/// <summary>
	/// Initializes a new instance of the <see cref="PostgresErasureStore"/> class.
	/// </summary>
	public PostgresErasureStore(
		IOptions<PostgresErasureStoreOptions> options,
		ILogger<PostgresErasureStore> logger)
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
				(request_id, data_subject_id_hash, id_type, tenant_id, scope, legal_basis,
				 external_reference, requested_by, requested_at, scheduled_execution_at,
				 status, data_categories, created_at, updated_at)
			VALUES
				(@RequestId, @DataSubjectIdHash, @IdType, @TenantId, @Scope, @LegalBasis,
				 @ExternalReference, @RequestedBy, @RequestedAt, @ScheduledExecutionAt,
				 @Status, @DataCategories::jsonb, @CreatedAt, @UpdatedAt)";

		await using var connection = new NpgsqlConnection(_options.ConnectionString);
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
					PostgresComplianceJsonContext.Default.IReadOnlyListString)
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
			SELECT request_id, data_subject_id_hash, id_type, tenant_id, scope, legal_basis,
				   external_reference, requested_by, requested_at, scheduled_execution_at,
				   executed_at, completed_at, cancelled_at, cancellation_reason, cancelled_by,
				   status, keys_deleted, records_affected, certificate_id, error_message, updated_at
			FROM {_options.FullRequestsTableName}
			WHERE request_id = @RequestId";

		await using var connection = new NpgsqlConnection(_options.ConnectionString);
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
			SET status = @Status,
				error_message = @ErrorMessage,
				executed_at = CASE WHEN @Status = {(int)ErasureRequestStatus.InProgress} THEN @Now ELSE executed_at END,
				updated_at = @Now
			WHERE request_id = @RequestId";

		await using var connection = new NpgsqlConnection(_options.ConnectionString);
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
			SET status = @Status,
				keys_deleted = @KeysDeleted,
				records_affected = @RecordsAffected,
				certificate_id = @CertificateId,
				completed_at = @Now,
				updated_at = @Now
			WHERE request_id = @RequestId";

		await using var connection = new NpgsqlConnection(_options.ConnectionString);
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
			SET status = @Status,
				cancellation_reason = @Reason,
				cancelled_by = @CancelledBy,
				cancelled_at = @Now,
				updated_at = @Now
			WHERE request_id = @RequestId
			  AND status IN ({(int)ErasureRequestStatus.Pending}, {(int)ErasureRequestStatus.Scheduled})";

		await using var connection = new NpgsqlConnection(_options.ConnectionString);
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
			SELECT request_id, data_subject_id_hash, id_type, tenant_id, scope, legal_basis,
				   external_reference, requested_by, requested_at, scheduled_execution_at,
				   executed_at, completed_at, cancelled_at, cancellation_reason, cancelled_by,
				   status, keys_deleted, records_affected, certificate_id, error_message, updated_at
			FROM {_options.FullRequestsTableName}
			WHERE status = {(int)ErasureRequestStatus.Scheduled}
			  AND scheduled_execution_at <= @Now
			ORDER BY scheduled_execution_at
			LIMIT @MaxResults";

		await using var connection = new NpgsqlConnection(_options.ConnectionString);
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
			whereClauses.Add("status = @Status");
			parameters.Add("Status", (int)status.Value);
		}

		if (!string.IsNullOrEmpty(tenantId))
		{
			whereClauses.Add("tenant_id = @TenantId");
			parameters.Add("TenantId", tenantId);
		}

		if (fromDate.HasValue)
		{
			whereClauses.Add("requested_at >= @FromDate");
			parameters.Add("FromDate", fromDate.Value);
		}

		if (toDate.HasValue)
		{
			whereClauses.Add("requested_at <= @ToDate");
			parameters.Add("ToDate", toDate.Value);
		}

		var whereClause = whereClauses.Count > 0
			? "WHERE " + string.Join(" AND ", whereClauses)
			: string.Empty;

		var offset = (pageNumber - 1) * pageSize;
		parameters.Add("Offset", offset);
		parameters.Add("PageSize", pageSize);

		var sql = $@"
			SELECT request_id, data_subject_id_hash, id_type, tenant_id, scope, legal_basis,
				   external_reference, requested_by, requested_at, scheduled_execution_at,
				   executed_at, completed_at, cancelled_at, cancellation_reason, cancelled_by,
				   status, keys_deleted, records_affected, certificate_id, error_message, updated_at
			FROM {_options.FullRequestsTableName}
			{whereClause}
			ORDER BY requested_at DESC
			LIMIT @PageSize OFFSET @Offset";

		await using var connection = new NpgsqlConnection(_options.ConnectionString);
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
				(certificate_id, request_id, data_subject_reference, request_received_at, completed_at,
				 method, summary, verification, legal_basis, signature, retain_until, created_at)
			VALUES
				(@CertificateId, @RequestId, @DataSubjectReference, @RequestReceivedAt, @CompletedAt,
				 @Method, @Summary::jsonb, @Verification::jsonb, @LegalBasis, @Signature, @RetainUntil, @CreatedAt)";

		await using var connection = new NpgsqlConnection(_options.ConnectionString);
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
				PostgresComplianceJsonContext.Default.ErasureSummary),
			Verification = JsonSerializer.Serialize(
				certificate.Verification,
				PostgresComplianceJsonContext.Default.VerificationSummary),
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
			SELECT certificate_id, request_id, data_subject_reference, request_received_at, completed_at,
				   method, summary, verification, legal_basis, signature, retain_until
			FROM {_options.FullCertificatesTableName}
			WHERE request_id = @RequestId";

		await using var connection = new NpgsqlConnection(_options.ConnectionString);
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
			SELECT certificate_id, request_id, data_subject_reference, request_received_at, completed_at,
				   method, summary, verification, legal_basis, signature, retain_until
			FROM {_options.FullCertificatesTableName}
			WHERE certificate_id = @CertificateId";

		await using var connection = new NpgsqlConnection(_options.ConnectionString);
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
			WHERE retain_until < @Now";

		await using var connection = new NpgsqlConnection(_options.ConnectionString);
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

	[LoggerMessage(LogLevel.Debug, "Ensured Postgres erasure schema and tables exist")]
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
		var createSchemaSql = $@"CREATE SCHEMA IF NOT EXISTS ""{_options.SchemaName}""";

		var createRequestsTableSql = $@"
			CREATE TABLE IF NOT EXISTS {_options.FullRequestsTableName} (
				request_id UUID NOT NULL PRIMARY KEY,
				data_subject_id_hash VARCHAR(128) NOT NULL,
				id_type INT NOT NULL,
				tenant_id VARCHAR(256) NULL,
				scope INT NOT NULL,
				legal_basis INT NOT NULL,
				external_reference VARCHAR(256) NULL,
				requested_by VARCHAR(256) NOT NULL,
				requested_at TIMESTAMPTZ NOT NULL,
				scheduled_execution_at TIMESTAMPTZ NULL,
				executed_at TIMESTAMPTZ NULL,
				completed_at TIMESTAMPTZ NULL,
				cancelled_at TIMESTAMPTZ NULL,
				cancellation_reason VARCHAR(1000) NULL,
				cancelled_by VARCHAR(256) NULL,
				status INT NOT NULL,
				keys_deleted INT NULL,
				records_affected INT NULL,
				certificate_id UUID NULL,
				error_message VARCHAR(2000) NULL,
				data_categories JSONB NULL,
				created_at TIMESTAMPTZ NOT NULL,
				updated_at TIMESTAMPTZ NOT NULL
			)";

		var createRequestsIndexesSql = $@"
			CREATE INDEX IF NOT EXISTS ix_{_options.RequestsTableName}_status
				ON {_options.FullRequestsTableName} (status, scheduled_execution_at);
			CREATE INDEX IF NOT EXISTS ix_{_options.RequestsTableName}_tenant
				ON {_options.FullRequestsTableName} (tenant_id, requested_at);
			CREATE INDEX IF NOT EXISTS ix_{_options.RequestsTableName}_subject
				ON {_options.FullRequestsTableName} (data_subject_id_hash)";

		var createCertificatesTableSql = $@"
			CREATE TABLE IF NOT EXISTS {_options.FullCertificatesTableName} (
				certificate_id UUID NOT NULL PRIMARY KEY,
				request_id UUID NOT NULL,
				data_subject_reference VARCHAR(256) NOT NULL,
				request_received_at TIMESTAMPTZ NOT NULL,
				completed_at TIMESTAMPTZ NOT NULL,
				method INT NOT NULL,
				summary JSONB NOT NULL,
				verification JSONB NOT NULL,
				legal_basis INT NOT NULL,
				signature VARCHAR(512) NOT NULL,
				retain_until TIMESTAMPTZ NOT NULL,
				created_at TIMESTAMPTZ NOT NULL
			)";

		var createCertificatesIndexesSql = $@"
			CREATE INDEX IF NOT EXISTS ix_{_options.CertificatesTableName}_request
				ON {_options.FullCertificatesTableName} (request_id);
			CREATE INDEX IF NOT EXISTS ix_{_options.CertificatesTableName}_retain
				ON {_options.FullCertificatesTableName} (retain_until)";

		await using var connection = new NpgsqlConnection(_options.ConnectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		_ = await connection.ExecuteAsync(new CommandDefinition(createSchemaSql, cancellationToken: cancellationToken, commandTimeout: _options.CommandTimeoutSeconds))
			.ConfigureAwait(false);
		_ = await connection.ExecuteAsync(new CommandDefinition(createRequestsTableSql, cancellationToken: cancellationToken, commandTimeout: _options.CommandTimeoutSeconds))
			.ConfigureAwait(false);
		_ = await connection.ExecuteAsync(new CommandDefinition(createRequestsIndexesSql, cancellationToken: cancellationToken, commandTimeout: _options.CommandTimeoutSeconds))
			.ConfigureAwait(false);
		_ = await connection.ExecuteAsync(new CommandDefinition(createCertificatesTableSql, cancellationToken: cancellationToken, commandTimeout: _options.CommandTimeoutSeconds))
			.ConfigureAwait(false);
		_ = await connection.ExecuteAsync(new CommandDefinition(createCertificatesIndexesSql, cancellationToken: cancellationToken, commandTimeout: _options.CommandTimeoutSeconds))
			.ConfigureAwait(false);

		LogSchemaEnsured();
	}

	// Internal row classes for Dapper mapping - Postgres uses snake_case column names
	[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Dapper materializes this type.")]
	private sealed class ErasureRequestRow
	{
		// ReSharper disable InconsistentNaming - Column names use snake_case
		public Guid request_id { get; init; }
		public string data_subject_id_hash { get; init; } = string.Empty;
		public int id_type { get; init; }
		public string? tenant_id { get; init; }
		public int scope { get; init; }
		public int legal_basis { get; init; }
		public string? external_reference { get; init; }
		public string requested_by { get; init; } = string.Empty;
		public DateTimeOffset requested_at { get; init; }
		public DateTimeOffset? scheduled_execution_at { get; init; }
		public DateTimeOffset? executed_at { get; init; }
		public DateTimeOffset? completed_at { get; init; }
		public DateTimeOffset? cancelled_at { get; init; }
		public string? cancellation_reason { get; init; }
		public string? cancelled_by { get; init; }
		public int status { get; init; }
		public int? keys_deleted { get; init; }
		public int? records_affected { get; init; }
		public Guid? certificate_id { get; init; }
		public string? error_message { get; init; }
		public DateTimeOffset updated_at { get; init; }
		// ReSharper restore InconsistentNaming

		public ErasureStatus ToStatus() => new()
		{
			RequestId = request_id,
			DataSubjectIdHash = data_subject_id_hash,
			IdType = (DataSubjectIdType)id_type,
			TenantId = tenant_id,
			Scope = (ErasureScope)scope,
			LegalBasis = (ErasureLegalBasis)legal_basis,
			ExternalReference = external_reference,
			RequestedBy = requested_by,
			RequestedAt = requested_at,
			ScheduledExecutionAt = scheduled_execution_at,
			ExecutedAt = executed_at,
			CompletedAt = completed_at,
			CancelledAt = cancelled_at,
			CancellationReason = cancellation_reason,
			CancelledBy = cancelled_by,
			Status = (ErasureRequestStatus)status,
			KeysDeleted = keys_deleted,
			RecordsAffected = records_affected,
			CertificateId = certificate_id,
			ErrorMessage = error_message,
			UpdatedAt = updated_at
		};
	}

	[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Dapper materializes this type.")]
	private sealed class CertificateRow
	{
		// ReSharper disable InconsistentNaming - Column names use snake_case
		public Guid certificate_id { get; init; }
		public Guid request_id { get; init; }
		public string data_subject_reference { get; init; } = string.Empty;
		public DateTimeOffset request_received_at { get; init; }
		public DateTimeOffset completed_at { get; init; }
		public int method { get; init; }
		public string summary { get; init; } = string.Empty;
		public string verification { get; init; } = string.Empty;
		public int legal_basis { get; init; }
		public string signature { get; init; } = string.Empty;
		public DateTimeOffset retain_until { get; init; }
		// ReSharper restore InconsistentNaming

		public ErasureCertificate ToCertificate() => new()
		{
			CertificateId = certificate_id,
			RequestId = request_id,
			DataSubjectReference = data_subject_reference,
			RequestReceivedAt = request_received_at,
			CompletedAt = completed_at,
			Method = (ErasureMethod)method,
			Summary = JsonSerializer.Deserialize(
				summary,
				PostgresComplianceJsonContext.Default.ErasureSummary) ?? new ErasureSummary(),
			Verification = JsonSerializer.Deserialize(
				verification,
				PostgresComplianceJsonContext.Default.VerificationSummary) ?? CreateDefaultVerificationSummary(),
			LegalBasis = (ErasureLegalBasis)legal_basis,
			Signature = signature,
			RetainUntil = retain_until
		};
	}
}

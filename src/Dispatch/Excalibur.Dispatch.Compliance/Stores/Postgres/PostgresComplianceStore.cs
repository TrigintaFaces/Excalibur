// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.RegularExpressions;

using Dapper;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Npgsql;

namespace Excalibur.Dispatch.Compliance;

/// <summary>
/// Configuration options for Postgres compliance store.
/// </summary>
public sealed class PostgresComplianceOptions
{
	/// <summary>
	/// Gets or sets the Postgres connection string.
	/// </summary>
	public string? ConnectionString { get; set; }

	/// <summary>
	/// Gets or sets the schema name for compliance tables.
	/// Default: "compliance".
	/// </summary>
	public string SchemaName { get; set; } = "compliance";

	/// <summary>
	/// Gets or sets the table name prefix.
	/// Default: "dispatch_".
	/// </summary>
	public string TablePrefix { get; set; } = "dispatch_";

	/// <summary>
	/// Gets or sets the command timeout in seconds.
	/// Default: 30 seconds.
	/// </summary>
	public int CommandTimeoutSeconds { get; set; } = 30;

	/// <summary>
	/// Gets the fully qualified consent records table name.
	/// </summary>
	internal string QualifiedConsentTableName => $"\"{SchemaName}\".\"{TablePrefix}consent_records\"";

	/// <summary>
	/// Gets the fully qualified erasure logs table name.
	/// </summary>
	internal string QualifiedErasureLogsTableName => $"\"{SchemaName}\".\"{TablePrefix}erasure_logs\"";

	/// <summary>
	/// Gets the fully qualified subject access requests table name.
	/// </summary>
	internal string QualifiedSubjectAccessTableName => $"\"{SchemaName}\".\"{TablePrefix}subject_access_requests\"";
}

/// <summary>
/// Postgres implementation of <see cref="IComplianceStore"/> using Dapper for data access.
/// </summary>
/// <remarks>
/// <para>
/// Provides durable storage for consent records, erasure logs, and subject access
/// request tracking in Postgres. Uses Dapper for query execution per the project's
/// no-EntityFramework constraint.
/// </para>
/// <para>
/// This implementation accepts a <see cref="Func{NpgsqlConnection}"/> factory following
/// the CDC connection factory pattern (S547). Each operation opens a fresh connection
/// from the factory and disposes it when complete.
/// </para>
/// <para>
/// This implementation requires Postgres 12+ and the Npgsql driver.
/// </para>
/// </remarks>
public sealed partial class PostgresComplianceStore : IComplianceStore
{
	private readonly Func<NpgsqlConnection> _connectionFactory;
	private readonly PostgresComplianceOptions _options;
	private readonly ILogger<PostgresComplianceStore> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="PostgresComplianceStore"/> class.
	/// </summary>
	/// <param name="options">The Postgres compliance options.</param>
	/// <param name="logger">The logger.</param>
	public PostgresComplianceStore(
		IOptions<PostgresComplianceOptions> options,
		ILogger<PostgresComplianceStore> logger)
		: this(CreateConnectionFactory(options?.Value), options?.Value, logger)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="PostgresComplianceStore"/> class
	/// with a connection factory.
	/// </summary>
	/// <param name="connectionFactory">A factory function that creates <see cref="NpgsqlConnection"/> instances.</param>
	/// <param name="options">The Postgres compliance options.</param>
	/// <param name="logger">The logger.</param>
	public PostgresComplianceStore(
		Func<NpgsqlConnection> connectionFactory,
		PostgresComplianceOptions? options,
		ILogger<PostgresComplianceStore> logger)
	{
		ArgumentNullException.ThrowIfNull(connectionFactory);
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(logger);

		ValidateSqlIdentifier(options.SchemaName, nameof(PostgresComplianceOptions.SchemaName));
		ValidateSqlIdentifier(options.TablePrefix, nameof(PostgresComplianceOptions.TablePrefix));

		_connectionFactory = connectionFactory;
		_options = options;
		_logger = logger;
	}

	/// <inheritdoc />
	public async Task StoreConsentAsync(
		ConsentRecord record,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(record);

		var sql = $"""
		           INSERT INTO {_options.QualifiedConsentTableName}
		           	(subject_id, purpose, granted_at, expires_at, legal_basis, is_withdrawn, withdrawn_at)
		           VALUES
		           	(@SubjectId, @Purpose, @GrantedAt, @ExpiresAt, @LegalBasis, @IsWithdrawn, @WithdrawnAt)
		           ON CONFLICT (subject_id, purpose) DO UPDATE SET
		           	granted_at = EXCLUDED.granted_at,
		           	expires_at = EXCLUDED.expires_at,
		           	legal_basis = EXCLUDED.legal_basis,
		           	is_withdrawn = EXCLUDED.is_withdrawn,
		           	withdrawn_at = EXCLUDED.withdrawn_at
		           """;

		await using var connection = _connectionFactory();
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		var command = new CommandDefinition(
			sql,
			new
			{
				record.SubjectId,
				record.Purpose,
				record.GrantedAt,
				record.ExpiresAt,
				LegalBasis = (int)record.LegalBasis,
				record.IsWithdrawn,
				record.WithdrawnAt
			},
			commandTimeout: _options.CommandTimeoutSeconds,
			cancellationToken: cancellationToken);

		_ = await connection.ExecuteAsync(command).ConfigureAwait(false);

		LogPostgresOperation("StoreConsent", record.SubjectId);
	}

	/// <inheritdoc />
	public async Task<ConsentRecord?> GetConsentAsync(
		string subjectId,
		string purpose,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(subjectId);
		ArgumentException.ThrowIfNullOrWhiteSpace(purpose);

		var sql = $"""
		           SELECT subject_id, purpose, granted_at, expires_at, legal_basis, is_withdrawn, withdrawn_at
		           FROM {_options.QualifiedConsentTableName}
		           WHERE subject_id = @SubjectId AND purpose = @Purpose
		           """;

		await using var connection = _connectionFactory();
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		var command = new CommandDefinition(
			sql,
			new { SubjectId = subjectId, Purpose = purpose },
			commandTimeout: _options.CommandTimeoutSeconds,
			cancellationToken: cancellationToken);

		var row = await connection.QuerySingleOrDefaultAsync<ConsentRecordRow>(command).ConfigureAwait(false);

		LogPostgresOperation("GetConsent", subjectId);

		return row is null ? null : MapToConsentRecord(row);
	}

	/// <inheritdoc />
	public async Task StoreErasureLogAsync(
		string subjectId,
		string details,
		DateTimeOffset erasedAt,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(subjectId);

		var sql = $"""
		           INSERT INTO {_options.QualifiedErasureLogsTableName}
		           	(subject_id, details, erased_at)
		           VALUES
		           	(@SubjectId, @Details, @ErasedAt)
		           """;

		await using var connection = _connectionFactory();
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		var command = new CommandDefinition(
			sql,
			new { SubjectId = subjectId, Details = details, ErasedAt = erasedAt },
			commandTimeout: _options.CommandTimeoutSeconds,
			cancellationToken: cancellationToken);

		_ = await connection.ExecuteAsync(command).ConfigureAwait(false);

		LogPostgresOperation("StoreErasureLog", subjectId);
	}

	/// <inheritdoc />
	public async Task StoreSubjectAccessRequestAsync(
		SubjectAccessResult result,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(result);

		var sql = $"""
		           INSERT INTO {_options.QualifiedSubjectAccessTableName}
		           	(request_id, status, deadline, fulfilled_at)
		           VALUES
		           	(@RequestId, @Status, @Deadline, @FulfilledAt)
		           ON CONFLICT (request_id) DO UPDATE SET
		           	status = EXCLUDED.status,
		           	deadline = EXCLUDED.deadline,
		           	fulfilled_at = EXCLUDED.fulfilled_at
		           """;

		await using var connection = _connectionFactory();
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		var command = new CommandDefinition(
			sql,
			new
			{
				result.RequestId,
				Status = (int)result.Status,
				result.Deadline,
				result.FulfilledAt
			},
			commandTimeout: _options.CommandTimeoutSeconds,
			cancellationToken: cancellationToken);

		_ = await connection.ExecuteAsync(command).ConfigureAwait(false);

		LogPostgresOperation("StoreSubjectAccessRequest", result.RequestId);
	}

	private static Func<NpgsqlConnection> CreateConnectionFactory(PostgresComplianceOptions? options)
	{
		ArgumentNullException.ThrowIfNull(options);

		if (string.IsNullOrWhiteSpace(options.ConnectionString))
		{
			throw new ArgumentException("ConnectionString is required.", nameof(options));
		}

		return () => new NpgsqlConnection(options.ConnectionString);
	}

	private static ConsentRecord MapToConsentRecord(ConsentRecordRow row)
	{
		return new ConsentRecord
		{
			SubjectId = row.subject_id,
			Purpose = row.purpose,
			GrantedAt = row.granted_at,
			ExpiresAt = row.expires_at,
			LegalBasis = (LegalBasis)row.legal_basis,
			IsWithdrawn = row.is_withdrawn,
			WithdrawnAt = row.withdrawn_at
		};
	}

	private static void ValidateSqlIdentifier(string identifier, string parameterName)
	{
		if (!SqlIdentifierRegex().IsMatch(identifier))
		{
			throw new ArgumentException(
				$"SQL identifier '{parameterName}' contains invalid characters. Only alphanumeric characters and underscores are allowed.",
				parameterName);
		}
	}

	[GeneratedRegex(@"^[a-zA-Z0-9_]+$")]
	private static partial Regex SqlIdentifierRegex();

	[LoggerMessage(
		LogLevel.Debug,
		"Postgres compliance store: {Operation} for {Identifier}")]
	private partial void LogPostgresOperation(string operation, string identifier);

	/// <summary>
	/// Row type for Dapper materialization of consent records from Postgres.
	/// </summary>
	[System.Diagnostics.CodeAnalysis.SuppressMessage(
		"Performance",
		"CA1812:Avoid uninstantiated internal classes",
		Justification = "Dapper materializes rows via reflection.")]
	private sealed class ConsentRecordRow
	{
		// ReSharper disable InconsistentNaming - Column names use snake_case
		public string subject_id { get; set; } = string.Empty;
		public string purpose { get; set; } = string.Empty;
		public DateTimeOffset granted_at { get; set; }
		public DateTimeOffset? expires_at { get; set; }
		public int legal_basis { get; set; }
		public bool is_withdrawn { get; set; }
		public DateTimeOffset? withdrawn_at { get; set; }
		// ReSharper restore InconsistentNaming
	}
}

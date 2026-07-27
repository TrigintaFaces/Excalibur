// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

using Dapper;

using Excalibur.Dispatch;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Npgsql;

namespace Excalibur.Compliance.Stores.Postgres;

/// <summary>
/// Configuration options for Postgres compliance store.
/// </summary>
public sealed class PostgresComplianceOptions
{
	/// <summary>
	/// Gets or sets the Postgres connection string.
	/// </summary>
	[Required]
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
	/// Gets or sets a value indicating whether the store provisions its schema and tables on first use.
	/// Default: <see langword="true"/>.
	/// </summary>
	/// <value>
	/// <see langword="true"/> to create the schema and tables when they are absent; <see langword="false"/> to
	/// verify they exist and fail fast when they do not.
	/// </value>
	/// <remarks>
	/// Deployments that provision schema out of band — a migration tool, a DBA-owned change process, or a
	/// least-privilege runtime account without DDL rights — set this to <see langword="false"/>. Verification
	/// still runs, so an absent table is reported at startup with its cause attached rather than surfacing as a
	/// raw provider error on the first write.
	/// </remarks>
	public bool AutoCreateSchema { get; set; } = true;

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
/// the CDC connection factory pattern. Each operation opens a fresh connection
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
	private readonly ITenantContext? _tenantContext;
	private volatile bool _initialized;

	/// <summary>
	/// Initializes a new instance of the <see cref="PostgresComplianceStore"/> class.
	/// </summary>
	/// <param name="options">The Postgres compliance options.</param>
	/// <param name="tenantContext">
	/// The ambient tenant context, or <see langword="null"/> when multi-tenancy is not registered.
	/// </param>
	/// <param name="logger">The logger.</param>
	public PostgresComplianceStore(
		IOptions<PostgresComplianceOptions> options,
		ITenantContext? tenantContext,
		ILogger<PostgresComplianceStore> logger)
		: this(CreateConnectionFactory(options?.Value), options?.Value, tenantContext, logger)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="PostgresComplianceStore"/> class
	/// with a connection factory.
	/// </summary>
	/// <param name="connectionFactory">A factory function that creates <see cref="NpgsqlConnection"/> instances.</param>
	/// <param name="options">The Postgres compliance options.</param>
	/// <param name="tenantContext">
	/// The ambient tenant context, or <see langword="null"/> when multi-tenancy is not registered.
	/// </param>
	/// <param name="logger">The logger.</param>
	/// <remarks>
	/// The tenant context is supplied as a constructor dependency rather than located from a service provider
	/// inside a query method, so a caller cannot substitute a different tenant's context mid-operation. The
	/// tenant <em>value</em> is then resolved from it per operation: this store is registered with a lifetime
	/// that outlives a single tenant's request, so a scope captured here would be served to every subsequent
	/// tenant.
	/// </remarks>
	public PostgresComplianceStore(
		Func<NpgsqlConnection> connectionFactory,
		PostgresComplianceOptions? options,
		ITenantContext? tenantContext,
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

		// The DEPENDENCY is fixed at construction; the tenant VALUE is resolved per operation from it. This
		// store is registered as a singleton, so freezing the scope here would serve the first request's
		// tenant to every subsequent tenant for the lifetime of the process.
		_tenantContext = tenantContext;
	}

	/// <summary>
	/// Gets the tenant value bound to every statement this store emits, resolved for the current operation.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Never <see langword="null"/>. An unscoped store persists the reserved framework sentinel, so the column
	/// is <c>NOT NULL</c> in the schema and a uniqueness constraint over it constrains: in Postgres
	/// <c>NULL != NULL</c>, so a nullable tenant column would make every untenanted row distinct and silently
	/// disable the very constraint that prevents cross-tenant collision.
	/// </para>
	/// <para>
	/// Resolved per call rather than cached, because the store outlives any single tenant's request. The
	/// injected context is the ambient one, so this reads the tenant of the operation in flight;
	/// <see cref="TenantScope.FromContext"/> fails closed when multi-tenancy is registered but unresolved,
	/// and a null context is the legitimate single-tenant path.
	/// </para>
	/// </remarks>
	private string TenantValue
	{
		get
		{
			var scope = TenantScope.FromContext(_tenantContext);
			return scope.IsScoped ? scope.TenantId! : TenantScope.Untenanted.TenantId!;
		}
	}

	/// <inheritdoc />
	public async Task StoreConsentAsync(
		ConsentRecord record,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(record);

		var sql = $"""
		           INSERT INTO {_options.QualifiedConsentTableName}
		           	(tenant_id, subject_id, purpose, granted_at, expires_at, legal_basis, is_withdrawn, withdrawn_at)
		           VALUES
		           	(@TenantId, @SubjectId, @Purpose, @GrantedAt, @ExpiresAt, @LegalBasis, @IsWithdrawn, @WithdrawnAt)
		           ON CONFLICT (tenant_id, subject_id, purpose) DO UPDATE SET
		           	granted_at = EXCLUDED.granted_at,
		           	expires_at = EXCLUDED.expires_at,
		           	legal_basis = EXCLUDED.legal_basis,
		           	is_withdrawn = EXCLUDED.is_withdrawn,
		           	withdrawn_at = EXCLUDED.withdrawn_at
		           """;

		await using var connection = _connectionFactory();
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
		await EnsureInitializedAsync(connection, cancellationToken).ConfigureAwait(false);

		var command = new CommandDefinition(
			sql,
			new
			{
				TenantId = TenantValue,
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
		           WHERE tenant_id = @TenantId AND subject_id = @SubjectId AND purpose = @Purpose
		           """;

		await using var connection = _connectionFactory();
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
		await EnsureInitializedAsync(connection, cancellationToken).ConfigureAwait(false);

		var command = new CommandDefinition(
			sql,
			new { TenantId = TenantValue, SubjectId = subjectId, Purpose = purpose },
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
		           	(tenant_id, subject_id, details, erased_at)
		           VALUES
		           	(@TenantId, @SubjectId, @Details, @ErasedAt)
		           """;

		await using var connection = _connectionFactory();
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
		await EnsureInitializedAsync(connection, cancellationToken).ConfigureAwait(false);

		var command = new CommandDefinition(
			sql,
			new { TenantId = TenantValue, SubjectId = subjectId, Details = details, ErasedAt = erasedAt },
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
		           	(tenant_id, request_id, status, deadline, fulfilled_at)
		           VALUES
		           	(@TenantId, @RequestId, @Status, @Deadline, @FulfilledAt)
		           ON CONFLICT (tenant_id, request_id) DO UPDATE SET
		           	status = EXCLUDED.status,
		           	deadline = EXCLUDED.deadline,
		           	fulfilled_at = EXCLUDED.fulfilled_at
		           """;

		await using var connection = _connectionFactory();
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
		await EnsureInitializedAsync(connection, cancellationToken).ConfigureAwait(false);

		var command = new CommandDefinition(
			sql,
			new
			{
				TenantId = TenantValue,
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
			throw new ArgumentException(
				$"'{nameof(PostgresComplianceOptions)}.{nameof(PostgresComplianceOptions.ConnectionString)}' is required. " +
				$"Configure it via services.Configure<{nameof(PostgresComplianceOptions)}>(config.GetSection(\"PostgresCompliance\")) " +
				"or set the ConnectionString property directly.",
				nameof(options));
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

	/// <summary>
	/// Ensures the schema and tables this store writes to exist, once per store instance.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Initialization never completes without either creating the schema or verifying it. Marking the store
	/// initialized after doing neither would defer the failure to the first query, where it surfaces as a raw
	/// provider error far from its cause.
	/// </para>
	/// <para>
	/// Deliberately unsynchronized. Concurrent first calls may both run this, which is harmless: the DDL is
	/// <c>IF NOT EXISTS</c> throughout and the verification path is a read, so the work is idempotent and the
	/// race costs at most one redundant round trip. This follows the framework convention for lazily
	/// initialized state — the same contract as <c>ConcurrentDictionary.GetOrAdd</c>, whose factory runs
	/// outside the lock and may run more than once — and it avoids holding a lock across arbitrary database
	/// I/O, which is the more expensive mistake.
	/// </para>
	/// </remarks>
	private async Task EnsureInitializedAsync(NpgsqlConnection connection, CancellationToken cancellationToken)
	{
		if (_initialized)
		{
			return;
		}

		if (_options.AutoCreateSchema)
		{
			await CreateSchemaIfNotExistsAsync(connection, cancellationToken).ConfigureAwait(false);
		}
		else
		{
			await VerifySchemaExistsAsync(connection, cancellationToken).ConfigureAwait(false);
		}

		_initialized = true;
	}

	/// <summary>
	/// Creates the compliance schema and its three tables when absent.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <c>tenant_id</c> is <c>NOT NULL</c> on every table and participates in every primary key. Both
	/// properties are load-bearing rather than stylistic: in Postgres <c>NULL != NULL</c>, so a nullable
	/// tenant column would make every untenanted row distinct and silently disable the uniqueness constraint
	/// that prevents one tenant's write from overwriting another's. An unscoped store writes the reserved
	/// framework sentinel, which is a real value and therefore constrains.
	/// </para>
	/// <para>
	/// The primary keys are composite and tenant-first. A key of <c>(subject_id, purpose)</c> alone means two
	/// tenants recording consent for the same data subject collapse onto one row, so the second tenant's
	/// withdrawal silently revokes the first tenant's grant — a cross-tenant write, not merely a read leak.
	/// </para>
	/// </remarks>
	private async Task CreateSchemaIfNotExistsAsync(NpgsqlConnection connection, CancellationToken cancellationToken)
	{
		var sql = $"""
		           CREATE SCHEMA IF NOT EXISTS "{_options.SchemaName}";

		           CREATE TABLE IF NOT EXISTS {_options.QualifiedConsentTableName} (
		           	tenant_id TEXT NOT NULL,
		           	subject_id TEXT NOT NULL,
		           	purpose TEXT NOT NULL,
		           	granted_at TIMESTAMPTZ NOT NULL,
		           	expires_at TIMESTAMPTZ NULL,
		           	legal_basis INT NOT NULL,
		           	is_withdrawn BOOLEAN NOT NULL DEFAULT FALSE,
		           	withdrawn_at TIMESTAMPTZ NULL,
		           	CONSTRAINT pk_{_options.TablePrefix}consent_records PRIMARY KEY (tenant_id, subject_id, purpose)
		           );

		           CREATE TABLE IF NOT EXISTS {_options.QualifiedErasureLogsTableName} (
		           	tenant_id TEXT NOT NULL,
		           	subject_id TEXT NOT NULL,
		           	details TEXT NULL,
		           	erased_at TIMESTAMPTZ NOT NULL
		           );

		           CREATE INDEX IF NOT EXISTS ix_{_options.TablePrefix}erasure_logs_tenant_subject
		           	ON {_options.QualifiedErasureLogsTableName} (tenant_id, subject_id);

		           CREATE TABLE IF NOT EXISTS {_options.QualifiedSubjectAccessTableName} (
		           	tenant_id TEXT NOT NULL,
		           	request_id TEXT NOT NULL,
		           	status INT NOT NULL,
		           	deadline TIMESTAMPTZ NULL,
		           	fulfilled_at TIMESTAMPTZ NULL,
		           	CONSTRAINT pk_{_options.TablePrefix}subject_access_requests PRIMARY KEY (tenant_id, request_id)
		           );
		           """;

		var command = new CommandDefinition(
			sql,
			commandTimeout: _options.CommandTimeoutSeconds,
			cancellationToken: cancellationToken);

		_ = await connection.ExecuteAsync(command).ConfigureAwait(false);

		LogSchemaEnsured();
	}

	/// <summary>
	/// Confirms the required tables exist when automatic provisioning is disabled.
	/// </summary>
	/// <exception cref="InvalidOperationException">A required table is absent.</exception>
	private async Task VerifySchemaExistsAsync(NpgsqlConnection connection, CancellationToken cancellationToken)
	{
		const string Sql = """
		                   SELECT table_name
		                   FROM information_schema.tables
		                   WHERE table_schema = @SchemaName
		                   """;

		var command = new CommandDefinition(
			Sql,
			new { _options.SchemaName },
			commandTimeout: _options.CommandTimeoutSeconds,
			cancellationToken: cancellationToken);

		var present = (await connection.QueryAsync<string>(command).ConfigureAwait(false)).ToHashSet(StringComparer.Ordinal);

		string[] required =
		[
			$"{_options.TablePrefix}consent_records",
			$"{_options.TablePrefix}erasure_logs",
			$"{_options.TablePrefix}subject_access_requests"
		];

		var missing = required.Where(t => !present.Contains(t)).ToArray();
		if (missing.Length > 0)
		{
			throw new InvalidOperationException(
				$"The compliance schema '{_options.SchemaName}' is missing required table(s): {string.Join(", ", missing)}. " +
				$"Provision them out of band, or set {nameof(PostgresComplianceOptions.AutoCreateSchema)} to true.");
		}
	}

	[GeneratedRegex(@"^[a-zA-Z0-9_]+$")]
	private static partial Regex SqlIdentifierRegex();

	[LoggerMessage(
		LogLevel.Debug,
		"Ensured Postgres compliance schema and tables exist")]
	private partial void LogSchemaEnsured();

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

// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Saga.Idempotency;
using Excalibur.Saga.SqlServer.Requests;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Saga.SqlServer;

/// <summary>
/// SQL Server implementation of <see cref="ISagaIdempotencyProvider"/> using Dapper.
/// </summary>
/// <remarks>
/// Tracks processed message idempotency keys in a SQL Server table to prevent
/// duplicate saga message processing in distributed systems with at-least-once delivery.
/// SQL is encapsulated in <see cref="DataRequestBase{TConnection,TModel}"/>-derived
/// request classes under <c>Requests/</c>.
/// </remarks>
internal sealed partial class SqlServerSagaIdempotencyProvider : ISagaIdempotencyProvider
{
	private readonly string _connectionString;
	private readonly SqlServerSagaIdempotencyOptions _options;
	private readonly ILogger<SqlServerSagaIdempotencyProvider> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="SqlServerSagaIdempotencyProvider"/> class.
	/// </summary>
	/// <param name="connectionString">The SQL Server connection string.</param>
	/// <param name="options">The idempotency options.</param>
	/// <param name="logger">The logger.</param>
	public SqlServerSagaIdempotencyProvider(
		string connectionString,
		IOptions<SqlServerSagaIdempotencyOptions> options,
		ILogger<SqlServerSagaIdempotencyProvider> logger)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(logger);

		_connectionString = connectionString;
		_options = options.Value;
		_logger = logger;
	}

	/// <inheritdoc />
	public async Task<bool> IsProcessedAsync(string sagaId, string idempotencyKey, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(sagaId);
		ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);

		var request = new IsIdempotencyKeyProcessedRequest(sagaId, idempotencyKey, _options.QualifiedTableName, cancellationToken);

		await using var connection = new SqlConnection(_connectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		return await request.ResolveAsync(connection).ConfigureAwait(false);
	}

	/// <inheritdoc />
	public async Task MarkProcessedAsync(string sagaId, string idempotencyKey, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(sagaId);
		ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);

		var request = new MarkIdempotencyKeyProcessedRequest(sagaId, idempotencyKey, _options.QualifiedTableName, cancellationToken);

		await using var connection = new SqlConnection(_connectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		await request.ResolveAsync(connection).ConfigureAwait(false);

		LogMarkedProcessed(sagaId, idempotencyKey);
	}

	[LoggerMessage(3100, LogLevel.Debug, "Marked saga {SagaId} idempotency key {IdempotencyKey} as processed")]
	private partial void LogMarkedProcessed(string sagaId, string idempotencyKey);
}

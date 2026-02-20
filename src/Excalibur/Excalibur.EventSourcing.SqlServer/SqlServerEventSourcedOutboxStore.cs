// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Data;

using Excalibur.Data.Abstractions;
using Excalibur.Data.Abstractions.Observability;
using Excalibur.Dispatch.Abstractions.Diagnostics;
using Excalibur.EventSourcing.Outbox;
using Excalibur.EventSourcing.SqlServer.Requests;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace Excalibur.EventSourcing.SqlServer;

/// <summary>
/// SQL Server implementation of <see cref="IEventSourcedOutboxStore"/>.
/// </summary>
/// <remarks>
/// <para>
/// Provides transactional outbox operations for reliable event publishing in event-sourced systems.
/// Uses parameterized queries and explicit transaction support for atomic consistency with aggregate persistence.
/// </para>
/// <para>
/// This class supports two constructor patterns:
/// <list type="bullet">
/// <item><description>Simple: Connection string for most users</description></item>
/// <item><description>Advanced: Connection factory for multi-database, pooling, or IDb integration</description></item>
/// </list>
/// </para>
/// <para>
/// <strong>Table Schema:</strong>
/// The implementation expects an <c>EventSourcedOutbox</c> table with columns matching <see cref="OutboxMessage"/>:
/// Id, AggregateId, AggregateType, EventType, EventData, CreatedAt, PublishedAt, RetryCount, MessageType, Metadata.
/// </para>
/// </remarks>
public sealed class SqlServerEventSourcedOutboxStore : IEventSourcedOutboxStore
{
	private readonly Func<SqlConnection> _connectionFactory;
	private readonly ILogger<SqlServerEventSourcedOutboxStore> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="SqlServerEventSourcedOutboxStore"/> class.
	/// </summary>
	/// <param name="connectionString">The SQL Server connection string.</param>
	/// <param name="logger">The logger instance.</param>
	/// <remarks>
	/// This is the simple constructor for most users.
	/// Use <see cref="SqlServerEventSourcedOutboxStore(Func{SqlConnection}, ILogger{SqlServerEventSourcedOutboxStore})"/>
	/// for advanced scenarios like multi-database setups or custom connection pooling.
	/// </remarks>
	public SqlServerEventSourcedOutboxStore(string connectionString, ILogger<SqlServerEventSourcedOutboxStore> logger)
		: this(CreateConnectionFactory(connectionString), logger)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="SqlServerEventSourcedOutboxStore"/> class with a connection factory.
	/// </summary>
	/// <param name="connectionFactory">
	/// A factory function that creates <see cref="SqlConnection"/> instances.
	/// The caller is responsible for ensuring the factory returns properly configured connections.
	/// </param>
	/// <param name="logger">The logger instance.</param>
	/// <remarks>
	/// <para>
	/// This is the advanced constructor for scenarios that need custom connection management:
	/// </para>
	/// <list type="bullet">
	/// <item><description>Multi-database setups with marker interfaces (e.g., IDomainDb, IEventStoreDb)</description></item>
	/// <item><description>Custom connection pooling</description></item>
	/// <item><description>Integration with <see cref="IDb"/> abstraction</description></item>
	/// </list>
	/// </remarks>
	public SqlServerEventSourcedOutboxStore(
		Func<SqlConnection> connectionFactory,
		ILogger<SqlServerEventSourcedOutboxStore> logger)
	{
		_connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	/// <inheritdoc/>
	public async Task AddAsync(
		OutboxMessage message,
		IDbTransaction transaction,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(message);
		ArgumentNullException.ThrowIfNull(transaction);

		var stopwatch = ValueStopwatch.StartNew();
		var result = WriteStoreTelemetry.Results.Success;

		try
		{
			if (transaction.Connection is not SqlConnection connection)
			{
				result = WriteStoreTelemetry.Results.Failure;
				throw new ArgumentException(
					"Transaction must be associated with a SqlConnection.",
					nameof(transaction));
			}

			_ = await connection.ResolveAsync(
					new AddOutboxMessageRequest(message, transaction, cancellationToken))
				.ConfigureAwait(false);

			_logger.LogDebug(
				"Added outbox message {MessageId} for {AggregateType}/{AggregateId}",
				message.Id, message.AggregateType, message.AggregateId);
		}
		catch
		{
			result = WriteStoreTelemetry.Results.Failure;
			throw;
		}
		finally
		{
			WriteStoreTelemetry.RecordOperation(
				WriteStoreTelemetry.Stores.OutboxStore,
				WriteStoreTelemetry.Providers.SqlServer,
				"add",
				result,
				stopwatch.Elapsed);
		}
	}

	/// <inheritdoc/>
	public async Task<IReadOnlyList<OutboxMessage>> GetPendingAsync(
		int batchSize,
		CancellationToken cancellationToken)
	{
		var stopwatch = ValueStopwatch.StartNew();
		var result = WriteStoreTelemetry.Results.Success;

		await using var connection = _connectionFactory();
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		try
		{
			var messages = await connection.ResolveAsync(
					new GetPendingOutboxMessagesRequest(batchSize, cancellationToken))
				.ConfigureAwait(false);

			_logger.LogDebug("Retrieved {Count} pending outbox messages", messages.Count);

			return messages;
		}
		catch
		{
			result = WriteStoreTelemetry.Results.Failure;
			throw;
		}
		finally
		{
			WriteStoreTelemetry.RecordOperation(
				WriteStoreTelemetry.Stores.OutboxStore,
				WriteStoreTelemetry.Providers.SqlServer,
				"get_pending",
				result,
				stopwatch.Elapsed);
		}
	}

	/// <inheritdoc/>
	public async Task MarkAsPublishedAsync(
		Guid messageId,
		IDbTransaction? transaction,
		CancellationToken cancellationToken)
	{
		var stopwatch = ValueStopwatch.StartNew();
		var result = WriteStoreTelemetry.Results.Success;

		try
		{
			if (transaction != null)
			{
				try
				{
					if (transaction.Connection is not SqlConnection transactionConnection)
					{
						throw new ArgumentException(
							"Transaction must be associated with a SqlConnection.",
							nameof(transaction));
					}

					_ = await transactionConnection.ResolveAsync(
							new MarkOutboxMessagePublishedRequest(messageId, transaction, cancellationToken))
						.ConfigureAwait(false);
				}
				catch
				{
					result = WriteStoreTelemetry.Results.Failure;
					throw;
				}
			}
			else
			{
				try
				{
					await using var connection = _connectionFactory();
					await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

					_ = await connection.ResolveAsync(
							new MarkOutboxMessagePublishedRequest(messageId, null, cancellationToken))
						.ConfigureAwait(false);
				}
				catch
				{
					result = WriteStoreTelemetry.Results.Failure;
					throw;
				}
			}

			_logger.LogDebug("Marked outbox message {MessageId} as published", messageId);
		}
		finally
		{
			WriteStoreTelemetry.RecordOperation(
				WriteStoreTelemetry.Stores.OutboxStore,
				WriteStoreTelemetry.Providers.SqlServer,
				"mark_published",
				result,
				stopwatch.Elapsed);
		}
	}

	/// <inheritdoc/>
	public async Task<int> DeletePublishedOlderThanAsync(
		TimeSpan retentionPeriod,
		CancellationToken cancellationToken)
	{
		var stopwatch = ValueStopwatch.StartNew();
		var result = WriteStoreTelemetry.Results.Success;

		await using var connection = _connectionFactory();
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		try
		{
			var deletedCount = await connection.ResolveAsync(
					new DeletePublishedOutboxMessagesRequest(retentionPeriod, cancellationToken))
				.ConfigureAwait(false);

			_logger.LogDebug(
				"Deleted {Count} published outbox messages older than {RetentionPeriod}",
				deletedCount, retentionPeriod);

			return deletedCount;
		}
		catch
		{
			result = WriteStoreTelemetry.Results.Failure;
			throw;
		}
		finally
		{
			WriteStoreTelemetry.RecordOperation(
				WriteStoreTelemetry.Stores.OutboxStore,
				WriteStoreTelemetry.Providers.SqlServer,
				"cleanup_published",
				result,
				stopwatch.Elapsed);
		}
	}

	/// <inheritdoc/>
	public async Task IncrementRetryCountAsync(
		Guid messageId,
		CancellationToken cancellationToken)
	{
		var stopwatch = ValueStopwatch.StartNew();
		var result = WriteStoreTelemetry.Results.Success;

		await using var connection = _connectionFactory();
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		try
		{
			_ = await connection.ResolveAsync(
					new IncrementOutboxRetryCountRequest(messageId, cancellationToken))
				.ConfigureAwait(false);

			_logger.LogDebug("Incremented retry count for outbox message {MessageId}", messageId);
		}
		catch
		{
			result = WriteStoreTelemetry.Results.Failure;
			throw;
		}
		finally
		{
			WriteStoreTelemetry.RecordOperation(
				WriteStoreTelemetry.Stores.OutboxStore,
				WriteStoreTelemetry.Providers.SqlServer,
				"increment_retry",
				result,
				stopwatch.Elapsed);
		}
	}

	private static Func<SqlConnection> CreateConnectionFactory(string connectionString)
	{
		ArgumentNullException.ThrowIfNull(connectionString);
		return () => new SqlConnection(connectionString);
	}
}

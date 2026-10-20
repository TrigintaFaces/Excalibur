// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Data;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

using Excalibur.Dispatch.Delivery;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;

namespace Excalibur.Data.SqlServer.Scheduling;

/// <summary>Durable SQL Server schedule storage with fenced, renewable occurrence ownership.</summary>
/// <remarks>
/// Provision scripts/004_CreateScheduleSchema.sql before use. Administration is store-global;
/// message tenant metadata does not restrict inventory or administrative operations. Handler effects
/// remain at-least-once. Database restore or cloning requires an offline authority-epoch rotation.
/// Ambient transactions are rejected and ambiguous commit responses are never automatically retried.
/// </remarks>
public sealed class SqlServerScheduleStore : IScheduleExecutionLeaseRenewal
{
	private const string Columns = "Id, Generation, MessageName, MessageBody, CronExpression, TimeZoneId, IntervalTicks, NextExecutionUtc, LastExecutionUtc, Enabled, MissedExecutionBehavior, MetadataJson, OwnerToken, AdmittedOccurrence, LeaseExpiresUtc, ProgressCommitted";
	private readonly string _connectionString;
	private readonly int _commandTimeout;
	private readonly int _lockTimeout;

	/// <summary>Initializes a store using an immutable copy of the supplied options.</summary>
	/// <param name="options">Connection and lease settings.</param>
	public SqlServerScheduleStore(IOptions<SqlServerScheduleStoreOptions> options)
	{
		ArgumentNullException.ThrowIfNull(options);
		var value = options.Value;
		ArgumentException.ThrowIfNullOrWhiteSpace(value.ConnectionString);
		ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(value.LeaseDuration, TimeSpan.Zero);
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value.CommandTimeoutSeconds);
		ArgumentOutOfRangeException.ThrowIfNegative(value.LockTimeoutMilliseconds);
		_connectionString = value.ConnectionString;
		_commandTimeout = value.CommandTimeoutSeconds;
		_lockTimeout = value.LockTimeoutMilliseconds;
		LeaseDuration = value.LeaseDuration;
	}

	/// <inheritdoc />
	public TimeSpan LeaseDuration { get; }

	/// <inheritdoc />
	public async Task<IEnumerable<IScheduledMessage>> GetAllAsync(CancellationToken cancellationToken) =>
		(await GetExecutionSnapshotsAsync(cancellationToken).ConfigureAwait(false)).Select(snapshot => snapshot.Message).ToArray();

	/// <inheritdoc />
	public async Task<IReadOnlyList<ScheduleExecutionSnapshot>> GetExecutionSnapshotsAsync(CancellationToken cancellationToken)
	{
		RejectAmbientTransaction();
		await using var connection = new SqlConnection(_connectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
		await using var command = Command(connection, null, $"SELECT {Columns} FROM dispatch.ScheduledMessage");
		await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
		var snapshots = new List<ScheduleExecutionSnapshot>();
		while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
		{
			var row = ReadRow(reader);
			snapshots.Add(new ScheduleExecutionSnapshot(row.Generation, row.Message));
		}
		return snapshots;
	}

	/// <inheritdoc />
	public async Task StoreAsync(IScheduledMessage message, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(message);
		// Read caller-owned fields exactly once before validating or awaiting.
		var owned = new ScheduleExecutionSnapshot(Guid.NewGuid(), message);
		ArgumentException.ThrowIfNullOrWhiteSpace(owned.Message.MessageName);
		ArgumentNullException.ThrowIfNull(owned.Message.MessageBody);
		if (owned.Message.Id == Guid.Empty)
		{
			throw new ArgumentException("The schedule identifier must be nonempty.", nameof(message));
		}
		_ = await MutateAsync(owned.Message.Id, async (connection, transaction, _, row, _, token) =>
		{
			const string values = "@Id, @Generation, @MessageName, @MessageBody, @CronExpression, @TimeZoneId, @IntervalTicks, @NextExecutionUtc, @LastExecutionUtc, @Enabled, @MissedExecutionBehavior, @MetadataJson";
			const string assignments = "Generation=@Generation, MessageName=@MessageName, MessageBody=@MessageBody, CronExpression=@CronExpression, TimeZoneId=@TimeZoneId, IntervalTicks=@IntervalTicks, NextExecutionUtc=@NextExecutionUtc, LastExecutionUtc=@LastExecutionUtc, Enabled=@Enabled, MissedExecutionBehavior=@MissedExecutionBehavior, MetadataJson=@MetadataJson, OwnerToken=NULL, AdmittedOccurrence=NULL, LeaseExpiresUtc=NULL, ProgressCommitted=0";
			await using var command = Command(connection, transaction, row is null
				? $"INSERT dispatch.ScheduledMessage ({Columns[..Columns.IndexOf(", OwnerToken", StringComparison.Ordinal)]}) VALUES ({values})"
				: $"UPDATE dispatch.ScheduledMessage SET {assignments} WHERE Id=@Id");
			AddMessage(command, owned);
			_ = await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
			return true;
		}, cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc />
	public async Task CompleteAsync(Guid scheduleId, CancellationToken cancellationToken)
	{
		_ = await MutateAsync(scheduleId, async (connection, transaction, _, row, _, token) =>
		{
			if (row is null)
			{
				return false;
			}
			await using var command = Command(connection, transaction,
				"UPDATE dispatch.ScheduledMessage SET Enabled=0, Generation=@Generation, OwnerToken=NULL, AdmittedOccurrence=NULL, LeaseExpiresUtc=NULL, ProgressCommitted=0 WHERE Id=@Id");
			Add(command, "@Id", SqlDbType.UniqueIdentifier, scheduleId);
			Add(command, "@Generation", SqlDbType.UniqueIdentifier, Guid.NewGuid());
			_ = await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
			return true;
		}, cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc />
	public Task<ScheduleExecutionLease?> TryAcquireExecutionAsync(Guid scheduleId, Guid generation,
		DateTimeOffset occurrenceUtc, CancellationToken cancellationToken) => MutateAsync<ScheduleExecutionLease?>(scheduleId,
		async (connection, transaction, storeId, row, now, token) =>
		{
			// `occurrenceUtc > now` is the due-ness term, and `now` is the STORE's clock -- SYSUTCDATETIME,
			// sampled above inside this transaction -- never a value the caller supplied.
			//
			// Without it, due-ness rested entirely on each host's own TimeProvider, and the failure was not
			// "a host is sometimes early": admission is first-come, so the host whose clock runs FASTEST
			// wins the race for every occurrence, deterministically, and a single misconfigured host
			// captures the whole schedule inventory and runs all of it ahead of time. LastExecutionUtc was
			// then stamped from that same skewed clock, so the row recorded an execution instant that never
			// happened and nothing downstream could tell.
			//
			// No tolerance window: a host that polls early simply does not admit and tries again on its
			// next poll, which costs one poll interval and needs no agreement between clocks.
			if (row is null || !row.Message.Enabled || row.Generation != generation
				|| row.Message.NextExecutionUtc != occurrenceUtc || occurrenceUtc > now
				|| (row.OwnerToken is not null && row.ExpiresUtc > now))
			{
				return null;
			}
			var expires = Expiry(now);
			var lease = new ScheduleExecutionLease(storeId, scheduleId, generation, occurrenceUtc, Guid.NewGuid());
			await using var command = Command(connection, transaction,
				"UPDATE dispatch.ScheduledMessage SET OwnerToken=@Owner, AdmittedOccurrence=@Occurrence, LeaseExpiresUtc=@Expires, ProgressCommitted=0 WHERE Id=@Id");
			Add(command, "@Id", SqlDbType.UniqueIdentifier, scheduleId);
			Add(command, "@Owner", SqlDbType.UniqueIdentifier, lease.OwnerToken);
			Add(command, "@Occurrence", SqlDbType.DateTimeOffset, occurrenceUtc);
			Add(command, "@Expires", SqlDbType.DateTimeOffset, expires);
			_ = await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
			return lease;
		}, cancellationToken);

	/// <inheritdoc />
	public Task<bool> TryRenewExecutionAsync(ScheduleExecutionLease lease, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(lease);
		return MutateAsync(lease.ScheduleId, async (connection, transaction, storeId, row, now, token) =>
		{
			if (!Matches(storeId, row, lease) || row!.ExpiresUtc <= now)
			{
				return false;
			}
			var expires = Expiry(now);
			await using var command = Command(connection, transaction,
				"UPDATE dispatch.ScheduledMessage SET LeaseExpiresUtc=@Expires WHERE Id=@Id");
			Add(command, "@Id", SqlDbType.UniqueIdentifier, lease.ScheduleId);
			Add(command, "@Expires", SqlDbType.DateTimeOffset, expires);
			_ = await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
			return true;
		}, cancellationToken);
	}

	/// <inheritdoc />
	public Task<bool> TryUpdateProgressAsync(ScheduleExecutionLease lease, ScheduleExecutionResult result,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(lease);
		ArgumentNullException.ThrowIfNull(result);
		return MutateAsync(lease.ScheduleId, async (connection, transaction, storeId, row, now, token) =>
		{
			if (!Matches(storeId, row, lease) || row!.ExpiresUtc <= now || row.ProgressCommitted)
			{
				return false;
			}
			await using var command = Command(connection, transaction,
				"UPDATE dispatch.ScheduledMessage SET LastExecutionUtc=@Last, NextExecutionUtc=@Next, Enabled=CASE WHEN @Disable=1 THEN 0 ELSE Enabled END, ProgressCommitted=1 WHERE Id=@Id");
			Add(command, "@Id", SqlDbType.UniqueIdentifier, lease.ScheduleId);
			Add(command, "@Last", SqlDbType.DateTimeOffset, result.LastExecutionUtc);
			Add(command, "@Next", SqlDbType.DateTimeOffset, result.NextExecutionUtc);
			Add(command, "@Disable", SqlDbType.Bit, result.Disable);
			_ = await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
			return true;
		}, cancellationToken);
	}

	/// <inheritdoc />
	public Task<bool> ReleaseExecutionAsync(ScheduleExecutionLease lease, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(lease);
		return MutateAsync(lease.ScheduleId, async (connection, transaction, storeId, row, now, token) =>
		{
			if (!Matches(storeId, row, lease))
			{
				return false;
			}
			await using var command = Command(connection, transaction,
				"UPDATE dispatch.ScheduledMessage SET OwnerToken=NULL, AdmittedOccurrence=NULL, LeaseExpiresUtc=NULL WHERE Id=@Id");
			Add(command, "@Id", SqlDbType.UniqueIdentifier, lease.ScheduleId);
			_ = await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
			return true;
		}, cancellationToken);
	}

	private async Task<T> MutateAsync<T>(Guid id,
		Func<SqlConnection, SqlTransaction, Guid, Row?, DateTimeOffset, CancellationToken, Task<T>> mutate,
		CancellationToken cancellationToken)
	{
		RejectAmbientTransaction();
		await using var connection = new SqlConnection(_connectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
		await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
		// This shared epoch lock must precede every row/range lock, and remains held through commit.
		await using var epoch = Command(connection, transaction,
			"SELECT StoreId FROM dispatch.ScheduleAuthority WITH (HOLDLOCK) WHERE SingletonId=1");
		var value = await epoch.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
		if (value is not Guid storeId || storeId == Guid.Empty)
		{
			throw new InvalidOperationException("The schedule authority singleton is missing or invalid.");
		}
		Row? row;
		await using (var query = Command(connection, transaction,
			$"SELECT {Columns} FROM dispatch.ScheduledMessage WITH (UPDLOCK, HOLDLOCK) WHERE Id=@Id"))
		{
			Add(query, "@Id", SqlDbType.UniqueIdentifier, id);
			await using var reader = await query.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
			row = await reader.ReadAsync(cancellationToken).ConfigureAwait(false) ? ReadRow(reader) : null;
		}
		// Never sample time before waiting for the authority row lock.
		await using var clock = Command(connection, transaction, "SELECT TODATETIMEOFFSET(SYSUTCDATETIME(), '+00:00')");
		var now = (DateTimeOffset)(await clock.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false))!;
		var result = await mutate(connection, transaction, storeId, row, now, cancellationToken).ConfigureAwait(false);
		await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
		return result;
	}

	[SuppressMessage("Security", "CA2100:Review SQL queries for security vulnerabilities",
		Justification = "Only internal constant SQL fragments and a validated integer lock timeout form command text. All caller data uses typed SQL parameters.")]
	private SqlCommand Command(SqlConnection connection, SqlTransaction? transaction, string sql) =>
		new($"SET LOCK_TIMEOUT {_lockTimeout}; {sql}", connection, transaction) { CommandTimeout = _commandTimeout };

	private DateTimeOffset Expiry(DateTimeOffset now)
	{
		try
		{
			// SQL's authoritative timestamp plus integer ticks avoids SQL 2022 DATEADD(int) limits.
			return now.AddTicks(LeaseDuration.Ticks);
		}
		catch (ArgumentOutOfRangeException exception)
		{
			throw new InvalidOperationException("The lease duration exceeds the SQL datetimeoffset range at the authoritative clock.", exception);
		}
	}

	private static bool Matches(Guid storeId, Row? row, ScheduleExecutionLease lease) =>
		row is not null && lease.StoreId == storeId && row.Message.Id == lease.ScheduleId
		&& row.Generation == lease.Generation && row.OwnerToken == lease.OwnerToken && row.Occurrence == lease.OccurrenceUtc;

	private static void RejectAmbientTransaction()
	{
		if (System.Transactions.Transaction.Current is not null)
		{
			throw new InvalidOperationException("Schedule authority operations cannot enlist in an ambient transaction.");
		}
	}

	private static void Add(SqlCommand command, string name, SqlDbType type, object? value)
	{
		var parameter = command.Parameters.Add(name, type);
		if (type == SqlDbType.NVarChar)
		{
			parameter.Size = -1;
		}
		if (type == SqlDbType.DateTimeOffset)
		{
			parameter.Scale = 7;
		}
		parameter.Value = value ?? DBNull.Value;
	}

	private static void AddMessage(SqlCommand command, ScheduleExecutionSnapshot snapshot)
	{
		var message = snapshot.Message;
		Add(command, "@Id", SqlDbType.UniqueIdentifier, message.Id);
		Add(command, "@Generation", SqlDbType.UniqueIdentifier, snapshot.Generation);
		Add(command, "@MessageName", SqlDbType.NVarChar, message.MessageName);
		Add(command, "@MessageBody", SqlDbType.NVarChar, message.MessageBody);
		Add(command, "@CronExpression", SqlDbType.NVarChar, message.CronExpression);
		Add(command, "@TimeZoneId", SqlDbType.NVarChar, message.TimeZoneId);
		Add(command, "@IntervalTicks", SqlDbType.BigInt, message.Interval?.Ticks);
		Add(command, "@NextExecutionUtc", SqlDbType.DateTimeOffset, message.NextExecutionUtc);
		Add(command, "@LastExecutionUtc", SqlDbType.DateTimeOffset, message.LastExecutionUtc);
		Add(command, "@Enabled", SqlDbType.Bit, message.Enabled);
		Add(command, "@MissedExecutionBehavior", SqlDbType.Int, message.MissedExecutionBehavior is { } behavior ? (int)behavior : null);
		Add(command, "@MetadataJson", SqlDbType.NVarChar, JsonSerializer.Serialize(
			new ScheduleMetadata(message.CorrelationId, message.TraceParent, message.TenantId, message.UserId),
			ScheduleMetadataJsonContext.Default.ScheduleMetadata));
	}

	private static Row ReadRow(SqlDataReader reader)
	{
		var metadata = JsonSerializer.Deserialize(reader.GetString(11), ScheduleMetadataJsonContext.Default.ScheduleMetadata)
			?? throw new InvalidOperationException("Schedule metadata cannot be JSON null.");
		var message = new ScheduledMessage
		{
			Id = reader.GetGuid(0), MessageName = reader.GetString(2), MessageBody = reader.GetString(3),
			CronExpression = reader.IsDBNull(4) ? null! : reader.GetString(4),
			TimeZoneId = reader.IsDBNull(5) ? null : reader.GetString(5),
			Interval = reader.IsDBNull(6) ? null : TimeSpan.FromTicks(reader.GetInt64(6)),
			NextExecutionUtc = reader.IsDBNull(7) ? null : reader.GetDateTimeOffset(7),
			LastExecutionUtc = reader.IsDBNull(8) ? null : reader.GetDateTimeOffset(8),
			Enabled = reader.GetBoolean(9),
			MissedExecutionBehavior = reader.IsDBNull(10) ? null : (MissedExecutionBehavior)reader.GetInt32(10),
			CorrelationId = metadata.CorrelationId, TraceParent = metadata.TraceParent,
			TenantId = metadata.TenantId, UserId = metadata.UserId,
		};
		return new Row(message, reader.GetGuid(1), reader.IsDBNull(12) ? null : reader.GetGuid(12),
			reader.IsDBNull(13) ? null : reader.GetDateTimeOffset(13), reader.IsDBNull(14) ? null : reader.GetDateTimeOffset(14),
			reader.GetBoolean(15));
	}

	private sealed record Row(ScheduledMessage Message, Guid Generation, Guid? OwnerToken,
		DateTimeOffset? Occurrence, DateTimeOffset? ExpiresUtc, bool ProgressCommitted);
}

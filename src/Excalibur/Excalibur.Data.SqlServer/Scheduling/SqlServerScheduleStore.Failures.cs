// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Data;
using Excalibur.Dispatch.Delivery;
using Microsoft.Data.SqlClient;

namespace Excalibur.Data.SqlServer.Scheduling;

public sealed partial class SqlServerScheduleStore
{
	private const string FailureColumns = Columns + ", StoreId, QuarantineId, QuarantineVersion, Reason, Disposition, RecordedUtc, LastFailureOwner, RecoveryCommandId, RecoveryGeneration, RecoveryRecordedUtc, LastFailureReason";
	private const string FailureKeyPredicate = "StoreId=@Store AND Id=@Id AND Generation=@Generation AND FailureOccurrenceUtc=@Occurrence";

	/// <inheritdoc />
	public async Task<ScheduleFailureExecutionAdmission?> TryAcquireExecutionAsync(Guid scheduleId, Guid generation,
		DateTimeOffset occurrenceUtc, int requestedMaxAdmissionAttempts, CancellationToken cancellationToken)
	{
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(requestedMaxAdmissionAttempts);
		return await MutateAsync<ScheduleFailureExecutionAdmission?>(scheduleId, async (connection, transaction, storeId, row, now, token) =>
		{
			if (!CanAdmit(row, generation, occurrenceUtc, now)) { return null; }
			if (row!.CapturedMaxAdmissionAttempts is null && row.AdmissionAttempts > 0)
			{
				throw new InvalidOperationException("Consumed legacy schedule admission history has no captured budget. Explicit administrative repair is required before preserving execution.");
			}
			var selection = row.CapturedMaxAdmissionAttempts.HasValue ? ScheduleAdmissionPolicySelection.UsedExisting : ScheduleAdmissionPolicySelection.CapturedNew;
			var limit = row.CapturedMaxAdmissionAttempts ?? requestedMaxAdmissionAttempts;
			var lease = await GrantAsync(connection, transaction, storeId, row, occurrenceUtc, now, limit, token).ConfigureAwait(false);
			var history = await ReadFailureAsync(connection, transaction, storeId, scheduleId, generation, occurrenceUtc, token).ConfigureAwait(false);
			var recovered = await HasRecoveryLineageAsync(connection, transaction, storeId, scheduleId, generation, occurrenceUtc, token).ConfigureAwait(false);
			return new ScheduleFailureExecutionAdmission(lease, limit, selection, lease.AdmissionAttempt == 1 && history is null && !recovered);
		}, cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc />
	public Task<bool> TryCompleteExecutionAsync(ScheduleExecutionLease lease, ScheduleExecutionResult result,
		ScheduleExecutionCompletionDisposition disposition, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(lease);
		ArgumentNullException.ThrowIfNull(result);
		if (!Enum.IsDefined(disposition)) { return Task.FromResult(false); }
		return MutateAsync(lease.ScheduleId, async (connection, transaction, storeId, row, now, token) =>
		{
			if (!Matches(storeId, row, lease) || row!.ExpiresUtc <= now || row.ProgressCommitted
				|| !row.Message.Enabled || row.CapturedMaxAdmissionAttempts is null || row.AdmissionAttempts > row.CapturedMaxAdmissionAttempts.Value) { return false; }
			if (!result.Disable && (result.NextExecutionUtc is null || result.NextExecutionUtc <= lease.OccurrenceUtc)) { return false; }
			var history = await ReadFailureAsync(connection, transaction, storeId, lease.ScheduleId, lease.Generation, lease.OccurrenceUtc, token).ConfigureAwait(false);
			if (history?.Snapshot.Disposition == ScheduleFailureDisposition.Quarantined) { return false; }
			if (disposition != ScheduleExecutionCompletionDisposition.Fulfilled)
			{
				if (row.AdmissionAttempts != 1 || history is not null || result.LastExecutionUtc != row.Message.LastExecutionUtc
					|| (disposition == ScheduleExecutionCompletionDisposition.PolicyDisabled && !result.Disable)
					|| (disposition == ScheduleExecutionCompletionDisposition.PolicySkipped && result.Disable)
					|| await HasRecoveryLineageAsync(connection, transaction, storeId, lease.ScheduleId, lease.Generation, lease.OccurrenceUtc, token).ConfigureAwait(false)) { return false; }
			}
			if (disposition == ScheduleExecutionCompletionDisposition.Fulfilled && history is not null)
			{
				await using var settled = Command(connection, transaction,
					$"DELETE FROM dispatch.ScheduleOccurrenceFailure WHERE {FailureKeyPredicate} AND Disposition=0");
				AddFailureKey(settled, history.Snapshot.Token);
				_ = await settled.ExecuteNonQueryAsync(token).ConfigureAwait(false);
			}
			await using var update = Command(connection, transaction,
				"UPDATE dispatch.ScheduledMessage SET LastExecutionUtc=@Last,NextExecutionUtc=@Next,Enabled=CASE WHEN @Disable=1 THEN 0 ELSE Enabled END,ProgressCommitted=1,AdmissionAttempts=0,CapturedMaxAdmissionAttempts=NULL WHERE Id=@Id");
			Add(update, "@Id", SqlDbType.UniqueIdentifier, lease.ScheduleId);
			Add(update, "@Last", SqlDbType.DateTimeOffset, result.LastExecutionUtc);
			Add(update, "@Next", SqlDbType.DateTimeOffset, result.NextExecutionUtc);
			Add(update, "@Disable", SqlDbType.Bit, result.Disable);
			_ = await update.ExecuteNonQueryAsync(token).ConfigureAwait(false);
			return true;
		}, cancellationToken);
	}

	/// <inheritdoc />
	public Task<bool> TryRecordFailureAsync(ScheduleExecutionLease lease, ScheduleOccurrenceFailure failure, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(lease);
		ArgumentNullException.ThrowIfNull(failure);
		if (!Enum.IsDefined(failure.Reason) || !Enum.IsDefined(failure.Disposition)) { throw new ArgumentOutOfRangeException(nameof(failure)); }
		return MutateAsync(lease.ScheduleId, async (connection, transaction, storeId, row, now, token) =>
		{
			if (!Matches(storeId, row, lease) || row!.ExpiresUtc <= now || row.ProgressCommitted || row.CapturedMaxAdmissionAttempts is null) { return false; }
			if (failure.Disposition == ScheduleFailureDisposition.Retrying && row.AdmissionAttempts > row.CapturedMaxAdmissionAttempts.Value) { return false; }
			var history = await ReadFailureAsync(connection, transaction, storeId, lease.ScheduleId, lease.Generation, lease.OccurrenceUtc, token).ConfigureAwait(false);
			if (history is not null && history.LastOwner == lease.OwnerToken)
			{
				return history.LastRequestedReason == failure.Reason && history.Snapshot.Disposition == failure.Disposition;
			}
			if (history?.Snapshot.Disposition == ScheduleFailureDisposition.Quarantined) { return false; }
			if (failure.Disposition == ScheduleFailureDisposition.Quarantined && row.AdmissionAttempts <= row.CapturedMaxAdmissionAttempts.Value) { return false; }
			await WriteFailureAsync(connection, transaction, storeId, row, lease.OccurrenceUtc, lease.OwnerToken, failure, now, history, token).ConfigureAwait(false);
			if (failure.Disposition == ScheduleFailureDisposition.Quarantined)
			{
				await using var disable = Command(connection, transaction, "UPDATE dispatch.ScheduledMessage SET Enabled=0 WHERE Id=@Id");
				Add(disable, "@Id", SqlDbType.UniqueIdentifier, lease.ScheduleId);
				_ = await disable.ExecuteNonQueryAsync(token).ConfigureAwait(false);
			}
			return true;
		}, cancellationToken);
	}

	/// <inheritdoc />
	public Task<ScheduleQuarantineRecoveryReceipt?> TryResumeQuarantinedOccurrenceAsync(ScheduleQuarantineToken expectedQuarantineToken,
		Guid recoveryCommandId, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(expectedQuarantineToken);
		if (recoveryCommandId == Guid.Empty) { throw new ArgumentException("Recovery command must be nonempty.", nameof(recoveryCommandId)); }
		return MutateAsync<ScheduleQuarantineRecoveryReceipt?>(expectedQuarantineToken.ScheduleId,
			async (connection, transaction, storeId, row, now, token) =>
		{
			// Command replay is checked BEFORE current generation/state: a later success/replacement
			// cannot turn identical old command replay into a new budget grant or a false conflict.
			await using (var replay = Command(connection, transaction,
				$"SELECT {FailureColumns} FROM dispatch.ScheduleOccurrenceFailure WITH (UPDLOCK,HOLDLOCK) WHERE RecoveryCommandId=@Command"))
			{
				Add(replay, "@Command", SqlDbType.UniqueIdentifier, recoveryCommandId);
				await using var reader = await replay.ExecuteReaderAsync(token).ConfigureAwait(false);
				if (await reader.ReadAsync(token).ConfigureAwait(false))
				{
					var found = ReadFailure(reader).Snapshot;
					return found.Token == expectedQuarantineToken && found.Token.StoreId == storeId ? found.RecoveryReceipt : null;
				}
			}
			if (storeId != expectedQuarantineToken.StoreId || row is null || row.Generation != expectedQuarantineToken.Generation
				|| row.Message.NextExecutionUtc != expectedQuarantineToken.OccurrenceUtc || row.Message.Enabled) { return null; }
			var retained = await ReadFailureAsync(connection, transaction, storeId, row.Message.Id, row.Generation, expectedQuarantineToken.OccurrenceUtc, token).ConfigureAwait(false);
			if (retained is null || retained.Snapshot.Token != expectedQuarantineToken || retained.Snapshot.Disposition != ScheduleFailureDisposition.Quarantined || retained.Snapshot.RecoveryReceipt is not null) { return null; }
			var fresh = Guid.NewGuid();
			var limit = retained.Snapshot.MaxAdmissionAttempts;
			await using (var receipt = Command(connection, transaction,
				$"UPDATE dispatch.ScheduleOccurrenceFailure SET RecoveryCommandId=@Command, RecoveryGeneration=@Fresh, RecoveryRecordedUtc=@Now WHERE {FailureKeyPredicate}"))
			{
				AddFailureKey(receipt, expectedQuarantineToken);
				Add(receipt, "@Command", SqlDbType.UniqueIdentifier, recoveryCommandId);
				Add(receipt, "@Fresh", SqlDbType.UniqueIdentifier, fresh);
				Add(receipt, "@Now", SqlDbType.DateTimeOffset, now);
				_ = await receipt.ExecuteNonQueryAsync(token).ConfigureAwait(false);
			}
			await using var resume = Command(connection, transaction,
				"UPDATE scheduledRow SET Generation=@Fresh,MessageName=history.MessageName,MessageBody=history.MessageBody,CronExpression=history.CronExpression,TimeZoneId=history.TimeZoneId,CronRecurrenceProfileId=history.CronRecurrenceProfileId,CronRecurrenceProfileVersion=history.CronRecurrenceProfileVersion,IntervalTicks=history.IntervalTicks,LastExecutionUtc=history.LastExecutionUtc,MissedExecutionBehavior=history.MissedExecutionBehavior,MetadataJson=history.MetadataJson,Enabled=1,NextExecutionUtc=@Occurrence,OwnerToken=NULL,AdmittedOccurrence=NULL,LeaseExpiresUtc=NULL,ProgressCommitted=0,AdmissionAttempts=0,CapturedMaxAdmissionAttempts=@Limit FROM dispatch.ScheduledMessage scheduledRow INNER JOIN dispatch.ScheduleOccurrenceFailure history ON scheduledRow.Id=history.Id WHERE scheduledRow.Id=@Id AND history.StoreId=@Store AND history.Generation=@OriginalGeneration AND history.FailureOccurrenceUtc=@Occurrence");
			Add(resume, "@Fresh", SqlDbType.UniqueIdentifier, fresh);
			Add(resume, "@Occurrence", SqlDbType.DateTimeOffset, expectedQuarantineToken.OccurrenceUtc);
			Add(resume, "@Limit", SqlDbType.Int, limit);
			Add(resume, "@Id", SqlDbType.UniqueIdentifier, row.Message.Id);
			Add(resume, "@Store", SqlDbType.UniqueIdentifier, storeId);
			Add(resume, "@OriginalGeneration", SqlDbType.UniqueIdentifier, expectedQuarantineToken.Generation);
			_ = await resume.ExecuteNonQueryAsync(token).ConfigureAwait(false);
			return new ScheduleQuarantineRecoveryReceipt(recoveryCommandId, expectedQuarantineToken, fresh, expectedQuarantineToken.OccurrenceUtc, limit, now);
		}, cancellationToken);
	}

	/// <inheritdoc />
	public async Task<ScheduleOccurrenceFailureSnapshot?> GetOccurrenceFailureAsync(Guid scheduleId, Guid generation,
		DateTimeOffset occurrenceUtc, string? tenantId, CancellationToken cancellationToken)
	{
		RejectAmbientTransaction();
		await using var connection = new SqlConnection(_connectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
		await using var query = Command(connection, null,
			$"SELECT {FailureColumns} FROM dispatch.ScheduleOccurrenceFailure WHERE StoreId=(SELECT StoreId FROM dispatch.ScheduleAuthority WHERE SingletonId=1) AND Id=@Id AND Generation=@Generation AND FailureOccurrenceUtc=@Occurrence AND ((TenantId COLLATE Latin1_General_100_BIN2=@Tenant COLLATE Latin1_General_100_BIN2 AND DATALENGTH(TenantId)=DATALENGTH(@Tenant)) OR (TenantId IS NULL AND @Tenant IS NULL))");
		Add(query, "@Id", SqlDbType.UniqueIdentifier, scheduleId);
		Add(query, "@Generation", SqlDbType.UniqueIdentifier, generation);
		Add(query, "@Occurrence", SqlDbType.DateTimeOffset, occurrenceUtc);
		Add(query, "@Tenant", SqlDbType.NVarChar, tenantId);
		await using var reader = await query.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
		return await reader.ReadAsync(cancellationToken).ConfigureAwait(false) ? ReadFailure(reader).Snapshot : null;
	}

	/// <inheritdoc />
	public async Task<IReadOnlyList<ScheduleOccurrenceFailureSnapshot>> GetQuarantinesAsync(string? tenantId,
		Guid? afterQuarantineId, int maximumCount, CancellationToken cancellationToken)
	{
		ArgumentOutOfRangeException.ThrowIfLessThan(maximumCount, 1);
		ArgumentOutOfRangeException.ThrowIfGreaterThan(maximumCount, 256);
		RejectAmbientTransaction();
		await using var connection = new SqlConnection(_connectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
		await using var query = Command(connection, null,
			$"SELECT TOP (@Maximum) {FailureColumns} FROM dispatch.ScheduleOccurrenceFailure WHERE StoreId=(SELECT StoreId FROM dispatch.ScheduleAuthority WHERE SingletonId=1) AND Disposition=1 AND (@After IS NULL OR QuarantineOrdinal>@After COLLATE Latin1_General_100_BIN2) AND ((TenantId COLLATE Latin1_General_100_BIN2=@Tenant COLLATE Latin1_General_100_BIN2 AND DATALENGTH(TenantId)=DATALENGTH(@Tenant)) OR (TenantId IS NULL AND @Tenant IS NULL)) ORDER BY QuarantineOrdinal");
		Add(query, "@Maximum", SqlDbType.Int, maximumCount);
		Add(query, "@After", SqlDbType.NVarChar, afterQuarantineId?.ToString("N"));
		Add(query, "@Tenant", SqlDbType.NVarChar, tenantId);
		await using var reader = await query.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
		var result = new List<ScheduleOccurrenceFailureSnapshot>();
		while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) { result.Add(ReadFailure(reader).Snapshot); }
		return result;
	}

	private static bool CanAdmit(Row? row, Guid generation, DateTimeOffset occurrence, DateTimeOffset now) =>
		row is not null && row.Message.Enabled && row.Generation == generation && row.Message.NextExecutionUtc == occurrence && occurrence <= now && !(row.OwnerToken is not null && row.ExpiresUtc > now);

	private async Task<ScheduleExecutionLease> GrantAsync(SqlConnection connection, SqlTransaction transaction, Guid storeId,
		Row row, DateTimeOffset occurrence, DateTimeOffset now, int? capturedLimit, CancellationToken token)
	{
		if (row.CapturedMaxAdmissionAttempts is null) { ValidateCronRecurrence(row.Message); }
		var attempt = checked(row.AdmissionAttempts + 1);
		var lease = new ScheduleExecutionLease(storeId, row.Message.Id, row.Generation, occurrence, Guid.NewGuid()) { AdmissionAttempt = attempt };
		await using var command = Command(connection, transaction,
			"UPDATE dispatch.ScheduledMessage SET OwnerToken=@Owner,AdmittedOccurrence=@Occurrence,LeaseExpiresUtc=@Expires,ProgressCommitted=0,AdmissionAttempts=@Attempts,CapturedMaxAdmissionAttempts=@Limit WHERE Id=@Id");
		Add(command, "@Id", SqlDbType.UniqueIdentifier, row.Message.Id);
		Add(command, "@Owner", SqlDbType.UniqueIdentifier, lease.OwnerToken);
		Add(command, "@Occurrence", SqlDbType.DateTimeOffset, occurrence);
		Add(command, "@Expires", SqlDbType.DateTimeOffset, Expiry(now));
		Add(command, "@Attempts", SqlDbType.Int, attempt);
		Add(command, "@Limit", SqlDbType.Int, capturedLimit);
		_ = await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
		return lease;
	}

	private async Task<FailureRow?> ReadFailureAsync(SqlConnection connection, SqlTransaction transaction, Guid storeId,
		Guid id, Guid generation, DateTimeOffset occurrence, CancellationToken token)
	{
		await using var query = Command(connection, transaction,
			$"SELECT {FailureColumns} FROM dispatch.ScheduleOccurrenceFailure WITH (UPDLOCK,HOLDLOCK) WHERE {FailureKeyPredicate}");
		AddFailureKey(query, new ScheduleQuarantineToken(storeId, Guid.Empty, 0, id, generation, occurrence));
		await using var reader = await query.ExecuteReaderAsync(token).ConfigureAwait(false);
		return await reader.ReadAsync(token).ConfigureAwait(false) ? ReadFailure(reader) : null;
	}

	private async Task<bool> HasRecoveryLineageAsync(SqlConnection connection, SqlTransaction transaction, Guid storeId,
		Guid id, Guid generation, DateTimeOffset occurrence, CancellationToken token)
	{
		await using var query = Command(connection, transaction,
			"SELECT TOP(1) 1 FROM dispatch.ScheduleOccurrenceFailure WITH (HOLDLOCK) WHERE StoreId=@Store AND Id=@Id AND RecoveryGeneration=@Generation AND FailureOccurrenceUtc=@Occurrence");
		Add(query, "@Store", SqlDbType.UniqueIdentifier, storeId);
		Add(query, "@Id", SqlDbType.UniqueIdentifier, id);
		Add(query, "@Generation", SqlDbType.UniqueIdentifier, generation);
		Add(query, "@Occurrence", SqlDbType.DateTimeOffset, occurrence);
		return await query.ExecuteScalarAsync(token).ConfigureAwait(false) is not null;
	}

	private async Task WriteFailureAsync(SqlConnection connection, SqlTransaction transaction, Guid storeId, Row row,
		DateTimeOffset occurrence, Guid owner, ScheduleOccurrenceFailure failure, DateTimeOffset now, FailureRow? previous, CancellationToken token)
	{
		var identity = previous?.Snapshot.Token ?? new ScheduleQuarantineToken(storeId, Guid.NewGuid(), 0, row.Message.Id, row.Generation, occurrence);
		var version = checked(identity.Version + 1);
		var retainedReason = failure.Disposition == ScheduleFailureDisposition.Quarantined ? previous?.Snapshot.Reason ?? failure.Reason : failure.Reason;
		var sql = previous is null
			? $"INSERT dispatch.ScheduleOccurrenceFailure ({Columns},StoreId,FailureOccurrenceUtc,QuarantineId,QuarantineVersion,Reason,Disposition,RecordedUtc,LastFailureOwner,TenantId,LastFailureReason) SELECT {Columns},@Store,@Occurrence,@Quarantine,@Version,@Reason,@Disposition,@Now,@Owner,@Tenant,@RequestedReason FROM dispatch.ScheduledMessage WHERE Id=@Id"
			: $"UPDATE dispatch.ScheduleOccurrenceFailure SET QuarantineVersion=@Version,Reason=@Reason,Disposition=@Disposition,RecordedUtc=@Now,LastFailureOwner=@Owner,LastFailureReason=@RequestedReason,AdmissionAttempts=@Attempts WHERE {FailureKeyPredicate}";
		await using var write = Command(connection, transaction, sql);
		AddFailureKey(write, identity);
		Add(write, "@Quarantine", SqlDbType.UniqueIdentifier, identity.QuarantineId);
		Add(write, "@Version", SqlDbType.BigInt, version);
		Add(write, "@Reason", SqlDbType.Int, (int)retainedReason);
		Add(write, "@RequestedReason", SqlDbType.Int, (int)failure.Reason);
		Add(write, "@Disposition", SqlDbType.Int, (int)failure.Disposition);
		Add(write, "@Now", SqlDbType.DateTimeOffset, now);
		Add(write, "@Owner", SqlDbType.UniqueIdentifier, owner);
		Add(write, "@Attempts", SqlDbType.Int, row.AdmissionAttempts);
		Add(write, "@Tenant", SqlDbType.NVarChar, row.Message.TenantId);
		_ = await write.ExecuteNonQueryAsync(token).ConfigureAwait(false);
	}

	private static void AddFailureKey(SqlCommand command, ScheduleQuarantineToken identity)
	{
		Add(command, "@Store", SqlDbType.UniqueIdentifier, identity.StoreId);
		Add(command, "@Id", SqlDbType.UniqueIdentifier, identity.ScheduleId);
		Add(command, "@Generation", SqlDbType.UniqueIdentifier, identity.Generation);
		Add(command, "@Occurrence", SqlDbType.DateTimeOffset, identity.OccurrenceUtc);
	}

	private static FailureRow ReadFailure(SqlDataReader reader)
	{
		var row = ReadRow(reader);
		var identity = new ScheduleQuarantineToken(reader.GetGuid(20), reader.GetGuid(21), reader.GetInt64(22), row.Message.Id, row.Generation, row.Message.NextExecutionUtc!.Value);
		ScheduleQuarantineRecoveryReceipt? receipt = reader.IsDBNull(27) ? null :
			new(reader.GetGuid(27), identity, reader.GetGuid(28), identity.OccurrenceUtc, row.CapturedMaxAdmissionAttempts!.Value, reader.GetDateTimeOffset(29));
		return new FailureRow(new ScheduleOccurrenceFailureSnapshot(identity, (ScheduleFailureReason)reader.GetInt32(23),
			(ScheduleFailureDisposition)reader.GetInt32(24), row.AdmissionAttempts, row.CapturedMaxAdmissionAttempts!.Value,
			reader.GetDateTimeOffset(25), row.Message, receipt), reader.GetGuid(26), (ScheduleFailureReason)reader.GetInt32(30));
	}

	private sealed record FailureRow(ScheduleOccurrenceFailureSnapshot Snapshot, Guid LastOwner, ScheduleFailureReason LastRequestedReason);
}

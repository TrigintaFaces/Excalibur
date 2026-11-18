// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Data;
using Dapper;
using Excalibur.Data;
using Excalibur.Dispatch;

namespace Excalibur.Outbox.SqlServer.Requests;

internal enum SqlServerClaimTransition { Sent, Failed, DeadLettered }

/// <summary>A locked preimage decision and guarded transition in the caller-owned transaction.</summary>
[NoTenantTerm(TenantConfinement.IdentityAddressed,
    "Cross-tenant completion addresses the globally unique claimed Id and exact acquisition; tenant provenance is never rewritten.")]
internal sealed class SqlServerClaimCompletionRequest : DataRequestBase<IDbConnection, OutboxCompletionOutcome>
{
    internal SqlServerClaimCompletionRequest(string table, string messageId, string? claimIdentity,
        SqlServerClaimTransition transition, string? error, int attempts, DateTimeOffset? nextAttemptAt,
        int floorSeconds, int leaseSeconds, int commandTimeout, long? fencingToken, IDbTransaction transaction, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        var assignment = transition switch
        {
            SqlServerClaimTransition.Sent => "SET Status = 2, SentAt = @LastAttemptAt, LastError = NULL, LeasedAt = NULL, LeasedBy = NULL, FencingToken = CASE WHEN @FencingToken IS NULL THEN FencingToken ELSE @FencingToken END",
            SqlServerClaimTransition.Failed => OutboxFailureMark.SetClause +
                OutboxFailureMark.NextAttemptClause(nextAttemptAt.HasValue, hasFloor: true),
            SqlServerClaimTransition.DeadLettered => "SET Status = 5, LastError = @ErrorMessage, RetryCount = CASE WHEN RetryCount < @RetryCount THEN @RetryCount ELSE RetryCount END, LastAttemptAt = @LastAttemptAt, LeasedAt = NULL, LeasedBy = NULL",
            _ => throw new ArgumentOutOfRangeException(nameof(transition)),
        };
        var sql = $"""
            DECLARE @Found bit = 0, @Status int, @Lease datetimeoffset, @Owner nvarchar(255);
            SELECT @Found = 1, @Status = Status, @Lease = LeasedAt, @Owner = LeasedBy
            FROM {table} WITH (UPDLOCK, HOLDLOCK) WHERE Id = @MessageId;
            DECLARE @Now datetimeoffset = TODATETIMEOFFSET(SYSUTCDATETIME(), 0);
            SET @Outcome = {(int)OutboxCompletionOutcome.MessageNotFound};
            IF @Found = 1
            BEGIN
                IF @Status IN (2, 5)
                    SET @Outcome = {(int)OutboxCompletionOutcome.AlreadyTerminal};
                ELSE IF @ClaimIdentity IS NOT NULL AND
                    (@Owner IS NULL OR @Lease IS NULL OR @Lease < DATEADD(SECOND, -@LeaseSeconds, @Now)
                     OR DATALENGTH(@Owner) <> DATALENGTH(@ClaimIdentity)
                     OR CONVERT(varbinary(max), @Owner) <> CONVERT(varbinary(max), @ClaimIdentity))
                    SET @Outcome = {(int)OutboxCompletionOutcome.ClaimLost};
                ELSE
                BEGIN
                    UPDATE {table}
                    {assignment}
                    WHERE Id = @MessageId AND Status NOT IN (2, 5)
                      AND (@ClaimIdentity IS NULL OR
                          (LeasedAt IS NOT NULL AND LeasedAt >= DATEADD(SECOND, -@LeaseSeconds, @Now)
                           AND DATALENGTH(LeasedBy) = DATALENGTH(@ClaimIdentity)
                           AND CONVERT(varbinary(max), LeasedBy) = CONVERT(varbinary(max), @ClaimIdentity)));
                    DECLARE @Changed int = @@ROWCOUNT;
                    IF @Changed <> 1 THROW 51002, 'Locked eligible outbox transition did not mutate exactly one row.', 1;
                    SET @Outcome = {(int)OutboxCompletionOutcome.Applied};
                END;
            END;
            """;
        var parameters = new DynamicParameters();
        parameters.Add("MessageId", messageId);
        parameters.Add("ClaimIdentity", claimIdentity, DbType.String);
        if (transition != SqlServerClaimTransition.Sent)
        {
            parameters.Add("ErrorMessage", error, DbType.String);
            parameters.Add("RetryCount", attempts);
        }
        parameters.Add("LastAttemptAt", DateTimeOffset.UtcNow);
        parameters.Add("LeaseSeconds", leaseSeconds);
        if (transition == SqlServerClaimTransition.Sent) { parameters.Add("FencingToken", fencingToken, DbType.Int64); }
        if (transition == SqlServerClaimTransition.Failed)
        {
            parameters.Add("FloorSeconds", floorSeconds);
            if (nextAttemptAt.HasValue) { parameters.Add("NextAttemptDelayMs", OutboxFailureMark.ToServerDelayMilliseconds(nextAttemptAt.Value)); }
        }
        parameters.Add("Outcome", dbType: DbType.Int32, direction: ParameterDirection.Output);
        Command = CreateCommand(sql, parameters, transaction, commandTimeout, cancellationToken: token);
        ResolveAsync = connection => StaticDbCommand.ExecuteOutputsAsync(connection, Command, command =>
        {
            StaticDbCommand.AddParameter(command, "MessageId", StaticDbCommand.RequiredInput<string>(Parameters, "MessageId"), DbType.String);
            StaticDbCommand.AddNullableStringParameter(command, "ClaimIdentity", StaticDbCommand.Input<string?>(Parameters, "ClaimIdentity"));
            if (transition != SqlServerClaimTransition.Sent)
            {
                StaticDbCommand.AddNullableStringParameter(command, "ErrorMessage", StaticDbCommand.Input<string?>(Parameters, "ErrorMessage"));
                StaticDbCommand.AddParameter(command, "RetryCount", StaticDbCommand.Input<int>(Parameters, "RetryCount"), DbType.Int32);
            }
            StaticDbCommand.AddParameter(command, "LastAttemptAt", StaticDbCommand.Input<DateTimeOffset>(Parameters, "LastAttemptAt"), DbType.DateTimeOffset);
            StaticDbCommand.AddParameter(command, "LeaseSeconds", StaticDbCommand.Input<int>(Parameters, "LeaseSeconds"), DbType.Int32);
            if (transition == SqlServerClaimTransition.Sent)
            {
                StaticDbCommand.AddParameter(command, "FencingToken", StaticDbCommand.Input<long?>(Parameters, "FencingToken"), DbType.Int64);
            }
            if (transition == SqlServerClaimTransition.Failed)
            {
                StaticDbCommand.AddParameter(command, "FloorSeconds", StaticDbCommand.Input<int>(Parameters, "FloorSeconds"), DbType.Int32);
            }
            if (transition == SqlServerClaimTransition.Failed && Parameters.ParameterNames.Contains("NextAttemptDelayMs", StringComparer.Ordinal))
            {
                StaticDbCommand.AddParameter(command, "NextAttemptDelayMs", StaticDbCommand.Input<int>(Parameters, "NextAttemptDelayMs"), DbType.Int32);
            }
            StaticDbCommand.AddOutputParameter(command, "Outcome", DbType.Int32);
        }, command =>
        {
            var outcome = (OutboxCompletionOutcome)StaticDbCommand.Output<int>(command, Parameters, "Outcome", DbType.Int32);
            return outcome is OutboxCompletionOutcome.Applied or OutboxCompletionOutcome.ClaimLost or
                OutboxCompletionOutcome.MessageNotFound or OutboxCompletionOutcome.AlreadyTerminal
                ? outcome : throw new InvalidDataException("Invalid outbox transition outcome.");
        });
    }
}

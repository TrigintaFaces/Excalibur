// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Data;
using Dapper;
using Excalibur.Data;
using Excalibur.Dispatch;

namespace Excalibur.Outbox.Postgres;

internal enum PostgresClaimTransition { Sent, Failed, DeadLettered }

/// <summary>One dependent statement on the caller's connection; no transaction ownership is acquired.</summary>
[NoTenantTerm(TenantConfinement.IdentityAddressed,
    "Cross-tenant completion addresses the unique acquired message id and exact live acquisition; the archive retains original tenant provenance.")]
internal sealed class ClaimScopedOutboxCompletion : DataRequest<OutboxCompletionOutcome>
{
    internal ClaimScopedOutboxCompletion(string messageId, string claimIdentity, long? fencingToken,
        PostgresClaimTransition transition, string? error, int attempts, double? nextDelaySeconds, int floorSeconds,
        string outboxTable, string archiveTable, string fenceTable, int timeout, CancellationToken cancellationToken)
    {
        var authority = fencingToken.HasValue
            ? $"""
                authority AS (
                    INSERT INTO {fenceTable} AS f (scope_key,high_water_token) VALUES (@Scope,@Token)
                    ON CONFLICT (scope_key) DO UPDATE SET high_water_token=GREATEST(f.high_water_token,@Token)
                    RETURNING high_water_token
                ), checked_authority AS MATERIALIZED (
                    SELECT COALESCE(MAX(high_water_token),0) /
                        CASE WHEN COUNT(*)=1 AND COUNT(high_water_token)=1 AND MIN(high_water_token)>0
                             THEN 1 ELSE 0 END AS high_water_token
                    FROM authority
                )
                """
            : "authority AS MATERIALIZED (SELECT 0::bigint AS high_water_token)";
        var admitted = fencingToken.HasValue ? "AND (SELECT high_water_token FROM checked_authority)=@Token" : string.Empty;
        var fenceRefusal = fencingToken.HasValue
            ? $"WHEN (SELECT high_water_token FROM checked_authority)<>@Token THEN {(int)OutboxCompletionOutcome.FenceRefused}"
            : string.Empty;
        var exact = "convert_to(o.dispatcher_id,'UTF8')=convert_to(@ClaimIdentity,'UTF8')";
        var mutation = transition switch
        {
            PostgresClaimTransition.Sent => $"""
                changed AS (
                    DELETE FROM {outboxTable} o USING eligible e
                    WHERE o.message_id=e.message_id AND {exact}
                      AND o.dispatcher_timeout IS NOT NULL AND o.dispatcher_timeout>=e.decision_time
                    RETURNING o.message_id
                ), counts AS (
                    SELECT (SELECT COUNT(*) FROM eligible)::int AS eligible_count,
                           (SELECT COUNT(*) FROM changed)::int AS changed_count
                ), verified AS (
                    SELECT eligible_count, changed_count /
                        CASE WHEN eligible_count=changed_count AND eligible_count<=1 THEN 1 ELSE 0 END AS checked_count FROM counts
                )
                """,
            PostgresClaimTransition.Failed => FailureMutation(outboxTable, exact, nextDelaySeconds.HasValue),
            PostgresClaimTransition.DeadLettered => $"""
                archived AS (
                    INSERT INTO {archiveTable}
                        (message_id,tenant_id,message_type,message_metadata,message_body,occurred_on,attempts,error_message)
                    SELECT message_id,tenant_id,message_type,message_metadata,message_body,occurred_on,GREATEST(attempts,@Attempts),@Error
                    FROM eligible RETURNING message_id
                ), changed AS (
                    DELETE FROM {outboxTable} o USING archived a, eligible e
                    WHERE o.message_id=a.message_id AND o.message_id=e.message_id AND {exact}
                      AND o.dispatcher_timeout IS NOT NULL AND o.dispatcher_timeout>=e.decision_time
                    RETURNING o.message_id
                ), counts AS (
                    SELECT (SELECT COUNT(*) FROM eligible)::int AS eligible_count,
                           (SELECT COUNT(*) FROM archived)::int AS archived_count,
                           (SELECT COUNT(*) FROM changed)::int AS changed_count
                ), verified AS (
                    SELECT eligible_count, changed_count /
                        CASE WHEN eligible_count=archived_count AND archived_count=changed_count AND eligible_count<=1 THEN 1 ELSE 0 END AS checked_count FROM counts
                )
                """,
            _ => throw new ArgumentOutOfRangeException(nameof(transition)),
        };
        var sql = $"""
            WITH {authority},
            locked AS MATERIALIZED (
                SELECT o.message_id,o.tenant_id,o.message_type,o.message_metadata,o.message_body,o.occurred_on,
                       o.attempts,o.dispatcher_id,o.dispatcher_timeout
                FROM {outboxTable} o WHERE o.message_id=@MessageId {admitted}
                FOR UPDATE OF o
            ), decision AS MATERIALIZED (
                SELECT locked.*,clock_timestamp() AS decision_time FROM locked
            ), eligible AS MATERIALIZED (
                SELECT * FROM decision WHERE dispatcher_id IS NOT NULL AND dispatcher_timeout IS NOT NULL
                    AND dispatcher_timeout>=decision_time
                    AND convert_to(dispatcher_id,'UTF8')=convert_to(@ClaimIdentity,'UTF8')
            ), {mutation}
            SELECT CASE {fenceRefusal}
                WHEN NOT EXISTS (SELECT 1 FROM locked) THEN {(int)OutboxCompletionOutcome.MessageNotFound}
                WHEN eligible_count=0 THEN {(int)OutboxCompletionOutcome.ClaimLost}
                WHEN checked_count=1 THEN {(int)OutboxCompletionOutcome.Applied}
                ELSE {(int)OutboxCompletionOutcome.Unknown} END AS Outcome
            FROM verified;
            """;
        var parameters = new DynamicParameters();
        parameters.Add("MessageId", messageId);
        parameters.Add("ClaimIdentity", claimIdentity);
        if (fencingToken.HasValue) { parameters.Add("Scope", outboxTable); parameters.Add("Token", fencingToken.Value); }
        if (transition != PostgresClaimTransition.Sent) { parameters.Add("Error", error); }
        if (transition == PostgresClaimTransition.DeadLettered || (transition == PostgresClaimTransition.Failed && !nextDelaySeconds.HasValue))
        {
            parameters.Add("Attempts", attempts);
        }
        if (transition == PostgresClaimTransition.Failed)
        {
            parameters.Add("FloorSeconds", (double)floorSeconds);
            if (nextDelaySeconds.HasValue) { parameters.Add("NextDelay", nextDelaySeconds.Value); }
        }
        Command = CreateCommand(sql, parameters, commandTimeout: timeout, cancellationToken: cancellationToken);
        ResolveAsync = connection => StaticDbCommand.SingleAsync(connection, Command, command =>
        {
            StaticDbCommand.AddParameter(command,"MessageId",StaticDbCommand.RequiredInput<string>(Parameters,"MessageId"),DbType.String);
            StaticDbCommand.AddParameter(command,"ClaimIdentity",StaticDbCommand.RequiredInput<string>(Parameters,"ClaimIdentity"),DbType.String);
            if (fencingToken.HasValue)
            {
                StaticDbCommand.AddParameter(command,"Scope",StaticDbCommand.RequiredInput<string>(Parameters,"Scope"),DbType.String);
                StaticDbCommand.AddParameter(command,"Token",StaticDbCommand.Input<long>(Parameters,"Token"),DbType.Int64);
            }
            if (transition != PostgresClaimTransition.Sent)
            {
                StaticDbCommand.AddParameter(command,"Error",StaticDbCommand.RequiredInput<string>(Parameters,"Error"),DbType.String);
            }
            if (Parameters.ParameterNames.Contains("Attempts",StringComparer.Ordinal))
            {
                StaticDbCommand.AddParameter(command,"Attempts",StaticDbCommand.Input<int>(Parameters,"Attempts"),DbType.Int32);
            }
            if (transition == PostgresClaimTransition.Failed)
            {
                StaticDbCommand.AddParameter(command,"FloorSeconds",StaticDbCommand.Input<double>(Parameters,"FloorSeconds"),DbType.Double);
                if (nextDelaySeconds.HasValue)
                {
                    StaticDbCommand.AddParameter(command,"NextDelay",StaticDbCommand.Input<double>(Parameters,"NextDelay"),DbType.Double);
                }
            }
        }, static reader => StaticDbValue.Int32(reader,0) switch
        {
            (int)OutboxCompletionOutcome.Applied => OutboxCompletionOutcome.Applied,
            (int)OutboxCompletionOutcome.ClaimLost => OutboxCompletionOutcome.ClaimLost,
            (int)OutboxCompletionOutcome.MessageNotFound => OutboxCompletionOutcome.MessageNotFound,
            (int)OutboxCompletionOutcome.FenceRefused => OutboxCompletionOutcome.FenceRefused,
            _ => throw new InvalidDataException("Invalid PostgreSQL claim-completion outcome."),
        });
    }

    private static string FailureMutation(string table, string exact, bool scheduled)
    {
        var attempts = scheduled ? "o.attempts+1" : "GREATEST(o.attempts,@Attempts)";
        var interval = scheduled
            ? "GREATEST(make_interval(secs=>@NextDelay),make_interval(secs=>@FloorSeconds))"
            : "make_interval(secs=>@FloorSeconds)";
        return $"""
            changed AS (
                UPDATE {table} o SET attempts={attempts},error_message=@Error,
                    dispatcher_id=NULL,dispatcher_timeout=NULL,next_attempt_at=e.decision_time+{interval}
                FROM eligible e WHERE o.message_id=e.message_id AND {exact}
                    AND o.dispatcher_timeout IS NOT NULL AND o.dispatcher_timeout>=e.decision_time
                RETURNING o.message_id
            ), counts AS (
                SELECT (SELECT COUNT(*) FROM eligible)::int AS eligible_count,
                       (SELECT COUNT(*) FROM changed)::int AS changed_count
            ), verified AS (
                SELECT eligible_count, changed_count /
                    CASE WHEN eligible_count=changed_count AND eligible_count<=1 THEN 1 ELSE 0 END AS checked_count FROM counts
            )
            """;
    }
}

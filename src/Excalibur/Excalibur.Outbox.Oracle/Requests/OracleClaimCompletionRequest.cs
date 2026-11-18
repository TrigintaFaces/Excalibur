// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Data;

using Dapper;

using Excalibur.Data;
using Excalibur.Dispatch;

namespace Excalibur.Outbox.Oracle;

internal enum OracleClaimTransition { Sent, Failed, DeadLetter }

/// <summary>Closed owned-transaction request. Classifies from a locked preimage, never a later lookup.</summary>
[NoTenantTerm(TenantConfinement.IdentityAddressed,
	"The globally unique message id addresses the claimed row; its actual tenant is copied into the archive, never supplied by the worker.")]
internal sealed class OracleClaimCompletionRequest : DataRequest<OutboxCompletionOutcome>
{
	internal OracleClaimCompletionRequest(OracleOutboxStoreOptions options, OraclePhysicalOutboxScope scope, string messageId,
		OracleClaimTransition transition, string? error, int attempts, double delaySeconds,
		string? claimIdentity, long? token, IDbTransaction transaction, CancellationToken cancellationToken)
	{
		if (transition is not (OracleClaimTransition.Sent or OracleClaimTransition.Failed or OracleClaimTransition.DeadLetter))
		{
			throw new ArgumentOutOfRangeException(nameof(transition));
		}
		var sql = $"""
			DECLARE
			    v_high NUMBER;
			    v_owner {scope.OutboxTable}.dispatcher_id%TYPE;
			    v_timeout {scope.OutboxTable}.dispatcher_timeout%TYPE;
			    v_now TIMESTAMP WITH TIME ZONE;
			    v_owner_raw RAW(32767);
			    v_claim_raw RAW(32767);
			    v_inserted PLS_INTEGER;
			    v_deleted PLS_INTEGER;
			BEGIN
			    IF :UseFence = 1 THEN
			        BEGIN
			            MERGE INTO {scope.FenceTable} f
			            USING (SELECT :ScopeKey scope_key, :Token tok FROM dual) src
			            ON ({OraclePhysicalOutboxScope.Exact("f.scope_key", "src.scope_key")})
			            WHEN MATCHED THEN UPDATE SET f.high_water_token = GREATEST(f.high_water_token, src.tok)
			            WHEN NOT MATCHED THEN INSERT (scope_key, high_water_token) VALUES (src.scope_key, src.tok);
			        EXCEPTION WHEN DUP_VAL_ON_INDEX THEN
			            UPDATE {scope.FenceTable}
			            SET high_water_token = GREATEST(high_water_token, :Token) WHERE {OraclePhysicalOutboxScope.Exact("scope_key", ":ScopeKey")};
			        END;
			        SELECT high_water_token INTO v_high FROM {scope.FenceTable}
			        WHERE {OraclePhysicalOutboxScope.Exact("scope_key", ":ScopeKey")} FOR UPDATE;
			        IF v_high != :Token THEN
			            :Outcome := {(int)OutboxCompletionOutcome.FenceRefused};
			            RETURN;
			        END IF;
			    END IF;

			    BEGIN
			        SELECT dispatcher_id, dispatcher_timeout INTO v_owner, v_timeout
			        FROM {scope.OutboxTable} WHERE message_id = :MessageId FOR UPDATE;
			    EXCEPTION WHEN NO_DATA_FOUND THEN
			        :Outcome := {(int)OutboxCompletionOutcome.MessageNotFound};
			        RETURN;
			    END;

			    -- One post-lock server instant: lock waiting cannot extend an expired acquisition.
			    v_now := SYSTIMESTAMP;
			    IF :RequireClaim = 1 THEN
			        v_owner_raw := UTL_I18N.STRING_TO_RAW(v_owner, 'AL32UTF8');
			        v_claim_raw := UTL_I18N.STRING_TO_RAW(:ClaimIdentity, 'AL32UTF8');
			        IF v_owner IS NOT NULL AND v_timeout IS NOT NULL
			           AND v_timeout >= v_now
			           AND v_owner_raw IS NOT NULL AND v_claim_raw IS NOT NULL
			           AND UTL_RAW.LENGTH(v_owner_raw) = UTL_RAW.LENGTH(v_claim_raw)
			           AND v_owner_raw = v_claim_raw THEN
			            NULL;
			        ELSE
			            :Outcome := {(int)OutboxCompletionOutcome.ClaimLost};
			            RETURN;
			        END IF;
			    END IF;

			    IF :Transition = 0 THEN
			        DELETE FROM {scope.OutboxTable} WHERE message_id = :MessageId;
			        v_deleted := SQL%ROWCOUNT;
			        IF v_deleted != 1 THEN RAISE_APPLICATION_ERROR(-20001, 'Outbox sent deletion count mismatch'); END IF;
			    ELSIF :Transition = 1 THEN
			        UPDATE {scope.OutboxTable}
			        SET attempts = GREATEST(attempts, :Attempts),
			            error_message = :ErrorMessage,
			            dispatcher_id = NULL, dispatcher_timeout = NULL,
			            next_attempt_at = v_now + NUMTODSINTERVAL(GREATEST(:DelaySeconds, :FloorSeconds), 'SECOND')
			        WHERE message_id = :MessageId;
			        v_deleted := SQL%ROWCOUNT;
			        IF v_deleted != 1 THEN RAISE_APPLICATION_ERROR(-20001, 'Outbox failure update count mismatch'); END IF;
			    ELSE
			        INSERT INTO {scope.ArchiveTable}
			            (message_id, tenant_id, message_type, message_metadata, message_body, occurred_on, attempts, error_message)
			        SELECT message_id, tenant_id, message_type, message_metadata, message_body, occurred_on,
			               GREATEST(attempts, :Attempts), :ErrorMessage
			        FROM {scope.OutboxTable} WHERE message_id = :MessageId;
			        v_inserted := SQL%ROWCOUNT;
			        IF v_inserted != 1 THEN RAISE_APPLICATION_ERROR(-20001, 'Outbox archive insertion count mismatch'); END IF;
			        DELETE FROM {scope.OutboxTable} WHERE message_id = :MessageId;
			        v_deleted := SQL%ROWCOUNT;
			        IF v_deleted != 1 THEN RAISE_APPLICATION_ERROR(-20001, 'Outbox archive deletion count mismatch'); END IF;
			    END IF;
			    :Outcome := {(int)OutboxCompletionOutcome.Applied};
			END;
			""";
		var parameters = new DynamicParameters();
		Command = new CommandDefinition(sql, parameters, transaction,
			DbTimeouts.RegularTimeoutSeconds, cancellationToken: cancellationToken);
		ResolveAsync = async connection => await StaticDbCommand.ExecuteOutputsAsync(connection, Command, command =>
		{
			OracleOutboxRowReader.Initialize(command);
			StaticDbCommand.AddParameter(command, "UseFence", token.HasValue ? 1 : 0, DbType.Int32);
			StaticDbCommand.AddParameter(command, "ScopeKey", scope.Key, DbType.String);
			StaticDbCommand.AddParameter(command, "Token", token ?? 0, DbType.Int64);
			StaticDbCommand.AddParameter(command, "MessageId", messageId, DbType.String);
			StaticDbCommand.AddParameter(command, "RequireClaim", claimIdentity is not null ? 1 : 0, DbType.Int32);
			StaticDbCommand.AddNullableStringParameter(command, "ClaimIdentity", claimIdentity);
			StaticDbCommand.AddParameter(command, "Transition", (int)transition, DbType.Int32);
			StaticDbCommand.AddParameter(command, "Attempts", attempts, DbType.Int32);
			StaticDbCommand.AddNullableStringParameter(command, "ErrorMessage", error);
			StaticDbCommand.AddParameter(command, "DelaySeconds", delaySeconds, DbType.Double);
			StaticDbCommand.AddParameter(command, "FloorSeconds", options.FailureBackoffFloorSeconds, DbType.Int32);
			StaticDbCommand.AddOutputParameter(command, "Outcome", DbType.Int32);
		}, command =>
		{
			var outcome = (OutboxCompletionOutcome)StaticDbCommand.Output<int>(command, parameters, "Outcome", DbType.Int32);
			return outcome is OutboxCompletionOutcome.Applied or OutboxCompletionOutcome.ClaimLost
				or OutboxCompletionOutcome.MessageNotFound or OutboxCompletionOutcome.FenceRefused
				? outcome : throw new InvalidDataException("Oracle returned an unsupported outbox completion outcome.");
		}).ConfigureAwait(false);
	}
}

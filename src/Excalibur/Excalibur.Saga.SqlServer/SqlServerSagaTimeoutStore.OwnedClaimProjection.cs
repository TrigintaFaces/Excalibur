// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Dapper;
using Excalibur.Data;
using Excalibur.Data.Validation;
using Excalibur.Dispatch;
using Excalibur.Saga.Abstractions;
using Microsoft.Data.SqlClient;

namespace Excalibur.Saga.SqlServer;

public sealed partial class SqlServerSagaTimeoutStore
{
	private static ClaimedSagaTimeout CreateClaim(TimeoutRecord r, string claimToken)
	{
		var timeout = new SagaTimeout(
				r.TimeoutId,
				r.SagaId,
				r.SagaType,
				r.TimeoutType,
				r.TimeoutData,
				r.DueAt,
				r.ScheduledAt)
			{
				// Carried out so the delivery service can re-establish this timeout's own tenant before dispatch.
				// Read through the store-read factory, which maps a legacy NULL or the sentinel onto the
				// untenanted partition without rejecting either.
				TenantId = KeyedTenantPartition.FromStoredValue(r.TenantId).TenantId,
			};
		if (r.OwnedIncarnationId is null && r.OwnedGeneration is null && r.OwnedStateType is null)
		{
			return new ClaimedSagaTimeout(timeout, claimToken);
		}
		if (r.OwnedIncarnationId is null || r.OwnedGeneration is null || r.OwnedStateType is null
			|| !Guid.TryParse(r.SagaId, out var sagaId) || sagaId == Guid.Empty)
		{
			throw new InvalidOperationException("Claimed timeout has partial or invalid original authority.");
		}
		var original = new Excalibur.Dispatch.Messaging.SagaTimeoutDeliveryIdentity(
			new Excalibur.Dispatch.Messaging.SagaScopeIdentity(timeout.TenantId!, r.OwnedStateType, sagaId),
			r.TimeoutId, r.OwnedIncarnationId.Value, r.OwnedGeneration.Value);
		return new ClaimedSagaTimeout(timeout, claimToken, original);
	}


    private string CaptureQualifiedTarget()
    {
        var schema = _options.SchemaName;
        var table = _options.TableName;
        SqlIdentifierValidator.ThrowIfInvalid(schema, nameof(SqlServerSagaTimeoutStoreOptions.SchemaName));
        SqlIdentifierValidator.ThrowIfInvalid(table, nameof(SqlServerSagaTimeoutStoreOptions.TableName));
        return $"[{schema}].[{table}]";
    }

    private async Task<bool> HasOwnedClaimColumnsAsync(SqlConnection connection, string qualifiedTarget, CancellationToken token)
    {
        var columns = (await connection.ResolveAsync(new OwnedColumnsRequest(qualifiedTarget, token)).ConfigureAwait(false)).ToArray();
        if (columns.Length == 0)
        {
            const string incomplete = """
                SELECT CASE WHEN OBJECT_ID(@TableName,N'U')=OBJECT_ID(N'dbo.SagaTimeouts',N'U')
                    AND OBJECT_ID(N'dispatch.SagaTimeoutMembership',N'U') IS NOT NULL THEN 1 ELSE 0 END;
                """;
            var hasAtomicMetadata = await connection.ExecuteScalarAsync<int>(new CommandDefinition(incomplete,
                new { TableName = qualifiedTarget }, cancellationToken: token)).ConfigureAwait(false);
            if (hasAtomicMetadata != 0)
            {
                throw new InvalidOperationException("Atomic timeout metadata exists without its original payload projection.");
            }
            return false; // Ordinary state-only schema only; no owned-origin certification or manual permission.
        }
        if (columns.Length != 3
            || !columns.Any(c => c.Name == "OwnedIncarnationId" && c.TypeId == 36 && c.MaxLength == 16 && c.IsNullable)
            || !columns.Any(c => c.Name == "OwnedGeneration" && c.TypeId == 127 && c.MaxLength == 8 && c.IsNullable)
            || !columns.Any(c => c.Name == "OwnedStateType" && c.TypeId == 231 && c.MaxLength == 1000 && c.IsNullable))
        {
            throw new InvalidOperationException("Timeout owned-authority schema is partial or incompatible.");
        }
        return true;
    }

    private sealed record OwnedColumn(string Name, byte TypeId, short MaxLength, bool IsNullable);
}

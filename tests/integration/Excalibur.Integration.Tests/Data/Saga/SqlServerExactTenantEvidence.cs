// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;
using Excalibur.Saga.Abstractions;
using Microsoft.Data.SqlClient;

namespace Excalibur.Integration.Tests.Data.Saga;

internal sealed class ExactSqlTenantContext(string tenantId) : ITenantContext
{
	public string? TenantId { get; } = tenantId;
	public bool HasTenant => true;
}

internal static class SqlServerExactTenantEvidence
{
	internal static async Task RequireSagaCollationAsync(string connectionString)
	{
		await using var connection = new SqlConnection(connectionString);
		await connection.OpenAsync().ConfigureAwait(false);
		await using var command = new SqlCommand("SELECT collation_name FROM sys.columns WHERE object_id = OBJECT_ID(N'[dispatch].[sagas]') AND name = N'TenantId'", connection);
		(await command.ExecuteScalarAsync().ConfigureAwait(false)).ShouldBe("Latin1_General_BIN2");
		await RequireAliasesAsync(connection).ConfigureAwait(false);
	}

	internal static async Task RequireTimeoutCollationAsync(string connectionString)
	{
		await using var connection = new SqlConnection(connectionString);
		await connection.OpenAsync().ConfigureAwait(false);
		await using var command = new SqlCommand("SELECT collation_name FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[SagaTimeouts]') AND name = N'TenantId'", connection);
		(await command.ExecuteScalarAsync().ConfigureAwait(false)).ShouldBe("Latin1_General_BIN2");
		await RequireAliasesAsync(connection).ConfigureAwait(false);
	}

	internal static async Task<string?> ReadClaimAsync(string connectionString, string timeoutId)
	{
		await using var connection = new SqlConnection(connectionString);
		await connection.OpenAsync().ConfigureAwait(false);
		await using var command = new SqlCommand("SELECT ClaimedBy FROM [dbo].[SagaTimeouts] WHERE TimeoutId = @TimeoutId", connection);
		_ = command.Parameters.AddWithValue("@TimeoutId", timeoutId);
		return await command.ExecuteScalarAsync().ConfigureAwait(false) as string;
	}

	internal static async Task ExpireClaimAsync(string connectionString, ClaimedSagaTimeout claim)
	{
		await using var connection = new SqlConnection(connectionString);
		await connection.OpenAsync().ConfigureAwait(false);
		await using var command = new SqlCommand("UPDATE [dbo].[SagaTimeouts] SET ClaimedAt = DATEADD(DAY, -1, SYSDATETIMEOFFSET()) WHERE TimeoutId = @TimeoutId AND ClaimedBy = @ClaimToken", connection);
		_ = command.Parameters.AddWithValue("@TimeoutId", claim.Timeout.TimeoutId);
		_ = command.Parameters.AddWithValue("@ClaimToken", claim.ClaimToken);
		(await command.ExecuteNonQueryAsync().ConfigureAwait(false)).ShouldBe(1);
	}

	private static async Task RequireAliasesAsync(SqlConnection connection)
	{
		await using var command = new SqlCommand("SELECT CASE WHEN N'tenantA' COLLATE Latin1_General_BIN2 <> N'TENANTA' AND N'tenantA' COLLATE Latin1_General_BIN2 = N'tenantA ' THEN 1 ELSE 0 END", connection);
		(await command.ExecuteScalarAsync().ConfigureAwait(false)).ShouldBe(1);
	}
}

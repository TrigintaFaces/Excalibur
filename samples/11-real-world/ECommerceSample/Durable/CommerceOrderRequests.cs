// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
using System.Data;
using System.Text.Json;
using Dapper;
using Excalibur.Data;

namespace Excalibur.Dispatch.Examples.ECommerceSample;

internal sealed record SqlOrderEvidence(string Fingerprint, string IntentId, bool IntentCommitted, string OrderJson);

// This runnable profile is explicitly single-tenant. It must not be described as a multi-tenant example.
[NoTenantTerm(TenantConfinement.NoTenantDimension, "The SQL order profile is single-tenant; its closed profile does not register multi-tenancy; dbo.CommerceOrders is its one order namespace.")]
internal sealed class CreateCommerceOrders : DataRequest<int>
{
	public CreateCommerceOrders(CancellationToken token)
	{
		const string sql = """
			IF OBJECT_ID(N'dbo.CommerceOrders', N'U') IS NULL
			CREATE TABLE dbo.CommerceOrders
			(
			 OrderId nvarchar(128) NOT NULL PRIMARY KEY,
			 Fingerprint varchar(64) NOT NULL,
			 IntentId nvarchar(256) NOT NULL,
			 IntentCommitted bit NOT NULL,
			 OrderJson nvarchar(max) NOT NULL
			);
			EXEC(N'CREATE OR ALTER TRIGGER dbo.CommerceOrdersImmutable ON dbo.CommerceOrders
			INSTEAD OF UPDATE, DELETE AS
			BEGIN SET NOCOUNT ON; THROW 51000, ''Commerce order identities are immutable; retain them with inbox identities.'', 1; END');
			""";
		Command = CreateCommand(sql, cancellationToken: token);
		ResolveAsync = connection => connection.ExecuteAsync(Command);
	}
}

[NoTenantTerm(TenantConfinement.NoTenantDimension, "Single-tenant SQL order profile; the order identity is immutable and not shared across tenant deployments.")]
internal sealed class ReadCommerceOrder : DataRequest<SqlOrderEvidence?>
{
	public ReadCommerceOrder(string id, IDbTransaction? transaction, CancellationToken token)
	{
		// The transaction path serializes business identity even when an old inbox identity has expired.
		//
		// Each branch spells its statement out WHOLE rather than concatenating a lock-hint variable in.
		// Two reasons, and the second is why it is written this way deliberately: a consumer copying
		// this sample reads a complete SELECT instead of assembling one in their head, and the shipped
		// DDL sweep can only diff a table's columns against its write path when the SQL resolves from
		// literals -- a variable in the middle made this statement unreadable to it, leaving the
		// table definition in this file unverified against the columns it reads.
		var parameters = new DynamicParameters();
		parameters.Add("Id", id, DbType.String, size: 128);
		var sql = transaction is null
			? "SELECT Fingerprint, IntentId, IntentCommitted, OrderJson FROM dbo.CommerceOrders WHERE OrderId = @Id"
			: "SELECT Fingerprint, IntentId, IntentCommitted, OrderJson FROM dbo.CommerceOrders WITH (UPDLOCK, HOLDLOCK) WHERE OrderId = @Id";
		Command = CreateCommand(sql, parameters, transaction: transaction, cancellationToken: token);
		ResolveAsync = connection => connection.QuerySingleOrDefaultAsync<SqlOrderEvidence>(Command);
	}
}

[NoTenantTerm(TenantConfinement.NoTenantDimension, "Single-tenant SQL order profile; business state is inserted on the inbox-owned local transaction.")]
internal sealed class InsertCommerceOrder : DataRequest<int>
{
	public InsertCommerceOrder(OrderSubmissionPlan plan, IDbTransaction transaction, CancellationToken token)
	{
		var parameters = new DynamicParameters();
		parameters.Add("Id", plan.Id, DbType.String, size: 128);
		parameters.Add("Fingerprint", plan.Fingerprint, DbType.AnsiString, size: 64);
		parameters.Add("IntentId", plan.IntentId, DbType.String, size: 256);
		parameters.Add("OrderJson", JsonSerializer.Serialize(plan.Order, CommerceJsonContext.Default.OrderRecord), DbType.String);
		Command = CreateCommand("INSERT INTO dbo.CommerceOrders (OrderId,Fingerprint,IntentId,IntentCommitted,OrderJson) "
			+ "VALUES (@Id,@Fingerprint,@IntentId,1,@OrderJson)", parameters, transaction: transaction, cancellationToken: token);
		ResolveAsync = connection => connection.ExecuteAsync(Command);
	}
}

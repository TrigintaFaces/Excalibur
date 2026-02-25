// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.MySql.Diagnostics;

namespace Excalibur.Data.Tests.MySql;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class DataMySqlEventIdShould
{
	[Fact]
	public void HaveConnectionManagementIds()
	{
		DataMySqlEventId.ConnectionOpened.ShouldBe(103000);
		DataMySqlEventId.ConnectionClosed.ShouldBe(103001);
		DataMySqlEventId.ConnectionPoolCreated.ShouldBe(103002);
		DataMySqlEventId.ConnectionFailed.ShouldBe(103003);
	}

	[Fact]
	public void HaveQueryExecutionIds()
	{
		DataMySqlEventId.QueryExecuting.ShouldBe(103100);
		DataMySqlEventId.QueryExecuted.ShouldBe(103101);
		DataMySqlEventId.QueryFailed.ShouldBe(103102);
	}

	[Fact]
	public void HaveTransactionManagementIds()
	{
		DataMySqlEventId.TransactionStarted.ShouldBe(103200);
		DataMySqlEventId.TransactionCommitted.ShouldBe(103201);
		DataMySqlEventId.TransactionRolledBack.ShouldBe(103202);
	}

	[Fact]
	public void HaveRetryPolicyIds()
	{
		DataMySqlEventId.RetryAttempt.ShouldBe(103300);
	}

	[Fact]
	public void HavePersistenceProviderIds()
	{
		DataMySqlEventId.ExecutingDataRequest.ShouldBe(103400);
		DataMySqlEventId.ExecutingDataRequestInTransaction.ShouldBe(103401);
		DataMySqlEventId.FailedToExecuteDataRequest.ShouldBe(103402);
		DataMySqlEventId.ConnectionTestSuccessful.ShouldBe(103403);
		DataMySqlEventId.ConnectionTestFailed.ShouldBe(103404);
		DataMySqlEventId.FailedToRetrieveMetrics.ShouldBe(103405);
		DataMySqlEventId.InitializingProvider.ShouldBe(103406);
		DataMySqlEventId.FailedToRetrieveConnectionPoolStatistics.ShouldBe(103407);
		DataMySqlEventId.DisposingProvider.ShouldBe(103408);
		DataMySqlEventId.ClearedConnectionPools.ShouldBe(103409);
		DataMySqlEventId.ErrorDisposingProvider.ShouldBe(103410);
		DataMySqlEventId.PersistenceRetryAttempt.ShouldBe(103411);
	}

	[Fact]
	public void HaveErrorHandlingIds()
	{
		DataMySqlEventId.DeadlockDetected.ShouldBe(103500);
		DataMySqlEventId.DuplicateEntry.ShouldBe(103501);
		DataMySqlEventId.MySqlError.ShouldBe(103502);
	}

	[Fact]
	public void HaveUniqueEventIds()
	{
		var ids = new[]
		{
			DataMySqlEventId.ConnectionOpened,
			DataMySqlEventId.ConnectionClosed,
			DataMySqlEventId.ConnectionPoolCreated,
			DataMySqlEventId.ConnectionFailed,
			DataMySqlEventId.QueryExecuting,
			DataMySqlEventId.QueryExecuted,
			DataMySqlEventId.QueryFailed,
			DataMySqlEventId.TransactionStarted,
			DataMySqlEventId.TransactionCommitted,
			DataMySqlEventId.TransactionRolledBack,
			DataMySqlEventId.RetryAttempt,
			DataMySqlEventId.ExecutingDataRequest,
			DataMySqlEventId.ExecutingDataRequestInTransaction,
			DataMySqlEventId.FailedToExecuteDataRequest,
			DataMySqlEventId.ConnectionTestSuccessful,
			DataMySqlEventId.ConnectionTestFailed,
			DataMySqlEventId.FailedToRetrieveMetrics,
			DataMySqlEventId.InitializingProvider,
			DataMySqlEventId.FailedToRetrieveConnectionPoolStatistics,
			DataMySqlEventId.DisposingProvider,
			DataMySqlEventId.ClearedConnectionPools,
			DataMySqlEventId.ErrorDisposingProvider,
			DataMySqlEventId.PersistenceRetryAttempt,
			DataMySqlEventId.DeadlockDetected,
			DataMySqlEventId.DuplicateEntry,
			DataMySqlEventId.MySqlError,
		};

		ids.ShouldBeUnique();
	}
}

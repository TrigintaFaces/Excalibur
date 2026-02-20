// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Data.MySql.Diagnostics;

/// <summary>
/// Event IDs for MySQL data access (103000-103999).
/// </summary>
/// <remarks>
/// <para>Subcategory ranges:</para>
/// <list type="bullet">
/// <item>103000-103099: Connection Management</item>
/// <item>103100-103199: Query Execution</item>
/// <item>103200-103299: Transaction Management</item>
/// <item>103300-103399: Retry Policy</item>
/// <item>103400-103499: Persistence Provider</item>
/// <item>103500-103599: Error Handling</item>
/// </list>
/// </remarks>
public static class DataMySqlEventId
{
	// ========================================
	// 103000-103099: Connection Management
	// ========================================

	/// <summary>MySQL connection opened.</summary>
	public const int ConnectionOpened = 103000;

	/// <summary>MySQL connection closed.</summary>
	public const int ConnectionClosed = 103001;

	/// <summary>Connection pool created.</summary>
	public const int ConnectionPoolCreated = 103002;

	/// <summary>Connection failed.</summary>
	public const int ConnectionFailed = 103003;

	// ========================================
	// 103100-103199: Query Execution
	// ========================================

	/// <summary>Query executing.</summary>
	public const int QueryExecuting = 103100;

	/// <summary>Query executed successfully.</summary>
	public const int QueryExecuted = 103101;

	/// <summary>Query failed.</summary>
	public const int QueryFailed = 103102;

	// ========================================
	// 103200-103299: Transaction Management
	// ========================================

	/// <summary>Transaction started.</summary>
	public const int TransactionStarted = 103200;

	/// <summary>Transaction committed.</summary>
	public const int TransactionCommitted = 103201;

	/// <summary>Transaction rolled back.</summary>
	public const int TransactionRolledBack = 103202;

	// ========================================
	// 103300-103399: Retry Policy
	// ========================================

	/// <summary>Retry attempt for transient error.</summary>
	public const int RetryAttempt = 103300;

	// ========================================
	// 103400-103499: Persistence Provider
	// ========================================

	/// <summary>Executing data request.</summary>
	public const int ExecutingDataRequest = 103400;

	/// <summary>Executing data request in transaction.</summary>
	public const int ExecutingDataRequestInTransaction = 103401;

	/// <summary>Failed to execute data request.</summary>
	public const int FailedToExecuteDataRequest = 103402;

	/// <summary>Connection test successful.</summary>
	public const int ConnectionTestSuccessful = 103403;

	/// <summary>Connection test failed.</summary>
	public const int ConnectionTestFailed = 103404;

	/// <summary>Failed to retrieve metrics.</summary>
	public const int FailedToRetrieveMetrics = 103405;

	/// <summary>Initializing provider.</summary>
	public const int InitializingProvider = 103406;

	/// <summary>Failed to retrieve connection pool statistics.</summary>
	public const int FailedToRetrieveConnectionPoolStatistics = 103407;

	/// <summary>Disposing provider.</summary>
	public const int DisposingProvider = 103408;

	/// <summary>Cleared connection pools.</summary>
	public const int ClearedConnectionPools = 103409;

	/// <summary>Error disposing provider.</summary>
	public const int ErrorDisposingProvider = 103410;

	/// <summary>Persistence provider retry attempt.</summary>
	public const int PersistenceRetryAttempt = 103411;

	// ========================================
	// 103500-103599: Error Handling
	// ========================================

	/// <summary>Deadlock detected.</summary>
	public const int DeadlockDetected = 103500;

	/// <summary>Duplicate entry detected.</summary>
	public const int DuplicateEntry = 103501;

	/// <summary>MySQL error occurred.</summary>
	public const int MySqlError = 103502;
}

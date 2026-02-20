// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Data.Redis.Diagnostics;

/// <summary>
/// Event IDs for Redis persistence provider (107000-107999).
/// </summary>
/// <remarks>
/// <para>Subcategory ranges:</para>
/// <list type="bullet">
/// <item>107000-107099: Connection/Initialization</item>
/// <item>107100-107199: Data Request Execution</item>
/// <item>107200-107299: Retry Policy</item>
/// </list>
/// </remarks>
public static class DataRedisEventId
{
	// ========================================
	// 107000-107099: Connection/Initialization
	// ========================================

	/// <summary>Initializing provider.</summary>
	public const int Initializing = 107000;

	/// <summary>Connection test successful.</summary>
	public const int ConnectionTestSuccessful = 107001;

	/// <summary>Connection test failed.</summary>
	public const int ConnectionTestFailed = 107002;

	/// <summary>Disposing provider.</summary>
	public const int Disposing = 107003;

	/// <summary>Dispose error.</summary>
	public const int DisposeError = 107004;

	/// <summary>Metadata retrieval failed.</summary>
	public const int MetadataRetrievalFailed = 107005;

	/// <summary>Pool stats retrieval failed.</summary>
	public const int PoolStatsFailed = 107006;

	// ========================================
	// 107100-107199: Data Request Execution
	// ========================================

	/// <summary>Executing request.</summary>
	public const int ExecutingRequest = 107100;

	/// <summary>Execution failed.</summary>
	public const int ExecutionFailed = 107101;

	/// <summary>Executing request in transaction.</summary>
	public const int ExecutingRequestInTransaction = 107102;

	/// <summary>Transaction execution failed.</summary>
	public const int TransactionExecutionFailed = 107103;

	// ========================================
	// 107200-107299: Retry Policy
	// ========================================

	/// <summary>Retry warning.</summary>
	public const int RetryWarning = 107200;

	/// <summary>Document retry warning.</summary>
	public const int DocumentRetryWarning = 107201;

	/// <summary>Provider retry warning.</summary>
	public const int ProviderRetryWarning = 107202;

	// ========================================
	// 107300-107399: Outbox Store
	// ========================================

	/// <summary>Outbox message staged.</summary>
	public const int OutboxMessageStaged = 107300;

	/// <summary>Outbox message enqueued.</summary>
	public const int OutboxMessageEnqueued = 107301;

	/// <summary>Outbox message sent.</summary>
	public const int OutboxMessageSent = 107302;

	/// <summary>Outbox message failed.</summary>
	public const int OutboxMessageFailed = 107303;

	/// <summary>Outbox cleaned up.</summary>
	public const int OutboxCleanedUp = 107304;

	// ========================================
	// 107400-107499: Inbox Store
	// ========================================

	/// <summary>Inbox entry created.</summary>
	public const int InboxEntryCreated = 107400;

	/// <summary>Inbox entry processed.</summary>
	public const int InboxEntryProcessed = 107401;

	/// <summary>TryMarkAsProcessed succeeded.</summary>
	public const int InboxTryMarkProcessedSuccess = 107402;

	/// <summary>TryMarkAsProcessed detected duplicate.</summary>
	public const int InboxTryMarkProcessedDuplicate = 107403;

	/// <summary>Inbox entry failed.</summary>
	public const int InboxEntryFailed = 107404;

	/// <summary>Inbox entries cleaned up.</summary>
	public const int InboxCleanedUp = 107405;
}

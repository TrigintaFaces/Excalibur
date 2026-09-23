// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.Cdc.Postgres.Diagnostics;

/// <summary>
/// Event IDs for Postgres CDC processor operations (102300-102399).
/// </summary>
internal static class CdcPostgresEventId
{
	// ========================================
	// 102300-102399: CDC Processor
	// ========================================

	/// <summary>CDC processor starting.</summary>
	public const int CdcProcessorStarting = 102300;

	/// <summary>Resuming from position.</summary>
	public const int CdcResumingFromPosition = 102301;

	/// <summary>Connected to replication stream.</summary>
	public const int CdcConnectedToReplicationStream = 102302;

	/// <summary>Created replication slot.</summary>
	public const int CdcCreatedReplicationSlot = 102303;

	/// <summary>Replication slot already exists.</summary>
	public const int CdcReplicationSlotExists = 102304;

	/// <summary>Processed a change event.</summary>
	public const int CdcProcessedChange = 102305;

	/// <summary>Confirmed position.</summary>
	public const int CdcConfirmedPosition = 102306;

	/// <summary>CDC processor stopping.</summary>
	public const int CdcProcessorStopping = 102307;

	/// <summary>CDC processing error.</summary>
	public const int CdcProcessingError = 102308;

	/// <summary>CDC fatal (non-retryable) error — processor stops instead of reconnecting.</summary>
	public const int CdcFatalError = 102309;

	/// <summary>Synchronous Dispose() could not release the replication connection.</summary>
	public const int CdcSyncDisposeLeaksReplicationConnection = 102310;

	/// <summary>
	/// An overlapping replication call found a loop already running and returned without processing.
	/// Specified single-flight behaviour, not an error — but it is logged so a host that is persistently
	/// overlapping (a timer firing faster than a batch drains) can see it rather than infer it from
	/// throughput.
	/// </summary>
	public const int CdcConcurrentInvocationSkipped = 102311;
}

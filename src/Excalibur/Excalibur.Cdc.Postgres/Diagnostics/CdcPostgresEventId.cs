// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Cdc.Postgres.Diagnostics;

/// <summary>
/// Event IDs for Postgres CDC processor operations (102300-102399).
/// </summary>
public static class CdcPostgresEventId
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
}

// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Cdc.Diagnostics;

/// <summary>
/// Event IDs for CDC processing hosted service (3100-3149).
/// </summary>
/// <remarks>
/// <para>
/// These IDs fall within the Excalibur reserved range (3000-4999).
/// </para>
/// </remarks>
public static class CdcProcessingEventId
{
	/// <summary>CDC background service disabled.</summary>
	public const int CdcBackgroundServiceDisabled = 3100;

	/// <summary>CDC background service starting.</summary>
	public const int CdcBackgroundServiceStarting = 3101;

	/// <summary>CDC background service error during processing cycle.</summary>
	public const int CdcBackgroundServiceError = 3102;

	/// <summary>CDC background service stopped.</summary>
	public const int CdcBackgroundServiceStopped = 3103;

	/// <summary>CDC background service processed changes.</summary>
	public const int CdcBackgroundServiceProcessedChanges = 3104;

	/// <summary>CDC background service drain timeout exceeded.</summary>
	public const int CdcBackgroundServiceDrainTimeout = 3105;

	/// <summary>In-memory CDC processor processing batch.</summary>
	public const int InMemoryCdcProcessingBatch = 3106;

	/// <summary>In-memory CDC processor error processing change.</summary>
	public const int InMemoryCdcProcessingError = 3107;

	/// <summary>In-memory CDC processor completed processing.</summary>
	public const int InMemoryCdcProcessingComplete = 3108;
}

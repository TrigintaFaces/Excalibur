// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.EventSourcing.Redis.Diagnostics;

/// <summary>
/// Event IDs for Redis event sourcing infrastructure (115000-115999).
/// </summary>
/// <remarks>
/// <para>Subcategory ranges:</para>
/// <list type="bullet">
/// <item>115000-115099: Event Store Operations</item>
/// <item>115100-115199: Snapshot Store Operations</item>
/// <item>115200-115299: Concurrency / Lua Scripts</item>
/// </list>
/// </remarks>
public static class RedisEventSourcingEventId
{
	// ========================================
	// 115000-115099: Event Store Operations
	// ========================================

	/// <summary>Events loaded from stream.</summary>
	public const int EventsLoaded = 115000;

	/// <summary>Events appended to stream.</summary>
	public const int EventsAppended = 115001;

	/// <summary>Undispatched events retrieved.</summary>
	public const int UndispatchedEventsRetrieved = 115002;

	/// <summary>Event marked as dispatched.</summary>
	public const int EventMarkedDispatched = 115003;

	/// <summary>Event store operation failed.</summary>
	public const int EventStoreOperationFailed = 115004;

	// ========================================
	// 115100-115199: Snapshot Store Operations
	// ========================================

	/// <summary>Snapshot loaded.</summary>
	public const int SnapshotLoaded = 115100;

	/// <summary>Snapshot saved.</summary>
	public const int SnapshotSaved = 115101;

	/// <summary>Snapshots deleted.</summary>
	public const int SnapshotsDeleted = 115102;

	/// <summary>Snapshot not found.</summary>
	public const int SnapshotNotFound = 115103;

	// ========================================
	// 115200-115299: Concurrency / Lua Scripts
	// ========================================

	/// <summary>Concurrency conflict detected during append.</summary>
	public const int ConcurrencyConflict = 115200;

	/// <summary>Lua script executed.</summary>
	public const int LuaScriptExecuted = 115201;
}

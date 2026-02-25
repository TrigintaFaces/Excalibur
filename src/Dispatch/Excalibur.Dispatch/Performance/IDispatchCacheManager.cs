// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Performance;

/// <summary>
/// Provides centralized management of Dispatch performance caches.
/// </summary>
/// <remarks>
/// <para>
/// PERF-22: This interface coordinates the freezing of all internal caches
/// used by the Excalibur framework. Freezing caches converts them from
/// thread-safe concurrent dictionaries to <see cref="System.Collections.Frozen.FrozenDictionary{TKey, TValue}"/>
/// which provides O(1) lookups with zero synchronization overhead.
/// </para>
/// <para>
/// Use <see cref="FreezeAll"/> after the application has started and all handlers
/// have been registered. For automatic freezing, use <see cref="DispatchCacheOptimizationHostedService"/>
/// which triggers on <c>IHostApplicationLifetime.ApplicationStarted</c>.
/// </para>
/// </remarks>
public interface IDispatchCacheManager
{
	/// <summary>
	/// Gets a value indicating whether all caches have been frozen.
	/// </summary>
	/// <value>
	/// <see langword="true"/> if all caches are frozen; otherwise, <see langword="false"/>.
	/// </value>
	bool IsFrozen { get; }

	/// <summary>
	/// Gets the current freeze status of all caches.
	/// </summary>
	/// <returns>A <see cref="CacheFreezeStatus"/> containing the status of each cache.</returns>
	CacheFreezeStatus GetStatus();

	/// <summary>
	/// Freezes all Dispatch caches for optimal production performance.
	/// </summary>
	/// <remarks>
	/// <para>
	/// This method is idempotent - calling it multiple times has no additional effect
	/// after the first call.
	/// </para>
	/// <para>
	/// The following caches are frozen:
	/// <list type="bullet">
	/// <item>Handler invoker cache - Runtime-compiled handler invocation delegates</item>
	/// <item>Handler registry cache - Manual handler registrations</item>
	/// <item>Handler activator cache - Handler context setters</item>
	/// <item>Result factory cache - Message result factory delegates</item>
	/// <item>Middleware evaluator cache - Middleware applicability metadata</item>
	/// </list>
	/// </para>
	/// </remarks>
	void FreezeAll();
}

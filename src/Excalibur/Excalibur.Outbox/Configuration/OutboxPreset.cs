// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Outbox;

/// <summary>
/// Predefined configuration presets for outbox processing.
/// </summary>
/// <remarks>
/// <para>
/// Presets provide opinionated defaults for common scenarios, reducing the need
/// to configure individual settings. Use <see cref="OutboxOptions.Custom"/> for
/// full control over all settings.
/// </para>
/// <para>
/// <strong>Preset Selection Guide:</strong>
/// </para>
/// <list type="bullet">
///   <item>
///     <term><see cref="HighThroughput"/></term>
///     <description>For high-volume systems where latency is critical (e.g., real-time event processing)</description>
///   </item>
///   <item>
///     <term><see cref="Balanced"/></term>
///     <description>For most production systems with moderate throughput requirements (default)</description>
///   </item>
///   <item>
///     <term><see cref="HighReliability"/></term>
///     <description>For critical systems where message delivery guarantees are paramount (e.g., financial transactions)</description>
///   </item>
///   <item>
///     <term><see cref="Custom"/></term>
///     <description>For advanced users who need full control over all settings</description>
///   </item>
/// </list>
/// </remarks>
public enum OutboxPreset
{
	/// <summary>
	/// Optimized for maximum throughput with fast polling and high parallelism.
	/// </summary>
	/// <remarks>
	/// <para>Settings:</para>
	/// <list type="bullet">
	///   <item>BatchSize: 1000</item>
	///   <item>PollingInterval: 100ms</item>
	///   <item>MaxRetryCount: 3</item>
	///   <item>RetryDelay: 1 minute</item>
	///   <item>ParallelProcessing: Enabled (8 threads)</item>
	///   <item>RetentionPeriod: 1 day</item>
	///   <item>CleanupInterval: 15 minutes</item>
	/// </list>
	/// </remarks>
	HighThroughput,

	/// <summary>
	/// Balanced configuration suitable for most production scenarios.
	/// </summary>
	/// <remarks>
	/// <para>Settings:</para>
	/// <list type="bullet">
	///   <item>BatchSize: 100</item>
	///   <item>PollingInterval: 1 second</item>
	///   <item>MaxRetryCount: 5</item>
	///   <item>RetryDelay: 5 minutes</item>
	///   <item>ParallelProcessing: Enabled (4 threads)</item>
	///   <item>RetentionPeriod: 7 days</item>
	///   <item>CleanupInterval: 1 hour</item>
	/// </list>
	/// </remarks>
	Balanced,

	/// <summary>
	/// Optimized for maximum reliability with conservative settings.
	/// </summary>
	/// <remarks>
	/// <para>Settings:</para>
	/// <list type="bullet">
	///   <item>BatchSize: 10</item>
	///   <item>PollingInterval: 5 seconds</item>
	///   <item>MaxRetryCount: 10</item>
	///   <item>RetryDelay: 15 minutes</item>
	///   <item>ParallelProcessing: Disabled (sequential)</item>
	///   <item>RetentionPeriod: 30 days</item>
	///   <item>CleanupInterval: 6 hours</item>
	/// </list>
	/// </remarks>
	HighReliability,

	/// <summary>
	/// Custom configuration with all settings at their defaults.
	/// Use fluent methods to configure individual settings.
	/// </summary>
	Custom
}

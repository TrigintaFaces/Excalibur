// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Outbox;

/// <summary>
/// Mutable configuration class used by builders to accumulate settings
/// before creating an immutable <see cref="OutboxOptions"/> instance.
/// </summary>
/// <remarks>
/// This is used by the fluent builder API (IOutboxBuilder) and creates
/// options with the <see cref="OutboxPreset.Custom"/> preset since the
/// builder API allows arbitrary configuration.
/// </remarks>
internal sealed class OutboxConfiguration
{
	public int BatchSize { get; set; } = 100;
	public TimeSpan PollingInterval { get; set; } = TimeSpan.FromSeconds(5);
	public int MaxRetryCount { get; set; } = 3;
	public TimeSpan RetryDelay { get; set; } = TimeSpan.FromMinutes(5);
	public TimeSpan MessageRetentionPeriod { get; set; } = TimeSpan.FromDays(7);
	public bool EnableAutomaticCleanup { get; set; } = true;
	public TimeSpan CleanupInterval { get; set; } = TimeSpan.FromHours(1);
	public bool EnableBackgroundProcessing { get; set; } = true;
	public string? ProcessorId { get; set; }
	public bool EnableParallelProcessing { get; set; }
	public int MaxDegreeOfParallelism { get; set; } = 4;

	/// <summary>
	/// Creates an immutable <see cref="OutboxOptions"/> instance from this configuration.
	/// </summary>
	/// <returns>The configured options.</returns>
	/// <remarks>
	/// Options created via this path use <see cref="OutboxPreset.Custom"/> since
	/// the fluent builder API allows arbitrary configuration combinations.
	/// </remarks>
	public OutboxOptions ToOptions()
	{
		return new OutboxOptions(
			OutboxPreset.Custom,
			BatchSize,
			PollingInterval,
			MaxRetryCount,
			RetryDelay,
			MessageRetentionPeriod,
			EnableAutomaticCleanup,
			CleanupInterval,
			EnableBackgroundProcessing,
			ProcessorId,
			EnableParallelProcessing,
			MaxDegreeOfParallelism);
	}
}

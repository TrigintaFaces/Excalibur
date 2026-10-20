// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

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
	// NO INITIALIZER, and the absence is the fix. This defaulted to true while the builder route that
	// creates this object registers no hosted service and nothing on that route ever reads the flag -- so
	// the options reported that a drain was running in hosts that had none. That is the silent class: rows
	// accepted for delivery, nothing publishing them, and the one property a consumer would check to find
	// out saying it was switched on.
	//
	// With no initializer, IOutboxBuilder.EnableBackgroundProcessing() is the sole writer of true on this
	// route, and it registers the hosted service in the same call -- one writer, one act -- so the flag and
	// the registration cannot disagree. Restoring '= true' here reddens
	// OutboxBackgroundProcessingAgreementShould.
	//
	// Background processing remains an explicit opt-in. The hosted drain prefers a supplied publisher,
	// otherwise it resolves the framework processor in an owned asynchronous scope for each cycle.
	public bool EnableBackgroundProcessing { get; set; }
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
			EnableBackgroundProcessing,
			ProcessorId,
			EnableParallelProcessing,
			MaxDegreeOfParallelism);
	}
}

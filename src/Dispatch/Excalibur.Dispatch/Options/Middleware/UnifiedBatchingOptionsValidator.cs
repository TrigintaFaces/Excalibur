// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


using Excalibur.Dispatch.Options.Middleware;

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Middleware.Batch;

/// <summary>
/// Validates <see cref="UnifiedBatchingOptions"/> at startup so a misconfigured batching pipeline fails
/// fast (via <c>ValidateOnStart()</c>) rather than degrading silently once messages start flowing.
/// </summary>
/// <remarks>
/// <para>
/// Without a registered validator, <c>AddOptions&lt;UnifiedBatchingOptions&gt;().ValidateOnStart()</c> is a
/// no-op — there is nothing to run. <c>UseBatching</c> registers this one so the call is real.
/// </para>
/// <para>
/// <b>The checks are written out rather than delegated to the range attributes on the options type.</b>
/// The attribute-driven path (<c>ValidateDataAnnotations()</c>) resolves its rules by reflection, which
/// costs a trim and AOT warning on a consumer publishing ahead-of-time; and it cannot express the delay
/// rule at all, because the annotation ranges do not apply to a <see cref="TimeSpan"/>. Validating
/// explicitly keeps every rule in one place and leaves the consumer's AOT build clean. The attributes
/// remain on the options type for consumers who opt into annotation validation themselves.
/// </para>
/// <para>
/// Each value fails differently, and only one of the three announces itself:
/// </para>
/// <list type="bullet">
/// <item>
/// <description>
/// <b><see cref="UnifiedBatchingOptions.MaxBatchSize"/></b> at or below zero means a batch is never full,
/// so the batcher only ever flushes on the delay timer. Nothing errors — it is a silent throughput
/// collapse, which is the worst shape a misconfiguration can take.
/// </description>
/// </item>
/// <item>
/// <description>
/// <b><see cref="UnifiedBatchingOptions.MaxBatchDelay"/></b> at or below zero turns the flush timer into a
/// busy loop, burning a core for no throughput gain.
/// </description>
/// </item>
/// <item>
/// <description>
/// <b><see cref="UnifiedBatchingOptions.MaxParallelism"/></b> at or below zero leaves no worker permitted
/// to process a batch.
/// </description>
/// </item>
/// </list>
/// </remarks>
internal sealed class UnifiedBatchingOptionsValidator : IValidateOptions<UnifiedBatchingOptions>
{
	/// <inheritdoc/>
	public ValidateOptionsResult Validate(string? name, UnifiedBatchingOptions options)
	{
		if (options is null)
		{
			return ValidateOptionsResult.Fail($"{nameof(UnifiedBatchingOptions)} must not be null.");
		}

		var failures = new List<string>();

		if (options.MaxBatchSize <= 0)
		{
			failures.Add(
				$"{nameof(UnifiedBatchingOptions)}.{nameof(UnifiedBatchingOptions.MaxBatchSize)} must be greater "
				+ $"than zero (was {options.MaxBatchSize}). A batch that is never full flushes only on the delay "
				+ $"timer, which collapses throughput without reporting an error.");
		}

		if (options.MaxBatchDelay <= TimeSpan.Zero)
		{
			failures.Add(
				$"{nameof(UnifiedBatchingOptions)}.{nameof(UnifiedBatchingOptions.MaxBatchDelay)} must be greater "
				+ $"than zero (was {options.MaxBatchDelay}). A non-positive delay turns the flush timer into a "
				+ $"busy loop.");
		}

		if (options.MaxParallelism <= 0)
		{
			failures.Add(
				$"{nameof(UnifiedBatchingOptions)}.{nameof(UnifiedBatchingOptions.MaxParallelism)} must be greater "
				+ $"than zero (was {options.MaxParallelism}). A non-positive degree of parallelism leaves no worker "
				+ $"permitted to process a batch.");
		}

		return failures.Count > 0
			? ValidateOptionsResult.Fail(failures)
			: ValidateOptionsResult.Success;
	}
}

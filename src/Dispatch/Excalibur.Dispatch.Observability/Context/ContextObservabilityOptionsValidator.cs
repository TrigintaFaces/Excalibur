// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Observability.Context;

/// <summary>
/// AOT-safe validator for <see cref="ContextObservabilityOptions"/>.
/// Replaces DataAnnotations validation with explicit checks.
/// </summary>
internal sealed class ContextObservabilityOptionsValidator : IValidateOptions<ContextObservabilityOptions>
{
	/// <inheritdoc />
	public ValidateOptionsResult Validate(string? name, ContextObservabilityOptions options)
	{
		var failures = new List<string>();

		// ContextTracingOptions
		if (options.Tracing.MaxCustomItemsInTraces < 1)
		{
			failures.Add("Tracing.MaxCustomItemsInTraces must be at least 1.");
		}

		// ContextLimitsOptions
		if (options.Limits.MaxCustomItemsToCapture < 1)
		{
			failures.Add("Limits.MaxCustomItemsToCapture must be at least 1.");
		}

		if (options.Limits.MaxContextSizeBytes < 1)
		{
			failures.Add("Limits.MaxContextSizeBytes must be at least 1.");
		}

		if (options.Limits.MaxSnapshotsPerLineage < 1)
		{
			failures.Add("Limits.MaxSnapshotsPerLineage must be at least 1.");
		}

		if (options.Limits.MaxHistoryEventsPerContext < 1)
		{
			failures.Add("Limits.MaxHistoryEventsPerContext must be at least 1.");
		}

		if (options.Limits.MaxAnomalyQueueSize < 1)
		{
			failures.Add("Limits.MaxAnomalyQueueSize must be at least 1.");
		}

		// ContextExportOptions
		if (string.IsNullOrWhiteSpace(options.Export.ServiceName))
		{
			failures.Add("Export.ServiceName is required.");
		}

		if (string.IsNullOrWhiteSpace(options.Export.ServiceVersion))
		{
			failures.Add("Export.ServiceVersion is required.");
		}

		if (string.IsNullOrWhiteSpace(options.Export.PrometheusScrapePath))
		{
			failures.Add("Export.PrometheusScrapePath is required.");
		}

		return failures.Count > 0
			? ValidateOptionsResult.Fail(failures)
			: ValidateOptionsResult.Success;
	}
}

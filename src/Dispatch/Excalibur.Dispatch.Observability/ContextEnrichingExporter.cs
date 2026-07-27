// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Observability.Context;
using Excalibur.Dispatch.Observability.Diagnostics;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using OpenTelemetry;

namespace Excalibur.Dispatch.Observability;

/// <summary>
/// Custom exporter that enriches spans with context information.
/// </summary>
/// <param name="serviceProvider">The service provider for resolving dependencies.</param>
internal sealed partial class ContextEnrichingExporter(IServiceProvider serviceProvider) : BaseExporter<Activity>
{
	/// <summary>
	/// Exports a batch of activities after enriching them with context information.
	/// </summary>
	/// <param name="batch">The batch of activities to export.</param>
	/// <returns>The export result indicating success or failure.</returns>
	public override ExportResult Export(in Batch<Activity> batch)
	{
		// R0.8: Suppress: calling method with RequiresUnreferencedCode/RequiresDynamicCode - override cannot have attribute
#pragma warning disable IL2026, IL3050
		EnrichActivities(serviceProvider, batch);
#pragma warning restore IL2026, IL3050
		return ExportResult.Success;
	}

	[RequiresUnreferencedCode("Calls System.Text.Json.JsonSerializer.Serialize<TValue>(TValue, JsonSerializerOptions)")]
	[RequiresDynamicCode("Calls System.Text.Json.JsonSerializer.Serialize<TValue>(TValue, JsonSerializerOptions)")]
	[SuppressMessage("Design", "CA1031:Do not catch general exception types",
		Justification = "Fail-open enrichment: a throwing enricher must never break the trace export (Microsoft skip-pattern).")]
	private static void EnrichActivities(IServiceProvider serviceProvider, in Batch<Activity> batch)
	{
		var enricher = serviceProvider.GetService<IContextTraceEnricher>();
		if (enricher == null)
		{
			return;
		}

		var logger = serviceProvider.GetService<ILogger<ContextEnrichingExporter>>();

		foreach (var activity in batch)
		{
			// Try to get current message context
			var contextAccessor = serviceProvider.GetService<IMessageContextAccessor>();
			if (contextAccessor?.MessageContext == null)
			{
				continue;
			}

			try
			{
				enricher.EnrichActivity(activity, contextAccessor.MessageContext);
			}
			catch (Exception ex)
			{
				// Fail-open: a consumer-supplied enricher must never break the export pipeline.
				// Skip this activity's enrichment and continue with the rest of the batch.
				if (logger != null)
				{
					LogEnrichmentFailed(logger, ex);
				}
			}
		}
	}

	[LoggerMessage(
		EventId = ObservabilityEventId.TraceEnrichmentFailed,
		Level = LogLevel.Debug,
		Message = "Span enrichment skipped due to a throwing enricher; the trace export continues unaffected.")]
	private static partial void LogEnrichmentFailed(ILogger logger, Exception exception);
}

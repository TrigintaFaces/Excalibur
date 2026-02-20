// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Observability.Context;

using Microsoft.Extensions.DependencyInjection;

using OpenTelemetry;

namespace Excalibur.Dispatch.Observability;

/// <summary>
/// Custom exporter that enriches spans with context information.
/// </summary>
/// <param name="serviceProvider">The service provider for resolving dependencies.</param>
internal sealed class ContextEnrichingExporter(IServiceProvider serviceProvider) : BaseExporter<Activity>
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
	private static void EnrichActivities(IServiceProvider serviceProvider, in Batch<Activity> batch)
	{
		var enricher = serviceProvider.GetService<IContextTraceEnricher>();
		if (enricher == null)
		{
			return;
		}

		foreach (var activity in batch)
		{
			// Try to get current message context
			var contextAccessor = serviceProvider.GetService<IMessageContextAccessor>();
			if (contextAccessor?.MessageContext != null)
			{
				enricher.EnrichActivity(activity, contextAccessor.MessageContext);
			}
		}
	}
}

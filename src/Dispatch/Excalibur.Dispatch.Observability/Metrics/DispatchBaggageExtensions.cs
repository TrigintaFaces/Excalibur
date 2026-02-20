// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Security.Claims;

using Excalibur.Dispatch.Abstractions.Telemetry;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

using OpenTelemetry;

namespace Excalibur.Dispatch.Observability.Metrics;

/// <summary>
/// Extension methods for applying baggage information from HTTP context and utilities for baggage management.
/// </summary>
public static class DispatchBaggageExtensions
{
	/// <summary>
	/// Applies baggage information from the HTTP context to the current activity.
	/// </summary>
	/// <param name="httpContext"> The HTTP context to extract baggage from. </param>
	public static void ApplyBaggage(this HttpContext httpContext)
	{
		ArgumentNullException.ThrowIfNull(httpContext);

		// Extract trace identifier
		if (!string.IsNullOrEmpty(httpContext.TraceIdentifier))
		{
			_ = Baggage.SetBaggage("trace.id", httpContext.TraceIdentifier);
		}

		// Extract tenant ID from headers
		if (httpContext.Request.Headers.TryGetValue("X-Tenant-ID", out var tenantId) && !string.IsNullOrEmpty(tenantId))
		{
			_ = Baggage.SetBaggage("tenant.id", tenantId.ToString());
		}

		// Extract user ID from claims — sanitize PII before emitting as baggage
		var sanitizer = httpContext.RequestServices.GetService<ITelemetrySanitizer>();
		var userId = httpContext.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
		if (!string.IsNullOrEmpty(userId))
		{
			var sanitizedUserId = sanitizer?.SanitizeTag("user.id", userId) ?? userId;
			_ = Baggage.SetBaggage("user.id", sanitizedUserId);
		}

		// Extract user name from claims — sanitize PII before emitting as baggage
		var userName = httpContext.User?.FindFirst(ClaimTypes.Name)?.Value;
		if (!string.IsNullOrEmpty(userName))
		{
			var sanitizedUserName = sanitizer?.SanitizeTag("user.name", userName) ?? userName;
			_ = Baggage.SetBaggage("user.name", sanitizedUserName);
		}

		// Extract correlation ID from headers
		if (httpContext.Request.Headers.TryGetValue("X-Correlation-ID", out var correlationId) && !string.IsNullOrEmpty(correlationId))
		{
			_ = Baggage.SetBaggage("correlation.id", correlationId.ToString());
		}

		// Extract request ID from headers
		if (httpContext.Request.Headers.TryGetValue("X-Request-ID", out var requestId) && !string.IsNullOrEmpty(requestId))
		{
			_ = Baggage.SetBaggage("request.id", requestId.ToString());
		}

		// Extract session ID if available
		if (!string.IsNullOrEmpty(httpContext.Session?.Id))
		{
			_ = Baggage.SetBaggage("session.id", httpContext.Session.Id);
		}
	}

	/// <summary>
	/// Applies custom baggage key-value pairs.
	/// </summary>
	/// <param name="baggageItems"> The baggage items to apply. </param>
	public static void ApplyCustomBaggage(IEnumerable<KeyValuePair<string, string?>> baggageItems)
	{
		ArgumentNullException.ThrowIfNull(baggageItems);

		foreach (var item in baggageItems)
		{
			if (!string.IsNullOrEmpty(item.Value))
			{
				_ = Baggage.SetBaggage(item.Key, item.Value);
			}
		}
	}

	/// <summary>
	/// Gets all current baggage items.
	/// </summary>
	/// <returns> A dictionary of all current baggage items. </returns>
	public static IReadOnlyDictionary<string, string?> GetAllBaggage()
	{
		var baggage = new Dictionary<string, string?>(StringComparer.Ordinal);

		foreach (var item in Baggage.Current)
		{
			baggage[item.Key] = item.Value;
		}

		return baggage;
	}

	/// <summary>
	/// Clears all baggage items.
	/// </summary>
	public static void ClearBaggage()
	{
		// OpenTelemetry doesn't have a ClearAll method, so we need to remove each item Create a list to avoid modifying collection during iteration
		var keysToRemove = new List<string>();
		foreach (var item in Baggage.Current)
		{
			keysToRemove.Add(item.Key);
		}

		foreach (var key in keysToRemove)
		{
			_ = Baggage.RemoveBaggage(key);
		}
	}
}

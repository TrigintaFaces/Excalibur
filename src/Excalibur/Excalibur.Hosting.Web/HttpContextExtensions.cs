// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Net;

using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;

namespace Excalibur.Hosting.Web;

/// <summary>
/// Extension methods for <see cref="HttpContext" />.
/// </summary>
public static class HttpContextExtensions
{
	/// <summary>
	/// Gets the tenant ID from the HTTP context headers or query string.
	/// </summary>
	/// <param name="context"> The HTTP context. </param>
	/// <returns> The tenant ID, or null if not found. </returns>
	public static Guid? TenantId(this HttpContext context)
	{
		ArgumentNullException.ThrowIfNull(context);

		// Check header first
		if (context.Request.Headers.TryGetValue("X-Tenant-Id", out var headerValue))
		{
			if (Guid.TryParse(headerValue.ToString(), out var tenantId))
			{
				return tenantId;
			}
		}

		// Check query string
		if (context.Request.Query.TryGetValue("tenantId", out var queryValue))
		{
			if (Guid.TryParse(queryValue.ToString(), out var tenantId))
			{
				return tenantId;
			}
		}

		return null;
	}

	/// <summary>
	/// Gets the correlation ID from the HTTP context headers.
	/// </summary>
	/// <param name="context"> The HTTP context. </param>
	/// <returns> The correlation ID. </returns>
	public static Guid CorrelationId(this HttpContext context)
	{
		ArgumentNullException.ThrowIfNull(context);

		// Check for standard correlation ID headers
		var headers = context.Request.Headers;

		if (headers.TryGetValue("X-Correlation-Id", out var correlationId) ||
			headers.TryGetValue("X-Request-Id", out correlationId) ||
			headers.TryGetValue("X-Trace-Id", out correlationId))
		{
			if (Guid.TryParse(correlationId.ToString(), out var id))
			{
				return id;
			}
		}

		// Generate new correlation ID if not present
		return Guid.NewGuid();
	}

	/// <summary>
	/// Gets the ETag value from the HTTP context headers.
	/// </summary>
	/// <param name="context"> The HTTP context. </param>
	/// <returns> The ETag value, or null if not present. </returns>
	public static string? ETag(this HttpContext context)
	{
		ArgumentNullException.ThrowIfNull(context);

		if (context.Request.Headers.TryGetValue(HeaderNames.IfNoneMatch, out var ifNoneMatch))
		{
			return ifNoneMatch.ToString();
		}

		if (context.Request.Headers.TryGetValue(HeaderNames.IfMatch, out var ifMatch))
		{
			return ifMatch.ToString();
		}

		return null;
	}

	/// <summary>
	/// Gets the remote IP address from the HTTP context.
	/// </summary>
	/// <param name="context"> The HTTP context. </param>
	/// <returns> The remote IP address. </returns>
	public static IPAddress? RemoteIpAddress(this HttpContext context)
	{
		ArgumentNullException.ThrowIfNull(context);

		// Check for forwarded headers (common in reverse proxy scenarios)
		if (context.Request.Headers.TryGetValue("X-Forwarded-For", out var forwardedFor))
		{
			var ipString = forwardedFor.ToString().Split(',').FirstOrDefault()?.Trim();
			if (!string.IsNullOrEmpty(ipString) && IPAddress.TryParse(ipString, out var forwardedIp))
			{
				return forwardedIp;
			}
		}

		// Fall back to connection remote IP
		return context.Connection.RemoteIpAddress;
	}
}

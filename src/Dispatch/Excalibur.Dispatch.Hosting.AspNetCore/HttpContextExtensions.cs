// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Security.Claims;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Messaging;

using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;

namespace Excalibur.Dispatch.Hosting.AspNetCore;

/// <summary>
/// Helper extensions for creating <see cref="MessageContext" /> instances from <see cref="HttpContext" />./.
/// </summary>
public static class HttpContextExtensions
{
	/// <summary>
	/// Create a <see cref="MessageContext" /> populated with common request metadata.
	/// </summary>
	/// <param name="httpContext"> The HTTP context. </param>
	/// <exception cref="UnauthorizedAccessException"></exception>
	public static MessageContext CreateDispatchMessageContext(this HttpContext httpContext)
	{
		ArgumentNullException.ThrowIfNull(httpContext);

		var user = httpContext.User;

		if (user.Identity is not { IsAuthenticated: true })
		{
			throw new UnauthorizedAccessException(
					Resources.HttpContextExtensions_UserNotAuthenticated);
		}

		var context = new MessageContext
		{
			Source = "WebRequest",
			CorrelationId = httpContext.CorrelationId().Value.ToString(),
			CausationId = httpContext.CausationId()?.Value.ToString(),
			UserId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "anonymous",
			TenantId = httpContext.TenantId()?.Value,
			RequestServices = httpContext.RequestServices,
		};

		foreach (var header in httpContext.Request.Headers)
		{
			context.Items[header.Key] = header.Value.ToString();
		}

		return context;
	}

	/// <summary>
	/// Gets the request ETag from If-Match or If-None-Match headers.
	/// </summary>
	/// <param name="httpContext"> The current HTTP context. </param>
	/// <returns> The ETag value if present; otherwise, <see langword="null"/>. </returns>
	public static string? ETag(this HttpContext httpContext)
	{
		ArgumentNullException.ThrowIfNull(httpContext);

		var httpHeaders = httpContext.Request?.Headers;

		if (httpHeaders is null)
		{
			return null;
		}

		if (httpHeaders.TryGetValue(HeaderNames.IfMatch, out var ifMatchHeader))
		{
			return ifMatchHeader[0];
		}
		else if (httpHeaders.TryGetValue(HeaderNames.IfNoneMatch, out var ifNoneMatchHeader))
		{
			return ifNoneMatchHeader[0];
		}

		return null;
	}

	/// <summary>
	/// Retrieves the correlation ID from the request headers, or generates a new one if not present or invalid.
	/// </summary>
	/// <param name="httpContext"> The current HTTP context. </param>
	/// <returns> A <see cref="Guid" /> representing the correlation ID. </returns>
	/// <exception cref="ArgumentNullException"> Thrown if <paramref name="httpContext" /> is null. </exception>
	public static ICorrelationId CorrelationId(this HttpContext httpContext)
	{
		ArgumentNullException.ThrowIfNull(httpContext);

		if (httpContext.Request.Headers.TryGetValue(WellKnownHeaderNames.CorrelationId, out var correlationId) &&
			Guid.TryParse(correlationId, out var id))
		{
			return new CorrelationId(id);
		}

		return new CorrelationId(Guid.NewGuid());
	}

	/// <summary>
	/// Gets the causation ID from request headers when present.
	/// </summary>
	/// <param name="httpContext"> The current HTTP context. </param>
	/// <returns> The causation ID if present; otherwise, <see langword="null"/>. </returns>
	public static ICausationId? CausationId(this HttpContext httpContext)
	{
		ArgumentNullException.ThrowIfNull(httpContext);

		if (httpContext.Request.Headers.TryGetValue(WellKnownHeaderNames.CausationId, out var causationId) &&
			Guid.TryParse(causationId, out var id))
		{
			return new CausationId(id);
		}

		return null;
	}

	/// <summary>
	/// Resolves a tenant ID from headers, route values, query string, claims, or subdomain.
	/// </summary>
	/// <param name="httpContext"> The current HTTP context. </param>
	/// <returns> The tenant ID if resolved; otherwise, <see langword="null"/>. </returns>
	public static ITenantId? TenantId(this HttpContext httpContext)
	{
		ArgumentNullException.ThrowIfNull(httpContext);

		if (httpContext.Request.Headers.TryGetValue(WellKnownHeaderNames.TenantId, out var tenantId))
		{
			return new TenantId(tenantId);
		}

		if (httpContext.Request.RouteValues.TryGetValue("tenantId", out var routeTenantId))
		{
			return new TenantId(routeTenantId?.ToString());
		}

		if (httpContext.Request.Query.TryGetValue("tenantId", out var queryTenantId))
		{
			return new TenantId(queryTenantId);
		}

		var tenantClaim = httpContext.User.FindFirst("tenant_id")?.Value;
		if (!string.IsNullOrWhiteSpace(tenantClaim))
		{
			return new TenantId(tenantClaim);
		}

		var host = httpContext.Request.Host.Host;
		if (host.Contains('.', StringComparison.Ordinal))
		{
			var subdomain = host.Split('.')[0];
			if (subdomain is not "www" and not "app")
			{
				return new TenantId(subdomain);
			}
		}

		return null;
	}

	/// <summary>
	/// Retrieves the remote IP address of the current HTTP request.
	/// </summary>
	/// <param name="httpContext"> The HTTP context. </param>
	/// <returns> The remote IP address if available, otherwise <c> null </c>. </returns>
	public static string? RemoteIpAddress(this HttpContext httpContext)
	{
		ArgumentNullException.ThrowIfNull(httpContext);
		return httpContext.Connection?.RemoteIpAddress?.ToString();
	}
}

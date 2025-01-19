using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Net.Http.Headers;

namespace Excalibur.Hosting.Web;

/// <summary>
///     Provides extension methods for the <see cref="HttpContext" /> to retrieve commonly used values like correlation ID, ETag, tenant ID,
///     and remote IP address.
/// </summary>
internal static class HttpContextExtensions
{
	/// <summary>
	///     Retrieves the correlation ID from the request headers, or generates a new one if not present or invalid.
	/// </summary>
	/// <param name="context"> The current HTTP context. </param>
	/// <returns> A <see cref="Guid" /> representing the correlation ID. </returns>
	/// <exception cref="ArgumentNullException"> Thrown if <paramref name="context" /> is null. </exception>
	internal static Guid CorrelationId(this HttpContext context)
	{
		ArgumentNullException.ThrowIfNull(context, nameof(context));

		var httpHeaders = context.Request.Headers;
		var headerValue = httpHeaders[ExcaliburHeaderNames.CorrelationId].FirstOrDefault();

		return Guid.TryParse(headerValue, out var result) ? result : Guid.NewGuid();
	}

	/// <summary>
	///     Retrieves the ETag value from the request headers, if available.
	/// </summary>
	/// <param name="context"> The current HTTP context. </param>
	/// <returns> The ETag value as a string, or <c> null </c> if not found. </returns>
	/// <exception cref="ArgumentNullException"> Thrown if <paramref name="context" /> is null. </exception>
	internal static string? ETag(this HttpContext context)
	{
		ArgumentNullException.ThrowIfNull(context, nameof(context));

		var httpHeaders = context.Request?.Headers;
		string? etag = null;

		if (httpHeaders == null)
		{
			return etag;
		}

		if (httpHeaders.TryGetValue(HeaderNames.IfMatch, out var ifMatchHeader))
		{
			etag = ifMatchHeader.First();
		}
		else if (httpHeaders.TryGetValue(HeaderNames.IfNoneMatch, out var ifNoneMatchHeader))
		{
			etag = ifNoneMatchHeader.First();
		}

		return etag;
	}

	/// <summary>
	///     Retrieves the tenant ID from the route data, if available.
	/// </summary>
	/// <param name="context"> The current HTTP context. </param>
	/// <returns> The tenant ID as a string, or an empty string if not found. </returns>
	/// <exception cref="ArgumentNullException"> Thrown if <paramref name="context" /> is null. </exception>
	internal static string TenantId(this HttpContext context)
	{
		ArgumentNullException.ThrowIfNull(context, nameof(context));

		var routeValues = context.GetRouteData().Values;
		var tenantId = routeValues.TryGetValue("tenantId", out var value)
			? value!.ToString()!
			: string.Empty;

		return tenantId;
	}

	/// <summary>
	///     Retrieves the remote IP address of the client making the request.
	/// </summary>
	/// <param name="context"> The current HTTP context. </param>
	/// <returns> The remote IP address as a string, or <c> null </c> if not available. </returns>
	/// <exception cref="ArgumentNullException"> Thrown if <paramref name="context" /> is null. </exception>
	internal static string? RemoteIpAddress(this HttpContext context)
	{
		ArgumentNullException.ThrowIfNull(context, nameof(context));

		return context.Connection?.RemoteIpAddress?.ToString();
	}
}

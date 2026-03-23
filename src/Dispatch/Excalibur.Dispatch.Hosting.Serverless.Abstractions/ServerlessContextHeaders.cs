// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Hosting.Serverless;
using Excalibur.Dispatch.Messaging;

namespace Excalibur.Dispatch.Hosting;

/// <summary>
/// Shared utility for extracting context headers (CorrelationId, TenantId) from
/// serverless platform request headers and setting them on the serverless context.
/// </summary>
/// <remarks>
/// <para>
/// Each serverless provider supplies a header lookup function appropriate for its platform:
/// </para>
/// <list type="bullet">
/// <item>AWS Lambda: <c>key => apiGatewayEvent.Headers[key]</c></item>
/// <item>Azure Functions: <c>key => httpRequest.Headers[key]</c></item>
/// <item>Google Cloud Functions: <c>key => httpContext.Request.Headers[key]</c></item>
/// </list>
/// <para>
/// For non-HTTP triggers, the lookup function returns <c>null</c> and extraction
/// is skipped gracefully.
/// </para>
/// </remarks>
internal static class ServerlessContextHeaders
{
	/// <summary>
	/// Extracts CorrelationId and TenantId from request headers and stores them
	/// in the serverless context Properties dictionary.
	/// </summary>
	/// <param name="context">The serverless context to populate.</param>
	/// <param name="headerLookup">
	/// A function that returns the header value for a given header name,
	/// or <c>null</c> if the header is not present.
	/// </param>
	internal static void ExtractAndSet(
		IServerlessContext context,
		Func<string, string?> headerLookup)
	{
		ArgumentNullException.ThrowIfNull(context);
		ArgumentNullException.ThrowIfNull(headerLookup);

		var correlationId = headerLookup(WellKnownHeaderNames.CorrelationId);
		if (!string.IsNullOrWhiteSpace(correlationId) && Guid.TryParse(correlationId, out var correlationGuid))
		{
			context.Properties[WellKnownHeaderNames.CorrelationId] = new CorrelationId(correlationGuid);
		}

		var tenantId = headerLookup(WellKnownHeaderNames.TenantId);
		if (!string.IsNullOrWhiteSpace(tenantId))
		{
			context.Properties[WellKnownHeaderNames.TenantId] = new TenantId(tenantId);
		}
	}
}

// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Messaging;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Hosting.AspNetCore;

/// <summary>
/// Extension methods for dispatching messages from ASP.NET Core components.
/// </summary>
public static class DispatcherWebExtensions
{
	/// <summary>
	/// Creates a <see cref="MessageContext" /> from the current <see cref="HttpContext" />.
	/// </summary>
	/// <param name="context"> The HTTP context. </param>
	internal static MessageContext CreateMessageContext(this HttpContext context)
	{
		ArgumentNullException.ThrowIfNull(context);

		var messageContext = DispatchContextInitializer.CreateDefaultContext();
		var correlationGuid = context.RequestServices.GetRequiredService<ICorrelationId>().Value;

		// Sprint 71: Use direct properties only (no redundant Items[] writes)
		messageContext.CorrelationId = correlationGuid.ToString();
		messageContext.TenantId = context.RequestServices.GetRequiredService<ITenantId>().Value;

		return messageContext;
	}
}

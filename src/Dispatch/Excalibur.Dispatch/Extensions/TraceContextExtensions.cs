// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics;

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Extensions;

/// <summary>
/// Extension methods for working with distributed trace context.
/// </summary>
public static class TraceContextExtensions
{
	/// <summary>
	/// Retrieves the traceparent value from the context or the current <see cref="Activity" />.
	/// </summary>
	/// <param name="context"> Message context. </param>
	/// <returns> The traceparent string if available; otherwise, <c> null </c>. </returns>
	public static string? GetTraceParent(this IMessageContext context)
	{
		ArgumentNullException.ThrowIfNull(context);

		return context.TraceParent ?? Activity.Current?.Id;
	}
}

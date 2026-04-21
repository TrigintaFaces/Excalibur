// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Hosting.Builders;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Excalibur hosting builder extensions for audit configuration.
/// </summary>
public static class AuditExcaliburBuilderExtensions
{
	/// <summary>
	/// Registers <c>AuditMiddleware</c> and every context service it needs
	/// (<c>IActivityContext</c>, <c>ITenantId</c>, <c>ICorrelationId</c>,
	/// <c>IETag</c>, <c>IClientAddress</c>) with safe <c>TryAdd</c> defaults
	/// within the Excalibur composition root.
	/// </summary>
	/// <param name="builder">The Excalibur builder.</param>
	/// <returns>The same builder for fluent chaining.</returns>
	/// <example>
	/// <code>
	/// services.AddExcalibur(excalibur => excalibur
	///     .AddDispatch(...)
	///     .AddAudit());
	/// </code>
	/// </example>
	public static IExcaliburBuilder AddAudit(this IExcaliburBuilder builder)
	{
		ArgumentNullException.ThrowIfNull(builder);

		_ = builder.Services.AddExcaliburAudit();
		return builder;
	}
}
